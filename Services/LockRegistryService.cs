using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WhatInsides.Models;

namespace WhatInsides.Services;

public class LockRegistryService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhatInsides");

    private static readonly string RegistryPath = Path.Combine(AppDataFolder, "locked_registry.json");

    public ObservableCollection<LockedItemModel> LockedItems { get; } = new();

    public LockRegistryService()
    {
        LoadRegistry();
    }

    public void LoadRegistry()
    {
        LockedItems.Clear();
        try
        {
            if (File.Exists(RegistryPath))
            {
                string json = File.ReadAllText(RegistryPath);
                var items = JsonSerializer.Deserialize<ObservableCollection<LockedItemModel>>(json);
                if (items != null)
                {
                    foreach (var it in items)
                    {
                        // Ensure status on launch is locked
                        it.IsLocked = true;
                        it.TempPreviewPath = null;
                        LockedItems.Add(it);
                    }
                }
            }
        }
        catch { }
    }

    public void SaveRegistry()
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(LockedItems, options);
            File.WriteAllText(RegistryPath, json);
        }
        catch { }
    }

    public async Task<LockedItemModel> LockItemAsync(
        string targetPath, 
        string lockType, 
        string password, 
        bool shredOriginal = true, 
        int shredPasses = 1,
        IProgress<double>? progress = null)
    {
        bool isFolder = Directory.Exists(targetPath);
        if (!isFolder && !File.Exists(targetPath))
        {
            throw new FileNotFoundException("File atau folder tidak ditemukan di disk.", targetPath);
        }

        string itemName = isFolder ? new DirectoryInfo(targetPath).Name : Path.GetFileName(targetPath);
        long fileSize = 0;
        try
        {
            if (isFolder)
            {
                fileSize = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            }
            else
            {
                fileSize = new FileInfo(targetPath).Length;
            }
        }
        catch { }

        string finalLockedPath = targetPath;

        if (lockType == "FastHide")
        {
            if (!isFolder)
            {
                throw new InvalidOperationException("Kunci Cepat Instan hanya berlaku untuk folder.");
            }

            var res = NtfsLockerService.LockFolderInPlace(targetPath);
            if (!res.Success)
            {
                throw new Exception($"Gagal mengunci folder: {res.Error}");
            }
            finalLockedPath = res.NewPath;
        }
        else // Aes256 Full Encryption
        {
            if (isFolder)
            {
                finalLockedPath = await DirectLockService.EncryptDirectFolderAsync(targetPath, password, shredOriginal, shredPasses, progress);
            }
            else
            {
                finalLockedPath = await DirectLockService.EncryptDirectFileAsync(targetPath, password, shredOriginal, shredPasses, progress);
            }
        }

        var item = new LockedItemModel
        {
            Name = itemName,
            OriginalPath = targetPath,
            CurrentPath = finalLockedPath,
            LockType = lockType,
            IsFolder = isFolder,
            FileSize = fileSize,
            DateLocked = DateTime.Now,
            IsLocked = true
        };

        LockedItems.Add(item);
        SaveRegistry();
        return item;
    }

    public async Task UnlockPermanentAsync(LockedItemModel item, string password)
    {
        if (item.LockType == "FastHide")
        {
            var res = NtfsLockerService.UnlockFolderInPlace(item.CurrentPath, item.OriginalPath);
            if (!res.Success)
            {
                throw new Exception($"Gagal memulihkan folder: {res.Error}");
            }
        }
        else // Aes256
        {
            string outDir = Path.GetDirectoryName(item.OriginalPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            await DirectLockService.DecryptDirectAsync(item.CurrentPath, password, outDir);

            // Delete .wisafe
            try { File.Delete(item.CurrentPath); } catch { }
        }

        LockedItems.Remove(item);
        SaveRegistry();
    }

    public async Task ShredPermanentAsync(LockedItemModel item, int passes = 1)
    {
        try
        {
            if (item.IsFolder && Directory.Exists(item.CurrentPath))
            {
                await FileShredder.ShredDirectoryAsync(item.CurrentPath, passes);
            }
            else if (File.Exists(item.CurrentPath))
            {
                await FileShredder.ShredFileAsync(item.CurrentPath, passes);
            }
            else if (Directory.Exists(item.OriginalPath))
            {
                await FileShredder.ShredDirectoryAsync(item.OriginalPath, passes);
            }
            else if (File.Exists(item.OriginalPath))
            {
                await FileShredder.ShredFileAsync(item.OriginalPath, passes);
            }
        }
        catch { }

        LockedItems.Remove(item);
        SaveRegistry();
    }

    public async Task PreviewItemAsync(LockedItemModel item, string password)
    {
        if (item.LockType == "FastHide")
        {
            // Unhide folder temporarily and open in File Explorer
            var res = NtfsLockerService.UnlockFolderInPlace(item.CurrentPath, item.OriginalPath);
            if (res.Success)
            {
                item.IsLocked = false;
                item.CurrentPath = res.RestoredPath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{item.OriginalPath}\"",
                    UseShellExecute = true
                });
            }
        }
        else // Aes256
        {
            string parentDir = Path.GetDirectoryName(item.CurrentPath) ?? Path.GetTempPath();
            string tempDir = Path.Combine(parentDir, ".__wi_preview", item.Id);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            try { new DirectoryInfo(Path.Combine(parentDir, ".__wi_preview")).Attributes |= FileAttributes.Hidden; } catch { }

            string target = await DirectLockService.DecryptDirectAsync(item.CurrentPath, password, tempDir);
            item.IsLocked = false;
            item.TempPreviewPath = target;

            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        SaveRegistry();
    }

    public async Task RelockItemAsync(LockedItemModel item, string password)
    {
        if (item.LockType == "FastHide")
        {
            var res = NtfsLockerService.LockFolderInPlace(item.OriginalPath);
            if (res.Success)
            {
                item.CurrentPath = res.NewPath;
                item.IsLocked = true;
            }
        }
        else // Aes256
        {
            // If preview was modified or opened, clean up temp preview
            if (!string.IsNullOrEmpty(item.TempPreviewPath))
            {
                try
                {
                    if (File.Exists(item.TempPreviewPath)) File.Delete(item.TempPreviewPath);
                    if (Directory.Exists(item.TempPreviewPath)) Directory.Delete(item.TempPreviewPath, true);
                }
                catch { }
                item.TempPreviewPath = null;
            }
            item.IsLocked = true;
        }
        SaveRegistry();
    }

    public async Task RelockAllAsync(string password)
    {
        foreach (var item in LockedItems.Where(it => !it.IsLocked).ToList())
        {
            try
            {
                await RelockItemAsync(item, password);
            }
            catch { }
        }
    }
}

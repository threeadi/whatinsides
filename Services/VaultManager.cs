using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WhatInsides.Models;

namespace WhatInsides.Services;

public class VaultManager
{
    private readonly SettingsManager _settingsManager;
    private readonly ObservableCollection<VaultItem> _items = new();
    private string _currentPassword = string.Empty;

    public ReadOnlyObservableCollection<VaultItem> Items { get; }
    public bool IsUnlocked => !string.IsNullOrEmpty(_currentPassword);

    public VaultManager(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Items = new ReadOnlyObservableCollection<VaultItem>(_items);
    }

    public string GetVaultPath() => _settingsManager.GetVaultDirectory();

    public string GetTempPreviewDirectory()
    {
        string tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhatInsides",
            "TempPreview");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    public void PurgeTempPreview()
    {
        try
        {
            string tempDir = GetTempPreviewDirectory();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);
            }
        }
        catch { }
    }

    public void LockVault()
    {
        _currentPassword = string.Empty;
        _items.Clear();
        PurgeTempPreview();
    }

    public async Task<bool> UnlockVaultAsync(string password)
    {
        if (!_settingsManager.VerifyPassword(password))
        {
            return false;
        }

        _currentPassword = password;
        await LoadIndexAsync();
        return true;
    }

    public async Task SaveIndexAsync()
    {
        if (string.IsNullOrEmpty(_currentPassword)) return;

        string vaultDir = GetVaultPath();
        string indexPath = Path.Combine(vaultDir, "vault_index.widx");

        var list = _items.ToList();
        string json = JsonSerializer.Serialize(list);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using var memoryIn = new MemoryStream(jsonBytes);
        using var fileOut = new FileStream(indexPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await CryptoEngine.EncryptStreamAsync(memoryIn, fileOut, _currentPassword);
    }

    public async Task LoadIndexAsync()
    {
        _items.Clear();
        if (string.IsNullOrEmpty(_currentPassword)) return;

        string vaultDir = GetVaultPath();
        string indexPath = Path.Combine(vaultDir, "vault_index.widx");

        if (!File.Exists(indexPath)) return;

        try
        {
            using var fileIn = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var memoryOut = new MemoryStream();

            await CryptoEngine.DecryptStreamAsync(fileIn, memoryOut, _currentPassword);
            byte[] decryptedBytes = memoryOut.ToArray();
            string json = Encoding.UTF8.GetString(decryptedBytes);

            var loaded = JsonSerializer.Deserialize<List<VaultItem>>(json);
            if (loaded != null)
            {
                foreach (var item in loaded)
                {
                    // Check if encrypted file exists in vault
                    string fullPath = Path.Combine(vaultDir, item.EncryptedFileName);
                    if (File.Exists(fullPath))
                    {
                        _items.Add(item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading vault index: {ex.Message}");
        }
    }

    public async Task<VaultItem> LockFileAsync(
        string sourcePath,
        bool shredOriginal,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("File sumber tidak ditemukan.", sourcePath);

        if (string.IsNullOrEmpty(_currentPassword))
            throw new InvalidOperationException("Brankas dalam kondisi terkunci.");

        var fileInfo = new FileInfo(sourcePath);
        string id = Guid.NewGuid().ToString("N");
        string encryptedFileName = $"{id}.wiv";
        string targetEncryptedPath = Path.Combine(GetVaultPath(), encryptedFileName);

        using (var inStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var outStream = new FileStream(targetEncryptedPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await CryptoEngine.EncryptStreamAsync(inStream, outStream, _currentPassword, progress, cancellationToken);
        }

        var item = new VaultItem
        {
            Id = id,
            Name = fileInfo.Name,
            OriginalPath = sourcePath,
            EncryptedFileName = encryptedFileName,
            IsFolder = false,
            FileSize = fileInfo.Length,
            DateLocked = DateTime.Now,
            OriginalExtension = fileInfo.Extension,
            Category = VaultItem.DetectCategory(sourcePath, false)
        };

        if (shredOriginal)
        {
            int passes = _settingsManager.Settings.ShredPassCount;
            await FileShredder.ShredFileAsync(sourcePath, passes, cancellationToken);
        }

        _items.Add(item);
        await SaveIndexAsync();

        return item;
    }

    public async Task<VaultItem> LockFolderAsync(
        string sourceDir,
        bool shredOriginal,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException("Folder sumber tidak ditemukan.");

        if (string.IsNullOrEmpty(_currentPassword))
            throw new InvalidOperationException("Brankas dalam kondisi terkunci.");

        var dirInfo = new DirectoryInfo(sourceDir);
        string id = Guid.NewGuid().ToString("N");
        string encryptedFileName = $"{id}.wiv";
        string targetEncryptedPath = Path.Combine(GetVaultPath(), encryptedFileName);

        // Calculate total folder size
        long totalSize = 0;
        try
        {
            totalSize = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                .Sum(t => new FileInfo(t).Length);
        }
        catch { }

        // Create temporary zip archive
        string tempZip = Path.Combine(Path.GetTempPath(), $"__wi_fld_{id}.tmp");
        try
        {
            await Task.Run(() => ZipFile.CreateFromDirectory(sourceDir, tempZip, CompressionLevel.Fastest, false), cancellationToken);

            using (var inStream = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = new FileStream(targetEncryptedPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await CryptoEngine.EncryptStreamAsync(inStream, outStream, _currentPassword, progress, cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }

        var item = new VaultItem
        {
            Id = id,
            Name = dirInfo.Name,
            OriginalPath = sourceDir,
            EncryptedFileName = encryptedFileName,
            IsFolder = true,
            FileSize = totalSize,
            DateLocked = DateTime.Now,
            OriginalExtension = string.Empty,
            Category = "Folder"
        };

        if (shredOriginal)
        {
            int passes = _settingsManager.Settings.ShredPassCount;
            await FileShredder.ShredDirectoryAsync(sourceDir, passes, cancellationToken);
        }

        _items.Add(item);
        await SaveIndexAsync();

        return item;
    }

    public async Task<string> RestoreItemAsync(
        VaultItem item,
        string destinationDir,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentPassword))
            throw new InvalidOperationException("Brankas dalam kondisi terkunci.");

        string vaultDir = GetVaultPath();
        string encryptedFilePath = Path.Combine(vaultDir, item.EncryptedFileName);

        if (!File.Exists(encryptedFilePath))
            throw new FileNotFoundException("File terenkripsi di brankas hilang.");

        if (!Directory.Exists(destinationDir))
            Directory.CreateDirectory(destinationDir);

        string restoredPath;

        if (item.IsFolder)
        {
            restoredPath = Path.Combine(destinationDir, item.Name);
            string tempZip = Path.Combine(Path.GetTempPath(), $"__wi_ext_{item.Id}.tmp");

            try
            {
                using (var inStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var outStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await CryptoEngine.DecryptStreamAsync(inStream, outStream, _currentPassword, progress, cancellationToken);
                }

                if (Directory.Exists(restoredPath))
                {
                    // If directory already exists, create a numbered variation
                    restoredPath = Path.Combine(destinationDir, $"{item.Name}_restored_{DateTime.Now:yyyyMMdd_HHmmss}");
                }

                await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, restoredPath), cancellationToken);
            }
            finally
            {
                if (File.Exists(tempZip))
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
        }
        else
        {
            restoredPath = Path.Combine(destinationDir, item.Name);
            if (File.Exists(restoredPath))
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(item.Name);
                string ext = Path.GetExtension(item.Name);
                restoredPath = Path.Combine(destinationDir, $"{nameWithoutExt}_restored_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            }

            using (var inStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = new FileStream(restoredPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await CryptoEngine.DecryptStreamAsync(inStream, outStream, _currentPassword, progress, cancellationToken);
            }
        }

        // Remove from vault
        try { File.Delete(encryptedFilePath); } catch { }
        _items.Remove(item);
        await SaveIndexAsync();

        return restoredPath;
    }

    public async Task<string> PreviewItemAsync(
        VaultItem item,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentPassword))
            throw new InvalidOperationException("Brankas dalam kondisi terkunci.");

        string encryptedFilePath = Path.Combine(GetVaultPath(), item.EncryptedFileName);
        string tempDir = GetTempPreviewDirectory();
        string tempTarget;

        if (item.IsFolder)
        {
            tempTarget = Path.Combine(tempDir, item.Name);
            if (Directory.Exists(tempTarget)) Directory.Delete(tempTarget, true);

            string tempZip = Path.Combine(tempDir, $"__preview_{item.Id}.zip");
            try
            {
                using (var inStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var outStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await CryptoEngine.DecryptStreamAsync(inStream, outStream, _currentPassword, progress, cancellationToken);
                }

                await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, tempTarget), cancellationToken);
            }
            finally
            {
                if (File.Exists(tempZip)) try { File.Delete(tempZip); } catch { }
            }
        }
        else
        {
            tempTarget = Path.Combine(tempDir, item.Name);
            using (var inStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = new FileStream(tempTarget, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await CryptoEngine.DecryptStreamAsync(inStream, outStream, _currentPassword, progress, cancellationToken);
            }
        }

        // Open with default system viewer
        Process.Start(new ProcessStartInfo
        {
            FileName = tempTarget,
            UseShellExecute = true
        });

        return tempTarget;
    }

    public async Task DeleteItemPermanentlyAsync(VaultItem item)
    {
        string encryptedFilePath = Path.Combine(GetVaultPath(), item.EncryptedFileName);
        if (File.Exists(encryptedFilePath))
        {
            await FileShredder.ShredFileAsync(encryptedFilePath, 1);
        }

        _items.Remove(item);
        await SaveIndexAsync();
    }
}

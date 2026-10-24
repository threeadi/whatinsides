using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WhatInsides.Models;

namespace WhatInsides.Services;

public class VaultFileSystemService
{
    private FileSystemWatcher? _watcher;
    private VaultModel? _activeVault;
    private string? _activePassword;
    private readonly System.Timers.Timer _speedSimulationTimer;

    public event Action<double, double>? SpeedUpdated; // ReadSpeed, WriteSpeed in MB/s
    public event Action<string>? FileChangedInExplorer;

    private readonly Random _rand = new();
    private double _currentReadSpeed = 0;
    private double _currentWriteSpeed = 0;

    public VaultFileSystemService()
    {
        _speedSimulationTimer = new System.Timers.Timer(1200);
        _speedSimulationTimer.Elapsed += (s, e) =>
        {
            if (_activeVault != null && !_activeVault.IsLocked)
            {
                // Dynamic activity simulation based on active vault
                _currentReadSpeed = Math.Round(180 + _rand.NextDouble() * 50, 1);
                _currentWriteSpeed = Math.Round(80 + _rand.NextDouble() * 45, 1);
                SpeedUpdated?.Invoke(_currentReadSpeed, _currentWriteSpeed);
            }
            else
            {
                SpeedUpdated?.Invoke(0, 0);
            }
        };
    }

    public string PrepareMountDirectory(VaultModel vault)
    {
        string mountDir = Path.Combine(
            Path.GetTempPath(),
            "WhatInsides_Mount",
            vault.Id);

        if (!Directory.Exists(mountDir))
        {
            Directory.CreateDirectory(mountDir);
        }

        return mountDir;
    }

    public async Task MountVaultAsync(VaultModel vault, string password)
    {
        _activeVault = vault;
        _activePassword = password;
        string mountDir = PrepareMountDirectory(vault);
        vault.UnlockedDirectory = mountDir;
        vault.IsLocked = false;
        vault.LastActivity = DateTime.Now;

        // Decrypt files from encrypted storage path to mount directory
        if (Directory.Exists(vault.EncryptedStoragePath))
        {
            string[] encryptedFiles = Directory.GetFiles(vault.EncryptedStoragePath, "*.wiv");
            foreach (var encFile in encryptedFiles)
            {
                try
                {
                    string plainName = Path.GetFileNameWithoutExtension(encFile);
                    // If filename ends with _folder, it's a folder
                    string target = Path.Combine(mountDir, plainName);

                    using var inStream = new FileStream(encFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var outStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                    await CryptoEngine.DecryptStreamAsync(inStream, outStream, password);
                }
                catch { }
            }
        }

        // Setup real-time FileSystemWatcher on the mount folder
        StartWatcher(mountDir);
        _speedSimulationTimer.Start();
    }

    private void StartWatcher(string path)
    {
        StopWatcher();

        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileSystemChanged;
        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemChanged;
    }

    private void StopWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        FileChangedInExplorer?.Invoke(e.Name ?? string.Empty);
    }

    public void OpenInExplorer(VaultModel vault)
    {
        string? mountDir = vault.UnlockedDirectory;
        if (string.IsNullOrEmpty(mountDir) || !Directory.Exists(mountDir))
        {
            mountDir = PrepareMountDirectory(vault);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{mountDir}\"",
            UseShellExecute = true
        });
    }

    public async Task UnmountAndLockVaultAsync(VaultModel vault)
    {
        _speedSimulationTimer.Stop();
        StopWatcher();

        string? mountDir = vault.UnlockedDirectory;
        if (!string.IsNullOrEmpty(mountDir) && Directory.Exists(mountDir))
        {
            // Encrypt all files from mount directory to encrypted storage path
            if (!string.IsNullOrEmpty(_activePassword))
            {
                if (!Directory.Exists(vault.EncryptedStoragePath))
                {
                    Directory.CreateDirectory(vault.EncryptedStoragePath);
                }

                string[] plainFiles = Directory.GetFiles(mountDir, "*", SearchOption.AllDirectories);
                vault.FileCount = plainFiles.Length;
                long totalSize = 0;

                foreach (var file in plainFiles)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        totalSize += info.Length;
                        string relName = Path.GetFileName(file);
                        string encPath = Path.Combine(vault.EncryptedStoragePath, $"{relName}.wiv");

                        using var inStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var outStream = new FileStream(encPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await CryptoEngine.EncryptStreamAsync(inStream, outStream, _activePassword);
                    }
                    catch { }
                }

                vault.TotalBytes = totalSize;
            }

            // Cryptographically wipe/shred decrypted mount folder
            await FileShredder.ShredDirectoryAsync(mountDir, 1);
        }

        vault.IsLocked = true;
        vault.UnlockedDirectory = null;
        vault.LastActivity = DateTime.Now;
        _activeVault = null;
        _activePassword = null;
    }
}

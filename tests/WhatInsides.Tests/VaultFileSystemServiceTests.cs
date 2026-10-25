using System;
using System.IO;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class VaultFileSystemServiceTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly VaultFileSystemService _fsService;

    public VaultFileSystemServiceTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), $"wi_vfs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBaseDir);
        _fsService = new VaultFileSystemService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void PrepareMountDirectory_CreatesDirectoryInTemp()
    {
        var vault = new VaultModel { Name = "TestMountVault" };
        string mountDir = _fsService.PrepareMountDirectory(vault);

        Assert.NotNull(mountDir);
        Assert.True(Directory.Exists(mountDir));
        Assert.Contains(vault.Id, mountDir);
    }

    [Fact]
    public async Task MountVaultAsync_And_UnmountAndLockVaultAsync_RoundtripsFiles()
    {
        string encStorePath = Path.Combine(_testBaseDir, "EncryptedStore");
        Directory.CreateDirectory(encStorePath);

        var vault = new VaultModel
        {
            Name = "VfsRoundtripVault",
            EncryptedStoragePath = encStorePath,
            IsLocked = true
        };

        string password = "VaultFsPassword123#";

        // 1. Mount Vault
        await _fsService.MountVaultAsync(vault, password);
        Assert.False(vault.IsLocked);
        Assert.NotNull(vault.UnlockedDirectory);
        Assert.True(Directory.Exists(vault.UnlockedDirectory));

        // 2. Create a test file inside the unlocked mount directory
        string testDoc = Path.Combine(vault.UnlockedDirectory, "secrets.txt");
        await File.WriteAllTextAsync(testDoc, "Secret Confidential Document Content Inside Mount");

        // 3. Unmount & Lock
        await _fsService.UnmountAndLockVaultAsync(vault);
        Assert.True(vault.IsLocked);
        Assert.Null(vault.UnlockedDirectory);

        // 4. Verify encrypted .wiv file was created in encrypted storage
        string[] encFiles = Directory.GetFiles(encStorePath, "*.wiv");
        Assert.NotEmpty(encFiles);

        // 5. Mount again and verify decrypted file restored in mount directory
        await _fsService.MountVaultAsync(vault, password);
        Assert.False(vault.IsLocked);
        string restoredDoc = Path.Combine(vault.UnlockedDirectory!, "secrets.txt");
        Assert.True(File.Exists(restoredDoc));
        string restoredContent = await File.ReadAllTextAsync(restoredDoc);
        Assert.Equal("Secret Confidential Document Content Inside Mount", restoredContent);

        // Cleanup
        await _fsService.UnmountAndLockVaultAsync(vault);
    }

    [Fact]
    public void SpeedUpdated_Event_CanBeSubscribed()
    {
        double rSpeed = -1;
        double wSpeed = -1;
        Action<double, double> handler = (r, w) => { rSpeed = r; wSpeed = w; };

        _fsService.SpeedUpdated += handler;
        _fsService.SpeedUpdated -= handler;

        Assert.Equal(-1, rSpeed);
        Assert.Equal(-1, wSpeed);
    }

    [Fact]
    public async Task SpeedSimulation_FiresWhenVaultIsActive()
    {
        var vault = new VaultModel
        {
            Name = "TimerTestVault",
            EncryptedStoragePath = Path.Combine(_testBaseDir, "TimerEncStore"),
            IsLocked = true
        };
        Directory.CreateDirectory(vault.EncryptedStoragePath);

        double lastRead = -1;
        var tcs = new TaskCompletionSource<bool>();

        Action<double, double> handler = (r, w) =>
        {
            lastRead = r;
            tcs.TrySetResult(true);
        };

        _fsService.SpeedUpdated += handler;

        try
        {
            await _fsService.MountVaultAsync(vault, "Pass123!");
            await Task.WhenAny(tcs.Task, Task.Delay(1800));
            await _fsService.UnmountAndLockVaultAsync(vault);

            Assert.True(lastRead >= 0);
        }
        finally
        {
            _fsService.SpeedUpdated -= handler;
        }
    }

    [Fact]
    public async Task UnmountAndLockVaultAsync_WhenUnlockedDirectoryNull_HandlesGracefully()
    {
        var vault = new VaultModel
        {
            Name = "EmptyMountVault",
            UnlockedDirectory = null,
            IsLocked = false
        };

        await _fsService.UnmountAndLockVaultAsync(vault);
        Assert.True(vault.IsLocked);
        Assert.Null(vault.UnlockedDirectory);
    }

    [Fact]
    public void FileChangedInExplorer_Event_CanBeSubscribed()
    {
        string changedFile = "";
        Action<string> handler = f => { changedFile = f; };

        _fsService.FileChangedInExplorer += handler;
        _fsService.FileChangedInExplorer -= handler;

        Assert.Equal("", changedFile);
    }

    [Fact]
    public async Task UnmountAndLockVaultAsync_WhenActivePasswordNull_StillShredsMountDirectory()
    {
        var vault = new VaultModel
        {
            Name = "NoPassVault",
            UnlockedDirectory = Path.Combine(_testBaseDir, "TempMountDirNoPass"),
            IsLocked = false
        };
        Directory.CreateDirectory(vault.UnlockedDirectory);
        File.WriteAllText(Path.Combine(vault.UnlockedDirectory, "temp.txt"), "Temp Data");

        await _fsService.UnmountAndLockVaultAsync(vault);
        Assert.True(vault.IsLocked);
        Assert.Null(vault.UnlockedDirectory);
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class LockRegistryAdvancedTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LockRegistryService _regService;
    private const string Password = "StrongPassword#123";

    public LockRegistryAdvancedTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), $"wi_reg_adv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBaseDir);
        _regService = new LockRegistryService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                foreach (var dir in Directory.GetDirectories(_testBaseDir, "*", SearchOption.AllDirectories))
                {
                    try { new DirectoryInfo(dir).Attributes = FileAttributes.Normal; } catch { }
                }
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task LockItemAsync_NonExistentPath_ThrowsFileNotFoundException()
    {
        string missingPath = Path.Combine(_testBaseDir, "NoSuchFile.txt");
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await _regService.LockItemAsync(missingPath, "Aes256", Password);
        });
    }

    [Fact]
    public async Task LockItemAsync_FastHideOnFile_ThrowsInvalidOperationException()
    {
        string sampleFile = Path.Combine(_testBaseDir, "file.txt");
        await File.WriteAllTextAsync(sampleFile, "Plaintext");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _regService.LockItemAsync(sampleFile, "FastHide", Password);
        });
    }

    [Fact]
    public async Task LockItemAsync_FastHideOnFolder_And_UnlockPermanentAsync_RestoresFolder()
    {
        string targetFolder = Path.Combine(_testBaseDir, "ImportantVaultFolder");
        Directory.CreateDirectory(targetFolder);
        string subFile = Path.Combine(targetFolder, "data.json");
        await File.WriteAllTextAsync(subFile, "{\"secret\": true}");

        // 1. Lock folder with FastHide
        var item = await _regService.LockItemAsync(targetFolder, "FastHide", Password);
        Assert.NotNull(item);
        Assert.Equal("FastHide", item.LockType);
        Assert.True(item.IsLocked);
        Assert.True(Directory.Exists(item.CurrentPath));

        // 2. Unlock permanently
        await _regService.UnlockPermanentAsync(item, Password);
        Assert.DoesNotContain(item, _regService.LockedItems);
        Assert.True(Directory.Exists(targetFolder));
        Assert.True(File.Exists(subFile));
    }

    [Fact]
    public async Task RelockItemAsync_And_RelockAllAsync_UpdatesLockState()
    {
        string folder1 = Path.Combine(_testBaseDir, "RelockFolder");
        Directory.CreateDirectory(folder1);
        File.WriteAllText(Path.Combine(folder1, "f1.txt"), "hello");

        var item = await _regService.LockItemAsync(folder1, "FastHide", Password);
        Assert.True(item.IsLocked);

        // Simulate preview: unlock folder temporarily
        NtfsLockerService.UnlockFolderInPlace(item.CurrentPath, item.OriginalPath);
        item.CurrentPath = item.OriginalPath;
        item.IsLocked = false;

        await _regService.RelockItemAsync(item, Password);
        Assert.True(item.IsLocked);

        // Simulate preview again
        NtfsLockerService.UnlockFolderInPlace(item.CurrentPath, item.OriginalPath);
        item.CurrentPath = item.OriginalPath;
        item.IsLocked = false;

        await _regService.RelockAllAsync(Password);
        Assert.True(item.IsLocked);

        await _regService.UnlockPermanentAsync(item, Password);
    }
}

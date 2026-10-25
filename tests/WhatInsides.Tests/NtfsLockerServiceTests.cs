using System;
using System.IO;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class NtfsLockerServiceTests : IDisposable
{
    private readonly string _testBaseDir;

    public NtfsLockerServiceTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), $"wi_ntfs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBaseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                // Reset attributes before deleting
                foreach (var dir in Directory.GetDirectories(_testBaseDir, "*", SearchOption.AllDirectories))
                {
                    try { new DirectoryInfo(dir).Attributes = FileAttributes.Normal; } catch { }
                }
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void LockFolderInPlace_WhenDirectoryDoesNotExist_ReturnsFailure()
    {
        string nonExistent = Path.Combine(_testBaseDir, "MissingFolder");
        var (success, path, error) = NtfsLockerService.LockFolderInPlace(nonExistent);

        Assert.False(success);
        Assert.NotEmpty(error);
        Assert.Equal(nonExistent, path);
    }

    [Fact]
    public void LockFolderInPlace_And_UnlockFolderInPlace_RestoresFolder()
    {
        string targetFolder = Path.Combine(_testBaseDir, "ConfidentialPhotos");
        Directory.CreateDirectory(targetFolder);
        string sampleFile = Path.Combine(targetFolder, "pic.jpg");
        File.WriteAllText(sampleFile, "Image binary data");

        // 1. Lock folder
        var (lockSuccess, lockedPath, lockError) = NtfsLockerService.LockFolderInPlace(targetFolder);

        Assert.True(lockSuccess);
        Assert.Empty(lockError);
        Assert.True(Directory.Exists(lockedPath));

        var lockedDirInfo = new DirectoryInfo(lockedPath);
        Assert.True(lockedDirInfo.Attributes.HasFlag(FileAttributes.Hidden));

        // 2. Unlock folder
        var (unlockSuccess, restoredPath, unlockError) = NtfsLockerService.UnlockFolderInPlace(lockedPath, targetFolder);

        Assert.True(unlockSuccess);
        Assert.Empty(unlockError);
        Assert.True(Directory.Exists(restoredPath));
        Assert.True(File.Exists(sampleFile));

        var restoredDirInfo = new DirectoryInfo(restoredPath);
        Assert.False(restoredDirInfo.Attributes.HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public void UnlockFolderInPlace_WhenLockedDirectoryMissing_ReturnsFailure()
    {
        string missingLocked = Path.Combine(_testBaseDir, "GhostFolder.{21EC2020-3AEA-1069-A2DD-08002B30309D}");
        string missingOrig = Path.Combine(_testBaseDir, "GhostFolder");

        var (success, path, error) = NtfsLockerService.UnlockFolderInPlace(missingLocked, missingOrig);

        Assert.False(success);
        Assert.NotEmpty(error);
    }
}

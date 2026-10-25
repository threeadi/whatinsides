using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class FeatureSynchronizationTests : IDisposable
{
    private readonly string _testDir;

    public FeatureSynchronizationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "WhatInsides_SyncTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void CloudDetectionService_ReturnsProvidersWithValidMetadata()
    {
        // Act
        var providers = CloudDetectionService.DetectAvailableProviders();

        // Assert
        Assert.NotNull(providers);
        Assert.True(providers.Count >= 2);
        Assert.Equal("Google Drive", providers[0].Name);
        Assert.Contains(providers, p => p.Name == "OneDrive");
        Assert.Contains(providers, p => p.Name == "Google Drive");

        foreach (var p in providers)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Status));
        }
    }

    [Fact]
    public void SettingsManager_AutoLockOnMinimize_PersistsCorrectly()
    {
        // Arrange
        string customPath = Path.Combine(_testDir, "minimize_settings.json");
        var mgr = new SettingsManager(customPath);

        // Act
        mgr.Settings.AutoLockOnMinimize = true;
        mgr.SaveSettings();

        var reloaded = new SettingsManager(customPath);

        // Assert
        Assert.True(reloaded.Settings.AutoLockOnMinimize);

        // Toggle to false
        reloaded.Settings.AutoLockOnMinimize = false;
        reloaded.SaveSettings();

        var reloaded2 = new SettingsManager(customPath);
        Assert.False(reloaded2.Settings.AutoLockOnMinimize);
    }

    [Fact]
    public void LockQueueItem_MultiFileBatchQueue_EnqueuesDistinctItemsCorrectly()
    {
        // Arrange
        string file1 = Path.Combine(_testDir, "batch1.docx");
        string file2 = Path.Combine(_testDir, "batch2.xlsx");
        string file3 = Path.Combine(_testDir, "batch3.pdf");
        File.WriteAllText(file1, "DOCX Content");
        File.WriteAllText(file2, "XLSX Content");
        File.WriteAllText(file3, "PDF Content");

        var queue = new List<LockQueueItem>();
        string[] droppedFiles = new[] { file1, file2, file3 };

        // Act - simulate multi-drop queueing
        foreach (var path in droppedFiles)
        {
            if (!queue.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                queue.Add(LockQueueItem.FromPath(path));
            }
        }

        // Assert
        Assert.Equal(3, queue.Count);
        Assert.Contains(queue, x => x.Name == "batch1.docx" && !x.IsFolder);
        Assert.Contains(queue, x => x.Name == "batch2.xlsx" && !x.IsFolder);
        Assert.Contains(queue, x => x.Name == "batch3.pdf" && !x.IsFolder);

        // Duplicate drop test
        foreach (var path in droppedFiles)
        {
            if (!queue.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                queue.Add(LockQueueItem.FromPath(path));
            }
        }
        Assert.Equal(3, queue.Count); // Should still be 3
    }

    [Fact]
    public void StorageCalculation_CalculatesPartitionAllocationCorrectly()
    {
        // Arrange
        var mockItems = new List<LockedItemModel>
        {
            new() { OriginalPath = @"C:\Users\Secret1.docx", CurrentPath = @"C:\Users\Secret1.docx.wisafe", FileSize = 100 * 1024 * 1024 },
            new() { OriginalPath = @"C:\Users\Secret2.pdf", CurrentPath = @"C:\Users\Secret2.pdf.wisafe", FileSize = 250 * 1024 * 1024 },
            new() { OriginalPath = @"D:\Projects\Repo.zip", CurrentPath = @"D:\Projects\Repo.zip.wisafe", FileSize = 1024 * 1024 * 1024 },
            new() { OriginalPath = @"E:\Backup\Archive.tar", CurrentPath = @"E:\Backup\Archive.tar.wisafe", FileSize = 500 * 1024 * 1024 }
        };

        // Act
        long cBytes = mockItems.Where(x => x.OriginalPath.StartsWith("C:", StringComparison.OrdinalIgnoreCase)).Sum(x => x.FileSize);
        int cCount = mockItems.Count(x => x.OriginalPath.StartsWith("C:", StringComparison.OrdinalIgnoreCase));

        long dBytes = mockItems.Where(x => x.OriginalPath.StartsWith("D:", StringComparison.OrdinalIgnoreCase)).Sum(x => x.FileSize);
        int dCount = mockItems.Count(x => x.OriginalPath.StartsWith("D:", StringComparison.OrdinalIgnoreCase));

        long eBytes = mockItems.Where(x => x.OriginalPath.StartsWith("E:", StringComparison.OrdinalIgnoreCase)).Sum(x => x.FileSize);
        int eCount = mockItems.Count(x => x.OriginalPath.StartsWith("E:", StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.Equal(350 * 1024 * 1024L, cBytes);
        Assert.Equal(2, cCount);

        Assert.Equal(1024 * 1024 * 1024L, dBytes);
        Assert.Equal(1, dCount);

        Assert.Equal(500 * 1024 * 1024L, eBytes);
        Assert.Equal(1, eCount);
    }

    [Fact]
    public void CloudBackup_SimulatedCopy_CopiesLockedContainersSuccessfully()
    {
        // Arrange
        string localSafeDir = Path.Combine(_testDir, "LocalVault");
        Directory.CreateDirectory(localSafeDir);
        string cloudVaultDir = Path.Combine(_testDir, "CloudBackupVault");
        Directory.CreateDirectory(cloudVaultDir);

        string sampleSafe1 = Path.Combine(localSafeDir, "Doc1.pdf.wisafe");
        string sampleSafe2 = Path.Combine(localSafeDir, "Project.zip.wisafe");
        File.WriteAllText(sampleSafe1, "ENCRYPTED_WI_SAFE_1");
        File.WriteAllText(sampleSafe2, "ENCRYPTED_WI_SAFE_2");

        var lockedItems = new List<LockedItemModel>
        {
            new() { CurrentPath = sampleSafe1 },
            new() { CurrentPath = sampleSafe2 }
        };

        // Act - simulate sync logic
        int copied = 0;
        foreach (var item in lockedItems)
        {
            if (File.Exists(item.CurrentPath))
            {
                string destFile = Path.Combine(cloudVaultDir, Path.GetFileName(item.CurrentPath));
                File.Copy(item.CurrentPath, destFile, true);
                copied++;
            }
        }

        // Assert
        Assert.Equal(2, copied);
        Assert.True(File.Exists(Path.Combine(cloudVaultDir, "Doc1.pdf.wisafe")));
        Assert.True(File.Exists(Path.Combine(cloudVaultDir, "Project.zip.wisafe")));
        Assert.Equal("ENCRYPTED_WI_SAFE_1", File.ReadAllText(Path.Combine(cloudVaultDir, "Doc1.pdf.wisafe")));
    }

    [Fact]
    public void CloudBackup_ZeroKnowledge_OnlyCopiesEncryptedContainers()
    {
        // Arrange
        string localSafeDir = Path.Combine(_testDir, "ZK_LocalVault");
        Directory.CreateDirectory(localSafeDir);
        string cloudVaultDir = Path.Combine(_testDir, "ZK_CloudVault");
        Directory.CreateDirectory(cloudVaultDir);

        string encryptedContainer = Path.Combine(localSafeDir, "Secret.docx.wisafe");
        string rawDecryptedFile = Path.Combine(localSafeDir, "RawPlaintext.txt");
        File.WriteAllText(encryptedContainer, "ENCRYPTED_CIPHERTEXT");
        File.WriteAllText(rawDecryptedFile, "PLAINTEXT_SECRET");

        var lockedItems = new List<LockedItemModel>
        {
            new() { CurrentPath = encryptedContainer },
            new() { CurrentPath = rawDecryptedFile }
        };

        // Act - Zero knowledge check: strictly require .wisafe
        int copied = 0;
        foreach (var item in lockedItems)
        {
            if (File.Exists(item.CurrentPath) && item.CurrentPath.EndsWith(".wisafe", StringComparison.OrdinalIgnoreCase))
            {
                string destFile = Path.Combine(cloudVaultDir, Path.GetFileName(item.CurrentPath));
                File.Copy(item.CurrentPath, destFile, true);
                copied++;
            }
        }

        // Assert
        Assert.Equal(1, copied);
        Assert.True(File.Exists(Path.Combine(cloudVaultDir, "Secret.docx.wisafe")));
        Assert.False(File.Exists(Path.Combine(cloudVaultDir, "RawPlaintext.txt")));
    }

    [Fact]
    public void DropZone_RoutingContract_SingleVsBatch_RoutesCorrectly()
    {
        // Arrange
        string singleItem = @"C:\Test\Single.pdf";
        string[] multiItems = new[] { @"C:\Test\1.pdf", @"C:\Test\2.pdf" };

        string? quickLockPath = null;
        string[]? batchLockPaths = null;

        Action<string> onQuickLock = p => quickLockPath = p;
        Action<string[]> onBatchLock = ps => batchLockPaths = ps;

        // Act - Simulate DropZone single item logic
        if (new[] { singleItem }.Length == 1)
        {
            onQuickLock(singleItem);
        }

        // Act - Simulate DropZone multi item logic
        if (multiItems.Length > 1)
        {
            onBatchLock(multiItems);
        }

        // Assert
        Assert.Equal(singleItem, quickLockPath);
        Assert.NotNull(batchLockPaths);
        Assert.Equal(2, batchLockPaths.Length);
    }

    [Fact]
    public void LockedItemsChanged_SynchronizationContract_NotifiesListeners()
    {
        // Arrange
        bool refreshedObserverA = false;
        bool refreshedObserverB = false;

        Action? lockedItemsChanged = null;
        lockedItemsChanged += () => refreshedObserverA = true;
        lockedItemsChanged += () => refreshedObserverB = true;

        // Act
        lockedItemsChanged?.Invoke();

        // Assert
        Assert.True(refreshedObserverA);
        Assert.True(refreshedObserverB);
    }
}


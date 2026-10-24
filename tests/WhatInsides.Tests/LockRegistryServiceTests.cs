using System;
using System.IO;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class LockRegistryServiceTests
{
    [Fact]
    public void LockedItemModel_Formatters_ComputeCorrectStrings()
    {
        var item = new LockedItemModel
        {
            Name = "Finance2026.pdf",
            OriginalPath = @"D:\Documents\Finance2026.pdf",
            CurrentPath = @"D:\Documents\Finance2026.pdf.wisafe",
            LockType = "Aes256",
            IsFolder = false,
            FileSize = 1024 * 1024 * 5, // 5 MB
            DateLocked = new DateTime(2026, 10, 23, 15, 30, 0),
            IsLocked = true
        };

        Assert.Equal("5,0 MB", item.FormattedSize.Replace(".", ",")); // Handles cultural comma vs dot
        Assert.Contains("🛡️", item.LockTypeBadge);
        Assert.Equal("Terkunci", item.StatusBadge);

        item.IsLocked = false;
        Assert.Equal("Terbuka", item.StatusBadge);
    }

    [Fact]
    public void LockQueueItem_FormattersAndIcons_ComputeAccurately()
    {
        var folderQueue = new LockQueueItem { Path = @"D:\Photos\Travel", IsFolder = true, SizeBytes = 1024 * 500 };
        Assert.Equal("Travel", folderQueue.Name);
        Assert.Equal(@"D:\Photos\", folderQueue.DirectoryPath);
        Assert.Equal("Direktori", folderQueue.FormattedSize);

        var fileQueue = new LockQueueItem { Path = @"C:\Docs\Contract.pdf", IsFolder = false, SizeBytes = 1024 * 1024 * 2 };
        Assert.Equal("Contract.pdf", fileQueue.Name);
        Assert.Equal(@"C:\Docs\", fileQueue.DirectoryPath);
        Assert.Contains("MB", fileQueue.FormattedSize);
    }

    [Fact]
    public async Task LockItemAsync_And_ShredPermanentAsync_ManagesLifecycle()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wi_reg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "SecretDoc.txt");
        await File.WriteAllTextAsync(testFile, "Registry Lifecycle Verification Plaintext Content");

        var regService = new LockRegistryService();
        string masterPassword = "TestPassword123";

        // 1. Lock File (Aes256)
        var lockedItem = await regService.LockItemAsync(testFile, "Aes256", masterPassword, shredOriginal: false);
        Assert.NotNull(lockedItem);
        Assert.True(File.Exists(lockedItem.CurrentPath));
        Assert.EndsWith(".wisafe", lockedItem.CurrentPath);
        Assert.Contains(lockedItem, regService.LockedItems);

        // 2. Shred Permanent
        await regService.ShredPermanentAsync(lockedItem);
        Assert.False(File.Exists(lockedItem.CurrentPath));
        Assert.DoesNotContain(lockedItem, regService.LockedItems);

        // Clean up
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
}

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class FileShredderTests
{
    [Fact]
    public async Task ShredFileAsync_DeletesFileFromDisk()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"wi_shred_test_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "Sensitive confidential information to be erased.");

        Assert.True(File.Exists(tempFile));

        await FileShredder.ShredFileAsync(tempFile, passes: 1);

        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public async Task ShredFileAsync_3PassDoD_OverwritesAndDeletesSuccessfully()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"wi_shred_dod_{Guid.NewGuid():N}.bin");
        byte[] payload = new byte[64 * 1024]; // 64 KB
        Array.Fill(payload, (byte)0xFF);
        await File.WriteAllBytesAsync(tempFile, payload);

        Assert.True(File.Exists(tempFile));

        // 3-pass DoD 5220.22-M wipe
        await FileShredder.ShredFileAsync(tempFile, passes: 3);

        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public async Task ShredFileAsync_RemovesReadOnlyAttributeAndShreds()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"wi_shred_readonly_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "Readonly secret payload");
        File.SetAttributes(tempFile, FileAttributes.ReadOnly);

        Assert.True((File.GetAttributes(tempFile) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);

        await FileShredder.ShredFileAsync(tempFile, passes: 1);

        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public async Task ShredFileAsync_NonExistentFile_DoesNotThrow()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), $"wi_fake_{Guid.NewGuid():N}.tmp");
        var ex = await Record.ExceptionAsync(async () =>
        {
            await FileShredder.ShredFileAsync(fakePath, passes: 1);
        });

        Assert.Null(ex);
    }

    [Fact]
    public async Task ShredDirectoryAsync_RecursivelyDeletesAllFilesAndSubdirectories()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wi_shred_dir_{Guid.NewGuid():N}");
        string subDir1 = Path.Combine(tempDir, "SubDirA");
        string subDir2 = Path.Combine(subDir1, "SubDirB");

        Directory.CreateDirectory(subDir2);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "root.txt"), "Root file");
        await File.WriteAllTextAsync(Path.Combine(subDir1, "level1.txt"), "Level 1 file");
        await File.WriteAllTextAsync(Path.Combine(subDir2, "level2.txt"), "Level 2 file");

        Assert.True(Directory.Exists(tempDir));
        Assert.Equal(3, Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length);

        await FileShredder.ShredDirectoryAsync(tempDir, passes: 1);

        Assert.False(Directory.Exists(tempDir));
    }
}

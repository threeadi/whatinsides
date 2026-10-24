using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class DirectLockServiceTests
{
    [Fact]
    public async Task EncryptDirectFileAsync_And_DecryptDirectAsync_FileRoundtrip_RestoresContent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wi_direct_file_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string originalFile = Path.Combine(tempDir, "Report.txt");
        string originalContent = "Confidential Financial Report Q4 2026";
        await File.WriteAllTextAsync(originalFile, originalContent);

        string password = "StrongPassword7788!";

        try
        {
            // 1. Encrypt with shredOriginal = false
            string encryptedPath = await DirectLockService.EncryptDirectFileAsync(originalFile, password, shredOriginal: false);
            Assert.True(File.Exists(encryptedPath));
            Assert.EndsWith(DirectLockService.SAFE_EXTENSION, encryptedPath);

            // Delete original to simulate full encryption
            File.Delete(originalFile);

            // 2. Decrypt
            string decryptedPath = await DirectLockService.DecryptDirectAsync(encryptedPath, password, tempDir);
            Assert.True(File.Exists(decryptedPath));

            string restoredContent = await File.ReadAllTextAsync(decryptedPath);
            Assert.Equal(originalContent, restoredContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EncryptDirectFolderAsync_And_DecryptDirectAsync_FolderRoundtrip_RestoresAllFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wi_direct_folder_{Guid.NewGuid():N}");
        string folderToLock = Path.Combine(tempDir, "SecretFolder");
        string nestedDir = Path.Combine(folderToLock, "SubFolder");
        Directory.CreateDirectory(nestedDir);

        await File.WriteAllTextAsync(Path.Combine(folderToLock, "file1.txt"), "File 1 content");
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "file2.txt"), "Nested File 2 content");

        string password = "FolderSecretPassword!";

        try
        {
            // 1. Encrypt folder
            string encryptedPath = await DirectLockService.EncryptDirectFolderAsync(folderToLock, password, shredOriginal: true);
            Assert.True(File.Exists(encryptedPath));
            Assert.False(Directory.Exists(folderToLock)); // Original shredded

            // 2. Decrypt folder
            string decryptedFolder = await DirectLockService.DecryptDirectAsync(encryptedPath, password, tempDir);
            Assert.True(Directory.Exists(decryptedFolder));

            string file1Restored = Path.Combine(decryptedFolder, "file1.txt");
            string file2Restored = Path.Combine(decryptedFolder, "SubFolder", "file2.txt");

            Assert.True(File.Exists(file1Restored));
            Assert.True(File.Exists(file2Restored));
            Assert.Equal("File 1 content", await File.ReadAllTextAsync(file1Restored));
            Assert.Equal("Nested File 2 content", await File.ReadAllTextAsync(file2Restored));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

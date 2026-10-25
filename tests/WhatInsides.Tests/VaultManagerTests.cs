using System;
using System.IO;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class VaultManagerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly SettingsManager _settingsManager;
    private readonly VaultManager _vaultManager;
    private const string VaultPassword = "VaultPassword#2026!";

    public VaultManagerTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), $"wi_vm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBaseDir);
        string configPath = Path.Combine(_testBaseDir, "settings.json");

        _settingsManager = new SettingsManager(configPath);
        string vaultDir = Path.Combine(_testBaseDir, "MainVault");
        Directory.CreateDirectory(vaultDir);

        _settingsManager.Settings.CustomVaultPath = vaultDir;
        _settingsManager.Settings.CryptoAlgorithm = "AES-256-GCM";
        _settingsManager.SaveSettings();
        _settingsManager.SetMasterPassword(VaultPassword);

        _vaultManager = new VaultManager(_settingsManager);
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
    public void Constructor_InitializesInLockedState()
    {
        Assert.False(_vaultManager.IsUnlocked);
        Assert.Empty(_vaultManager.Items);
    }

    [Fact]
    public async Task UnlockVaultAsync_WithValidPassword_Unlocks()
    {
        bool success = await _vaultManager.UnlockVaultAsync(VaultPassword);
        Assert.True(success);
        Assert.True(_vaultManager.IsUnlocked);

        _vaultManager.LockVault();
        Assert.False(_vaultManager.IsUnlocked);
    }

    [Fact]
    public async Task UnlockVaultAsync_WithInvalidPassword_ReturnsFalse()
    {
        bool success = await _vaultManager.UnlockVaultAsync("WrongPassword#999");
        Assert.False(success);
        Assert.False(_vaultManager.IsUnlocked);
    }

    [Fact]
    public async Task LockFileAsync_And_RestoreItemAsync_RoundtripsFileContent()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        string originalFile = Path.Combine(_testBaseDir, "FinancialReport.docx");
        string expectedContent = "CONFIDENTIAL FINANCIAL REPORT 2026 Q4";
        await File.WriteAllTextAsync(originalFile, expectedContent);

        // 1. Lock file into vault
        var vaultItem = await _vaultManager.LockFileAsync(originalFile, shredOriginal: false);

        Assert.NotNull(vaultItem);
        Assert.Equal("FinancialReport.docx", vaultItem.Name);
        Assert.Contains(vaultItem, _vaultManager.Items);

        // Verify encrypted file exists in vault directory
        string encFilePath = Path.Combine(_vaultManager.GetVaultPath(), vaultItem.EncryptedFileName);
        Assert.True(File.Exists(encFilePath));

        // 2. Restore item to destination directory
        string restoreDir = Path.Combine(_testBaseDir, "Restored");
        Directory.CreateDirectory(restoreDir);
        string restoredFilePath = await _vaultManager.RestoreItemAsync(vaultItem, restoreDir);

        Assert.True(File.Exists(restoredFilePath));
        string restoredContent = await File.ReadAllTextAsync(restoredFilePath);
        Assert.Equal(expectedContent, restoredContent);
        Assert.DoesNotContain(vaultItem, _vaultManager.Items);
    }

    [Fact]
    public async Task DeleteItemPermanentlyAsync_RemovesItemAndEncryptedFile()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        string testFile = Path.Combine(_testBaseDir, "Disposable.txt");
        await File.WriteAllTextAsync(testFile, "Temporary Data To Be Deleted");

        var item = await _vaultManager.LockFileAsync(testFile, shredOriginal: false);
        string encFilePath = Path.Combine(_vaultManager.GetVaultPath(), item.EncryptedFileName);
        Assert.True(File.Exists(encFilePath));

        await _vaultManager.DeleteItemPermanentlyAsync(item);

        Assert.False(File.Exists(encFilePath));
        Assert.DoesNotContain(item, _vaultManager.Items);
    }

    [Fact]
    public void PurgeTempPreview_CleansPreviewFolder()
    {
        string previewDir = _vaultManager.GetTempPreviewDirectory();
        string dummyFile = Path.Combine(previewDir, "dummy_preview.tmp");
        File.WriteAllText(dummyFile, "Preview Content");

        _vaultManager.PurgeTempPreview();

        Assert.False(File.Exists(dummyFile));
    }

    [Fact]
    public async Task LockDirectoryAsync_And_RestoreItemAsync_RoundtripsDirectory()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        string sampleDir = Path.Combine(_testBaseDir, "ProjectSource");
        Directory.CreateDirectory(sampleDir);
        string subFile1 = Path.Combine(sampleDir, "main.rs");
        string subFile2 = Path.Combine(sampleDir, "Cargo.toml");
        await File.WriteAllTextAsync(subFile1, "fn main() { println!(\"Safe\"); }");
        await File.WriteAllTextAsync(subFile2, "[package]\nname = \"safe\"");

        // Lock directory
        var vaultItem = await _vaultManager.LockFolderAsync(sampleDir, shredOriginal: false);
        Assert.NotNull(vaultItem);
        Assert.True(vaultItem.IsFolder);
        Assert.Equal("ProjectSource", vaultItem.Name);
        Assert.Contains(vaultItem, _vaultManager.Items);

        // Restore directory
        string restoreDest = Path.Combine(_testBaseDir, "RestoredFolders");
        Directory.CreateDirectory(restoreDest);
        string restoredDir = await _vaultManager.RestoreItemAsync(vaultItem, restoreDest);

        Assert.True(Directory.Exists(restoredDir));
        Assert.True(File.Exists(Path.Combine(restoredDir, "main.rs")));
        Assert.True(File.Exists(Path.Combine(restoredDir, "Cargo.toml")));
    }

    [Fact]
    public async Task LockOperations_WhenLocked_ThrowInvalidOperationException()
    {
        _vaultManager.LockVault();

        string sampleFile = Path.Combine(_testBaseDir, "SampleLocked.txt");
        await File.WriteAllTextAsync(sampleFile, "Data");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vaultManager.LockFileAsync(sampleFile, shredOriginal: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vaultManager.LockFolderAsync(_testBaseDir, shredOriginal: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vaultManager.RestoreItemAsync(new VaultItem(), _testBaseDir));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vaultManager.PreviewItemAsync(new VaultItem()));
    }

    [Fact]
    public async Task LockFileAsync_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _vaultManager.LockFileAsync(Path.Combine(_testBaseDir, "NonExistentFile.txt"), shredOriginal: false));
    }

    [Fact]
    public async Task LockFolderAsync_WhenDirNotFound_ThrowsDirectoryNotFoundException()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _vaultManager.LockFolderAsync(Path.Combine(_testBaseDir, "NonExistentDir"), shredOriginal: false));
    }

    [Fact]
    public async Task UnlockVaultAsync_WrongPassword_ReturnsFalse()
    {
        bool result = await _vaultManager.UnlockVaultAsync("TotallyWrongPassword123!");
        Assert.False(result);
    }

    [Fact]
    public async Task LockFileAsync_WithShredOriginal_ShredsSourceFile()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        string sampleFile = Path.Combine(_testBaseDir, "ShredMe.txt");
        await File.WriteAllTextAsync(sampleFile, "Confidential Secret to Shred");

        var item = await _vaultManager.LockFileAsync(sampleFile, shredOriginal: true);

        Assert.NotNull(item);
        Assert.False(File.Exists(sampleFile));
    }

    [Fact]
    public async Task VaultIndex_ReloadsItems_OnSubsequentUnlock()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        string sampleFile = Path.Combine(_testBaseDir, "PersistIndex.txt");
        await File.WriteAllTextAsync(sampleFile, "Index Persistence Check");

        var item = await _vaultManager.LockFileAsync(sampleFile, shredOriginal: false);
        Assert.Contains(item, _vaultManager.Items);

        // Lock vault
        _vaultManager.LockVault();
        Assert.Empty(_vaultManager.Items);

        // Re-unlock
        bool reUnlocked = await _vaultManager.UnlockVaultAsync(VaultPassword);
        Assert.True(reUnlocked);
        Assert.Contains(_vaultManager.Items, x => x.Name == "PersistIndex.txt");
    }

    [Fact]
    public async Task RestoreItemAsync_WhenDestinationFileExists_CreatesNumberedRestoredFile()
    {
        await _vaultManager.UnlockVaultAsync(VaultPassword);

        string sampleFile = Path.Combine(_testBaseDir, "DuplicateRestore.txt");
        await File.WriteAllTextAsync(sampleFile, "Original Content");

        var item = await _vaultManager.LockFileAsync(sampleFile, shredOriginal: false);

        string restoreDir = Path.Combine(_testBaseDir, "RestoreDirConflict");
        Directory.CreateDirectory(restoreDir);
        // Pre-create file with same name to trigger conflict branch
        await File.WriteAllTextAsync(Path.Combine(restoreDir, "DuplicateRestore.txt"), "Existing File");

        string restoredPath = await _vaultManager.RestoreItemAsync(item, restoreDir);

        Assert.True(File.Exists(restoredPath));
        Assert.Contains("restored", restoredPath);
        Assert.NotEqual(Path.Combine(restoreDir, "DuplicateRestore.txt"), restoredPath);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class CredentialBackupTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storagePath;
    private readonly string _attachmentsDir;
    private readonly CredentialVaultService _service;
    private const string MasterPassword = "StrongMasterPassword#2026";

    public CredentialBackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WhatInsides_BackupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _storagePath = Path.Combine(_tempDir, "vault.json");
        _attachmentsDir = Path.Combine(_tempDir, "attachments");
        _service = new CredentialVaultService(_storagePath, _attachmentsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CreateEncryptedBackup_And_Restore_Roundtrip_PreservesAllItems()
    {
        // 1. Add specific items to vault
        _service.Add(new CredentialItem
        {
            Title = "Riot Games Main Account",
            Category = CredentialCategory.Login,
            Username = "radiant_player",
            Password = "V@lorant#2026Password!",
            Url = "https://playvalorant.com",
            TotpSecret = "JBSWY3DPEHPK3PXP"
        });

        _service.Add(new CredentialItem
        {
            Title = "AWS Production Key",
            Category = CredentialCategory.DeveloperServer,
            Username = "AKIAIOSFODNN7EXAMPLE",
            Password = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
        });

        string backupFile = Path.Combine(_tempDir, "backup_test.wivault");

        // 2. Create encrypted backup
        string resultPath = await _service.CreateEncryptedBackupAsync(backupFile, MasterPassword);
        Assert.True(File.Exists(resultPath));
        Assert.True(new FileInfo(resultPath).Length > 0);

        // 3. Clear vault or create a fresh service
        string freshStorage = Path.Combine(_tempDir, "vault_fresh.json");
        string freshAttachments = Path.Combine(_tempDir, "attachments_fresh");
        var freshService = new CredentialVaultService(freshStorage, freshAttachments);

        // 4. Restore into fresh vault
        var (count, attCount, msg) = await freshService.RestoreEncryptedBackupAsync(resultPath, MasterPassword, replaceExisting: true);

        Assert.True(count >= 2);
        var restoredRiot = freshService.GetAll().FirstOrDefault(x => x.Title == "Riot Games Main Account");
        Assert.NotNull(restoredRiot);
        Assert.Equal("radiant_player", restoredRiot.Username);
        Assert.Equal("V@lorant#2026Password!", restoredRiot.Password);
        Assert.Equal("JBSWY3DPEHPK3PXP", restoredRiot.TotpSecret);

        var restoredAws = freshService.GetAll().FirstOrDefault(x => x.Title == "AWS Production Key");
        Assert.NotNull(restoredAws);
        Assert.Equal("AKIAIOSFODNN7EXAMPLE", restoredAws.Username);
    }

    [Fact]
    public async Task RestoreEncryptedBackup_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        string backupFile = Path.Combine(_tempDir, "backup_wrong_pass.wivault");
        await _service.CreateEncryptedBackupAsync(backupFile, MasterPassword);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _service.RestoreEncryptedBackupAsync(backupFile, "CompletelyWrongPassword#999");
        });
    }

    [Fact]
    public async Task CreateEncryptedBackup_WithAttachments_PreservesAttachmentsOnRestore()
    {
        // 1. Add item and attachment
        var item = new CredentialItem
        {
            Title = "Server Root Key",
            Category = CredentialCategory.DeveloperServer,
            Username = "root",
            Password = "ServerPass#123"
        };
        _service.Add(item);

        string dummyFile = Path.Combine(_tempDir, "server_id_ed25519.pem");
        File.WriteAllText(dummyFile, "-----BEGIN OPENSSH PRIVATE KEY-----\nMOCK_KEY_DATA_CONTENT\n-----END OPENSSH PRIVATE KEY-----");
        await _service.AddAttachmentAsync(item.Id, dummyFile, MasterPassword);

        // 2. Create backup
        string backupFile = Path.Combine(_tempDir, "backup_with_att.wivault");
        await _service.CreateEncryptedBackupAsync(backupFile, MasterPassword);

        // 3. Restore to a new location
        string freshStorage = Path.Combine(_tempDir, "vault_att_fresh.json");
        string freshAttDir = Path.Combine(_tempDir, "attachments_restored");
        var freshService = new CredentialVaultService(freshStorage, freshAttDir);

        var (count, attRestored, _) = await freshService.RestoreEncryptedBackupAsync(backupFile, MasterPassword, replaceExisting: true);

        Assert.True(attRestored >= 1);
        var restoredItem = freshService.GetById(item.Id);
        Assert.NotNull(restoredItem);
        Assert.Single(restoredItem.Attachments);

        // Verify attachment file exists in decrypted/restored form in attachments directory
        string attFileOnDisk = Path.Combine(freshAttDir, restoredItem.Attachments[0].EncryptedFileName);
        Assert.True(File.Exists(attFileOnDisk));

        // Export and decrypt attachment
        string exportedFile = Path.Combine(_tempDir, "exported_recovered.pem");
        await freshService.ExportAttachmentAsync(restoredItem.Id, restoredItem.Attachments[0].Id, exportedFile, MasterPassword);
        string exportedContent = File.ReadAllText(exportedFile);
        Assert.Contains("MOCK_KEY_DATA_CONTENT", exportedContent);
    }

    [Fact]
    public async Task BackupToGoogleDrive_CreatesEncryptedFileAndLatestPointer()
    {
        var settingsMgr = new SettingsManager();
        string customCloudDir = Path.Combine(_tempDir, "FakeGoogleDrive");
        Directory.CreateDirectory(customCloudDir);
        settingsMgr.Settings.CloudBackupFolder = customCloudDir;

        _service.SettingsManager = settingsMgr;

        var (success, bPath, count, msg) = await _service.BackupToGoogleDriveAsync(MasterPassword);

        Assert.True(success);
        Assert.True(File.Exists(bPath));
        Assert.EndsWith(".wivault", bPath);

        string latestFile = Path.Combine(Path.GetDirectoryName(bPath)!, "WhatInsides_Credentials_Latest.wivault");
        Assert.True(File.Exists(latestFile));

        var backups = _service.GetGoogleDriveBackups();
        Assert.NotEmpty(backups);
        Assert.Contains(backups, f => f.FullName == bPath);
    }
}

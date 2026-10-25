using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class CredentialAttachmentTests : IDisposable
{
    private readonly string _tempVaultFile;
    private readonly string _tempAttachmentsDir;
    private readonly CredentialVaultService _vaultService;
    private const string MasterPassword = "StrongMasterPassword#2026";

    public CredentialAttachmentTests()
    {
        string root = Path.Combine(Path.GetTempPath(), $"wi_att_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        _tempVaultFile = Path.Combine(root, "vault.json");
        _tempAttachmentsDir = Path.Combine(root, "attachments");
        _vaultService = new CredentialVaultService(_tempVaultFile, _tempAttachmentsDir);
    }

    public void Dispose()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_tempVaultFile);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task AddAttachmentAsync_EncryptsFileAndPersistsMetadata()
    {
        var item = _vaultService.GetAll().First();
        string testFilePath = Path.Combine(Path.GetTempPath(), $"sample_key_{Guid.NewGuid():N}.pem");
        string originalContent = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEAzSecretPrivateKeyData...\n-----END RSA PRIVATE KEY-----";
        await File.WriteAllTextAsync(testFilePath, originalContent);

        try
        {
            var attachment = await _vaultService.AddAttachmentAsync(item.Id, testFilePath, MasterPassword);

            Assert.NotNull(attachment);
            Assert.Equal(Path.GetFileName(testFilePath), attachment.FileName);
            Assert.True(attachment.FileSizeBytes > 0);
            Assert.NotEmpty(attachment.EncryptedFileName);

            string encFilePath = Path.Combine(_vaultService.GetAttachmentsDirectory(), attachment.EncryptedFileName);
            Assert.True(File.Exists(encFilePath));

            // Verify encrypted content does not contain plain text
            byte[] encBytes = await File.ReadAllBytesAsync(encFilePath);
            string encString = Encoding.UTF8.GetString(encBytes);
            Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", encString);

            // Verify reloaded service retains attachment
            var reloadedService = new CredentialVaultService(_tempVaultFile, _tempAttachmentsDir);
            var reloadedItem = reloadedService.GetById(item.Id);
            Assert.NotNull(reloadedItem);
            Assert.Contains(reloadedItem.Attachments, a => a.Id == attachment.Id && a.FileName == attachment.FileName);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ExportAttachmentAsync_DecryptsFileExactMatchesOriginal()
    {
        var item = _vaultService.GetAll().First();
        string testFilePath = Path.Combine(Path.GetTempPath(), $"invoice_{Guid.NewGuid():N}.pdf");
        byte[] originalBytes = Encoding.UTF8.GetBytes("%PDF-1.7 confidential financial statement data 2026...");
        await File.WriteAllBytesAsync(testFilePath, originalBytes);

        string exportFilePath = Path.Combine(Path.GetTempPath(), $"exported_{Guid.NewGuid():N}.pdf");

        try
        {
            var attachment = await _vaultService.AddAttachmentAsync(item.Id, testFilePath, MasterPassword);
            await _vaultService.ExportAttachmentAsync(item.Id, attachment.Id, exportFilePath, MasterPassword);

            Assert.True(File.Exists(exportFilePath));
            byte[] exportedBytes = await File.ReadAllBytesAsync(exportFilePath);
            Assert.Equal(originalBytes, exportedBytes);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
            if (File.Exists(exportFilePath)) File.Delete(exportFilePath);
        }
    }

    [Fact]
    public async Task ExportAttachmentAsync_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        var item = _vaultService.GetAll().First();
        string testFilePath = Path.Combine(Path.GetTempPath(), $"id_card_{Guid.NewGuid():N}.png");
        await File.WriteAllTextAsync(testFilePath, "KTP Image binary raw sample 8392183");
        string exportFilePath = Path.Combine(Path.GetTempPath(), $"fail_export_{Guid.NewGuid():N}.png");

        try
        {
            var attachment = await _vaultService.AddAttachmentAsync(item.Id, testFilePath, MasterPassword);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await _vaultService.ExportAttachmentAsync(item.Id, attachment.Id, exportFilePath, "WrongPassword#999");
            });
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
            if (File.Exists(exportFilePath)) File.Delete(exportFilePath);
        }
    }

    [Fact]
    public async Task DecryptAttachmentToTempAsync_CreatesTempFileInDesignatedFolder()
    {
        var item = _vaultService.GetAll().First();
        string testFilePath = Path.Combine(Path.GetTempPath(), $"ssh_config_{Guid.NewGuid():N}.conf");
        await File.WriteAllTextAsync(testFilePath, "Host enterprise\n  HostName 10.0.0.1\n  User root");

        try
        {
            var attachment = await _vaultService.AddAttachmentAsync(item.Id, testFilePath, MasterPassword);
            string tempPath = await _vaultService.DecryptAttachmentToTempAsync(item.Id, attachment.Id, MasterPassword);

            Assert.True(File.Exists(tempPath));
            string tempContent = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("Host enterprise", tempContent);
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task DeleteAttachment_RemovesMetadataAndDeletesEncryptedFile()
    {
        var item = _vaultService.GetAll().First();
        string testFilePath = Path.Combine(Path.GetTempPath(), $"temp_cert_{Guid.NewGuid():N}.crt");
        await File.WriteAllTextAsync(testFilePath, "-----BEGIN CERTIFICATE-----\nMII...\n-----END CERTIFICATE-----");

        try
        {
            var attachment = await _vaultService.AddAttachmentAsync(item.Id, testFilePath, MasterPassword);
            string encFilePath = Path.Combine(_vaultService.GetAttachmentsDirectory(), attachment.EncryptedFileName);
            Assert.True(File.Exists(encFilePath));

            bool deleted = _vaultService.DeleteAttachment(item.Id, attachment.Id);
            Assert.True(deleted);
            Assert.DoesNotContain(item.Attachments, a => a.Id == attachment.Id);
            Assert.False(File.Exists(encFilePath));
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task PermanentDeleteCredential_CleansUpAllAssociatedAttachments()
    {
        var item = _vaultService.GetAll().First();
        string testFilePath = Path.Combine(Path.GetTempPath(), $"license_{Guid.NewGuid():N}.lic");
        await File.WriteAllTextAsync(testFilePath, "LICENSE_KEY=WI-2026-XCHA-POLY");

        try
        {
            var attachment = await _vaultService.AddAttachmentAsync(item.Id, testFilePath, MasterPassword);
            string encFilePath = Path.Combine(_vaultService.GetAttachmentsDirectory(), attachment.EncryptedFileName);
            Assert.True(File.Exists(encFilePath));

            // Permanently delete credential
            _vaultService.Delete(item.Id, permanent: true);

            Assert.Null(_vaultService.GetById(item.Id));
            Assert.False(File.Exists(encFilePath));
        }
        finally
        {
            if (File.Exists(testFilePath)) File.Delete(testFilePath);
        }
    }
}

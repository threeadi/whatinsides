using System;
using System.IO;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class SettingsManagerTests
{
    [Fact]
    public void SettingsManager_InitializesWithDefaultValues()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"wi_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var sm = new SettingsManager(tempSettings);
            Assert.NotNull(sm.Settings);
            Assert.True(sm.Settings.AutoLockMinutes >= 0);
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }

    [Fact]
    public void SetMasterPassword_And_VerifyPassword_WorksCorrectly()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"wi_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var sm = new SettingsManager(tempSettings);
            string testPassword = "MySecureVaultPassword2026!";
            string hint = "Year + exclamation";

            sm.SetMasterPassword(testPassword, hint);

            Assert.True(sm.Settings.IsPasswordConfigured);
            Assert.False(string.IsNullOrEmpty(sm.Settings.PasswordSalt));
            Assert.False(string.IsNullOrEmpty(sm.Settings.PasswordVerifier));
            Assert.Equal(hint, sm.Settings.PasswordHint);

            // Verify correct password
            Assert.True(sm.VerifyPassword(testPassword));

            // Verify incorrect password
            Assert.False(sm.VerifyPassword("WrongPassword123"));
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }

    [Fact]
    public void SaveSettings_And_LoadSettings_PersistsValues()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"wi_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var sm = new SettingsManager(tempSettings);
            sm.Settings.AutoLockMinutes = 15;
            sm.Settings.ClipboardAutoClearSeconds = 60;
            sm.Settings.AutoLockOnMinimize = true;
            sm.Settings.LockOnSleep = true;
            sm.Settings.MinimizeToTray = true;
            sm.Settings.CryptoAlgorithm = "AES-256-GCM";
            sm.Settings.KdfAlgorithm = "Argon2id";
            sm.Settings.CloudAutoUpload = false;
            sm.Settings.CloudBackupFolder = @"C:\Backups\WhatInsides";
            sm.Settings.CloudSyncWifiOnly = false;
            sm.Settings.CloudZeroKnowledgeE2E = true;

            sm.SaveSettings();

            // Create new instance which re-reads from disk
            var reloaded = new SettingsManager(tempSettings);
            Assert.Equal(15, reloaded.Settings.AutoLockMinutes);
            Assert.Equal(60, reloaded.Settings.ClipboardAutoClearSeconds);
            Assert.True(reloaded.Settings.AutoLockOnMinimize);
            Assert.True(reloaded.Settings.LockOnSleep);
            Assert.True(reloaded.Settings.MinimizeToTray);
            Assert.Equal("AES-256-GCM", reloaded.Settings.CryptoAlgorithm);
            Assert.Equal("Argon2id", reloaded.Settings.KdfAlgorithm);
            Assert.False(reloaded.Settings.CloudAutoUpload);
            Assert.Equal(@"C:\Backups\WhatInsides", reloaded.Settings.CloudBackupFolder);
            Assert.False(reloaded.Settings.CloudSyncWifiOnly);
            Assert.True(reloaded.Settings.CloudZeroKnowledgeE2E);
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }

    [Fact]
    public void GetVaultDirectory_ReturnsValidDirectory()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"wi_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var sm = new SettingsManager(tempSettings);
            string vaultDir = sm.GetVaultDirectory();

            Assert.False(string.IsNullOrWhiteSpace(vaultDir));
            Assert.True(Directory.Exists(vaultDir));
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }

    [Fact]
    public void VerifyPassword_StrictlyRejects_EmptyAndRandomPasswords()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"wi_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var sm = new SettingsManager(tempSettings);
            sm.SetMasterPassword("CorrectMasterPassword123!");

            // Random arbitrary passwords must fail
            Assert.False(sm.VerifyPassword("ngasal"));
            Assert.False(sm.VerifyPassword("random123"));
            Assert.False(sm.VerifyPassword(""));
            Assert.False(sm.VerifyPassword("   "));
            Assert.False(sm.VerifyPassword("admin123")); // not the password

            // Only exact password passes
            Assert.True(sm.VerifyPassword("CorrectMasterPassword123!"));
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }

    [Fact]
    public void ChangeMasterPassword_WithValidOldPassword_UpdatesVerifier_And_InvalidatesOldPassword()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"wi_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var sm = new SettingsManager(tempSettings);
            string oldPass = "OldPassword2026!";
            string newPass = "NewStrongMasterPassword2026!";

            sm.SetMasterPassword(oldPass);
            Assert.True(sm.VerifyPassword(oldPass));

            // Simulating change password logic: Verify old password first
            string attemptedOldRandom = "ngasal123";
            bool canChangeWithRandom = sm.VerifyPassword(attemptedOldRandom);
            Assert.False(canChangeWithRandom); // Must NOT be allowed to change with random old password!

            // Valid old password verified
            bool canChangeWithValid = sm.VerifyPassword(oldPass);
            Assert.True(canChangeWithValid);

            // Execute change
            sm.SetMasterPassword(newPass);

            // Post-change verification
            Assert.False(sm.VerifyPassword(oldPass)); // Old password MUST be invalid
            Assert.True(sm.VerifyPassword(newPass));  // New password MUST be valid

            // DPAPI Biometric retrieval also returns new password
            string? bioPass = sm.GetBiometricUnlockedPassword();
            Assert.Equal(newPass, bioPass);
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }
}

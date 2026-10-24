using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using WhatInsides.Models;

namespace WhatInsides.Services;

public class SettingsManager
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhatInsides");

    public static readonly string DefaultSettingsFilePath = Path.Combine(AppDataFolder, "settings.json");

    private readonly string _settingsFilePath;

    public AppSettings Settings { get; private set; }

    public SettingsManager(string? customSettingsPath = null)
    {
        _settingsFilePath = customSettingsPath ?? DefaultSettingsFilePath;
        Settings = LoadSettings();
    }

    public bool IsPasswordSet() => Settings.IsPasswordConfigured && !string.IsNullOrEmpty(Settings.PasswordVerifier);

    public string GetVaultDirectory()
    {
        if (!string.IsNullOrWhiteSpace(Settings.CustomVaultPath) && Directory.Exists(Settings.CustomVaultPath))
        {
            return Settings.CustomVaultPath;
        }

        string defaultVault = Path.Combine(AppDataFolder, "Vault");
        if (!Directory.Exists(defaultVault))
        {
            Directory.CreateDirectory(defaultVault);
        }
        return defaultVault;
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch { }

        return new AppSettings();
    }

    public void SaveSettings()
    {
        try
        {
            string dir = Path.GetDirectoryName(_settingsFilePath) ?? AppDataFolder;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { }
    }

    public void SetMasterPassword(string password, string hint = "")
    {
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        string verifier = CryptoEngine.CreatePasswordVerifier(password, salt);

        Settings.PasswordSalt = Convert.ToBase64String(salt);
        Settings.PasswordVerifier = verifier;
        Settings.PasswordHint = hint;
        Settings.IsPasswordConfigured = true;

        // Securely protect password using DPAPI (CurrentUser) for local biometric unlock
        try
        {
            byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            Settings.BiometricKeyBlob = Convert.ToBase64String(protectedBytes);
        }
        catch { }

        SaveSettings();
    }

    public string? GetBiometricUnlockedPassword()
    {
        try
        {
            if (!string.IsNullOrEmpty(Settings.BiometricKeyBlob))
            {
                byte[] protectedBytes = Convert.FromBase64String(Settings.BiometricKeyBlob);
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                string pass = System.Text.Encoding.UTF8.GetString(plainBytes);
                if (VerifyPassword(pass))
                {
                    return pass;
                }
            }
        }
        catch { }
        return null;
    }

    public void ReloadSettings()
    {
        Settings = LoadSettings();
    }

    public bool VerifyPassword(string password)
    {
        ReloadSettings();

        if (!Settings.IsPasswordConfigured || string.IsNullOrEmpty(Settings.PasswordSalt) || string.IsNullOrEmpty(Settings.PasswordVerifier))
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(Settings.PasswordSalt);
        return CryptoEngine.VerifyMasterPassword(password, salt, Settings.PasswordVerifier);
    }
}

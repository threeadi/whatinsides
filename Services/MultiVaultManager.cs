using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using WhatInsides.Models;

namespace WhatInsides.Services;

public class MultiVaultManager
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhatInsides");

    private static readonly string VaultsIndexFile = Path.Combine(AppDataFolder, "vaults_registry.json");

    public ObservableCollection<VaultModel> Vaults { get; } = new();
    public VaultFileSystemService FileSystemService { get; } = new();

    public MultiVaultManager()
    {
        LoadVaults();
    }

    public void LoadVaults()
    {
        Vaults.Clear();

        try
        {
            if (File.Exists(VaultsIndexFile))
            {
                string json = File.ReadAllText(VaultsIndexFile);
                var loaded = JsonSerializer.Deserialize<ObservableCollection<VaultModel>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    foreach (var v in loaded)
                    {
                        v.IsLocked = true; // Always locked on app start
                        Vaults.Add(v);
                    }
                    return;
                }
            }
        }
        catch { }

        // Seed initial vaults like in SecureFolderFS screenshot
        CreateDefaultVaults();
    }

    private void CreateDefaultVaults()
    {
        string defaultPath1 = Path.Combine(AppDataFolder, "Vaults", "SecureVault_Store");
        string defaultPath2 = Path.Combine(AppDataFolder, "Vaults", "MyVault_Store");

        Directory.CreateDirectory(defaultPath1);
        Directory.CreateDirectory(defaultPath2);

        // Vault 1: Secure Vault
        byte[] salt1 = RandomNumberGenerator.GetBytes(32);
        var v1 = new VaultModel
        {
            Name = "Secure Vault",
            EncryptedStoragePath = defaultPath1,
            LastActivity = DateTime.Now,
            IsLocked = true,
            PasswordSalt = Convert.ToBase64String(salt1),
            PasswordVerifier = CryptoEngine.CreatePasswordVerifier("admin123", salt1),
            PasswordHint = "Default password: admin123",
            EncryptionAlgorithm = "AES-256-GCM (XChaCha20)",
            KdfAlgorithm = "Argon2id KDF",
            CloudProvider = "OneDrive",
            FileCount = 3,
            TotalBytes = 14_850_000
        };

        // Vault 2: My Vault
        byte[] salt2 = RandomNumberGenerator.GetBytes(32);
        var v2 = new VaultModel
        {
            Name = "My Vault",
            EncryptedStoragePath = defaultPath2,
            LastActivity = DateTime.Now.AddHours(-3),
            IsLocked = true,
            PasswordSalt = Convert.ToBase64String(salt2),
            PasswordVerifier = CryptoEngine.CreatePasswordVerifier("admin123", salt2),
            PasswordHint = "Default password: admin123",
            EncryptionAlgorithm = "XChaCha20-Poly1305",
            KdfAlgorithm = "Argon2id KDF",
            CloudProvider = "Google Drive",
            FileCount = 1,
            TotalBytes = 4_200_000
        };

        Vaults.Add(v1);
        Vaults.Add(v2);

        SaveVaults();
    }

    public void SaveVaults()
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Vaults, options);
            File.WriteAllText(VaultsIndexFile, json);
        }
        catch { }
    }

    public VaultModel CreateNewVault(
        string name,
        string customPath,
        string password,
        string cloudProvider = "None",
        string algorithm = "AES-256-GCM",
        string hint = "")
    {
        string storageDir = !string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath)
            ? Path.Combine(customPath, $"{name}_EncryptedVault")
            : Path.Combine(AppDataFolder, "Vaults", $"{Guid.NewGuid():N}_Vault");

        if (!Directory.Exists(storageDir))
        {
            Directory.CreateDirectory(storageDir);
        }

        byte[] salt = RandomNumberGenerator.GetBytes(32);
        string verifier = CryptoEngine.CreatePasswordVerifier(password, salt);

        var vault = new VaultModel
        {
            Name = name,
            EncryptedStoragePath = storageDir,
            LastActivity = DateTime.Now,
            IsLocked = true,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordVerifier = verifier,
            PasswordHint = hint,
            EncryptionAlgorithm = algorithm,
            KdfAlgorithm = "Argon2id KDF",
            CloudProvider = cloudProvider,
            FileCount = 0,
            TotalBytes = 0
        };

        Vaults.Add(vault);
        SaveVaults();
        return vault;
    }

    public async Task<bool> UnlockVaultAsync(VaultModel vault, string password)
    {
        byte[] salt = Convert.FromBase64String(vault.PasswordSalt);
        bool isValid = CryptoEngine.VerifyMasterPassword(password, salt, vault.PasswordVerifier);

        if (!isValid) return false;

        await FileSystemService.MountVaultAsync(vault, password);
        vault.LastActivity = DateTime.Now;
        SaveVaults();
        return true;
    }

    public async Task LockVaultAsync(VaultModel vault)
    {
        await FileSystemService.UnmountAndLockVaultAsync(vault);
        SaveVaults();
    }
}

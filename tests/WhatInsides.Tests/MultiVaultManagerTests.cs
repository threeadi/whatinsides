using System;
using System.IO;
using System.Threading.Tasks;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class MultiVaultManagerTests : IDisposable
{
    private readonly string _testCustomPath;

    public MultiVaultManagerTests()
    {
        _testCustomPath = Path.Combine(Path.GetTempPath(), $"wi_multivault_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testCustomPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testCustomPath))
            {
                Directory.Delete(_testCustomPath, true);
            }
        }
        catch { }
    }

    [Fact]
    public void Constructor_InitializesVaultsCollection()
    {
        var manager = new MultiVaultManager();
        Assert.NotNull(manager.Vaults);
        Assert.NotEmpty(manager.Vaults);
        Assert.Contains(manager.Vaults, v => v.Name == "Secure Vault" || v.Name == "My Vault");
    }

    [Fact]
    public void CreateNewVault_AddsVaultAndCreatesDirectory()
    {
        var manager = new MultiVaultManager();
        string vaultName = "EnterpriseVault";
        string password = "EnterprisePassword#2026";

        var vault = manager.CreateNewVault(vaultName, _testCustomPath, password, "Google Drive", "AES-256-GCM", "Company Secret");

        Assert.NotNull(vault);
        Assert.Equal(vaultName, vault.Name);
        Assert.True(Directory.Exists(vault.EncryptedStoragePath));
        Assert.Contains(vault, manager.Vaults);
        Assert.True(vault.IsLocked);
    }

    [Fact]
    public async Task UnlockVaultAsync_WithCorrectPassword_UnlocksAndSetsMountDirectory()
    {
        var manager = new MultiVaultManager();
        string vaultName = "UnlockableVault";
        string password = "SecretPassword123!";

        var vault = manager.CreateNewVault(vaultName, _testCustomPath, password);

        bool success = await manager.UnlockVaultAsync(vault, password);
        Assert.True(success);
        Assert.False(vault.IsLocked);
        Assert.NotNull(vault.UnlockedDirectory);
        Assert.True(Directory.Exists(vault.UnlockedDirectory));

        // Re-lock
        await manager.LockVaultAsync(vault);
        Assert.True(vault.IsLocked);
        Assert.Null(vault.UnlockedDirectory);
    }

    [Fact]
    public async Task UnlockVaultAsync_WithWrongPassword_ReturnsFalse()
    {
        var manager = new MultiVaultManager();
        string vaultName = "StrictVault";
        string password = "CorrectPassword123";

        var vault = manager.CreateNewVault(vaultName, _testCustomPath, password);

        bool success = await manager.UnlockVaultAsync(vault, "IncorrectPassword999");
        Assert.False(success);
        Assert.True(vault.IsLocked);
    }

    [Fact]
    public void SaveVaults_PersistsWithoutThrowing()
    {
        var manager = new MultiVaultManager();
        manager.SaveVaults();
        Assert.NotEmpty(manager.Vaults);
    }
}

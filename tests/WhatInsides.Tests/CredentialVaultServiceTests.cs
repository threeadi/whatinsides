using System;
using System.IO;
using System.Linq;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class CredentialVaultServiceTests : IDisposable
{
    private readonly string _tempFile;

    public CredentialVaultServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"wi_cred_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public void Constructor_WhenFileMissing_PopulatesSeedDataAndSavesFile()
    {
        var service = new CredentialVaultService(_tempFile);
        var items = service.GetAll();

        Assert.NotEmpty(items);
        Assert.True(File.Exists(_tempFile));
        Assert.Contains(items, x => x.Title.Contains("Valorant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Add_InsertsItemAtBeginning_AndPersistsToFile()
    {
        var service = new CredentialVaultService(_tempFile);
        var newItem = new CredentialItem
        {
            Title = "Custom Test Service",
            Category = CredentialCategory.Login,
            Username = "user@test.local",
            Password = "SecurePassword123!",
            VaultPartition = @"D:\ Games & Tools"
        };

        service.Add(newItem);

        var retrieved = service.GetById(newItem.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Custom Test Service", retrieved.Title);

        // Reload from file to verify persistence
        var reloadedService = new CredentialVaultService(_tempFile);
        var reloadedItem = reloadedService.GetById(newItem.Id);
        Assert.NotNull(reloadedItem);
        Assert.Equal("user@test.local", reloadedItem.Username);
    }

    [Fact]
    public void Update_ModifiesProperties_AndReflectsInGetById()
    {
        var service = new CredentialVaultService(_tempFile);
        var item = service.GetAll().First();

        item.Title = "Updated Title 99";
        item.Password = "NewPassword99!";
        service.Update(item);

        var updated = service.GetById(item.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title 99", updated.Title);
        Assert.Equal("NewPassword99!", updated.Password);
    }

    [Fact]
    public void Delete_SoftDelete_MovesItemToTrash()
    {
        var service = new CredentialVaultService(_tempFile);
        var item = service.GetAll().First(x => !x.IsDeleted);

        service.Delete(item.Id, permanent: false);

        var trashed = service.GetById(item.Id);
        Assert.NotNull(trashed);
        Assert.True(trashed.IsDeleted);

        var activeItems = service.Filter(CredentialCategory.All).ToList();
        Assert.DoesNotContain(activeItems, x => x.Id == item.Id);

        var trashItems = service.Filter(CredentialCategory.Trash).ToList();
        Assert.Contains(trashItems, x => x.Id == item.Id);
    }

    [Fact]
    public void Delete_Permanent_RemovesItemCompletely()
    {
        var service = new CredentialVaultService(_tempFile);
        var item = service.GetAll().First();

        service.Delete(item.Id, permanent: true);

        Assert.Null(service.GetById(item.Id));
    }

    [Fact]
    public void Restore_RecoversItemFromTrash()
    {
        var service = new CredentialVaultService(_tempFile);
        var item = service.GetAll().First(x => !x.IsDeleted);

        service.Delete(item.Id, permanent: false);
        Assert.True(service.GetById(item.Id)!.IsDeleted);

        service.Restore(item.Id);
        Assert.False(service.GetById(item.Id)!.IsDeleted);
    }

    [Fact]
    public void ToggleFavorite_InvertsState()
    {
        var service = new CredentialVaultService(_tempFile);
        var item = service.GetAll().First(x => !x.IsDeleted);
        bool initial = item.IsFavorite;

        service.ToggleFavorite(item.Id);
        Assert.Equal(!initial, service.GetById(item.Id)!.IsFavorite);

        service.ToggleFavorite(item.Id);
        Assert.Equal(initial, service.GetById(item.Id)!.IsFavorite);
    }

    [Fact]
    public void Filter_WithSearchQuery_MatchesTitleOrUsernameOrUrl()
    {
        var service = new CredentialVaultService(_tempFile);
        var results = service.Filter(CredentialCategory.All, searchQuery: "AWS").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, x =>
            Assert.True(x.Title.Contains("AWS", StringComparison.OrdinalIgnoreCase) ||
                        x.Username.Contains("AWS", StringComparison.OrdinalIgnoreCase) ||
                        (x.Url != null && x.Url.Contains("AWS", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void Filter_WithCategory_ReturnsOnlyCategory()
    {
        var service = new CredentialVaultService(_tempFile);
        var results = service.Filter(CredentialCategory.Finance).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal(CredentialCategory.Finance, x.Category));
    }

    [Theory]
    [InlineData("Steam Vault", "", "Steam")]
    [InlineData("Valorant (Riot Games)", "playvalorant.com", "Valorant")]
    [InlineData("Discord Community", "", "Discord")]
    [InlineData("Personal GitHub", "github.com/login", "GitHub")]
    [InlineData("AWS Production Root", "aws.amazon.com", "AWS")]
    [InlineData("BCA Mobile / KlikBCA", "klikbca.com", "BCA")]
    [InlineData("ChatGPT Plus", "chatgpt.com", "OpenAI")]
    [InlineData("Netflix Family", "netflix.com", "Netflix")]
    [InlineData("Bank Mandiri Livin", "livin.bankmandiri.co.id", "Mandiri")]
    public void GetEffectiveBrandKey_AutoDetectsCommonBrands_FromTitleOrUrl(string title, string url, string expectedBrand)
    {
        var item = new CredentialItem
        {
            Title = title,
            Url = url,
            IconKey = "Auto"
        };

        string brand = item.GetEffectiveBrandKey();
        Assert.Equal(expectedBrand, brand);
    }

    [Fact]
    public void GetEffectiveBrandKey_ExplicitIconKey_OverridesAutoDetection()
    {
        var item = new CredentialItem
        {
            Title = "Valorant Account",
            IconKey = "Steam"
        };

        Assert.Equal("Steam", item.GetEffectiveBrandKey());
    }

    [Fact]
    public void Filter_WithPartition_FiltersMatchingVaultPartitionOnly()
    {
        var service = new CredentialVaultService(_tempFile);
        var dItems = service.Filter(CredentialCategory.All, partition: @"D:\ Data & Games").ToList();
        Assert.NotEmpty(dItems);
        Assert.All(dItems, x => Assert.Equal(@"D:\ Data & Games", x.VaultPartition));

        var allItems = service.Filter(CredentialCategory.All, partition: "ALL").ToList();
        Assert.True(allItems.Count >= dItems.Count);
    }

    [Fact]
    public void CredentialItem_RaisesPropertyChanged_WhenIsFavoriteOrRelatedPropertiesChange()
    {
        var item = new CredentialItem
        {
            Title = "Original Title",
            Category = CredentialCategory.Login,
            IsFavorite = false
        };

        var changedProps = new List<string>();
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProps.Add(e.PropertyName);
        };

        item.IsFavorite = true;
        Assert.Contains(nameof(CredentialItem.IsFavorite), changedProps);

        changedProps.Clear();
        item.Title = "New Title";
        Assert.Contains(nameof(CredentialItem.Title), changedProps);
        Assert.Contains(nameof(CredentialItem.DisplayLetter), changedProps);
    }

    [Fact]
    public void CredentialItem_CategoryDisplayName_ReturnsExpectedLabel_ForAllCategories()
    {
        Assert.Equal("Akun & Login", new CredentialItem { Category = CredentialCategory.Login }.CategoryDisplayName);
        Assert.Equal("Keuangan & Kartu", new CredentialItem { Category = CredentialCategory.Finance }.CategoryDisplayName);
        Assert.Equal("Identitas & Dokumen", new CredentialItem { Category = CredentialCategory.Identity }.CategoryDisplayName);
        Assert.Equal("Developer & Server", new CredentialItem { Category = CredentialCategory.DeveloperServer }.CategoryDisplayName);
        Assert.Equal("Kripto & Seed", new CredentialItem { Category = CredentialCategory.CryptoSeed }.CategoryDisplayName);
        Assert.Equal("Catatan & Lisensi", new CredentialItem { Category = CredentialCategory.SecureNote }.CategoryDisplayName);
    }

    [Fact]
    public void CredentialVaultService_CanSaveAndRetrieve_AllAdaptiveCategories()
    {
        var service = new CredentialVaultService(_tempFile);

        var categories = new[]
        {
            CredentialCategory.Login,
            CredentialCategory.Finance,
            CredentialCategory.Identity,
            CredentialCategory.DeveloperServer,
            CredentialCategory.CryptoSeed,
            CredentialCategory.SecureNote
        };

        foreach (var cat in categories)
        {
            service.Add(new CredentialItem
            {
                Title = $"Test Item for {cat}",
                Category = cat,
                Username = $"User_{cat}",
                Password = $"Secret_{cat}"
            });
        }

        var reloaded = new CredentialVaultService(_tempFile);
        foreach (var cat in categories)
        {
            var match = reloaded.GetAll().FirstOrDefault(x => x.Category == cat && x.Title == $"Test Item for {cat}");
            Assert.NotNull(match);
            Assert.Equal($"User_{cat}", match.Username);
            Assert.Equal($"Secret_{cat}", match.Password);
        }
    }

    [Fact]
    public void ExportJson_And_ImportJson_Roundtrip_PreservesCredentials()
    {
        var service = new CredentialVaultService(_tempFile);
        string exportPath = Path.Combine(Path.GetTempPath(), $"wi_export_{Guid.NewGuid():N}.json");

        try
        {
            service.ExportJson(exportPath);
            Assert.True(File.Exists(exportPath));

            string newDb = Path.Combine(Path.GetTempPath(), $"wi_import_target_{Guid.NewGuid():N}.json");
            File.WriteAllText(newDb, "[]"); // empty database

            var importService = new CredentialVaultService(newDb);
            var (count, msg) = importService.ImportJsonOrCsv(exportPath);

            Assert.True(count > 0);
            Assert.Contains(importService.GetAll(), x => x.Title.Contains("Valorant", StringComparison.OrdinalIgnoreCase));

            if (File.Exists(newDb)) File.Delete(newDb);
        }
        finally
        {
            if (File.Exists(exportPath)) File.Delete(exportPath);
        }
    }

    [Fact]
    public void ExportCsv_And_ImportCsv_Roundtrip_PreservesCredentials()
    {
        var service = new CredentialVaultService(_tempFile);
        string exportCsv = Path.Combine(Path.GetTempPath(), $"wi_export_{Guid.NewGuid():N}.csv");

        try
        {
            service.ExportCsv(exportCsv);
            Assert.True(File.Exists(exportCsv));

            string newDb = Path.Combine(Path.GetTempPath(), $"wi_import_csv_{Guid.NewGuid():N}.json");
            File.WriteAllText(newDb, "[]");

            var importService = new CredentialVaultService(newDb);
            var (count, msg) = importService.ImportJsonOrCsv(exportCsv);

            Assert.True(count > 0);
            Assert.Contains(importService.GetAll(), x => x.Title.Contains("Valorant", StringComparison.OrdinalIgnoreCase));

            if (File.Exists(newDb)) File.Delete(newDb);
        }
        finally
        {
            if (File.Exists(exportCsv)) File.Delete(exportCsv);
        }
    }

    [Fact]
    public void ImportBitwardenJson_ImportsItemsProperly()
    {
        string bwJsonPath = Path.Combine(Path.GetTempPath(), $"bw_mock_{Guid.NewGuid():N}.json");
        string bwContent = """
        {
            "encrypted": false,
            "items": [
                {
                    "name": "Bitwarden Exported Server",
                    "notes": "Testing Bitwarden import",
                    "login": {
                        "username": "admin@cloud.io",
                        "password": "BwPassword!2026",
                        "totp": "JBSWY3DPEHPK3PXP",
                        "uris": [ { "uri": "https://vault.bitwarden.com" } ]
                    }
                }
            ]
        }
        """;
        File.WriteAllText(bwJsonPath, bwContent);

        string targetDb = Path.Combine(Path.GetTempPath(), $"wi_bw_target_{Guid.NewGuid():N}.json");
        File.WriteAllText(targetDb, "[]");

        try
        {
            var service = new CredentialVaultService(targetDb);
            var (count, msg) = service.ImportJsonOrCsv(bwJsonPath);

            Assert.Equal(1, count);
            var imported = service.GetAll().FirstOrDefault(x => x.Title == "Bitwarden Exported Server");
            Assert.NotNull(imported);
            Assert.Equal("admin@cloud.io", imported.Username);
            Assert.Equal("BwPassword!2026", imported.Password);
            Assert.Equal("https://vault.bitwarden.com", imported.Url);
            Assert.Equal("JBSWY3DPEHPK3PXP", imported.TotpSecret);
        }
        finally
        {
            if (File.Exists(bwJsonPath)) File.Delete(bwJsonPath);
            if (File.Exists(targetDb)) File.Delete(targetDb);
        }
    }
}

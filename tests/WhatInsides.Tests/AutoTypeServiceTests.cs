using System;
using System.Collections.Generic;
using System.IO;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class AutoTypeServiceTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly CredentialVaultService _vaultService;
    private readonly SettingsManager _settingsManager;
    private readonly TotpService _totpService;
    private readonly AutoTypeService _autoTypeService;

    public AutoTypeServiceTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), $"wi_autotype_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBaseDir);

        string vaultPath = Path.Combine(_testBaseDir, "test_credentials.json");
        string attachDir = Path.Combine(_testBaseDir, "attachments");

        _vaultService = new CredentialVaultService(vaultPath, attachDir);
        _settingsManager = new SettingsManager();
        _totpService = new TotpService();

        _autoTypeService = new AutoTypeService(
            _vaultService,
            _settingsManager,
            () => "MasterPass123!",
            _totpService);
    }

    public void Dispose()
    {
        _autoTypeService.Dispose();
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData("GitHub: Let's build from here - Google Chrome", "GitHub: Let's build from here")]
    [InlineData("Sign in to your account - Mozilla Firefox", "Sign in to your account")]
    [InlineData("Microsoft Azure Portal - Microsoft Edge", "Microsoft Azure Portal")]
    [InlineData("Dashboard - Brave", "Dashboard")]
    [InlineData("Twitter Login - Opera", "Twitter Login")]
    [InlineData("Reddit - Vivaldi", "Reddit")]
    [InlineData("Notepad++", "Notepad++")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void CleanWindowTitle_StripsBrowserSuffixes(string? raw, string expected)
    {
        string cleaned = AutoTypeService.CleanWindowTitle(raw!);
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public void CalculateMatchScore_ScoresProcessAndTitles()
    {
        var item = new CredentialItem
        {
            Title = "GitHub",
            Username = "octocat",
            Url = "https://github.com/login",
            Tags = new List<string> { "#code", "#git" },
            IsFavorite = true
        };

        // Test 1: Full match process
        int score1 = AutoTypeService.CalculateMatchScore(item, "GitHub", "github", "github github: let's build");
        Assert.True(score1 >= 100);

        // Test 2: Unrelated window
        int score2 = AutoTypeService.CalculateMatchScore(item, "Spotify Music Player", "spotify", "spotify music player");
        Assert.Equal(0, score2);

        // Test 3: Tag match
        int score3 = AutoTypeService.CalculateMatchScore(item, "Coding Tools", "git-bash", "coding tools git-bash #code");
        Assert.True(score3 > 0);
    }

    [Fact]
    public void CalculateMatchScore_ZeroWhenEmptyTitleAndDomain()
    {
        var emptyItem = new CredentialItem
        {
            Title = "",
            Url = ""
        };

        int score = AutoTypeService.CalculateMatchScore(emptyItem, "Test", "test", "test");
        Assert.Equal(0, score);
    }

    [Fact]
    public void FindMatchingCredentials_RanksAndSortsMatches()
    {
        var item1 = new CredentialItem
        {
            Title = "GitLab",
            Username = "dev1",
            Url = "https://gitlab.com",
            IsFavorite = false
        };
        var item2 = new CredentialItem
        {
            Title = "GitHub",
            Username = "dev2",
            Url = "https://github.com",
            IsFavorite = true
        };
        _vaultService.Add(item1);
        _vaultService.Add(item2);

        var target = new TargetWindowInfo(new IntPtr(123), "GitHub: Where the world builds software", "chrome");
        var matches = _autoTypeService.FindMatchingCredentials(target);

        Assert.NotEmpty(matches);
        Assert.Equal("GitHub", matches[0].Title);
    }

    [Fact]
    public void TargetWindowInfo_Properties_AreAccurate()
    {
        var info = new TargetWindowInfo(new IntPtr(42), "My Window", "explorer");
        Assert.True(info.HasTarget);
        Assert.Equal("explorer (My Window)", info.DisplaySummary);

        var emptyInfo = new TargetWindowInfo(IntPtr.Zero, "", "");
        Assert.False(emptyInfo.HasTarget);
        Assert.Equal("", emptyInfo.DisplaySummary);

        var titleOnly = new TargetWindowInfo(new IntPtr(1), "OnlyTitle", "");
        Assert.Equal("OnlyTitle", titleOnly.DisplaySummary);

        var procOnly = new TargetWindowInfo(new IntPtr(1), "", "OnlyProc");
        Assert.Equal("OnlyProc", procOnly.DisplaySummary);
    }

    [Fact]
    public void RegisterAndUnregister_GlobalHotkeys_ZeroHandleReturnsFalse()
    {
        bool reg = _autoTypeService.RegisterGlobalHotkeys(IntPtr.Zero);
        Assert.False(reg);

        // Unregister should not throw when not registered
        _autoTypeService.UnregisterGlobalHotkeys();
    }

    [Fact]
    public async Task PerformAutoTypeAsync_InvalidInputs_ReturnsFalse()
    {
        bool res1 = await _autoTypeService.PerformAutoTypeAsync(IntPtr.Zero, new CredentialItem { Title = "Test" });
        Assert.False(res1);

        bool res2 = await _autoTypeService.PerformAutoTypeAsync(new IntPtr(999), null!);
        Assert.False(res2);
    }

    [Fact]
    public void HandleHotkeyMessage_WithNoMatches_RaisesRequestAutofillPicker()
    {
        bool eventFired = false;
        _autoTypeService.RequestAutofillPicker += (target, items) =>
        {
            eventFired = true;
        };

        _autoTypeService.HandleHotkeyMessage(AutoTypeService.HOTKEY_AUTOTYPE_ID, IntPtr.Zero);
        Assert.True(eventFired);
    }
}

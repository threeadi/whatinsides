using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class ThemeManagerTests
{
    [Fact]
    public void SetTheme_UpdatesCurrentTheme_And_FiresEvent()
    {
        AppTheme receivedTheme = AppTheme.Dark;
        bool eventFired = false;

        ThemeManager.ThemeChanged += theme =>
        {
            receivedTheme = theme;
            eventFired = true;
        };

        // Switch to Light
        ThemeManager.SetTheme(AppTheme.Light);
        Assert.Equal(AppTheme.Light, ThemeManager.CurrentTheme);
        Assert.True(eventFired);
        Assert.Equal(AppTheme.Light, receivedTheme);

        // Switch back to Dark
        eventFired = false;
        ThemeManager.SetTheme(AppTheme.Dark);
        Assert.Equal(AppTheme.Dark, ThemeManager.CurrentTheme);
        Assert.True(eventFired);
        Assert.Equal(AppTheme.Dark, receivedTheme);
    }
}

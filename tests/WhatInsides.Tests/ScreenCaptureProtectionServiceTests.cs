using System;
using System.Text.Json;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class ScreenCaptureProtectionServiceTests
{
    [Fact]
    public void DisplayAffinity_Constants_MatchWin32Values()
    {
        Assert.Equal(0x00000000u, ScreenCaptureProtectionService.WDA_NONE);
        Assert.Equal(0x00000001u, ScreenCaptureProtectionService.WDA_MONITOR);
        Assert.Equal(0x00000011u, ScreenCaptureProtectionService.WDA_EXCLUDEFROMCAPTURE);
    }

    [Fact]
    public void ApplyProtection_ZeroHandle_ReturnsFalseWithoutThrowing()
    {
        bool enableResult = ScreenCaptureProtectionService.ApplyProtection(IntPtr.Zero, true);
        Assert.False(enableResult);

        bool disableResult = ScreenCaptureProtectionService.ApplyProtection(IntPtr.Zero, false);
        Assert.False(disableResult);
    }

    [Fact]
    public void GetCurrentAffinity_ZeroHandle_ReturnsWdaNone()
    {
        uint affinity = ScreenCaptureProtectionService.GetCurrentAffinity(IntPtr.Zero);
        Assert.Equal(ScreenCaptureProtectionService.WDA_NONE, affinity);
    }

    [Fact]
    public void ApplyProtection_NullWindow_DoesNotThrow()
    {
        ScreenCaptureProtectionService.ApplyProtection(window: null!, enable: true);
        ScreenCaptureProtectionService.ApplyProtection(window: null!, enable: false);
    }

    [Fact]
    public void AppSettings_PreventScreenCapture_DefaultsToTrueAndSerializes()
    {
        var settings = new AppSettings();
        Assert.True(settings.PreventScreenCapture);

        settings.PreventScreenCapture = false;
        string json = JsonSerializer.Serialize(settings);
        Assert.Contains("\"PreventScreenCapture\":false", json);

        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(deserialized);
        Assert.False(deserialized.PreventScreenCapture);
    }
}

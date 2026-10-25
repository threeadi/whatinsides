using System;
using System.IO;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class CloudDetectionServiceTests
{
    [Fact]
    public void DetectAvailableProviders_ReturnsGoogleDriveAndOneDrive()
    {
        var providers = CloudDetectionService.DetectAvailableProviders();

        Assert.NotNull(providers);
        Assert.True(providers.Count >= 2);
        Assert.Contains(providers, p => p.Name == "Google Drive");
        Assert.Contains(providers, p => p.Name == "OneDrive");
    }

    [Fact]
    public void CloudProviderInfo_DefaultProperties_InitializeProperly()
    {
        var info = new CloudProviderInfo
        {
            Name = "Custom Provider",
            Path = @"C:\CloudSync",
            IsDetected = true,
            Status = "Active",
            AccountEmail = "user@cloud.com"
        };

        Assert.Equal("Custom Provider", info.Name);
        Assert.Equal(@"C:\CloudSync", info.Path);
        Assert.True(info.IsDetected);
        Assert.Equal("Active", info.Status);
        Assert.Equal("user@cloud.com", info.AccountEmail);
    }
}

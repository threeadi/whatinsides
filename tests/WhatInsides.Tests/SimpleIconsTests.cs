using System;
using System.IO;
using System.Linq;
using MahApps.Metro.IconPacks;
using Xunit;

namespace WhatInsides.Tests;

public class SimpleIconsTests
{
    [Fact]
    public void VerifyIconPackResolutions()
    {
        var faIndex = PackIconDataFactory<PackIconFontAwesomeKind>.DataIndex.Value;
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.FolderSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.FolderOpenSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.LockSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.LockOpenSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.MagnifyingGlassSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.ArrowRotateRightSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.PlusSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.TrashCanSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.PenToSquareSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.CopySolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.XmarkSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.EyeSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.EyeSlashSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.StarSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.CreditCardSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.BuildingColumnsSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.FingerprintSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.ServerSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.ShieldHalvedSolid));
        Assert.True(faIndex.ContainsKey(PackIconFontAwesomeKind.SlidersSolid));
    }

    [Fact]
    public void VerifyAwsGeometry()
    {
        string awsPath = "M 3.2 12.8 c -.1 .9 .4 1.4 1.2 1.4 c .8 0 1.4 -.5 1.7 -1.1 v 1.0 h 1.3 v -4.3 c 0 -1.6 -1.1 -2.3 -2.7 -2.3 c -1.5 0 -2.6 .7 -2.8 1.9 l 1.4 .3 c .2 -.6 .6 -.9 1.4 -.9 c .8 0 1.3 .3 1.3 1.1 v .6 c -2.1 .2 -3.4 .7 -3.4 2.3 z m 1.4 -.1 c 0 -.8 .7 -1.2 1.9 -1.3 v 1.2 c -.3 .6 -.9 .9 -1.4 .9 c -.4 0 -.5 -.3 -.5 -.8 z M 8.8 7.7 h 1.6 l 1.4 5.3 l 1.3 -5.3 h 1.4 l 1.3 5.3 l 1.4 -5.3 h 1.6 l -2.1 7.2 h -1.5 l -1.4 -5.3 l -1.4 5.3 h -1.5 z M 21.8 10.4 c -.1 -1.8 -1.5 -2.7 -3.1 -2.7 c -1.8 0 -3.0 1.0 -3.0 2.6 c 0 1.3 .8 2.0 2.2 2.3 l .7 .2 c .8 .2 1.3 .5 1.3 1.1 c 0 .6 -.6 1.0 -1.4 1.0 c -.9 0 -1.5 -.4 -1.6 -1.2 l -1.4 .3 c .2 1.5 1.5 2.2 3.0 2.2 c 1.8 0 3.1 -1.0 3.1 -2.7 c 0 -1.4 -.9 -2.1 -2.3 -2.4 l -.7 -.2 c -.7 -.2 -1.2 -.4 -1.2 -1.0 c 0 -.5 .5 -.9 1.2 -.9 c .8 0 1.3 .4 1.4 1.0 z M 21.8 17.5 C 15.0 21.0 7.0 21.0 1.5 17.2 c -.3 -.2 0 -.6 .4 -.4 C 6.8 19.8 14.5 19.8 21.8 17.5 Z M 23.2 16.0 c -.4 -.5 -2.2 -.2 -3.2 -.1 c -.3 0 -.3 -.2 -.1 -.4 c 1.6 -1.1 4.1 -.8 4.4 -.4 c .3 .4 -.1 2.8 -1.5 4.0 c -.3 .2 -.5 .1 -.4 -.2 c .3 -.8 1.1 -2.5 .8 -2.9 z";
        var geo = System.Windows.Media.Geometry.Parse(awsPath);
        Assert.NotNull(geo);
        Assert.False(geo.IsEmpty());
        Assert.True(geo.Bounds.Width > 15 && geo.Bounds.Width <= 24);
        Assert.True(geo.Bounds.Height > 8 && geo.Bounds.Height <= 24);
    }

    [Fact]
    public void VerifyCredentialConverters()
    {
        var catConv = new WhatInsides.Converters.CredentialCategoryToColorConverter();
        var brush = catConv.Convert(WhatInsides.Models.CredentialCategory.Login, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(brush);

        var geoConv = new WhatInsides.Converters.CredentialCategoryToGeometryConverter();
        var geo = geoConv.Convert(WhatInsides.Models.CredentialCategory.Login, typeof(System.Windows.Media.Geometry), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(geo);
    }

    [Theory]
    [InlineData("Bank Central Asia", "", "", "BCA")]
    [InlineData("Mandiri Livin", "livin.bankmandiri.co.id", "", "Mandiri")]
    [InlineData("Tabungan BRI", "", "", "BRI")]
    [InlineData("Kartu Jenius BTPN", "", "", "Jenius")]
    [InlineData("Ethereum Cold Wallet", "", "", "Ethereum")]
    [InlineData("Solana Staking", "phantom.app", "", "Solana")]
    [InlineData("AWS Prod Account", "", "", "AWS")]
    [InlineData("Server Ubuntu 24.04", "192.168.1.100", "root@ubuntu", "Ubuntu")]
    [InlineData("Dokumen KTP Jakarta", "", "", "IdCard")]
    [InlineData("Paspor RI", "", "", "Passport")]
    [InlineData("Lisensi Windows 11 Pro", "", "", "Windows")]
    public void VerifyEffectiveBrandDetection(string title, string url, string username, string expectedBrand)
    {
        var item = new WhatInsides.Models.CredentialItem
        {
            Title = title,
            Url = url,
            Username = username
        };
        Assert.Equal(expectedBrand, item.GetEffectiveBrandKey());
    }

    [Fact]
    public void VerifyBrandBrushNotNull()
    {
        var brushBca = WhatInsides.Converters.CredentialToIconBrushConverter.GetBrandBrush("BCA");
        Assert.NotNull(brushBca);

        var brushEth = WhatInsides.Converters.CredentialToIconBrushConverter.GetBrandBrush("Ethereum");
        Assert.NotNull(brushEth);

        var brushAws = WhatInsides.Converters.CredentialToIconBrushConverter.GetBrandBrush("AWS");
        Assert.NotNull(brushAws);
    }
}




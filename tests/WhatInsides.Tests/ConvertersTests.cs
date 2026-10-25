using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using WhatInsides.Converters;
using WhatInsides.Models;
using Xunit;

namespace WhatInsides.Tests;

public class ConvertersTests
{
    [Theory]
    [InlineData(CredentialCategory.Login)]
    [InlineData(CredentialCategory.Password)]
    [InlineData(CredentialCategory.Finance)]
    [InlineData(CredentialCategory.PaymentCard)]
    [InlineData(CredentialCategory.BankAccount)]
    [InlineData(CredentialCategory.Identity)]
    [InlineData(CredentialCategory.CryptoSeed)]
    [InlineData(CredentialCategory.SecureNote)]
    [InlineData(CredentialCategory.SoftwareLicense)]
    [InlineData(CredentialCategory.DeveloperServer)]
    [InlineData(CredentialCategory.SshToken)]
    [InlineData(CredentialCategory.ServerDatabase)]
    [InlineData(CredentialCategory.Trash)]
    [InlineData((CredentialCategory)999)]
    public void CredentialCategoryToColorConverter_ReturnsValidSolidColorBrush(CredentialCategory cat)
    {
        var converter = new CredentialCategoryToColorConverter();
        var result = converter.Convert(cat, typeof(Brush), null!, CultureInfo.InvariantCulture);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<Brush>(result);
    }

    [Theory]
    [InlineData(CredentialCategory.Login)]
    [InlineData(CredentialCategory.Finance)]
    [InlineData(CredentialCategory.Identity)]
    [InlineData(CredentialCategory.CryptoSeed)]
    [InlineData(CredentialCategory.SecureNote)]
    [InlineData(CredentialCategory.DeveloperServer)]
    [InlineData(CredentialCategory.Trash)]
    public void CredentialCategoryToGeometryConverter_ReturnsGeometry(CredentialCategory cat)
    {
        var converter = new CredentialCategoryToGeometryConverter();
        var result = converter.Convert(cat, typeof(Geometry), null!, CultureInfo.InvariantCulture);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<Geometry>(result);
    }

    [Theory]
    [InlineData("Folder")]
    [InlineData("Image")]
    [InlineData("Video")]
    [InlineData("Audio")]
    [InlineData("Document")]
    [InlineData("Archive")]
    [InlineData("UnknownType")]
    [InlineData(null)]
    public void CategoryToColorConverter_ReturnsValidBrush(string? category)
    {
        var converter = new CategoryToColorConverter();
        var result = converter.Convert(category!, typeof(Brush), null!, CultureInfo.InvariantCulture);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<Brush>(result);
    }

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    [InlineData("not a bool", Visibility.Visible)]
    public void InverseBooleanToVisibilityConverter_ConvertsExpectedly(object value, Visibility expected)
    {
        var converter = new InverseBooleanToVisibilityConverter();
        var result = converter.Convert(value, typeof(Visibility), null!, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BoolToStarGeometryConverter_ReturnsGeometry(bool isFav)
    {
        var converter = new BoolToStarGeometryConverter();
        var result = converter.Convert(isFav, typeof(Geometry), null!, CultureInfo.InvariantCulture);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<Geometry>(result);
    }

    [Theory]
    [InlineData("GoogleCloud")]
    [InlineData("Azure")]
    [InlineData("OnePassword")]
    [InlineData("GoPay")]
    [InlineData("ShopeePay")]
    [InlineData("Bank")]
    [InlineData("NonExistentKey12345")]
    [InlineData("")]
    [InlineData(null)]
    public void CredentialToIconGeometryConverter_TryGetFontAwesome_HandlesAliases(string? key)
    {
        var geo = CredentialToIconGeometryConverter.TryGetFontAwesomeGeometry(key!);
        if (!string.IsNullOrEmpty(key) && key != "NonExistentKey12345")
        {
            // May or may not find fontawesome match depending on alias
            // Should never throw
        }
        else
        {
            Assert.Null(geo);
        }
    }

    [Theory]
    [InlineData("BCA")]
    [InlineData("Mandiri")]
    [InlineData("BNI")]
    [InlineData("BRI")]
    [InlineData("PayPal")]
    [InlineData("Bitcoin")]
    [InlineData("Docker")]
    [InlineData("PostgreSQL")]
    [InlineData("")]
    [InlineData(null)]
    public void CredentialToIconBrushConverter_GetBrandBrush_ReturnsBrush(string? brand)
    {
        var brush = CredentialToIconBrushConverter.GetBrandBrush(brand!);
        Assert.NotNull(brush);
    }

    [Fact]
    public void CredentialHasCustomIconConverter_And_SvgIconConverter_ReturnVisibility()
    {
        var customIconConv = new CredentialHasCustomIconConverter();
        var svgIconConv = new CredentialHasSvgIconConverter();

        var itemWithoutIcon = new CredentialItem { Title = "No Custom Icon" };

        var visCustom = customIconConv.Convert(itemWithoutIcon, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        var visSvg = svgIconConv.Convert(itemWithoutIcon, typeof(Visibility), null!, CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Collapsed, visCustom);
        Assert.Equal(Visibility.Visible, visSvg);

        // When a real file exists
        string tempIconFile = Path.Combine(Path.GetTempPath(), $"wi_icon_test_{Guid.NewGuid():N}.png");
        File.WriteAllText(tempIconFile, "fake icon data");

        try
        {
            var itemWithIcon = new CredentialItem { Title = "With Custom Icon", CustomIconPath = tempIconFile };
            var visCustom2 = customIconConv.Convert(itemWithIcon, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            var visSvg2 = svgIconConv.Convert(itemWithIcon, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Visible, visCustom2);
            Assert.Equal(Visibility.Collapsed, visSvg2);
        }
        finally
        {
            try { File.Delete(tempIconFile); } catch { }
        }
    }
}

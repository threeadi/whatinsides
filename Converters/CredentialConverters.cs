using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MahApps.Metro.IconPacks;
using WhatInsides.Models;

namespace WhatInsides.Converters;

public class CredentialCategoryToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        PackIconFontAwesomeKind kind = value switch
        {
            CredentialCategory.Finance or CredentialCategory.PaymentCard or CredentialCategory.BankAccount => PackIconFontAwesomeKind.CreditCardSolid,
            CredentialCategory.Identity => PackIconFontAwesomeKind.FingerprintSolid,
            CredentialCategory.CryptoSeed => PackIconFontAwesomeKind.CoinsSolid,
            CredentialCategory.SecureNote or CredentialCategory.SoftwareLicense => PackIconFontAwesomeKind.FileLinesSolid,
            CredentialCategory.DeveloperServer or CredentialCategory.SshToken or CredentialCategory.ServerDatabase => PackIconFontAwesomeKind.ServerSolid,
            CredentialCategory.Trash => PackIconFontAwesomeKind.TrashCanSolid,
            _ => PackIconFontAwesomeKind.KeySolid
        };

        var geo = CredentialToIconGeometryConverter.GetFontAwesomeGeometry(kind);
        if (geo != null && geo != Geometry.Empty)
        {
            return geo;
        }

        return Application.Current?.FindResource("IconKey") ?? Geometry.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CredentialCategoryToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string hexColor = value switch
        {
            CredentialCategory.Login or CredentialCategory.Password => "#60CDFF",        // Cyan
            CredentialCategory.Finance or CredentialCategory.PaymentCard or CredentialCategory.BankAccount => "#34D399", // Emerald
            CredentialCategory.Identity => "#A78BFA",    // Purple
            CredentialCategory.CryptoSeed => "#FBBF24",   // Amber
            CredentialCategory.SecureNote or CredentialCategory.SoftwareLicense => "#E2E8F0",  // Slate
            CredentialCategory.DeveloperServer or CredentialCategory.SshToken or CredentialCategory.ServerDatabase => "#FB923C",  // Orange
            CredentialCategory.Trash => "#F87171",       // Red
            _ => "#A1A1AA"
        };

        try
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor)!;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToStarGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isFav = value is true;
        if (isFav)
        {
            var star = CredentialToIconGeometryConverter.GetFontAwesomeGeometry(PackIconFontAwesomeKind.StarSolid);
            if (star != null && star != Geometry.Empty) return star;
        }
        else
        {
            var starReg = CredentialToIconGeometryConverter.GetFontAwesomeGeometry(PackIconFontAwesomeKind.StarRegular);
            if (starReg != null && starReg != Geometry.Empty) return starReg;
        }

        string key = isFav ? "IconStar" : "IconStarOutline";
        if (Application.Current?.TryFindResource(key) is Geometry geo)
        {
            return geo;
        }
        return Application.Current?.FindResource("IconStar") ?? Geometry.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CredentialToIconGeometryConverter : IValueConverter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<PackIconSimpleIconsKind, Geometry> _simpleIconsCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<PackIconFontAwesomeKind, Geometry> _fontAwesomeCache = new();

    public static Geometry? GetFontAwesomeGeometry(PackIconFontAwesomeKind kind)
    {
        return _fontAwesomeCache.GetOrAdd(kind, k =>
        {
            if (PackIconDataFactory<PackIconFontAwesomeKind>.DataIndex.Value.TryGetValue(k, out string? path) && !string.IsNullOrEmpty(path))
            {
                try
                {
                    var geo = Geometry.Parse(path);
                    geo.Freeze();
                    return geo;
                }
                catch
                {
                    return Geometry.Empty;
                }
            }
            return Geometry.Empty;
        });
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> _iconAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GoogleCloud"] = "Googlecloud",
        ["Azure"] = "Microsoftazure",
        ["OnePassword"] = "Onepassword",
        ["GoPay"] = "Gojek",
        ["ShopeePay"] = "Shopee",
        ["University"] = "BuildingColumns",
        ["Bank"] = "BuildingColumns",
        ["DrivingLicense"] = "IdBadge",
        ["Tax"] = "Receipt",
        ["Family"] = "Users",
        ["ShieldHalved"] = "ShieldHalved",
        ["Hospital"] = "Hospital"
    };

    public static Geometry? TryGetFontAwesomeGeometry(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (_iconAliases.TryGetValue(key, out var alias))
        {
            if (Enum.TryParse<PackIconFontAwesomeKind>(alias, ignoreCase: true, out var aKind) ||
                Enum.TryParse<PackIconFontAwesomeKind>(alias + "Solid", ignoreCase: true, out aKind) ||
                Enum.TryParse<PackIconFontAwesomeKind>(alias + "Brands", ignoreCase: true, out aKind))
            {
                var g = GetFontAwesomeGeometry(aKind);
                if (g != null && g != Geometry.Empty) return g;
            }
        }

        if (Enum.TryParse<PackIconFontAwesomeKind>(key, ignoreCase: true, out var kind) ||
            Enum.TryParse<PackIconFontAwesomeKind>(key + "Brands", ignoreCase: true, out kind) ||
            Enum.TryParse<PackIconFontAwesomeKind>(key + "Solid", ignoreCase: true, out kind))
        {
            return GetFontAwesomeGeometry(kind);
        }
        return null;
    }

    public static Geometry? TryGetSimpleIconGeometry(string brandKey)
    {
        if (string.IsNullOrWhiteSpace(brandKey)) return null;

        if (_iconAliases.TryGetValue(brandKey, out var alias))
        {
            if (Enum.TryParse<PackIconSimpleIconsKind>(alias, ignoreCase: true, out var aKind))
            {
                var g = GetSimpleIconGeometry(aKind);
                if (g != null && g != Geometry.Empty) return g;
            }
        }

        if (Enum.TryParse<PackIconSimpleIconsKind>(brandKey, ignoreCase: true, out var kind))
        {
            return GetSimpleIconGeometry(kind);
        }
        return null;
    }

    private static Geometry? GetSimpleIconGeometry(PackIconSimpleIconsKind kind)
    {
        return _simpleIconsCache.GetOrAdd(kind, k =>
        {
            if (PackIconDataFactory<PackIconSimpleIconsKind>.DataIndex.Value.TryGetValue(k, out string? path) && !string.IsNullOrEmpty(path))
            {
                try
                {
                    var geo = Geometry.Parse(path);
                    geo.Freeze();
                    return geo;
                }
                catch
                {
                    return Geometry.Empty;
                }
            }
            return Geometry.Empty;
        });
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CredentialItem item)
        {
            string brand = item.GetEffectiveBrandKey();
            if (!string.IsNullOrEmpty(brand))
            {
                // 1. Dedicated authentic bold 24x24 geometries for AWS & Amazon
                if (brand.Equals("AWS", StringComparison.OrdinalIgnoreCase) || brand.Equals("Amazon", StringComparison.OrdinalIgnoreCase))
                {
                    string k = brand.Equals("AWS", StringComparison.OrdinalIgnoreCase) ? "BrandAWS" : "BrandAmazon";
                    if (Application.Current?.TryFindResource(k) is Geometry dedicatedGeo && dedicatedGeo != Geometry.Empty)
                    {
                        return dedicatedGeo;
                    }
                }

                // 2. Check SimpleIcons pack (3,300+ brand vector geometries, native 24x24)
                var simpleGeo = TryGetSimpleIconGeometry(brand);
                if (simpleGeo != null && simpleGeo != Geometry.Empty)
                {
                    return simpleGeo;
                }

                // 3. Check FontAwesome pack (MicrosoftBrands, XboxBrands, etc.)
                var faGeo = TryGetFontAwesomeGeometry(brand);
                if (faGeo != null && faGeo != Geometry.Empty)
                {
                    return faGeo;
                }

                // 4. Fallback to resource dictionary
                string resKey = "Brand" + brand;
                if (Application.Current?.TryFindResource(resKey) is Geometry brandGeo)
                {
                    return brandGeo;
                }
            }

            return new CredentialCategoryToGeometryConverter().Convert(item.Category, targetType, parameter, culture);
        }

        return Application.Current?.FindResource("IconKey") ?? Geometry.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CredentialToIconBrushConverter : IValueConverter
{
    private static readonly System.Collections.Generic.Dictionary<string, string> _brandHexColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BCA"] = "#005E9E",
        ["Mandiri"] = "#F6A800",
        ["BNI"] = "#E25303",
        ["BRI"] = "#00529C",
        ["BSI"] = "#00A39D",
        ["CIMB"] = "#ED1B2D",
        ["BankJago"] = "#FF8C00",
        ["Jenius"] = "#00A3E0",
        ["Permata"] = "#87C040",
        ["Visa"] = "#1A1F71",
        ["Mastercard"] = "#EB001B",
        ["PayPal"] = "#0079C1",
        ["Stripe"] = "#635BFF",
        ["Wise"] = "#9FE870",
        ["GoPay"] = "#00AED6",
        ["OVO"] = "#4C3494",
        ["Dana"] = "#118EEA",
        ["ShopeePay"] = "#EE4D2D",
        ["CreditCard"] = "#38BDF8",
        ["University"] = "#60A5FA",
        ["BuildingColumns"] = "#60A5FA",
        ["Bitcoin"] = "#F7931A",
        ["Ethereum"] = "#627EEA",
        ["Solana"] = "#14F195",
        ["Binance"] = "#F3BA2F",
        ["Polygon"] = "#8247E5",
        ["Tether"] = "#26A17B",
        ["MetaMask"] = "#E2761B",
        ["Phantom"] = "#AB9FF2",
        ["Ledger"] = "#F5F5F5",
        ["Trezor"] = "#00854D",
        ["Coinbase"] = "#0052FF",
        ["Kraken"] = "#5741D9",
        ["Bybit"] = "#F7A600",
        ["Uniswap"] = "#FF007A",
        ["Cardano"] = "#0033AD",
        ["Ripple"] = "#23292F",
        ["Dogecoin"] = "#C2A633",
        ["Docker"] = "#2496ED",
        ["Kubernetes"] = "#326CE5",
        ["PostgreSQL"] = "#4169E1",
        ["MySQL"] = "#4479A1",
        ["MariaDB"] = "#003545",
        ["Redis"] = "#DC382D",
        ["MongoDB"] = "#47A248",
        ["SQLite"] = "#003B57",
        ["Ubuntu"] = "#E95420",
        ["Debian"] = "#A81D33",
        ["Linux"] = "#FCC624",
        ["Cloudflare"] = "#F38020",
        ["DigitalOcean"] = "#0080FF",
        ["Vercel"] = "#FFFFFF",
        ["Supabase"] = "#3ECF8E",
        ["Firebase"] = "#FFCA28",
        ["NGINX"] = "#009639",
        ["Apache"] = "#D22128",
        ["Server"] = "#38BDF8",
        ["Database"] = "#A78BFA",
        ["Terminal"] = "#34D399",
        ["Windows"] = "#0078D4",
        ["Adobe"] = "#FF0000",
        ["JetBrains"] = "#FC801D",
        ["Figma"] = "#F24E1E",
        ["Notion"] = "#FFFFFF",
        ["Bitwarden"] = "#175DDC",
        ["OnePassword"] = "#0A85EA",
        ["Key"] = "#FBBF24",
        ["FileLines"] = "#94A3B8",
        ["IdCard"] = "#38BDF8",
        ["Passport"] = "#34D399",
        ["DrivingLicense"] = "#FBBF24",
        ["Tax"] = "#A78BFA",
        ["Hospital"] = "#F472B6",
        ["Certificate"] = "#F59E0B"
    };

    public static Brush GetBrandBrush(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand)) return Application.Current?.TryFindResource("BrushAccentBlue") as Brush ?? Brushes.SkyBlue;

        if (_brandHexColors.TryGetValue(brand, out string? hex))
        {
            try
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
                brush.Freeze();
                return brush;
            }
            catch { }
        }

        string resKey = "BrushBrand" + brand;
        if (Application.Current?.TryFindResource(resKey) is Brush brandBrush)
        {
            return brandBrush;
        }

        return Application.Current?.TryFindResource("BrushAccentBlue") as Brush ?? Brushes.SkyBlue;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CredentialItem item)
        {
            string brand = item.GetEffectiveBrandKey();
            if (!string.IsNullOrEmpty(brand))
            {
                return GetBrandBrush(brand);
            }

            return new CredentialCategoryToColorConverter().Convert(item.Category, targetType, parameter, culture);
        }

        return Brushes.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CredentialHasCustomIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CredentialItem item && !string.IsNullOrWhiteSpace(item.CustomIconPath) && System.IO.File.Exists(item.CustomIconPath))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CredentialHasSvgIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CredentialItem item && !string.IsNullOrWhiteSpace(item.CustomIconPath) && System.IO.File.Exists(item.CustomIconPath))
        {
            return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CredentialCustomIconSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CredentialItem item && !string.IsNullOrWhiteSpace(item.CustomIconPath) && System.IO.File.Exists(item.CustomIconPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(item.CustomIconPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

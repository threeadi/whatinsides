using System;
using System.Windows;
using System.Windows.Media;

namespace WhatInsides.Services;

public enum AppTheme
{
    Dark,
    Light
}

public static class ThemeManager
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    public static void SetTheme(AppTheme theme, bool save = true)
    {
        CurrentTheme = theme;
        if (Application.Current != null)
        {
            var res = Application.Current.Resources;

            if (theme == AppTheme.Light)
            {
                ApplyColor(res, "ColorBgApp", "#F4F4F6");
                ApplyColor(res, "ColorBgSidebar", "#ECEEF1");
                ApplyColor(res, "ColorBgCard", "#FFFFFF");
                ApplyColor(res, "ColorBgCardHover", "#F5F6F8");
                ApplyColor(res, "ColorBgCardSelected", "#E8EAF0");
                ApplyColor(res, "ColorBorder", "#E0E3E8");
                ApplyColor(res, "ColorBorderSubtle", "#E8EBF0");

                ApplyColor(res, "ColorTextPrimary", "#18181B");
                ApplyColor(res, "ColorTextSecondary", "#52525B");
                ApplyColor(res, "ColorTextMuted", "#71717A");
                ApplyColor(res, "ColorTextFaint", "#A1A1AA");

                ApplyColor(res, "ColorPrimary", "#18181B");
                ApplyColor(res, "ColorPrimaryText", "#FFFFFF");
                ApplyColor(res, "ColorAccentBlue", "#0078D4");

                ApplyColor(res, "ColorSuccess", "#16A34A");
                ApplyColor(res, "ColorDanger", "#DC2626");
                ApplyColor(res, "ColorWarning", "#D97706");
            }
            else // Dark Mode (Default)
            {
                ApplyColor(res, "ColorBgApp", "#0A0A0C");
                ApplyColor(res, "ColorBgSidebar", "#0E0F12");
                ApplyColor(res, "ColorBgCard", "#101114");
                ApplyColor(res, "ColorBgCardHover", "#16171B");
                ApplyColor(res, "ColorBgCardSelected", "#1C1E23");
                ApplyColor(res, "ColorBorder", "#202227");
                ApplyColor(res, "ColorBorderSubtle", "#282A30");

                ApplyColor(res, "ColorTextPrimary", "#F4F4F5");
                ApplyColor(res, "ColorTextSecondary", "#A1A1AA");
                ApplyColor(res, "ColorTextMuted", "#71717A");
                ApplyColor(res, "ColorTextFaint", "#52525B");

                ApplyColor(res, "ColorPrimary", "#FFFFFF");
                ApplyColor(res, "ColorPrimaryText", "#0A0A0C");
                ApplyColor(res, "ColorAccentBlue", "#60CDFF");

                ApplyColor(res, "ColorSuccess", "#34D399");
                ApplyColor(res, "ColorDanger", "#F87171");
                ApplyColor(res, "ColorWarning", "#FBBF24");
            }
        }

        if (save)
        {
            try
            {
                var sm = new SettingsManager();
                sm.Settings.Theme = theme.ToString();
                sm.SaveSettings();
            }
            catch { }
        }

        ThemeChanged?.Invoke(theme);
    }

    private static void ApplyColor(ResourceDictionary res, string name, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        res[name] = color;
        res[name.Replace("Color", "Brush")] = new SolidColorBrush(color);
    }
}

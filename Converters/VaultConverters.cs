using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WhatInsides.Converters;

public class CategoryToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string category = value as string ?? "Other";
        string resourceKey = category switch
        {
            "Folder" => "IconFolder",
            "Image" => "IconImage",
            "Video" => "IconVideo",
            "Audio" => "IconMusic",
            "Document" => "IconFile",
            "Archive" => "IconArchive",
            _ => "IconFile"
        };

        if (Application.Current.TryFindResource(resourceKey) is Geometry geo)
        {
            return geo;
        }

        return Application.Current.FindResource("IconFile");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CategoryToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string category = value as string ?? "Other";
        string hexColor = category switch
        {
            "Folder" => "#F59E0B",   // Amber
            "Image" => "#06B6D4",    // Cyan
            "Video" => "#8B5CF6",    // Violet
            "Audio" => "#EC4899",    // Pink
            "Document" => "#3B82F6", // Blue
            "Archive" => "#10B981",  // Emerald
            _ => "#94A3B8"           // Slate
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

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

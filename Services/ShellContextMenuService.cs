using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WhatInsides.Services;

public static class ShellContextMenuService
{
    private const string MENU_NAME = "WhatInsides";
    private const string MENU_LABEL = "🔒 Kunci dengan WhatInsides";

    public static bool IsContextMenuRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\" + MENU_NAME);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool RegisterContextMenu()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WhatInsides.exe");

            string iconPath = exePath;
            string directIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(directIco))
            {
                iconPath = directIco;
            }

            string commandStr = $"\"{exePath}\" \"%1\"";

            // 1. Register for Files (*)
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\" + MENU_NAME))
            {
                key.SetValue("", MENU_LABEL);
                key.SetValue("Icon", iconPath);
                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", commandStr);
            }

            // 2. Register for Directories
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\" + MENU_NAME))
            {
                key.SetValue("", MENU_LABEL);
                key.SetValue("Icon", iconPath);
                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", commandStr);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool UnregisterContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\" + MENU_NAME, false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\" + MENU_NAME, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

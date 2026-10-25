using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WhatInsides.Services;

public static class ScreenCaptureProtectionService
{
    public const uint WDA_NONE = 0x00000000;
    public const uint WDA_MONITOR = 0x00000001;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint pdwAffinity);

    public static bool ApplyProtection(IntPtr hwnd, bool enable)
    {
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            if (!enable)
            {
                return SetWindowDisplayAffinity(hwnd, WDA_NONE);
            }

            // Try modern WDA_EXCLUDEFROMCAPTURE (Windows 10 2004+ and Windows 11)
            // Window is excluded from screen capture (becomes transparent to recorders)
            bool success = SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            if (!success)
            {
                // Fallback to WDA_MONITOR (window is rendered black on capture)
                success = SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScreenCaptureProtectionService] Error setting affinity: {ex.Message}");
            return false;
        }
    }

    public static void ApplyProtection(Window window, bool enable)
    {
        if (window == null) return;

        var helper = new WindowInteropHelper(window);
        if (helper.Handle != IntPtr.Zero)
        {
            ApplyProtection(helper.Handle, enable);
        }
        else
        {
            // HWND not created yet, register for SourceInitialized
            EventHandler? handler = null;
            handler = (s, e) =>
            {
                window.SourceInitialized -= handler;
                var h = new WindowInteropHelper(window);
                if (h.Handle != IntPtr.Zero)
                {
                    ApplyProtection(h.Handle, enable);
                }
            };
            window.SourceInitialized += handler;
        }
    }

    public static void ApplyToAllWindows(bool enable)
    {
        if (Application.Current == null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (Window window in Application.Current.Windows)
            {
                ApplyProtection(window, enable);
            }
        });
    }

    public static uint GetCurrentAffinity(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return WDA_NONE;
        return GetWindowDisplayAffinity(hwnd, out uint affinity) ? affinity : WDA_NONE;
    }
}

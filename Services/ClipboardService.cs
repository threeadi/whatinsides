using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace WhatInsides.Services;

public static class ClipboardService
{
    private static DispatcherTimer? _clearTimer;
    private static string? _lastCopiedText;

    public static event Action<string>? ClipboardCleared;

    #region Win32 API Interop for Secure Clipboard History Exclusion

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    #endregion

    /// <summary>
    /// Copies text to clipboard. By default, prevents the text from being recorded into
    /// Windows 10/11 Clipboard History (Win + V) or uploaded to Cloud Clipboard.
    /// Automatically clears active clipboard after timeoutSeconds.
    /// </summary>
    public static void Copy(string text, string label = "Data", int timeoutSeconds = 30, bool excludeFromHistory = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            bool nativeSuccess = false;
            if (excludeFromHistory)
            {
                nativeSuccess = SetClipboardTextSecure(text);
            }

            if (!nativeSuccess)
            {
                Clipboard.SetText(text);
            }

            _lastCopiedText = text;
            _clearTimer?.Stop();

            if (timeoutSeconds <= 0) return;

            _clearTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(timeoutSeconds)
            };
            _clearTimer.Tick += (s, e) =>
            {
                _clearTimer?.Stop();
                try
                {
                    if (Clipboard.ContainsText() && Clipboard.GetText() == _lastCopiedText)
                    {
                        Clipboard.Clear();
                        ClipboardCleared?.Invoke(label);
                    }
                }
                catch { }
            };
            _clearTimer.Start();
        }
        catch { }
    }

    private static bool SetClipboardTextSecure(string text)
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero)) return false;

            try
            {
                EmptyClipboard();

                // 1. Copy Unicode Text
                byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
                IntPtr hText = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
                if (hText == IntPtr.Zero) return false;

                IntPtr pText = GlobalLock(hText);
                if (pText == IntPtr.Zero) return false;
                Marshal.Copy(bytes, 0, pText, bytes.Length);
                GlobalUnlock(hText);

                if (SetClipboardData(CF_UNICODETEXT, hText) == IntPtr.Zero)
                {
                    return false;
                }

                // 2. Prevent storing in Windows 10/11 Clipboard History (Win + V)
                SetDWordFormat("CanIncludeInClipboardHistory", 0);

                // 3. Prevent synchronizing with Microsoft Cloud Clipboard
                SetDWordFormat("CanUploadToCloudClipboard", 0);

                // 4. Prevent clipboard monitoring processing
                SetDWordFormat("ExcludeClipboardContentFromMonitorProcessing", 0);

                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch
        {
            return false;
        }
    }

    private static void SetDWordFormat(string formatName, uint value)
    {
        uint format = RegisterClipboardFormat(formatName);
        if (format == 0) return;

        IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)sizeof(uint));
        if (hMem == IntPtr.Zero) return;

        IntPtr pMem = GlobalLock(hMem);
        if (pMem == IntPtr.Zero) return;

        Marshal.WriteInt32(pMem, (int)value);
        GlobalUnlock(hMem);
        SetClipboardData(format, hMem);
    }

    public static void ClearNow()
    {
        _clearTimer?.Stop();
        _lastCopiedText = null;
        try
        {
            Clipboard.Clear();
        }
        catch { }
    }

    public static void CancelTimer()
    {
        _clearTimer?.Stop();
        _lastCopiedText = null;
    }
}

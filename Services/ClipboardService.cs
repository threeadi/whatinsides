using System;
using System.Windows;
using System.Windows.Threading;

namespace WhatInsides.Services;

public static class ClipboardService
{
    private static DispatcherTimer? _clearTimer;
    private static string? _lastCopiedText;

    public static event Action<string>? ClipboardCleared;

    public static void Copy(string text, string label = "Data", int timeoutSeconds = 30)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            Clipboard.SetText(text);
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

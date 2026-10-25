using System;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WhatInsides.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Window _window;
    private readonly Action _onRestore;
    private readonly Action _onLock;
    private readonly Action _onExit;
    private readonly Action? _onAutofill;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _isDisposed = false;

    public TrayIconService(Window window, Action onRestore, Action onLock, Action onExit, Action? onAutofill = null)
    {
        _window = window;
        _onRestore = onRestore;
        _onLock = onLock;
        _onExit = onExit;
        _onAutofill = onAutofill;

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon();
            _notifyIcon.Text = "WhatInsides - Vault Terlindungi";

            // 1. Try to load custom icon from Assets/app.ico
            Drawing.Icon? icon = null;
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (!File.Exists(icoPath))
            {
                icoPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "app.ico");
            }

            if (File.Exists(icoPath))
            {
                try
                {
                    icon = new Drawing.Icon(icoPath);
                }
                catch { }
            }

            // 2. Fallback to process executable icon
            if (icon == null)
            {
                try
                {
                    string? processPath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                    {
                        icon = Drawing.Icon.ExtractAssociatedIcon(processPath);
                    }
                }
                catch { }
            }

            // 3. Fallback to system application icon
            icon ??= Drawing.SystemIcons.Shield ?? Drawing.SystemIcons.Application;

            _notifyIcon.Icon = icon;

            // Context Menu
            var contextMenu = new Forms.ContextMenuStrip();

            var itemHeader = new Forms.ToolStripMenuItem("WhatInsides Vault")
            {
                Enabled = false,
                Font = new Drawing.Font(Drawing.FontFamily.GenericSansSerif, 9, Drawing.FontStyle.Bold)
            };
            contextMenu.Items.Add(itemHeader);
            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var itemOpen = new Forms.ToolStripMenuItem("Buka Dashboard");
            itemOpen.Click += (s, e) => _window.Dispatcher.Invoke(_onRestore);
            contextMenu.Items.Add(itemOpen);

            if (_onAutofill != null)
            {
                var itemAutofill = new Forms.ToolStripMenuItem("Autofill Cepat (Ctrl+Alt+A)");
                itemAutofill.Click += (s, e) => _window.Dispatcher.Invoke(_onAutofill);
                contextMenu.Items.Add(itemAutofill);
            }

            var itemLock = new Forms.ToolStripMenuItem("Kunci Vault");
            itemLock.Click += (s, e) => _window.Dispatcher.Invoke(_onLock);
            contextMenu.Items.Add(itemLock);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var itemExit = new Forms.ToolStripMenuItem("Keluar");
            itemExit.Click += (s, e) => _window.Dispatcher.Invoke(_onExit);
            contextMenu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Click & Double-click
            _notifyIcon.DoubleClick += (s, e) => _window.Dispatcher.Invoke(_onRestore);
            _notifyIcon.Click += (s, e) =>
            {
                if (e is Forms.MouseEventArgs me && me.Button == Forms.MouseButtons.Left)
                {
                    _window.Dispatcher.Invoke(_onRestore);
                }
            };

            // Make visible immediately
            _notifyIcon.Visible = true;
        }
        catch { }
    }

    public void ShowBalloon(string title, string message)
    {
        if (_notifyIcon != null && _notifyIcon.Visible)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
        }
    }

    public void UpdateTooltip(string text)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}

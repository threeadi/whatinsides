using System;
using System.Windows;
using System.Windows.Input;
using WhatInsides.Services;

namespace WhatInsides.Views.Credentials;

public partial class SshAccessPromptWindow : Window
{
    public event Action<bool, bool>? AccessDecided;

    private bool _hasDecided = false;

    public SshAccessPromptWindow(string appName, string keyTitle, string keyType, string fingerprint)
    {
        InitializeComponent();

        TxtAppRequester.Text = string.IsNullOrWhiteSpace(appName) ? "Terminal" : appName;
        TxtKeyTitle.Text = string.IsNullOrWhiteSpace(keyTitle) ? "GitHub SSH Key" : keyTitle;
        TxtKeyFingerprint.Text = $"{keyType} • {fingerprint}";

        Loaded += (s, e) =>
        {
            ScreenCaptureProtectionService.ApplyProtection(this, true);
            BtnApprove.Focus();
        };
    }

    private void BtnApprove_Click(object sender, RoutedEventArgs e)
    {
        Decide(true);
    }

    private void BtnDeny_Click(object sender, RoutedEventArgs e)
    {
        Decide(false);
    }

    private void Decide(bool approved)
    {
        if (_hasDecided) return;
        _hasDecided = true;

        bool rememberAll = ChkApproveAll.IsChecked == true;
        AccessDecided?.Invoke(approved, rememberAll);
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Decide(true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Decide(false);
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Keep topmost security focus intact
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhatInsides.Models;
using WhatInsides.Services;

namespace WhatInsides.Views.Credentials;

public partial class QuickAutofillWindow : Window
{
    private TargetWindowInfo _targetWindow;
    private readonly AutoTypeService _autoTypeService;
    private readonly CredentialVaultService _vaultService;
    private readonly SettingsManager _settingsManager;
    private readonly TotpService _totpService;

    private List<CredentialItem> _allItems = new();
    private List<CredentialItem> _initialMatches = new();
    private bool _isLocked = false;

    public event Action<string, string>? ToastRequested;

    public QuickAutofillWindow(
        AutoTypeService autoTypeService,
        CredentialVaultService vaultService,
        SettingsManager settingsManager,
        TotpService totpService)
    {
        InitializeComponent();

        _autoTypeService = autoTypeService;
        _vaultService = vaultService;
        _settingsManager = settingsManager;
        _totpService = totpService;
        _targetWindow = new TargetWindowInfo(IntPtr.Zero, string.Empty, string.Empty);

        Loaded += QuickAutofillWindow_Loaded;
    }

    public void SetupTarget(TargetWindowInfo target, List<CredentialItem> initialMatches, bool isLocked)
    {
        _targetWindow = target;
        _initialMatches = initialMatches ?? new List<CredentialItem>();
        _isLocked = isLocked;

        if (target.HasTarget)
        {
            TxtDetectedAppBadge.Text = $"Target: {target.DisplaySummary}";
            TxtDetectedAppBadge.Visibility = Visibility.Visible;
        }
        else
        {
            TxtDetectedAppBadge.Text = "Mode Spotlight Autofill";
        }

        if (_isLocked)
        {
            PanelVaultLocked.Visibility = Visibility.Visible;
            LstAutofillItems.Visibility = Visibility.Collapsed;
            PanelEmptyMatches.Visibility = Visibility.Collapsed;
            PbQuickUnlock.Password = string.Empty;
            TxtQuickUnlockError.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanelVaultLocked.Visibility = Visibility.Collapsed;
            LstAutofillItems.Visibility = Visibility.Visible;
            LoadAndFilterItems();
        }
    }

    private void QuickAutofillWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLocked)
        {
            PbQuickUnlock.Focus();
        }
        else
        {
            TxtAutofillSearch.Focus();
        }
    }

    private void LoadAndFilterItems()
    {
        _allItems = _vaultService.GetAll()
            .Where(x => !x.IsDeleted)
            .ToList();

        string query = TxtAutofillSearch?.Text.Trim() ?? string.Empty;
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        List<CredentialItem> filtered;

        if (string.IsNullOrEmpty(query))
        {
            // If empty search, prioritize initial contextual matches (e.g. Zoom)
            if (_initialMatches.Count > 0)
            {
                filtered = _initialMatches;
            }
            else
            {
                filtered = _allItems
                    .OrderByDescending(x => x.IsFavorite)
                    .ThenBy(x => x.Title)
                    .Take(25)
                    .ToList();
            }
        }
        else
        {
            string q = query.ToLowerInvariant();
            filtered = _allItems
                .Where(x =>
                    x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.DomainOnly.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (x.Url != null && x.Url.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    x.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    x.GetEffectiveBrandKey().Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.IsFavorite)
                .ThenBy(x => x.Title)
                .ToList();
        }

        LstAutofillItems.ItemsSource = filtered;

        if (filtered.Count > 0)
        {
            LstAutofillItems.SelectedIndex = 0;
            PanelEmptyMatches.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanelEmptyMatches.Visibility = Visibility.Visible;
        }
    }

    private void TxtAutofillSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLocked)
        {
            LoadAndFilterItems();
        }
    }

    private void TxtAutofillSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (LstAutofillItems.Items.Count > 0)
            {
                int next = Math.Min(LstAutofillItems.Items.Count - 1, LstAutofillItems.SelectedIndex + 1);
                LstAutofillItems.SelectedIndex = next;
                LstAutofillItems.ScrollIntoView(LstAutofillItems.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (LstAutofillItems.Items.Count > 0)
            {
                int prev = Math.Max(0, LstAutofillItems.SelectedIndex - 1);
                LstAutofillItems.SelectedIndex = prev;
                LstAutofillItems.ScrollIntoView(LstAutofillItems.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (LstAutofillItems.SelectedItem is CredentialItem item)
            {
                bool withEnter = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                ExecuteAutofill(item, withEnter ? AutoTypeSequence.UsernameTabPasswordEnter : AutoTypeSequence.UsernameTabPassword);
            }
            e.Handled = true;
        }
    }

    private void LstAutofillItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstAutofillItems.SelectedItem is CredentialItem item)
        {
            ExecuteAutofill(item, AutoTypeSequence.UsernameTabPassword);
        }
    }

    private void LstAutofillItems_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LstAutofillItems.SelectedItem is CredentialItem item)
        {
            bool withEnter = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            ExecuteAutofill(item, withEnter ? AutoTypeSequence.UsernameTabPasswordEnter : AutoTypeSequence.UsernameTabPassword);
            e.Handled = true;
        }
    }

    private void ExecuteAutofill(CredentialItem item, AutoTypeSequence sequence)
    {
        Close();

        if (_targetWindow.Hwnd != IntPtr.Zero)
        {
            _ = Task.Run(async () =>
            {
                await _autoTypeService.PerformAutoTypeAsync(_targetWindow.Hwnd, item, sequence);
            });
        }
        else
        {
            // If no foreground target window captured, copy password to clipboard as helpful fallback
            int timeout = _settingsManager?.Settings.ClipboardAutoClearSeconds ?? 30;
            ClipboardService.Copy(item.Password, "Kata Sandi", timeout);
            ToastRequested?.Invoke("Sandi Tersalin", $"'{item.Title}' disalin ke clipboard (Dibersihkan dalam {timeout}d).");
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        int timeout = _settingsManager?.Settings.ClipboardAutoClearSeconds ?? 30;

        // Shortcut: Ctrl + C (Copy Password)
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (LstAutofillItems.SelectedItem is CredentialItem item)
            {
                ClipboardService.Copy(item.Password, "Kata Sandi", timeout);
                ToastRequested?.Invoke("Kata Sandi Tersalin", $"'{item.Title}' disalin (Dibersihkan dalam {timeout}d).");
                Close();
                e.Handled = true;
            }
        }
        // Shortcut: Ctrl + B (Copy Username)
        else if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (LstAutofillItems.SelectedItem is CredentialItem item && !string.IsNullOrEmpty(item.Username))
            {
                ClipboardService.Copy(item.Username, "Username", timeout);
                ToastRequested?.Invoke("Username Tersalin", $"{item.Username} (Dibersihkan dalam {timeout}d)");
                Close();
                e.Handled = true;
            }
        }
        // Shortcut: Ctrl + T (Copy TOTP Code)
        else if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (LstAutofillItems.SelectedItem is CredentialItem item && item.HasTotp)
            {
                string code = _totpService.GenerateCode(item.TotpSecret);
                ClipboardService.Copy(code, "Kode TOTP", timeout);
                ToastRequested?.Invoke("Kode 2FA Tersalin", $"{code} (Dibersihkan dalam {timeout}d)");
                Close();
                e.Handled = true;
            }
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Close overlay when user clicks outside
        try
        {
            Close();
        }
        catch { }
    }

    private void BtnCloseAutofill_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PbQuickUnlock_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AttemptQuickUnlock();
            e.Handled = true;
        }
    }

    private void BtnQuickUnlock_Click(object sender, RoutedEventArgs e)
    {
        AttemptQuickUnlock();
    }

    private void AttemptQuickUnlock()
    {
        string entered = PbQuickUnlock.Password;
        if (_settingsManager.VerifyPassword(entered))
        {
            _isLocked = false;
            PanelVaultLocked.Visibility = Visibility.Collapsed;
            LstAutofillItems.Visibility = Visibility.Visible;
            LoadAndFilterItems();
            TxtAutofillSearch.Focus();
        }
        else
        {
            TxtQuickUnlockError.Visibility = Visibility.Visible;
            PbQuickUnlock.SelectAll();
            PbQuickUnlock.Focus();
        }
    }
}

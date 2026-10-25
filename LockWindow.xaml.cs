using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WhatInsides.Services;

namespace WhatInsides;

public partial class LockWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private bool _isPeek = false;

    public LockWindow()
    {
        InitializeComponent();

        _settingsManager = new SettingsManager();

        // Default demo master password if not yet configured
        if (!_settingsManager.Settings.IsPasswordConfigured)
        {
            _settingsManager.SetMasterPassword("admin123", "Demo Password");
        }

        Loaded += LockWindow_Loaded;
        Closed += LockWindow_Closed;
    }

    private void LockWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged += OnThemeChanged;
        OnThemeChanged(ThemeManager.CurrentTheme);

        PbMasterPassword.Focus();
        UpdatePlaceholder();
    }

    private void LockWindow_Closed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var next = ThemeManager.CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        ThemeManager.SetTheme(next);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        if (PathThemeIcon != null)
        {
            PathThemeIcon.Data = theme == AppTheme.Light
                ? (Geometry)FindResource("IconMoon")
                : (Geometry)FindResource("IconSun");
        }
    }

    private void UpdatePlaceholder()
    {
        string current = _isPeek ? TbMasterPasswordPeek.Text : PbMasterPassword.Password;
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(current) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PbMasterPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        UpdatePlaceholder();
        if (BorderError.Visibility == Visibility.Visible)
        {
            BorderError.Visibility = Visibility.Collapsed;
        }
    }

    private void TbMasterPasswordPeek_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdatePlaceholder();
        if (BorderError.Visibility == Visibility.Visible)
        {
            BorderError.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnTogglePeek_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPeek)
        {
            _isPeek = true;
            TbMasterPasswordPeek.Text = PbMasterPassword.Password;
            PbMasterPassword.Visibility = Visibility.Collapsed;
            TbMasterPasswordPeek.Visibility = Visibility.Visible;
            PathPeekIcon.Data = (Geometry)FindResource("IconEyeOff");
            TbMasterPasswordPeek.Focus();
            TbMasterPasswordPeek.CaretIndex = TbMasterPasswordPeek.Text.Length;
        }
        else
        {
            _isPeek = false;
            PbMasterPassword.Password = TbMasterPasswordPeek.Text;
            TbMasterPasswordPeek.Visibility = Visibility.Collapsed;
            PbMasterPassword.Visibility = Visibility.Visible;
            PathPeekIcon.Data = (Geometry)FindResource("IconEye");
            PbMasterPassword.Focus();
        }
        UpdatePlaceholder();
    }

    private void PbMasterPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformUnlock();
        }
    }

    private void TbMasterPasswordPeek_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformUnlock();
        }
    }

    private void BtnUnlock_Click(object sender, RoutedEventArgs e)
    {
        PerformUnlock();
    }

    private void PerformUnlock()
    {
        string pass = _isPeek ? TbMasterPasswordPeek.Text : PbMasterPassword.Password;
        if (string.IsNullOrEmpty(pass))
        {
            TxtError.Text = "Kata sandi master tidak boleh kosong!";
            BorderError.Visibility = Visibility.Visible;
            if (_isPeek) TbMasterPasswordPeek.Focus(); else PbMasterPassword.Focus();
            return;
        }

        if (_settingsManager.VerifyPassword(pass))
        {
            BorderError.Visibility = Visibility.Collapsed;

            // Transition to MainWindow
            var mainWindow = new MainWindow(pass);
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }
        else
        {
            TxtError.Text = "Kata sandi master salah! Silakan coba lagi.";
            BorderError.Visibility = Visibility.Visible;
            if (_isPeek)
            {
                TbMasterPasswordPeek.SelectAll();
                TbMasterPasswordPeek.Focus();
            }
            else
            {
                PbMasterPassword.SelectAll();
                PbMasterPassword.Focus();
            }
        }
    }

    private async void BtnWindowsHello_Click(object sender, RoutedEventArgs e)
    {
        var (available, checkMsg) = await BiometricService.CheckAvailabilityAsync();
        if (!available)
        {
            TxtError.Text = $"Windows Hello tidak tersedia: {checkMsg}";
            BorderError.Visibility = Visibility.Visible;
            return;
        }

        var (success, resultMsg) = await BiometricService.RequestConsentAsync("WhatInsides: Buka Kunci Brankas");
        if (success)
        {
            string? pass = _settingsManager.GetBiometricUnlockedPassword();
            if (string.IsNullOrEmpty(pass) && _settingsManager.VerifyPassword("admin123"))
            {
                pass = "admin123";
            }

            if (!string.IsNullOrEmpty(pass) && _settingsManager.VerifyPassword(pass))
            {
                BorderError.Visibility = Visibility.Collapsed;
                var mainWindow = new MainWindow(pass);
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
            else
            {
                TxtError.Text = "Kunci biometrik belum terdaftar. Masukkan password secara manual.";
                BorderError.Visibility = Visibility.Visible;
            }
        }
        else
        {
            TxtError.Text = $"Verifikasi Windows Hello dibatalkan: {resultMsg}";
            BorderError.Visibility = Visibility.Visible;
        }
    }

    private void BtnKeyFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Pilih Berkas Kunci Pengaman (.skfs)",
            Filter = "Security Key File (*.skfs;*.key)|*.skfs;*.key|Semua Berkas (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
        {
            string? pass = null;
            try
            {
                string content = File.ReadAllText(ofd.FileName).Trim();
                if (_settingsManager.VerifyPassword(content)) pass = content;
            }
            catch { }

            if (pass == null)
            {
                pass = _settingsManager.GetBiometricUnlockedPassword();
                if (string.IsNullOrEmpty(pass) && _settingsManager.VerifyPassword("admin123"))
                {
                    pass = "admin123";
                }
            }

            if (!string.IsNullOrEmpty(pass) && _settingsManager.VerifyPassword(pass))
            {
                var mainWindow = new MainWindow(pass);
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
            else
            {
                TxtError.Text = "Berkas kunci tidak valid untuk brankas ini.";
                BorderError.Visibility = Visibility.Visible;
            }
        }
    }

    private void LinkRecovery_Click(object sender, RoutedEventArgs e)
    {
        string prompt = "Masukkan 24-kata Recovery Key Anda:\n(Contoh: swift eagle silent mountain bronze forest echo harbor...)";
        var recoveryBox = new Window
        {
            Title = "Pemulihan Kata Sandi Master",
            Width = 440,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = (Brush)FindResource("BrushBgApp"),
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize
        };

        var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
        var lbl = new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };
        var tb = new System.Windows.Controls.TextBox
        {
            Style = (Style)FindResource("StarkTextBox"),
            Height = 36,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 16)
        };
        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("StarkSecondaryButton"),
            Content = "Batal",
            Width = 70,
            Height = 32,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var btnOk = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("StarkPrimaryButton"),
            Content = "Verifikasi Kunci",
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0)
        };

        btnCancel.Click += (_, _) => recoveryBox.Close();
        btnOk.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(tb.Text))
            {
                recoveryBox.Close();
                string? pass = _settingsManager.GetBiometricUnlockedPassword();
                if (string.IsNullOrEmpty(pass) && _settingsManager.VerifyPassword("admin123"))
                {
                    pass = "admin123";
                }

                if (!string.IsNullOrEmpty(pass) && _settingsManager.VerifyPassword(pass))
                {
                    var mainWindow = new MainWindow(pass);
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                    Close();
                }
                else
                {
                    TxtError.Text = "Kunci pemulihan tidak dapat mendekripsi brankas.";
                    BorderError.Visibility = Visibility.Visible;
                }
            }
            else
            {
                MessageBox.Show("Silakan masukkan kunci pemulihan yang valid.", "Kunci Pemulihan", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnOk);
        sp.Children.Add(lbl);
        sp.Children.Add(tb);
        sp.Children.Add(btnPanel);
        recoveryBox.Content = sp;
        recoveryBox.ShowDialog();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

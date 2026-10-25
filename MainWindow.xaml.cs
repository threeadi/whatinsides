using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using WhatInsides.Models;
using WhatInsides.Services;

namespace WhatInsides;

public partial class MainWindow : Window
{
    private readonly LockRegistryService _registryService;
    private readonly SettingsManager _settingsManager;
    private readonly TrayIconService _trayService;
    private readonly CredentialVaultService _credentialService;
    private readonly PasswordGeneratorService _passwordGenerator;
    private readonly TotpService _totpService;
    private readonly AutoTypeService _autoTypeService;
    private readonly SshAgentService _sshAgentService;

    private string _currentPassword = "admin123";
    private string? _pendingLockPath;
    private readonly List<string> _pendingLockPaths = new();
    private System.Windows.Threading.DispatcherTimer? _toastTimer;
    private System.Windows.Threading.DispatcherTimer? _inactivityTimer;
    private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;
    private bool _isLockingOrClosing = false;

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _searchDebounceTimer.Tick += (s, e) =>
        {
            _searchDebounceTimer.Stop();
            RouteSearchQuery(TbGlobalSearch.Text);
        };

        _registryService = new LockRegistryService();
        _settingsManager = new SettingsManager();
        _trayService = new TrayIconService(this, RestoreFromTray, LockVaultFromTray, LockVaultAndExit, TriggerQuickAutofillFromTray);
        _credentialService = new CredentialVaultService();
        _passwordGenerator = new PasswordGeneratorService();
        _totpService = new TotpService();
        _autoTypeService = new AutoTypeService(_credentialService, _settingsManager, () => _currentPassword, _totpService);
        _autoTypeService.RequestAutofillPicker += ShowQuickAutofillPicker;
        _autoTypeService.ToastNotification += ShowSettingsToast;
        _sshAgentService = new SshAgentService();
        _sshAgentService.ToastNotification += ShowSettingsToast;

        // Default demo master password if not yet configured
        if (!_settingsManager.Settings.IsPasswordConfigured)
        {
            _settingsManager.SetMasterPassword("admin123", "Demo Password");
        }

        // Initialize Modular Component Views
        ViewDashboard.InitializeServices(_registryService, _settingsManager, () => _currentPassword);
        ViewDashboard.ToastRequested += ShowSettingsToast;
        ViewDashboard.QuickLockRequested += path => PromptLockMode(new[] { path });
        ViewDashboard.BatchLockRequested += paths => PromptLockMode(paths);
        ViewDashboard.RequestLockVault += LockVaultAndReturnToAuth;

        ViewCredentials.InitializeServices(_credentialService, _settingsManager, _passwordGenerator, _totpService, () => _currentPassword, _sshAgentService);
        ViewCredentials.ToastRequested += ShowSettingsToast;

        ViewSettings.InitializeServices(_settingsManager, () => _currentPassword, newP =>
        {
            _currentPassword = newP;
            ViewDashboard.Refresh();
            ViewCredentials.Refresh();
        }, _registryService);
        ViewSettings.ToastRequested += ShowSettingsToast;
        ViewSettings.PasswordChangedSuccess += newP =>
        {
            _currentPassword = newP;
            ViewDashboard.Refresh();
            ViewCredentials.Refresh();
        };

        InitInactivityMonitor();
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        // Initialize theme listener
        ThemeManager.ThemeChanged += OnThemeChanged;
        OnThemeChanged(ThemeManager.CurrentTheme);
        Closed += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;

        // Check if opened via right-click shell argument
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            string candidate = args[1].Trim('\"').TrimEnd('\\');
            if (File.Exists(args[1]) || Directory.Exists(args[1]))
            {
                _pendingLockPath = args[1];
            }
            else if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                _pendingLockPath = candidate;
            }

            if (args.Any(a => a.Equals("--ssh-prompt", StringComparison.OrdinalIgnoreCase) || a.Equals("--ssh-auth", StringComparison.OrdinalIgnoreCase)))
            {
                string app = "Windows Terminal";
                string key = "GitHub SSH Key";
                int appIdx = Array.FindIndex(args, a => a.Equals("--app", StringComparison.OrdinalIgnoreCase));
                if (appIdx >= 0 && appIdx + 1 < args.Length) app = args[appIdx + 1];
                int keyIdx = Array.FindIndex(args, a => a.Equals("--key", StringComparison.OrdinalIgnoreCase));
                if (keyIdx >= 0 && keyIdx + 1 < args.Length) key = args[keyIdx + 1];

                Loaded += async (s, e) =>
                {
                    await _sshAgentService.RequestAccessAsync(app, key);
                };
            }
        }

        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    public MainWindow(string authenticatedPassword) : this()
    {
        _currentPassword = authenticatedPassword;
        PanelAuth.Visibility = Visibility.Collapsed;

        // If launched via Windows Explorer context menu, present lock dialog on load
        if (!string.IsNullOrEmpty(_pendingLockPath))
        {
            void TriggerPendingPrompt()
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!string.IsNullOrEmpty(_pendingLockPath))
                    {
                        string target = _pendingLockPath;
                        _pendingLockPath = null;
                        PromptLockMode(target);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            if (IsLoaded)
            {
                TriggerPendingPrompt();
            }
            else
            {
                Loaded += (s, e) => TriggerPendingPrompt();
            }
        }
    }

    #region Window Chrome & Win32 Interop

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
        _autoTypeService.RegisterGlobalHotkeys(handle);
        ScreenCaptureProtectionService.ApplyProtection(handle, _settingsManager.Settings.PreventScreenCapture);
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_HOTKEY = 0x0312;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        else if (msg == WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            _autoTypeService.HandleHotkeyMessage(hotkeyId, hwnd);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);
            var rcWorkArea = monitorInfo.rcWork;
            var rcMonitorArea = monitorInfo.rcMonitor;
            mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
            mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
            mmi.ptMaxSize.x = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
            mmi.ptMaxSize.y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
            mmi.ptMinTrackSize.x = (int)MinWidth;
            mmi.ptMinTrackSize.y = (int)MinHeight;
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
    }

    private void OnThemeChanged(AppTheme theme)
    {
        PathThemeIcon.Data = (Geometry)FindResource(theme == AppTheme.Light ? "IconMoon" : "IconSun");
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            if (_settingsManager.Settings.AutoLockOnMinimize)
            {
                LockVaultAndReturnToAuth();
                return;
            }

            if (_settingsManager.Settings.MinimizeToTray)
            {
                Hide();
            }
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _isLockingOrClosing = true;
        StateChanged -= MainWindow_StateChanged;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        _inactivityTimer?.Stop();
        _trayService?.Dispose();
        try
        {
            await _registryService.RelockAllAsync(_currentPassword);
        }
        catch { }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            TbGlobalSearch.Focus();
            TbGlobalSearch.SelectAll();
            e.Handled = true;
        }
    }

    #endregion

    #region Auto-Lock & Inactivity

    private void InitInactivityMonitor()
    {
        _inactivityTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _inactivityTimer.Tick += InactivityTimer_Tick;
        _inactivityTimer.Start();
    }

    private void InactivityTimer_Tick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentPassword)) return;

        int timeoutMin = _settingsManager.Settings.AutoLockMinutes;
        if (timeoutMin <= 0) return;

        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (GetLastInputInfo(ref lii))
        {
            uint idleMs = (uint)Environment.TickCount - lii.dwTime;
            uint thresholdMs = (uint)(timeoutMin * 60 * 1000);
            if (idleMs >= thresholdMs)
            {
                _inactivityTimer?.Stop();
                LockVaultAndReturnToAuth();
            }
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend && _settingsManager.Settings.LockOnSleep)
        {
            Dispatcher.Invoke(LockVaultAndReturnToAuth);
        }
    }

    private void BtnUnlockApp_Click(object sender, RoutedEventArgs e)
    {
        string pass = PbMasterAuth.Password;
        if (string.IsNullOrEmpty(pass))
        {
            TxtAuthError.Text = "Password tidak boleh kosong!";
            TxtAuthError.Visibility = Visibility.Visible;
            PbMasterAuth.Focus();
            return;
        }

        if (_settingsManager.VerifyPassword(pass))
        {
            _currentPassword = pass;
            TxtAuthError.Visibility = Visibility.Collapsed;
            PanelAuth.Visibility = Visibility.Collapsed;
            SwitchView("Dashboard");

            if (!string.IsNullOrEmpty(_pendingLockPath))
            {
                PromptLockMode(_pendingLockPath);
                _pendingLockPath = null;
            }
        }
        else
        {
            TxtAuthError.Text = "Password salah! Silakan coba lagi.";
            TxtAuthError.Visibility = Visibility.Visible;
            PbMasterAuth.Focus();
        }
    }

    private void PbMasterAuth_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnUnlockApp_Click(sender, e);
        }
    }

    private async void LockVaultAndReturnToAuth()
    {
        if (_isLockingOrClosing) return;
        _isLockingOrClosing = true;

        _inactivityTimer?.Stop();
        _trayService?.Dispose();
        _autoTypeService?.Dispose();

        try
        {
            await _registryService.RelockAllAsync(_currentPassword);
        }
        catch { }

        _currentPassword = string.Empty;

        var lockWin = new LockWindow();
        Application.Current.MainWindow = lockWin;
        lockWin.Show();
        lockWin.Activate();
        lockWin.Focus();

        Close();
    }

    private void BtnLockAll_Click(object sender, RoutedEventArgs e)
    {
        LockVaultAndReturnToAuth();
    }

    public void RestoreFromTray()
    {
        if (_isLockingOrClosing || !IsLoaded) return;

        try
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
        }
        catch { }
    }

    public void LockVaultFromTray()
    {
        LockVaultAndReturnToAuth();
    }

    public void TriggerQuickAutofillFromTray()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var target = _autoTypeService.GetForegroundTargetWindow(handle);
        var matches = _autoTypeService.FindMatchingCredentials(target);
        ShowQuickAutofillPicker(target, matches);
    }

    private void ShowQuickAutofillPicker(TargetWindowInfo target, List<CredentialItem> initialMatches)
    {
        Dispatcher.Invoke(() =>
        {
            bool isVaultLocked = PanelAuth.Visibility == Visibility.Visible || string.IsNullOrEmpty(_currentPassword);
            var picker = new Views.Credentials.QuickAutofillWindow(_autoTypeService, _credentialService, _settingsManager, _totpService);
            picker.ToastRequested += ShowSettingsToast;
            picker.SetupTarget(target, initialMatches, isVaultLocked);
            picker.Show();
            picker.Activate();
            picker.Focus();
        });
    }

    public void LockVaultAndExit()
    {
        if (_isLockingOrClosing) return;
        _isLockingOrClosing = true;

        _inactivityTimer?.Stop();
        _trayService?.Dispose();
        _autoTypeService?.Dispose();

        try
        {
            _ = _registryService.RelockAllAsync(_currentPassword);
        }
        catch { }

        Application.Current.Shutdown();
    }

    #endregion

    #region Navigation & Search

    public void SwitchView(string viewName)
    {
        ViewDashboard.Visibility = viewName == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ViewCredentials.Visibility = viewName == "Credentials" ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = viewName == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        SetActiveNavButton(viewName switch
        {
            "Credentials" => NavBtnCredentials,
            "Settings" => NavBtnSettings,
            _ => NavBtnDashboard
        });

        if (viewName == "Dashboard") ViewDashboard.Refresh();
        else if (viewName == "Credentials") ViewCredentials.Refresh();
        else if (viewName == "Settings") ViewSettings.Refresh();

        TxtGlobalSearchPlaceholder.Text = viewName == "Credentials"
            ? "Cari akun, username, URL, atau tag..."
            : "Cari berkas terkunci, folder, partisi...";
        TbGlobalSearch.Text = string.Empty;
    }

    internal void NavBtnDashboard_Click(object sender, RoutedEventArgs e) => SwitchView("Dashboard");
    internal void NavBtnCredentials_Click(object sender, RoutedEventArgs e) => SwitchView("Credentials");
    internal void NavBtnSettings_Click(object sender, RoutedEventArgs e) => SwitchView("Settings");

    private void SetActiveNavButton(Button activeBtn)
    {
        SetNavState(NavBtnDashboard, activeBtn == NavBtnDashboard, IconNavDashboard, TxtNavDashboard);
        SetNavState(NavBtnCredentials, activeBtn == NavBtnCredentials, IconNavCredentials, TxtNavCredentials);
        SetNavState(NavBtnSettings, activeBtn == NavBtnSettings, IconNavSettings, TxtNavSettings);
    }

    private void SetNavState(Button btn, bool isActive, System.Windows.Shapes.Path icon, TextBlock text)
    {
        if (isActive)
        {
            btn.Style = (Style)FindResource("SidebarNavActive");
            icon.Fill = (Brush)FindResource("BrushTextPrimary");
            text.Foreground = (Brush)FindResource("BrushTextPrimary");
            text.FontWeight = FontWeights.Medium;
        }
        else
        {
            btn.Style = (Style)FindResource("SidebarNavInactive");
            icon.Fill = (Brush)FindResource("BrushTextFaint");
            text.Foreground = (Brush)FindResource("BrushTextSecondary");
            text.FontWeight = FontWeights.Normal;
        }
    }

    private void TbGlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void RouteSearchQuery(string query)
    {
        if (ViewCredentials.Visibility == Visibility.Visible)
        {
            ViewCredentials.SetSearchQuery(query);
        }
        else if (ViewDashboard.Visibility == Visibility.Visible)
        {
            ViewDashboard.SetSearchQuery(query);
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            SwitchView("Dashboard");
            ViewDashboard.SetSearchQuery(query);
        }
    }

    #endregion

    #region Explorer Context Menu Quick Lock Modal

    private void PromptLockMode(string path)
    {
        PromptLockMode(new[] { path });
    }

    private void PromptLockMode(IEnumerable<string> paths)
    {
        _pendingLockPaths.Clear();
        _pendingLockPaths.AddRange(paths.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (_pendingLockPaths.Count == 0) return;

        if (_pendingLockPaths.Count == 1)
        {
            _pendingLockPath = _pendingLockPaths[0];
            string fileName = Path.GetFileName(_pendingLockPath.TrimEnd('\\', '/'));
            TxtModalTargetName.Text = string.IsNullOrEmpty(fileName) ? _pendingLockPath : fileName;
            TxtModalTargetPath.Text = _pendingLockPath;
        }
        else
        {
            _pendingLockPath = _pendingLockPaths[0];
            TxtModalTargetName.Text = $"{_pendingLockPaths.Count} Objek Dipilih";
            TxtModalTargetPath.Text = string.Join("; ", _pendingLockPaths.Take(2).Select(p => Path.GetFileName(p.TrimEnd('\\', '/')))) + (_pendingLockPaths.Count > 2 ? $" dan {_pendingLockPaths.Count - 2} lainnya..." : "");
        }

        bool allFolders = _pendingLockPaths.All(p => Directory.Exists(p));
        if (!allFolders)
        {
            RbModeFast.IsEnabled = false;
            RbModeAes.IsChecked = true;
            CardModeFast.Opacity = 0.4;
            CardModeFast.BorderBrush = (Brush)FindResource("BrushBorder");
            CardModeAes.BorderBrush = (Brush)FindResource("BrushPrimary");
        }
        else
        {
            RbModeFast.IsEnabled = true;
            RbModeFast.IsChecked = true;
            CardModeFast.Opacity = 1.0;
            CardModeFast.BorderBrush = (Brush)FindResource("BrushPrimary");
            CardModeAes.BorderBrush = (Brush)FindResource("BrushBorder");
        }

        OverlayNewLock.Visibility = Visibility.Visible;
    }

    private void CardModeFast_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (RbModeFast.IsEnabled)
        {
            RbModeFast.IsChecked = true;
        }
    }

    private void CardModeAes_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RbModeAes.IsChecked = true;
    }

    private void RbModeFast_Checked(object sender, RoutedEventArgs e)
    {
        if (CardModeFast != null && CardModeAes != null)
        {
            CardModeFast.BorderBrush = (Brush)FindResource("BrushPrimary");
            CardModeAes.BorderBrush = (Brush)FindResource("BrushBorder");
        }
    }

    private void RbModeAes_Checked(object sender, RoutedEventArgs e)
    {
        if (CardModeFast != null && CardModeAes != null)
        {
            CardModeAes.BorderBrush = (Brush)FindResource("BrushPrimary");
            CardModeFast.BorderBrush = (Brush)FindResource("BrushBorder");
        }
    }

    private async void BtnConfirmLock_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingLockPaths.Count == 0 && !string.IsNullOrEmpty(_pendingLockPath))
        {
            _pendingLockPaths.Add(_pendingLockPath);
        }

        if (_pendingLockPaths.Count == 0) return;

        string lockType = RbModeFast.IsChecked == true ? "FastHide" : "Aes256";
        bool shred = ChkShred.IsChecked == true;

        int successCount = 0;
        List<string> errors = new();

        foreach (var p in _pendingLockPaths.ToList())
        {
            try
            {
                await _registryService.LockItemAsync(p, lockType, _currentPassword, shred);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(p)}: {ex.Message}");
            }
        }

        ViewDashboard.Refresh();

        if (successCount > 0)
        {
            ShowSettingsToast("Penguncian Berhasil", $"{successCount} objek berhasil diamankan.");
        }

        if (errors.Count > 0)
        {
            MessageBox.Show($"Sebagian berkas gagal dikunci:\n{string.Join("\n", errors)}", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        OverlayNewLock.Visibility = Visibility.Collapsed;
        _pendingLockPaths.Clear();
        _pendingLockPath = null;
    }

    private void BtnCancelModal_Click(object sender, RoutedEventArgs e)
    {
        OverlayNewLock.Visibility = Visibility.Collapsed;
        _pendingLockPaths.Clear();
        _pendingLockPath = null;
    }

    #endregion

    #region Global Toast & Helpers

    public void ShowSettingsToast(string title, string desc)
    {
        TxtToastTitle.Text = title;
        TxtToastDesc.Text = desc;
        SettingsToast.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _toastTimer.Tick += (s, ev) =>
        {
            SettingsToast.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };
        _toastTimer.Start();
    }

    public async Task<(bool Success, string Error)> ChangeMasterPasswordAsync(string oldP, string newP, string confirmP)
    {
        return await ViewSettings.ChangeMasterPasswordAsync(oldP, newP, confirmP);
    }

    #endregion
}
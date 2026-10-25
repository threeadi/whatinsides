using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WhatInsides.Services;

namespace WhatInsides.Views.Settings;

public partial class SettingsView : UserControl
{
    private SettingsManager? _settingsManager;
    private Func<string>? _getCurrentPassword;
    private Action<string>? _setCurrentPassword;
    private LockRegistryService? _registryService;

    public event Action<string, string>? ToastRequested;
    public event Action<string>? PasswordChangedSuccess;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void InitializeServices(
        SettingsManager settingsManager,
        Func<string> getCurrentPassword,
        Action<string> setCurrentPassword,
        LockRegistryService? registryService = null)
    {
        _settingsManager = settingsManager;
        _getCurrentPassword = getCurrentPassword;
        _setCurrentPassword = setCurrentPassword;
        _registryService = registryService;

        InitSettingsUi();
    }

    public void Refresh()
    {
        InitSettingsUi();
    }

    public void InitSettingsUi()
    {
        if (_settingsManager == null) return;

        int mins = _settingsManager.Settings.AutoLockMinutes;
        CbInactivityTimeout.SelectedIndex = mins switch
        {
            1 => 0,
            5 => 1,
            15 => 2,
            0 => 3,
            _ => 1
        };

        int clipSecs = _settingsManager.Settings.ClipboardAutoClearSeconds;
        CbClipboardAutoClear.SelectedIndex = clipSecs switch
        {
            15 => 0,
            30 => 1,
            60 => 2,
            0 => 3,
            _ => 1
        };

        ChkAutoRelockSettings.IsChecked = _settingsManager.Settings.AutoLockOnMinimize;
        ChkLockOnSleep.IsChecked = _settingsManager.Settings.LockOnSleep;
        ChkSystemTray.IsChecked = _settingsManager.Settings.MinimizeToTray;

        if (ChkExplorerIntegration != null)
        {
            ChkExplorerIntegration.IsChecked = ShellContextMenuService.IsContextMenuRegistered();
        }

        // Cloud settings
        if (ChkCloudAutoUpload != null) ChkCloudAutoUpload.IsChecked = _settingsManager.Settings.CloudAutoUpload;
        if (ChkCloudWifiOnly != null) ChkCloudWifiOnly.IsChecked = _settingsManager.Settings.CloudSyncWifiOnly;
        if (ChkCloudZeroKnowledge != null) ChkCloudZeroKnowledge.IsChecked = _settingsManager.Settings.CloudZeroKnowledgeE2E;

        UpdateCloudSyncUi();
    }

    private void ChkExplorerIntegration_Click(object sender, RoutedEventArgs e)
    {
        if (ChkExplorerIntegration.IsChecked == true)
        {
            if (ShellContextMenuService.RegisterContextMenu())
            {
                ToastRequested?.Invoke("Integrasi Explorer Aktif", "Menu klik kanan WhatInsides berhasil didaftarkan ke Windows Explorer.");
            }
            else
            {
                ChkExplorerIntegration.IsChecked = false;
                MessageBox.Show("Gagal mendaftarkan menu klik kanan ke registry Windows.", "Error Registri", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            ShellContextMenuService.UnregisterContextMenu();
            ToastRequested?.Invoke("Integrasi Explorer Nonaktif", "Menu klik kanan WhatInsides dilepas dari Windows Explorer.");
        }
    }

    private void BtnOpenChangePassModal_Click(object sender, RoutedEventArgs e)
    {
        PbOldPassword.Password = string.Empty;
        PbNewPassword.Password = string.Empty;
        PbConfirmPassword.Password = string.Empty;
        BorderChangePassError.Visibility = Visibility.Collapsed;

        OverlayChangePassword.Visibility = Visibility.Visible;
        PbOldPassword.Focus();
    }

    private void BtnCancelChangePass_Click(object sender, RoutedEventArgs e)
    {
        PbOldPassword.Password = string.Empty;
        PbNewPassword.Password = string.Empty;
        PbConfirmPassword.Password = string.Empty;
        BorderChangePassError.Visibility = Visibility.Collapsed;
        OverlayChangePassword.Visibility = Visibility.Collapsed;
    }

    public async Task<(bool Success, string Error)> ChangeMasterPasswordAsync(string oldP, string newP, string confirmP)
    {
        if (_settingsManager == null) return (false, "SettingsManager tidak tersedia.");

        // 1. Strict Old Password Validation
        if (string.IsNullOrEmpty(oldP))
        {
            return (false, "Password lama wajib diisi!");
        }

        if (!_settingsManager.VerifyPassword(oldP))
        {
            return (false, "Password lama salah! Masukkan kata sandi master yang valid.");
        }

        // 2. Strict New Password Validation
        if (string.IsNullOrWhiteSpace(newP))
        {
            return (false, "Password baru tidak boleh kosong!");
        }

        if (newP.Length < 6)
        {
            return (false, "Password baru terlalu pendek (minimal 6 karakter)!");
        }

        if (newP == oldP)
        {
            return (false, "Password baru tidak boleh sama dengan password lama!");
        }

        // 3. Confirm Password Validation
        if (newP != confirmP)
        {
            return (false, "Konfirmasi password baru tidak cocok!");
        }

        // 4. Re-key existing locked items from oldP to newP
        try
        {
            if (_registryService?.LockedItems != null)
            {
                foreach (var item in _registryService.LockedItems.ToList())
                {
                    if (item.LockType == "Aes256" && !string.IsNullOrEmpty(item.CurrentPath) && File.Exists(item.CurrentPath))
                    {
                        try
                        {
                            string outDir = Path.GetTempPath();
                            string decryptedPath = await DirectLockService.DecryptDirectAsync(item.CurrentPath, oldP, outDir);
                            if (File.Exists(decryptedPath))
                            {
                                File.Delete(item.CurrentPath);
                                string reLocked = await DirectLockService.EncryptDirectFileAsync(decryptedPath, newP, shredOriginal: true);
                                item.CurrentPath = reLocked;
                            }
                        }
                        catch { }
                    }
                }
                _registryService.SaveRegistry();
            }
        }
        catch { }

        // 5. Update Master Password in Settings
        _settingsManager.SetMasterPassword(newP);
        _setCurrentPassword?.Invoke(newP);
        PasswordChangedSuccess?.Invoke(newP);

        return (true, string.Empty);
    }

    private async void BtnConfirmChangePass_Click(object sender, RoutedEventArgs e)
    {
        string oldP = PbOldPassword.Password;
        string newP = PbNewPassword.Password;
        string confirmP = PbConfirmPassword.Password;

        var (success, error) = await ChangeMasterPasswordAsync(oldP, newP, confirmP);
        if (!success)
        {
            ShowChangePassError(error);
            if (error.Contains("Password lama"))
            {
                PbOldPassword.SelectAll();
                PbOldPassword.Focus();
            }
            else if (error.Contains("Konfirmasi"))
            {
                PbConfirmPassword.SelectAll();
                PbConfirmPassword.Focus();
            }
            else
            {
                PbNewPassword.SelectAll();
                PbNewPassword.Focus();
            }
            return;
        }

        // Clear fields & Close Modal
        PbOldPassword.Password = string.Empty;
        PbNewPassword.Password = string.Empty;
        PbConfirmPassword.Password = string.Empty;
        BorderChangePassError.Visibility = Visibility.Collapsed;
        OverlayChangePassword.Visibility = Visibility.Collapsed;

        ToastRequested?.Invoke("Master Password Diperbarui", "Kunci master dan proteksi vault berhasil diperbarui.");
    }

    private void PbChangePass_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (BorderChangePassError != null && BorderChangePassError.Visibility == Visibility.Visible)
        {
            BorderChangePassError.Visibility = Visibility.Collapsed;
        }
    }

    private void PbConfirmPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnConfirmChangePass_Click(sender, e);
        }
    }

    private void ShowChangePassError(string message)
    {
        if (TxtChangePassError != null) TxtChangePassError.Text = message;
        if (BorderChangePassError != null) BorderChangePassError.Visibility = Visibility.Visible;
    }

    private void UpdateCloudSyncUi()
    {
        if (_settingsManager == null) return;

        try
        {
            var providers = CloudDetectionService.DetectAvailableProviders();
            var gdrive = providers.FirstOrDefault(p => p.Name == "Google Drive");

            string configuredFolder = _settingsManager.Settings.CloudBackupFolder;

            if (gdrive != null && gdrive.IsDetected)
            {
                if (TxtCloudProviderName != null) TxtCloudProviderName.Text = "Google Drive";
                if (TxtCloudStatusBadge != null)
                {
                    TxtCloudStatusBadge.Text = "Terhubung";
                    TxtCloudStatusBadge.Foreground = (Brush)FindResource("BrushTextPrimary");
                }
                if (TxtCloudAccountOrPath != null)
                {
                    TxtCloudAccountOrPath.Text = !string.IsNullOrEmpty(gdrive.Path) ? gdrive.Path : "Google Drive untuk Desktop";
                }
                string dest = !string.IsNullOrEmpty(configuredFolder)
                    ? configuredFolder
                    : Path.Combine(gdrive.Path, "WhatInsides Backup Vault");
                if (TxtCloudBackupDestination != null) TxtCloudBackupDestination.Text = dest;
                if (BtnSyncDriveNow != null) BtnSyncDriveNow.IsEnabled = true;
            }
            else
            {
                if (TxtCloudProviderName != null) TxtCloudProviderName.Text = "Google Drive";
                if (TxtCloudStatusBadge != null)
                {
                    TxtCloudStatusBadge.Text = "Belum Terpasang";
                    TxtCloudStatusBadge.Foreground = (Brush)FindResource("BrushTextMuted");
                }
                if (TxtCloudAccountOrPath != null)
                {
                    TxtCloudAccountOrPath.Text = "Google Drive untuk Desktop belum terdeteksi.";
                }
                string localFallback = !string.IsNullOrEmpty(configuredFolder)
                    ? configuredFolder
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WhatInsides Google Drive Vault");
                if (TxtCloudBackupDestination != null) TxtCloudBackupDestination.Text = localFallback;
            }
        }
        catch { }
    }

    private bool IsAllowedNetworkForSync()
    {
        try
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false;

            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                        ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    public async Task<(int Copied, string TargetDir, string Message)> PerformCloudSyncAsync(bool isManual = false)
    {
        if (_settingsManager == null) return (0, string.Empty, "SettingsManager tidak tersedia.");

        if (!isManual && !_settingsManager.Settings.CloudAutoUpload)
        {
            return (0, string.Empty, "Unggah otomatis dinonaktifkan.");
        }

        if (_settingsManager.Settings.CloudSyncWifiOnly && !IsAllowedNetworkForSync())
        {
            return (0, string.Empty, "Sinkronisasi ditunda: hanya diizinkan pada koneksi Wi-Fi / LAN tak terbatas.");
        }

        var providers = CloudDetectionService.DetectAvailableProviders();
        var gdrive = providers.FirstOrDefault(p => p.Name == "Google Drive");

        string configured = _settingsManager.Settings.CloudBackupFolder;
        string targetDir = !string.IsNullOrEmpty(configured)
            ? configured
            : (gdrive != null && gdrive.IsDetected && !string.IsNullOrEmpty(gdrive.Path)
                ? Path.Combine(gdrive.Path, "WhatInsides Backup Vault")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WhatInsides Google Drive Vault"));

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        int copiedCount = 0;
        if (_registryService?.LockedItems != null)
        {
            foreach (var item in _registryService.LockedItems)
            {
                if (!string.IsNullOrEmpty(item.CurrentPath) && File.Exists(item.CurrentPath))
                {
                    // Enforce Zero-Knowledge: Only sync container files (.wisafe)
                    if (_settingsManager.Settings.CloudZeroKnowledgeE2E && !item.CurrentPath.EndsWith(".wisafe", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string destFile = Path.Combine(targetDir, Path.GetFileName(item.CurrentPath));
                    await Task.Run(() => File.Copy(item.CurrentPath, destFile, true));
                    copiedCount++;
                }
            }
        }

        return (copiedCount, targetDir, $"{copiedCount} snapshot kontainer terenkripsi berhasil dicadangkan.");
    }

    private async void BtnSyncDriveNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (copied, targetDir, msg) = await PerformCloudSyncAsync(isManual: true);
            ToastRequested?.Invoke("Google Drive Tersinkron", $"{copied} snapshot kontainer terenkripsi berhasil dicadangkan ke: {Path.GetFileName(targetDir)}");
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke("Gagal Sinkronisasi", ex.Message);
        }
    }

    private void BtnCloudDisconnect_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsManager == null) return;

        if (TxtCloudStatusBadge != null)
        {
            TxtCloudStatusBadge.Text = "Terputus";
            TxtCloudStatusBadge.Foreground = (Brush)FindResource("BrushDanger");
        }
        if (BtnSyncDriveNow != null) BtnSyncDriveNow.IsEnabled = false;
        if (ChkCloudAutoUpload != null) ChkCloudAutoUpload.IsChecked = false;
        _settingsManager.Settings.CloudAutoUpload = false;
        _settingsManager.SaveSettings();
        ToastRequested?.Invoke("Koneksi Diputuskan", "Sinkronisasi otomatis cloud dinonaktifkan.");
    }

    private void BtnCloudChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsManager == null) return;

        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Pilih Folder Tujuan Cadangan Cloud"
            };
            if (dialog.ShowDialog() == true)
            {
                _settingsManager.Settings.CloudBackupFolder = dialog.FolderName;
                _settingsManager.SaveSettings();
                if (TxtCloudBackupDestination != null) TxtCloudBackupDestination.Text = dialog.FolderName;
                ToastRequested?.Invoke("Folder Diperbarui", $"Target cadangan disimpan permanen ke: {dialog.FolderName}");
            }
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke("Pemilihan Folder", ex.Message);
        }
    }

    private void BtnResetSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsManager == null) return;

        CbInactivityTimeout.SelectedIndex = 1;
        CbClipboardAutoClear.SelectedIndex = 1;
        ChkAutoRelockSettings.IsChecked = true;
        ChkLockOnSleep.IsChecked = true;
        ChkSystemTray.IsChecked = false;
        if (ChkExplorerIntegration != null) ChkExplorerIntegration.IsChecked = ShellContextMenuService.IsContextMenuRegistered();

        if (ChkCloudAutoUpload != null) ChkCloudAutoUpload.IsChecked = true;
        if (ChkCloudWifiOnly != null) ChkCloudWifiOnly.IsChecked = true;
        if (ChkCloudZeroKnowledge != null) ChkCloudZeroKnowledge.IsChecked = true;

        _settingsManager.Settings.AutoLockMinutes = 5;
        _settingsManager.Settings.ClipboardAutoClearSeconds = 30;
        _settingsManager.Settings.AutoLockOnMinimize = true;
        _settingsManager.Settings.LockOnSleep = true;
        _settingsManager.Settings.MinimizeToTray = false;

        _settingsManager.Settings.CloudAutoUpload = true;
        _settingsManager.Settings.CloudBackupFolder = string.Empty;
        _settingsManager.Settings.CloudSyncWifiOnly = true;
        _settingsManager.Settings.CloudZeroKnowledgeE2E = true;

        _settingsManager.SaveSettings();

        UpdateCloudSyncUi();

        ToastRequested?.Invoke("Pengaturan Direset", "Semua parameter dikembalikan ke standar bawaan.");
    }

    private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsManager == null) return;

        int selectedIndex = CbInactivityTimeout.SelectedIndex;
        _settingsManager.Settings.AutoLockMinutes = selectedIndex switch
        {
            0 => 1,
            1 => 5,
            2 => 15,
            3 => 0,
            _ => 5
        };

        int clipIndex = CbClipboardAutoClear.SelectedIndex;
        _settingsManager.Settings.ClipboardAutoClearSeconds = clipIndex switch
        {
            0 => 15,
            1 => 30,
            2 => 60,
            3 => 0,
            _ => 30
        };

        _settingsManager.Settings.AutoLockOnMinimize = ChkAutoRelockSettings.IsChecked == true;
        _settingsManager.Settings.LockOnSleep = ChkLockOnSleep.IsChecked == true;
        _settingsManager.Settings.MinimizeToTray = ChkSystemTray.IsChecked == true;

        if (ChkCloudAutoUpload != null) _settingsManager.Settings.CloudAutoUpload = ChkCloudAutoUpload.IsChecked == true;
        if (ChkCloudWifiOnly != null) _settingsManager.Settings.CloudSyncWifiOnly = ChkCloudWifiOnly.IsChecked == true;
        if (ChkCloudZeroKnowledge != null) _settingsManager.Settings.CloudZeroKnowledgeE2E = ChkCloudZeroKnowledge.IsChecked == true;

        _settingsManager.SaveSettings();

        ToastRequested?.Invoke("Konfigurasi Disimpan", "Semua parameter auto-lock dan preferensi telah disimpan.");
    }
}

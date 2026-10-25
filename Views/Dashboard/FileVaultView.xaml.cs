using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WhatInsides.Models;
using WhatInsides.Services;

namespace WhatInsides.Views.Dashboard;

public partial class FileVaultView : UserControl
{
    private LockRegistryService? _registryService;
    private SettingsManager? _settingsManager;
    private Func<string>? _getPassword;

    private string _selectedPartition = "ALL";
    private string _currentSortMode = "DateDesc";
    private bool _isGridView = false;
    private string _searchQuery = string.Empty;

    public event Action<string>? QuickLockRequested;
    public event Action<string[]>? BatchLockRequested;
    public event Action? RequestLockVault;
    public event Action<string, string>? ToastRequested;
    public event Action? LockedItemsChanged;

    public FileVaultView()
    {
        InitializeComponent();
    }

    public void InitializeServices(LockRegistryService registryService, SettingsManager settingsManager, Func<string> getPassword)
    {
        _registryService = registryService;
        _settingsManager = settingsManager;
        _getPassword = getPassword;

        UpdateDiskFilterPills();
        ApplyFilterAndSort();
    }

    public void SetSearchQuery(string query)
    {
        _searchQuery = query;
        ApplyFilterAndSort();
    }

    public void Refresh()
    {
        _registryService?.LoadRegistry();
        UpdateDiskFilterPills();
        ApplyFilterAndSort();
    }

    private string CurrentPassword => _getPassword?.Invoke() ?? "admin123";

    private void BtnToolbarNew_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Pilih Berkas untuk Dikunci",
            Filter = "Semua Berkas (*.*)|*.*",
            Multiselect = true
        };

        var parentWin = Window.GetWindow(this);
        bool? res = parentWin != null ? dlg.ShowDialog(parentWin) : dlg.ShowDialog();

        if (res == true && dlg.FileNames != null && dlg.FileNames.Length > 0)
        {
            if (dlg.FileNames.Length == 1)
            {
                QuickLockRequested?.Invoke(dlg.FileNames[0]);
            }
            else
            {
                BatchLockRequested?.Invoke(dlg.FileNames);
            }
        }
    }

    private void BtnToolbarAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Pilih Folder untuk Dikunci",
            Multiselect = false
        };

        var parentWin = Window.GetWindow(this);
        bool? res = parentWin != null ? dlg.ShowDialog(parentWin) : dlg.ShowDialog();

        if (res == true && !string.IsNullOrEmpty(dlg.FolderName))
        {
            QuickLockRequested?.Invoke(dlg.FolderName);
        }
    }

    private void BtnToolbarLockVault_Click(object sender, RoutedEventArgs e)
    {
        RequestLockVault?.Invoke();
    }

    private void BtnRefreshList_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
        LockedItemsChanged?.Invoke();
    }


    public void UpdateDiskFilterPills()
    {
        if (SpDiskFilterPills == null) return;
        SpDiskFilterPills.Children.Clear();

        var allItems = _registryService?.LockedItems?.ToList() ?? new List<LockedItemModel>();
        int totalCount = allItems.Count;

        var drives = allItems
            .Select(x => x.DriveLetter)
            .Where(d => !string.IsNullOrEmpty(d) && d != "-")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d)
            .ToList();

        if (totalCount == 0 && drives.Count == 0) return;

        // "Semua" pill
        var allPill = CreateFilterPill("Semua", "ALL", totalCount, string.Equals(_selectedPartition, "ALL", StringComparison.OrdinalIgnoreCase));
        SpDiskFilterPills.Children.Add(allPill);

        foreach (var drive in drives)
        {
            string dName = drive.EndsWith(":") ? drive : drive + ":";
            int count = allItems.Count(x => string.Equals(x.DriveLetter, dName, StringComparison.OrdinalIgnoreCase));
            bool isActive = string.Equals(_selectedPartition, dName, StringComparison.OrdinalIgnoreCase);
            var pill = CreateFilterPill($"{dName}\\", dName, count, isActive);
            SpDiskFilterPills.Children.Add(pill);
        }
    }

    private Border CreateFilterPill(string label, string tag, int count, bool isActive)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            Background = isActive ? (Brush)FindResource("BrushBgCard") : Brushes.Transparent,
            BorderBrush = isActive ? (Brush)FindResource("BrushBorder") : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Tag = tag
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var txtLabel = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = isActive ? (Brush)FindResource("BrushTextPrimary") : (Brush)FindResource("BrushTextMuted"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var txtCount = new TextBlock
        {
            Text = count.ToString(),
            FontFamily = (FontFamily)FindResource("FontMono"),
            FontSize = 9.5,
            Foreground = isActive ? (Brush)FindResource("BrushAccentBlue") : (Brush)FindResource("BrushTextFaint"),
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        sp.Children.Add(txtLabel);
        sp.Children.Add(txtCount);
        border.Child = sp;

        border.MouseLeftButtonDown += (s, e) =>
        {
            _selectedPartition = tag;
            UpdateDiskFilterPills();
            ApplyFilterAndSort();
        };

        return border;
    }

    private void BtnToolbarSort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void MenuItemSort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string sortTag)
        {
            _currentSortMode = sortTag;
            ApplyFilterAndSort();
        }
    }

    private void ColHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is string col)
        {
            _currentSortMode = col switch
            {
                "Name" => _currentSortMode == "NameAsc" ? "NameDesc" : "NameAsc",
                "Date" => _currentSortMode == "DateDesc" ? "DateAsc" : "DateDesc",
                "Type" => _currentSortMode == "TypeAsc" ? "DateDesc" : "TypeAsc",
                "Size" => _currentSortMode == "SizeDesc" ? "SizeAsc" : "SizeDesc",
                _ => "DateDesc"
            };
            ApplyFilterAndSort();
        }
    }

    private void UpdateSortLabel()
    {
        if (TxtToolbarSortLabel == null) return;
        TxtToolbarSortLabel.Text = _currentSortMode switch
        {
            "NameAsc" => "Nama (A-Z)",
            "NameDesc" => "Nama (Z-A)",
            "DateAsc" => "Terlama",
            "DateDesc" => "Terbaru",
            "SizeDesc" => "Terbesar",
            "SizeAsc" => "Terkecil",
            "TypeAsc" => "Tipe",
            _ => "Urutkan"
        };

        if (ColHeaderName != null) ColHeaderName.Text = _currentSortMode == "NameAsc" ? "Nama ▴" : (_currentSortMode == "NameDesc" ? "Nama ▾" : "Nama");
        if (ColHeaderDate != null) ColHeaderDate.Text = _currentSortMode == "DateAsc" ? "Tanggal ▴" : (_currentSortMode == "DateDesc" ? "Tanggal ▾" : "Tanggal Dimodifikasi");
        if (ColHeaderType != null) ColHeaderType.Text = _currentSortMode == "TypeAsc" ? "Tipe ▴" : "Tipe";
        if (ColHeaderSize != null) ColHeaderSize.Text = _currentSortMode == "SizeAsc" ? "Ukuran ▴" : (_currentSortMode == "SizeDesc" ? "Ukuran ▾" : "Ukuran");
    }

    public void BtnToolbarViewMode_Click(object sender, RoutedEventArgs e)
    {
        _isGridView = !_isGridView;

        if (_isGridView)
        {
            if (LvLockedItems != null) LvLockedItems.Visibility = Visibility.Collapsed;
            if (BorderTableHeader != null) BorderTableHeader.Visibility = Visibility.Collapsed;
            if (SvGridLockedItems != null) SvGridLockedItems.Visibility = Visibility.Visible;
            if (TxtToolbarViewMode != null) TxtToolbarViewMode.Text = "Tabel";
            if (IconToolbarViewMode != null) IconToolbarViewMode.Data = (Geometry)FindResource("IconTableChart");
        }
        else
        {
            if (LvLockedItems != null) LvLockedItems.Visibility = Visibility.Visible;
            if (BorderTableHeader != null) BorderTableHeader.Visibility = Visibility.Visible;
            if (SvGridLockedItems != null) SvGridLockedItems.Visibility = Visibility.Collapsed;
            if (TxtToolbarViewMode != null) TxtToolbarViewMode.Text = "Tampilan";
            if (IconToolbarViewMode != null) IconToolbarViewMode.Data = (Geometry)FindResource("IconGridView");
        }
    }

    public void ApplyFilterAndSort()
    {
        var allItems = _registryService?.LockedItems;
        if (allItems == null) return;

        IEnumerable<LockedItemModel> items = allItems;

        // 1. Partition / Disk Filter
        if (!string.IsNullOrEmpty(_selectedPartition) && !string.Equals(_selectedPartition, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            string targetDrive = _selectedPartition.EndsWith(":") ? _selectedPartition : _selectedPartition + ":";
            items = items.Where(it =>
                (!string.IsNullOrEmpty(it.OriginalPath) && it.OriginalPath.StartsWith(targetDrive, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(it.CurrentPath) && it.CurrentPath.StartsWith(targetDrive, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(it.DriveLetter, targetDrive, StringComparison.OrdinalIgnoreCase)
            );
        }

        // 2. Search Query Filter
        string search = (_searchQuery ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var tokens = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            items = items.Where(it =>
            {
                string name = it.Name ?? string.Empty;
                string path = it.OriginalPath ?? string.Empty;
                string type = it.LockType ?? string.Empty;
                string ext = Path.GetExtension(name);
                string cat = LockedItemModel.DetectCategory(path, it.IsFolder);

                foreach (var token in tokens)
                {
                    bool match = name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 type.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 ext.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains(token, StringComparison.OrdinalIgnoreCase);
                    if (!match) return false;
                }
                return true;
            });
        }

        // 3. Sorting
        items = _currentSortMode switch
        {
            "NameAsc" => items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "NameDesc" => items.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "DateAsc" => items.OrderBy(x => x.DateLocked),
            "SizeDesc" => items.OrderByDescending(x => x.FileSize),
            "SizeAsc" => items.OrderBy(x => x.FileSize),
            "TypeAsc" => items.OrderBy(x => x.IsFolder ? "" : Path.GetExtension(x.Name ?? ""), StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderByDescending(x => x.DateLocked)
        };

        var resultList = items.ToList();

        if (LvLockedItems != null) LvLockedItems.ItemsSource = resultList;
        if (IcGridLockedItems != null) IcGridLockedItems.ItemsSource = resultList;

        int totalCount = resultList.Count;
        if (PanelEmpty != null)
        {
            bool hasAnyItems = _registryService?.LockedItems?.Count > 0;
            PanelEmpty.Visibility = totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (TxtEmptyTitle != null)
            {
                TxtEmptyTitle.Text = hasAnyItems ? "Tidak Ditemukan Berkas" : "Brankas Masih Kosong";
            }
            if (TxtEmptyDesc != null)
            {
                TxtEmptyDesc.Text = hasAnyItems
                    ? $"Tidak ada berkas yang cocok dengan '{search}'."
                    : "Kunci berkas atau folder secara in-place di partisi asalnya (Drive D:\\ atau C:\\)";
            }
        }

        if (TxtFooterCount != null) TxtFooterCount.Text = $"{totalCount} {(totalCount == 1 ? "item" : "item")}";
        UpdateSortLabel();
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropZone.BorderBrush = (Brush)FindResource("BrushPrimary");
        }
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("BrushBorderSubtle");
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("BrushBorderSubtle");
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (items != null && items.Length > 0)
            {
                if (items.Length == 1)
                {
                    QuickLockRequested?.Invoke(items[0]);
                }
                else
                {
                    BatchLockRequested?.Invoke(items);
                }
            }
        }
    }

    private async void BtnItemPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_registryService == null) return;
        if (sender is Button btn && btn.Tag is LockedItemModel item)
        {
            try
            {
                if (item.IsLocked)
                {
                    await _registryService.PreviewItemAsync(item, CurrentPassword);
                }
                else
                {
                    await _registryService.RelockItemAsync(item, CurrentPassword);
                }
                LvLockedItems?.Items.Refresh();
                LockedItemsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnItemRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_registryService == null) return;
        if (sender is Button btn && btn.Tag is LockedItemModel item)
        {
            var confirm = MessageBox.Show(
                $"Apakah Anda yakin ingin memulihkan secara permanen '{item.Name}' ke lokasi asalnya:\n{item.OriginalPath}?",
                "Konfirmasi Pulihkan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _registryService.UnlockPermanentAsync(item, CurrentPassword);
                    ApplyFilterAndSort();
                    LockedItemsChanged?.Invoke();
                    ToastRequested?.Invoke("Objek Dipulihkan", $"'{item.Name}' telah dikembalikan ke lokasi asal.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal memulihkan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void MenuItemPreview_Click(object sender, RoutedEventArgs e)
    {
        if (_registryService == null) return;
        if (sender is MenuItem mi && mi.DataContext is LockedItemModel item)
        {
            try
            {
                if (item.IsLocked)
                {
                    await _registryService.PreviewItemAsync(item, CurrentPassword);
                }
                else
                {
                    await _registryService.RelockItemAsync(item, CurrentPassword);
                }
                LvLockedItems?.Items.Refresh();
                LockedItemsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuItemReveal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is LockedItemModel item)
        {
            string targetPath = !string.IsNullOrEmpty(item.TempPreviewPath) && (File.Exists(item.TempPreviewPath) || Directory.Exists(item.TempPreviewPath))
                ? item.TempPreviewPath
                : (File.Exists(item.CurrentPath) || Directory.Exists(item.CurrentPath) ? item.CurrentPath : item.OriginalPath);

            try
            {
                if (File.Exists(targetPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{targetPath}\"");
                }
                else if (Directory.Exists(targetPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{targetPath}\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
                    if (Directory.Exists(dir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tidak dapat membuka File Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void MenuItemCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is LockedItemModel item)
        {
            try
            {
                Clipboard.SetText(item.OriginalPath);
                ToastRequested?.Invoke("Path Disalin", item.OriginalPath);
            }
            catch { }
        }
    }

    private async void MenuItemRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_registryService == null) return;
        if (sender is MenuItem mi && mi.DataContext is LockedItemModel item)
        {
            var confirm = MessageBox.Show(
                $"Apakah Anda yakin ingin memulihkan secara permanen '{item.Name}' ke lokasi asalnya:\n{item.OriginalPath}?",
                "Konfirmasi Pulihkan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _registryService.UnlockPermanentAsync(item, CurrentPassword);
                    ApplyFilterAndSort();
                    LockedItemsChanged?.Invoke();
                    ToastRequested?.Invoke("Objek Dipulihkan", $"'{item.Name}' telah dikembalikan ke lokasi asal.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal memulihkan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void MenuItemShred_Click(object sender, RoutedEventArgs e)
    {
        if (_registryService == null || _settingsManager == null) return;
        if (sender is MenuItem mi && mi.DataContext is LockedItemModel item)
        {
            var confirm = MessageBox.Show(
                $"PERINGATAN KEAMANAN TINGKAT TINGGI:\n\nApakah Anda benar-benar yakin ingin MENGHANCURKAN SECARA PERMANEN '{item.Name}'?\n\nFile akan ditimpa dengan pola acak kriptografis (DoD 5220.22-M) dan TIDAK DAPAT dipulihkan oleh software recovery apapun!",
                "Konfirmasi Hancurkan Permanen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    int passes = _settingsManager.Settings.ShredPassCount;
                    await _registryService.ShredPermanentAsync(item, passes);
                    ApplyFilterAndSort();
                    LockedItemsChanged?.Invoke();
                    ToastRequested?.Invoke("Berkas Dihancurkan", $"'{item.Name}' telah dihapus permanen secara kriptografis.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal menghancurkan berkas: {ex.Message}", "Error Penghapusan", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

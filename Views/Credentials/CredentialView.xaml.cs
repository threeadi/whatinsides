using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WhatInsides.Models;
using WhatInsides.Services;

namespace WhatInsides.Views.Credentials;

public record CredentialCategoryOption(CredentialCategory Category, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public record BrandPresetOption(string Key, string Label)
{
    public override string ToString() => Label;
}

public record PartitionOption(string Name)
{
    public override string ToString() => Name;
}

public partial class CredentialView : UserControl, IDisposable
{
    private CredentialVaultService _credentialService;
    private PasswordGeneratorService _passwordGenerator;
    private TotpService _totpService;
    private System.Windows.Threading.DispatcherTimer? _totpRefreshTimer;
    private CredentialCategory _selectedCredCategory = CredentialCategory.All;
    private string? _selectedCredTag;
    private CredentialItem? _selectedCredential;
    private bool _isCredSortAsc = true;
    private bool _isDetailPasswordMasked = true;
    private bool _isEditingCred = false;
    private string? _editingCredId;
    private string? _editingCustomIconPath;
    private List<AttachmentItem> _editingAttachments = new();

    public string SelectedPartition { get; set; } = "ALL";
    public event Action<string, string>? ToastRequested;

    private static readonly Dictionary<CredentialCategory, (string Key, string Label)[]> _categoryBrandPresets = new()
    {
        [CredentialCategory.Finance] = new[]
        {
            ("Auto", "Otomatis (Berdasarkan Judul / Bank)"),
            ("BCA", "Bank Central Asia (BCA)"),
            ("Mandiri", "Bank Mandiri"),
            ("BNI", "Bank Negara Indonesia (BNI)"),
            ("BRI", "Bank Rakyat Indonesia (BRI)"),
            ("BSI", "Bank Syariah Indonesia (BSI)"),
            ("CIMB", "CIMB Niaga"),
            ("BankJago", "Bank Jago"),
            ("Jenius", "Jenius (BTPN)"),
            ("Permata", "Permata Bank"),
            ("Visa", "Visa Card"),
            ("Mastercard", "Mastercard"),
            ("PayPal", "PayPal"),
            ("Stripe", "Stripe"),
            ("Wise", "Wise"),
            ("GoPay", "GoPay"),
            ("OVO", "OVO"),
            ("Dana", "DANA"),
            ("ShopeePay", "ShopeePay"),
            ("CreditCard", "Kartu Kredit (Generic)"),
            ("University", "Rekening Bank (Generic)")
        },
        [CredentialCategory.Identity] = new[]
        {
            ("Auto", "Otomatis (Berdasarkan Jenis Dokumen)"),
            ("IdCard", "KTP / Kartu Identitas"),
            ("Passport", "Paspor Republik Indonesia"),
            ("DrivingLicense", "Surat Izin Mengemudi (SIM)"),
            ("Tax", "NPWP / Dokumen Pajak"),
            ("Hospital", "BPJS Kesehatan / Ketenagakerjaan"),
            ("Certificate", "Ijazah / Sertifikat Keahlian"),
            ("Family", "Kartu Keluarga (KK)"),
            ("ShieldHalved", "Dokumen Rahasia Negara")
        },
        [CredentialCategory.DeveloperServer] = new[]
        {
            ("Auto", "Otomatis (Berdasarkan Host / Layanan / DB)"),
            ("AWS", "Amazon Web Services (AWS)"),
            ("GoogleCloud", "Google Cloud Platform (GCP)"),
            ("Azure", "Microsoft Azure"),
            ("GitHub", "GitHub"),
            ("GitLab", "GitLab"),
            ("Bitbucket", "Bitbucket"),
            ("Docker", "Docker"),
            ("Kubernetes", "Kubernetes (K8s)"),
            ("PostgreSQL", "PostgreSQL"),
            ("MySQL", "MySQL"),
            ("Redis", "Redis"),
            ("MongoDB", "MongoDB"),
            ("SQLite", "SQLite"),
            ("Ubuntu", "Ubuntu Linux"),
            ("Debian", "Debian Linux"),
            ("Linux", "Linux (Generic)"),
            ("Cloudflare", "Cloudflare"),
            ("DigitalOcean", "DigitalOcean"),
            ("Vercel", "Vercel"),
            ("Supabase", "Supabase"),
            ("Firebase", "Google Firebase"),
            ("NGINX", "NGINX"),
            ("Server", "Server Host (Generic)"),
            ("Database", "Database Cluster (Generic)"),
            ("Terminal", "SSH / Terminal Console")
        },
        [CredentialCategory.CryptoSeed] = new[]
        {
            ("Auto", "Otomatis (Berdasarkan Jaringan / Dompet)"),
            ("Bitcoin", "Bitcoin (BTC)"),
            ("Ethereum", "Ethereum (ETH)"),
            ("Solana", "Solana (SOL)"),
            ("Binance", "Binance (BNB)"),
            ("Polygon", "Polygon (MATIC)"),
            ("Tether", "Tether (USDT)"),
            ("MetaMask", "MetaMask Wallet"),
            ("Phantom", "Phantom Wallet"),
            ("Ledger", "Ledger Hardware Wallet"),
            ("Trezor", "Trezor Hardware Wallet"),
            ("Coinbase", "Coinbase"),
            ("Kraken", "Kraken Exchange"),
            ("Bybit", "Bybit"),
            ("Uniswap", "Uniswap DEX"),
            ("Cardano", "Cardano (ADA)"),
            ("Ripple", "Ripple (XRP)"),
            ("Dogecoin", "Dogecoin (DOGE)")
        },
        [CredentialCategory.SecureNote] = new[]
        {
            ("Auto", "Otomatis (Berdasarkan Judul / Software)"),
            ("Windows", "Microsoft Windows (OS)"),
            ("Microsoft", "Microsoft 365 / Office"),
            ("Adobe", "Adobe Creative Cloud"),
            ("JetBrains", "JetBrains (IntelliJ / Rider)"),
            ("Figma", "Figma Design"),
            ("Notion", "Notion Workspace"),
            ("Apple", "Apple Software / macOS"),
            ("Bitwarden", "Bitwarden Vault"),
            ("Autodesk", "Autodesk / AutoCAD"),
            ("Key", "Serial Key / Lisensi (Generic)"),
            ("FileLines", "Catatan Rahasia (Generic)"),
            ("Lock", "Arsip Terkunci (Generic)")
        },
        [CredentialCategory.Login] = new[]
        {
            ("Auto", "Otomatis (Berdasarkan Judul / Website)"),
            ("Google", "Google Workspace / Gmail"),
            ("YouTube", "YouTube"),
            ("Microsoft", "Microsoft Account"),
            ("Apple", "Apple ID / iCloud"),
            ("GitHub", "GitHub"),
            ("Discord", "Discord"),
            ("Steam", "Steam"),
            ("EpicGames", "Epic Games"),
            ("RiotGames", "Riot Games"),
            ("Valorant", "Valorant"),
            ("PlayStation", "PlayStation Network"),
            ("Xbox", "Xbox Live"),
            ("Netflix", "Netflix"),
            ("Spotify", "Spotify"),
            ("X", "X (Twitter)"),
            ("Instagram", "Instagram"),
            ("TikTok", "TikTok"),
            ("OpenAI", "OpenAI (ChatGPT)"),
            ("Telegram", "Telegram"),
            ("Reddit", "Reddit")
        }
    };

    private void UpdateBrandPresetsForCategory(CredentialCategory category, string? currentIconKey = null)
    {
        if (CmbEditCredIconPreset == null) return;

        CredentialCategory normalizedCat = category switch
        {
            CredentialCategory.PaymentCard or CredentialCategory.BankAccount => CredentialCategory.Finance,
            CredentialCategory.SoftwareLicense => CredentialCategory.SecureNote,
            CredentialCategory.SshToken or CredentialCategory.ServerDatabase => CredentialCategory.DeveloperServer,
            CredentialCategory.Password => CredentialCategory.Login,
            _ => category
        };

        if (!_categoryBrandPresets.TryGetValue(normalizedCat, out var presets))
        {
            presets = _categoryBrandPresets[CredentialCategory.Login];
        }

        var brandList = presets.Select(p => new BrandPresetOption(p.Key, p.Label)).ToList();
        CmbEditCredIconPreset.ItemsSource = brandList;
        CmbEditCredIconPreset.DisplayMemberPath = nameof(BrandPresetOption.Label);
        CmbEditCredIconPreset.SelectedValuePath = nameof(BrandPresetOption.Key);

        if (!string.IsNullOrWhiteSpace(currentIconKey) && brandList.Any(x => x.Key.Equals(currentIconKey, StringComparison.OrdinalIgnoreCase)))
        {
            CmbEditCredIconPreset.SelectedValue = currentIconKey;
        }
        else
        {
            CmbEditCredIconPreset.SelectedValue = "Auto";
        }
    }

    private SettingsManager? _settingsManager;
    private Func<string>? _getPassword;
    private SshAgentService _sshAgentService;

    public CredentialView()
    {
        InitializeComponent();
        _credentialService = new CredentialVaultService();
        _passwordGenerator = new PasswordGeneratorService();
        _totpService = new TotpService();
        _sshAgentService = new SshAgentService();

        Loaded += CredentialView_Loaded;
        Unloaded += CredentialView_Unloaded;
    }

    public void InitializeServices(
        CredentialVaultService credentialService,
        SettingsManager settingsManager,
        PasswordGeneratorService passwordGenerator,
        TotpService totpService,
        Func<string> getPassword,
        SshAgentService? sshAgentService = null)
    {
        _credentialService = credentialService;
        _settingsManager = settingsManager;
        _passwordGenerator = passwordGenerator;
        _totpService = totpService;
        _getPassword = getPassword;
        if (sshAgentService != null) _sshAgentService = sshAgentService;

        ClipboardService.ClipboardCleared -= OnClipboardCleared;
        ClipboardService.ClipboardCleared += OnClipboardCleared;
    }

    private void OnClipboardCleared(string label)
    {
        Dispatcher.Invoke(() =>
        {
            ToastRequested?.Invoke("Clipboard Dibersihkan", $"{label} telah otomatis dibersihkan dari clipboard.");
        });
    }

    private void CredentialView_Loaded(object sender, RoutedEventArgs e)
    {
        InitCredentialsUi();
        InitTotpTimer();
        LoadCredentials();
    }

    private void CredentialView_Unloaded(object sender, RoutedEventArgs e)
    {
        _totpRefreshTimer?.Stop();
    }

    public void Dispose()
    {
        _totpRefreshTimer?.Stop();
    }

    public void Refresh()
    {
        LoadCredentials();
    }

    public void SetSearchQuery(string query)
    {
        if (TxtCredSearch != null)
        {
            TxtCredSearch.Text = query;
        }
    }

    private void InitCredentialsUi()
    {
        // Category options in Editor
        var catList = new System.Collections.Generic.List<CredentialCategoryOption>();
        foreach (CredentialCategory cat in Enum.GetValues<CredentialCategory>())
        {
            if (cat is CredentialCategory.All or CredentialCategory.Favorite or CredentialCategory.Trash) continue;
            if ((int)cat >= 100) continue; // Skip legacy obsolete values
            catList.Add(new CredentialCategoryOption(cat, new CredentialItem { Category = cat }.CategoryDisplayName));
        }
        CmbEditCredCategory.ItemsSource = catList;
        CmbEditCredCategory.DisplayMemberPath = nameof(CredentialCategoryOption.DisplayName);
        CmbEditCredCategory.SelectedValuePath = nameof(CredentialCategoryOption.Category);
        if (catList.Count > 0) CmbEditCredCategory.SelectedValue = CredentialCategory.Login;

        // Brand icon options in Editor
        UpdateBrandPresetsForCategory(CredentialCategory.Login, "Auto");

        // Partition options in Editor
        var partList = new System.Collections.Generic.List<PartitionOption>
        {
            new("D:\\ Data & Games"),
            new("E:\\ Koleksi"),
            new("C:\\ Windows & System")
        };
        CmbEditCredPartition.ItemsSource = partList;
        CmbEditCredPartition.DisplayMemberPath = nameof(PartitionOption.Name);
        CmbEditCredPartition.SelectedValuePath = nameof(PartitionOption.Name);
        CmbEditCredPartition.SelectedValue = "D:\\ Data & Games";

        RegeneratePasswordPreview();
    }

    private void InitTotpTimer()
    {
        _totpRefreshTimer?.Stop();
        _totpRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _totpRefreshTimer.Tick += (s, e) => UpdateTotpDisplay();
        _totpRefreshTimer.Start();
    }

    private void UpdateTotpDisplay()
    {
        if (_selectedCredential != null && _selectedCredential.HasTotp)
        {
            int remaining = _totpService.GetRemainingSeconds();
            TxtDetailTotpTimer.Text = $"{remaining}s";
            string code = _totpService.GenerateCode(_selectedCredential.TotpSecret);
            TxtDetailTotpCode.Text = _totpService.FormatCodeWithSpace(code);
        }
    }

    public void LoadCredentials()
    {
        if (TxtCatCountAll == null) return;

        // Update Category Counts
        TxtCatCountAll.Text = _credentialService.GetCountByCategory(CredentialCategory.All).ToString();
        TxtCatCountFav.Text = _credentialService.GetCountByCategory(CredentialCategory.Favorite).ToString();
        TxtCatCountLogin.Text = _credentialService.GetCountByCategory(CredentialCategory.Login).ToString();
        TxtCatCountFinance.Text = _credentialService.GetCountByCategory(CredentialCategory.Finance).ToString();
        TxtCatCountIdentity.Text = _credentialService.GetCountByCategory(CredentialCategory.Identity).ToString();
        TxtCatCountNote.Text = _credentialService.GetCountByCategory(CredentialCategory.SecureNote).ToString();
        TxtCatCountDevServer.Text = _credentialService.GetCountByCategory(CredentialCategory.DeveloperServer).ToString();
        TxtCatCountCrypto.Text = _credentialService.GetCountByCategory(CredentialCategory.CryptoSeed).ToString();
        TxtCatCountTrash.Text = _credentialService.GetCountByCategory(CredentialCategory.Trash).ToString();

        // Query filtered items
        var items = _credentialService.Filter(_selectedCredCategory, TxtCredSearch?.Text, _selectedCredTag, SelectedPartition).ToList();

        if (!_isCredSortAsc)
        {
            items = items.OrderByDescending(x => x.Title).ToList();
        }

        TxtCredTotalCount.Text = items.Count.ToString();
        LstCredentials.ItemsSource = items;

        if (items.Count > 0)
        {
            var target = (_selectedCredential != null)
                ? items.FirstOrDefault(x => x.Id == _selectedCredential.Id) ?? items[0]
                : items[0];

            LstCredentials.SelectedItem = target;
            DisplayCredentialDetail(target);
        }
        else
        {
            DisplayCredentialDetail(null);
        }

        UpdateAddNewButtonState();
    }

    public void UpdateAddNewButtonState()
    {
        if (BtnAddNewCred == null || TxtAddNewCredBtn == null) return;

        if (_selectedCredCategory == CredentialCategory.Trash)
        {
            BtnAddNewCred.Visibility = Visibility.Collapsed;
            return;
        }

        BtnAddNewCred.Visibility = Visibility.Visible;

        if (_selectedCredTag != null)
        {
            TxtAddNewCredBtn.Text = "Item Baru";
            BtnAddNewCred.ToolTip = $"Tambah Item Baru dengan tag {_selectedCredTag}";
            return;
        }

        var (label, tooltip) = _selectedCredCategory switch
        {
            CredentialCategory.All => ("Item Baru", "Tambah Item Kredensial Baru"),
            CredentialCategory.Favorite => ("Favorit Baru", "Tambah Item Favorit Baru"),
            CredentialCategory.Login or CredentialCategory.Password => ("Akun Baru", "Tambah Akun & Login Baru"),
            CredentialCategory.Finance or CredentialCategory.PaymentCard or CredentialCategory.BankAccount => ("Keuangan Baru", "Tambah Kartu / Rekening Baru"),
            CredentialCategory.Identity => ("Identitas Baru", "Tambah Dokumen & Identitas Baru"),
            CredentialCategory.SecureNote or CredentialCategory.SoftwareLicense => ("Catatan Baru", "Tambah Catatan Rahasia / Lisensi Baru"),
            CredentialCategory.DeveloperServer or CredentialCategory.SshToken or CredentialCategory.ServerDatabase => ("Server / Token Baru", "Tambah SSH Token / Server / Database"),
            CredentialCategory.CryptoSeed => ("Kripto Baru", "Tambah Kunci Kripto & Seed Baru"),
            _ => ("Item Baru", "Tambah Item Baru")
        };

        TxtAddNewCredBtn.Text = label;
        BtnAddNewCred.ToolTip = tooltip;
    }

    private void LstCredentials_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstCredentials.SelectedItem is CredentialItem item)
        {
            DisplayCredentialDetail(item);
        }
        else if (LstCredentials.Items.Count == 0)
        {
            DisplayCredentialDetail(null);
        }
    }

    public void UpdateDetailFavoriteState(CredentialItem? item)
    {
        if (IconDetailFav == null || BtnDetailFavorite == null) return;

        bool isFav = item?.IsFavorite == true;
        if (isFav)
        {
            IconDetailFav.Fill = (Brush)new BrushConverter().ConvertFromString("#FBBF24")!;
            var starSolid = Converters.CredentialToIconGeometryConverter.GetFontAwesomeGeometry(MahApps.Metro.IconPacks.PackIconFontAwesomeKind.StarSolid);
            IconDetailFav.Data = starSolid ?? (Geometry)FindResource("IconStar");
            BtnDetailFavorite.ToolTip = "Hapus dari Favorit";
        }
        else
        {
            IconDetailFav.Fill = (Brush)FindResource("BrushTextFaint");
            var starReg = Converters.CredentialToIconGeometryConverter.GetFontAwesomeGeometry(MahApps.Metro.IconPacks.PackIconFontAwesomeKind.StarRegular);
            IconDetailFav.Data = starReg ?? (Geometry)FindResource("IconStarOutline");
            BtnDetailFavorite.ToolTip = "Tandai Favorit";
        }
    }

    public void DisplayCredentialDetail(CredentialItem? item)
    {
        if (item == null)
        {
            _selectedCredential = null;
            CredDetailEmptyState.Visibility = Visibility.Visible;
            CredDetailActiveState.Visibility = Visibility.Collapsed;
            return;
        }

        _selectedCredential = item;
        CredDetailEmptyState.Visibility = Visibility.Collapsed;
        CredDetailActiveState.Visibility = Visibility.Visible;

        TxtDetailTitle.Text = item.Title;
        TxtDetailCategory.Text = item.CategoryDisplayName;
        TxtDetailUsername.Text = string.IsNullOrEmpty(item.Username) ? "-" : item.Username;

        switch (item.Category)
        {
            case CredentialCategory.Finance:
            case CredentialCategory.PaymentCard:
            case CredentialCategory.BankAccount:
                TxtDetailUsernameHeader.Text = "NOMOR KARTU / REKENING";
                TxtBtnCopyUsername.Text = "Salin No.";
                TxtDetailPasswordHeader.Text = "PIN / EXPIRY & CVV";
                TxtBtnCopyPassword.Text = "Salin PIN/CVV";
                TxtDetailTotpHeader.Text = "KODE CVV / CVC";
                TxtBtnCopyTotp.Text = "Salin CVV";
                TxtDetailUrlHeader.Text = "INTERNET BANKING / PORTAL";
                TxtDetailNotesHeader.Text = "CATATAN KEUANGAN";
                break;

            case CredentialCategory.Identity:
                TxtDetailUsernameHeader.Text = "NOMOR IDENTITAS / NIK / PASPOR";
                TxtBtnCopyUsername.Text = "Salin NIK";
                TxtDetailPasswordHeader.Text = "MASA BERLAKU / EXPIRY DATE";
                TxtBtnCopyPassword.Text = "Salin Info";
                TxtDetailNotesHeader.Text = "DATA IDENTITAS LENGKAP";
                break;

            case CredentialCategory.DeveloperServer:
            case CredentialCategory.SshToken:
            case CredentialCategory.ServerDatabase:
                TxtDetailUsernameHeader.Text = "HOST / USERNAME / KEY ID";
                TxtBtnCopyUsername.Text = "Salin Host/User";
                TxtDetailPasswordHeader.Text = "TOKEN / SECRET KEY / SANDI";
                TxtBtnCopyPassword.Text = "Salin Secret";
                TxtDetailTotpHeader.Text = "PORT / 2FA AUTHENTICATOR";
                TxtBtnCopyTotp.Text = "Salin Port";
                TxtDetailUrlHeader.Text = "ENDPOINT / REPOSITORY URL";
                TxtDetailNotesHeader.Text = "KONFIGURASI SERVER & ENV";
                break;

            case CredentialCategory.CryptoSeed:
                TxtDetailUsernameHeader.Text = "ALAMAT DOMPET PUBLIK";
                TxtBtnCopyUsername.Text = "Salin Alamat";
                TxtDetailPasswordHeader.Text = "MNEMONIC SEED PHRASE / KEY";
                TxtBtnCopyPassword.Text = "Salin Seed";
                TxtDetailUrlHeader.Text = "BLOCKCHAIN EXPLORER URL";
                TxtDetailNotesHeader.Text = "DERIVATION PATH & CATATAN";
                break;

            case CredentialCategory.SecureNote:
            case CredentialCategory.SoftwareLicense:
                TxtDetailUsernameHeader.Text = "ATAS NAMA / PEMEGANG LISENSI";
                TxtBtnCopyUsername.Text = "Salin";
                TxtDetailPasswordHeader.Text = "PRODUCT KEY / SERIAL NUMBER";
                TxtBtnCopyPassword.Text = "Salin Lisensi";
                TxtDetailNotesHeader.Text = "CATATAN RAHASIA TERENKRIPSI";
                break;

            default:
                TxtDetailUsernameHeader.Text = "NAMA PENGGUNA";
                TxtBtnCopyUsername.Text = "Salin";
                TxtDetailPasswordHeader.Text = "KATA SANDI";
                TxtBtnCopyPassword.Text = "Salin Sandi";
                TxtDetailTotpHeader.Text = "KODE 2FA / TOTP";
                TxtBtnCopyTotp.Text = "Salin Kode";
                TxtDetailUrlHeader.Text = "LOGIN URL / CLIENT";
                TxtDetailNotesHeader.Text = "CATATAN TERENKRIPSI";
                break;
        }

        _isDetailPasswordMasked = true;
        TxtDetailPasswordMasked.Visibility = Visibility.Visible;
        TxtDetailPasswordPlain.Visibility = Visibility.Collapsed;
        TxtDetailPasswordPlain.Text = item.Password;
        IconDetailEye.Data = (Geometry)FindResource("IconEye");

        if (!string.IsNullOrEmpty(item.Url))
        {
            RowDetailUrl.Visibility = Visibility.Visible;
            TxtDetailUrlText.Text = item.Url;
            BtnDetailUrlLink.Content = item.Url;
            BtnDetailUrlLink.Visibility = Visibility.Visible;
        }
        else
        {
            RowDetailUrl.Visibility = Visibility.Collapsed;
            BtnDetailUrlLink.Visibility = Visibility.Collapsed;
        }

        if (item.HasTotp)
        {
            RowDetailTotp.Visibility = Visibility.Visible;
            UpdateTotpDisplay();
        }
        else
        {
            RowDetailTotp.Visibility = Visibility.Collapsed;
        }

        TxtDetailPartition.Text = string.IsNullOrEmpty(item.VaultPartition) ? "D:\\ Data & Games" : item.VaultPartition;

        if (!string.IsNullOrEmpty(item.Notes))
        {
            CardDetailNotes.Visibility = Visibility.Visible;
            TxtDetailNotes.Text = item.Notes;
        }
        else
        {
            CardDetailNotes.Visibility = Visibility.Collapsed;
        }

        UpdateDetailFavoriteState(item);
        UpdateDetailBrandIcon(item);

        bool isDev = item?.Category is CredentialCategory.DeveloperServer or CredentialCategory.SshToken or CredentialCategory.ServerDatabase;
        if (RowDetailSshPrompt != null)
        {
            RowDetailSshPrompt.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;
        }

        if (item != null)
        {
            TxtDetailUpdatedAt.Text = $"Terakhir diperbarui: {item.UpdatedAt:dd MMM yyyy, HH:mm} WIB";
            TxtDetailId.Text = $"ID: {item.Id[..Math.Min(12, item.Id.Length)]}";
        }
        else
        {
            TxtDetailUpdatedAt.Text = string.Empty;
            TxtDetailId.Text = string.Empty;
        }

        UpdateDetailAttachments(item);
    }

    private string GetMasterPassword()
    {
        string? p = _getPassword?.Invoke();
        return string.IsNullOrEmpty(p) ? "WhatInsidesVaultSecret" : p;
    }

    private void UpdateDetailAttachments(CredentialItem? item)
    {
        if (CardDetailAttachments == null || LstDetailAttachments == null || BorderAttachmentEmpty == null || TxtDetailAttachmentsCount == null) return;

        if (item != null && item.Attachments.Count > 0)
        {
            TxtDetailAttachmentsCount.Text = $"{item.Attachments.Count} Berkas";
            BorderAttachmentEmpty.Visibility = Visibility.Collapsed;
            LstDetailAttachments.Visibility = Visibility.Visible;
            LstDetailAttachments.ItemsSource = null;
            LstDetailAttachments.ItemsSource = item.Attachments;
        }
        else
        {
            TxtDetailAttachmentsCount.Text = "0 Berkas";
            BorderAttachmentEmpty.Visibility = Visibility.Visible;
            LstDetailAttachments.Visibility = Visibility.Collapsed;
            LstDetailAttachments.ItemsSource = null;
        }
    }

    private async void BtnAddAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;

        var ofd = new OpenFileDialog
        {
            Title = "Pilih Berkas Lampiran untuk Diankripsi",
            Filter = "Semua Berkas (*.*)|*.*|Dokumen (*.pdf;*.docx;*.txt)|*.pdf;*.docx;*.txt|Kunci & Sertifikat (*.key;*.pem;*.pub;*.crt)|*.key;*.pem;*.pub;*.crt|Gambar (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            string masterPassword = GetMasterPassword();
            int successCount = 0;

            foreach (var path in ofd.FileNames)
            {
                try
                {
                    await _credentialService.AddAttachmentAsync(_selectedCredential.Id, path, masterPassword);
                    successCount++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal melampirkan '{Path.GetFileName(path)}': {ex.Message}", "Error Enkripsi Lampiran", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (successCount > 0)
            {
                UpdateDetailAttachments(_selectedCredential);
                LoadCredentials();
                ToastRequested?.Invoke("Lampiran Ditambahkan", $"{successCount} berkas berhasil dienkripsi ke brankas.");
            }
        }
    }

    private void CardDetailAttachments_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void CardDetailAttachments_Drop(object sender, DragEventArgs e)
    {
        if (_selectedCredential == null) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string masterPassword = GetMasterPassword();
                int count = 0;
                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        try
                        {
                            await _credentialService.AddAttachmentAsync(_selectedCredential.Id, file, masterPassword);
                            count++;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Gagal melampirkan '{Path.GetFileName(file)}': {ex.Message}", "Error Lampiran", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                if (count > 0)
                {
                    UpdateDetailAttachments(_selectedCredential);
                    LoadCredentials();
                    ToastRequested?.Invoke("Lampiran Ditambahkan", $"{count} berkas berhasil dienkripsi ke brankas.");
                }
            }
        }
    }

    private async void BtnPreviewAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;
        if (sender is Button btn && btn.Tag is AttachmentItem attachment)
        {
            try
            {
                string masterPassword = GetMasterPassword();
                string tempPath = await _credentialService.DecryptAttachmentToTempAsync(_selectedCredential.Id, attachment.Id, masterPassword);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });

                ToastRequested?.Invoke("Membuka Pratinjau", $"{attachment.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal membuka pratinjau: {ex.Message}", "Error Pratinjau", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnExportAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;
        if (sender is Button btn && btn.Tag is AttachmentItem attachment)
        {
            string ext = Path.GetExtension(attachment.FileName);
            var sfd = new SaveFileDialog
            {
                Title = "Simpan Berkas Lampiran",
                FileName = attachment.FileName,
                Filter = $"Format Asli (*{ext})|*{ext}|Semua Berkas (*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string masterPassword = GetMasterPassword();
                    await _credentialService.ExportAttachmentAsync(_selectedCredential.Id, attachment.Id, sfd.FileName, masterPassword);
                    ToastRequested?.Invoke("Berkas Diekspor", $"Tersimpan di: {Path.GetFileName(sfd.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal mengekspor berkas: {ex.Message}", "Error Ekspor", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void BtnDeleteAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;
        if (sender is Button btn && btn.Tag is AttachmentItem attachment)
        {
            var res = MessageBox.Show(
                $"Hapus lampiran terenkripsi '{attachment.FileName}' dari akun ini?",
                "Konfirmasi Hapus Lampiran",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _credentialService.DeleteAttachment(_selectedCredential.Id, attachment.Id);
                UpdateDetailAttachments(_selectedCredential);
                LoadCredentials();
                ToastRequested?.Invoke("Lampiran Dihapus", $"'{attachment.FileName}' telah dihapus.");
            }
        }
    }

    private async void BtnTestSshPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;
        await _sshAgentService.SimulateAccessRequestAsync(_selectedCredential, "Windows Terminal");
    }

    private void UpdateDetailBrandIcon(CredentialItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.CustomIconPath) && File.Exists(item.CustomIconPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(item.CustomIconPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                ImgDetailCustomIcon.Source = bmp;
                ImgDetailCustomIcon.Visibility = Visibility.Visible;
                IconDetailService.Visibility = Visibility.Collapsed;
                return;
            }
            catch { }
        }

        ImgDetailCustomIcon.Visibility = Visibility.Collapsed;
        IconDetailService.Visibility = Visibility.Visible;

        string brand = item.GetEffectiveBrandKey();

        if (brand.Equals("AWS", StringComparison.OrdinalIgnoreCase) || brand.Equals("Amazon", StringComparison.OrdinalIgnoreCase))
        {
            string k = brand.Equals("AWS", StringComparison.OrdinalIgnoreCase) ? "BrandAWS" : "BrandAmazon";
            if (TryFindResource(k) is Geometry dedicatedGeo && dedicatedGeo != Geometry.Empty)
            {
                IconDetailService.Data = dedicatedGeo;
                IconDetailService.Fill = (Brush)new BrushConverter().ConvertFromString("#FF9900")!;
                return;
            }
        }

        var siGeo = Converters.CredentialToIconGeometryConverter.TryGetSimpleIconGeometry(brand);
        var faGeo = Converters.CredentialToIconGeometryConverter.TryGetFontAwesomeGeometry(brand);

        if (siGeo != null && siGeo != Geometry.Empty)
        {
            IconDetailService.Data = siGeo;
            IconDetailService.Fill = Converters.CredentialToIconBrushConverter.GetBrandBrush(brand);
        }
        else if (faGeo != null && faGeo != Geometry.Empty)
        {
            IconDetailService.Data = faGeo;
            IconDetailService.Fill = Converters.CredentialToIconBrushConverter.GetBrandBrush(brand);
        }
        else if (!string.IsNullOrEmpty(brand) && TryFindResource("Brand" + brand) is Geometry brandGeo)
        {
            IconDetailService.Data = brandGeo;
            IconDetailService.Fill = Converters.CredentialToIconBrushConverter.GetBrandBrush(brand);
        }
        else
        {
            IconDetailService.Data = (Geometry)new Converters.CredentialCategoryToGeometryConverter().Convert(item.Category, typeof(Geometry), null!, System.Globalization.CultureInfo.InvariantCulture);
            IconDetailService.Fill = (Brush)new Converters.CredentialCategoryToColorConverter().Convert(item.Category, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void BtnCredCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && Enum.TryParse<CredentialCategory>(tagStr, out var cat))
        {
            _selectedCredCategory = cat;
            _selectedCredTag = null;

            Button[] catButtons = {
                BtnCredCatAll, BtnCredCatFav, BtnCredCatLogin, BtnCredCatFinance,
                BtnCredCatIdentity, BtnCredCatNote, BtnCredCatDevServer,
                BtnCredCatCrypto, BtnCredCatTrash
            };

            foreach (var b in catButtons)
            {
                b.Style = b == btn ? (Style)FindResource("SidebarNavActive") : (Style)FindResource("SidebarNavInactive");
            }

            TxtCredViewTitle.Text = cat switch
            {
                CredentialCategory.All => "Semua Item",
                CredentialCategory.Favorite => "Item Favorit",
                CredentialCategory.Trash => "Tempat Sampah",
                _ => new CredentialItem { Category = cat }.CategoryDisplayName
            };

            LoadCredentials();
        }
    }

    private void BorderActiveVaultSelector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (BorderActiveVaultSelector.ContextMenu != null)
        {
            BorderActiveVaultSelector.ContextMenu.PlacementTarget = BorderActiveVaultSelector;
            BorderActiveVaultSelector.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            BorderActiveVaultSelector.ContextMenu.IsOpen = true;
        }
    }

    private void MenuItemVaultPartition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tag)
        {
            SelectedPartition = tag;
            TxtCredActiveVault.Text = tag == "ALL" ? "Semua Brankas" : tag;
            LoadCredentials();
        }
    }

    private void BtnCredTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _selectedCredTag = _selectedCredTag == tag ? null : tag;
            TxtCredViewTitle.Text = _selectedCredTag != null ? $"Tag: {_selectedCredTag}" : "Semua Item";
            LoadCredentials();
        }
    }

    private void TxtCredSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        LoadCredentials();
    }

    private void BtnCredSort_Click(object sender, RoutedEventArgs e)
    {
        _isCredSortAsc = !_isCredSortAsc;
        LoadCredentials();
    }

    private void BtnAddNewCred_Click(object sender, RoutedEventArgs e)
    {
        _isEditingCred = false;
        _editingCredId = null;
        _editingCustomIconPath = null;

        CredentialCategory targetCategory;
        string modalTitle;

        if (_selectedCredCategory is CredentialCategory.All or CredentialCategory.Trash)
        {
            targetCategory = CredentialCategory.Login;
            modalTitle = "Item Baru";
        }
        else if (_selectedCredCategory == CredentialCategory.Favorite)
        {
            targetCategory = CredentialCategory.Login;
            modalTitle = "Item Favorit Baru";
        }
        else
        {
            targetCategory = _selectedCredCategory;
            modalTitle = $"{new CredentialItem { Category = targetCategory }.CategoryDisplayName} Baru";
        }

        TxtCredEditorModalTitle.Text = modalTitle;

        TxtEditCredTitle.Text = string.Empty;
        TxtEditCredUsername.Text = string.Empty;
        TxtEditCredPassword.Text = string.Empty;
        TxtEditCredTotp.Text = string.Empty;
        TxtEditCredUrl.Text = string.Empty;
        TxtEditCredTags.Text = _selectedCredTag ?? string.Empty;
        TxtEditCredNotes.Text = string.Empty;

        CmbEditCredCategory.SelectedValue = targetCategory;
        UpdateFormFieldsForCategory(targetCategory);
        UpdateBrandPresetsForCategory(targetCategory, "Auto");
        CmbEditCredPartition.SelectedValue = (SelectedPartition != null && SelectedPartition != "ALL") 
            ? SelectedPartition 
            : "D:\\ Data & Games";
        UpdateEditIconPreview();

        RegeneratePasswordPreview();
        BorderPassGenerator.Visibility = Visibility.Collapsed;
        BtnTogglePassGen.Content = "Buka Generator Sandi";

        _editingAttachments = new List<AttachmentItem>();
        UpdateEditAttachmentsDisplay();

        OverlayCredentialEditor.Visibility = Visibility.Visible;
        TxtEditCredTitle.Focus();
    }

    private void BtnDetailEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;

        _isEditingCred = true;
        _editingCredId = _selectedCredential.Id;
        _editingCustomIconPath = _selectedCredential.CustomIconPath;
        TxtCredEditorModalTitle.Text = "Edit Kredensial";

        TxtEditCredTitle.Text = _selectedCredential.Title;
        TxtEditCredUsername.Text = _selectedCredential.Username;
        TxtEditCredPassword.Text = _selectedCredential.Password;
        TxtEditCredTotp.Text = _selectedCredential.TotpSecret ?? string.Empty;
        TxtEditCredUrl.Text = _selectedCredential.Url ?? string.Empty;
        TxtEditCredTags.Text = string.Join(", ", _selectedCredential.Tags);
        TxtEditCredNotes.Text = _selectedCredential.Notes ?? string.Empty;

        CmbEditCredCategory.SelectedValue = _selectedCredential.Category;
        UpdateFormFieldsForCategory(_selectedCredential.Category);
        string targetIconKey = string.IsNullOrWhiteSpace(_selectedCredential.IconKey) ? "Auto" : _selectedCredential.IconKey;
        UpdateBrandPresetsForCategory(_selectedCredential.Category, targetIconKey);
        CmbEditCredPartition.SelectedValue = _selectedCredential.VaultPartition ?? "D:\\ Data & Games";
        UpdateEditIconPreview();

        BorderPassGenerator.Visibility = Visibility.Collapsed;
        BtnTogglePassGen.Content = "Buka Generator Sandi";

        _editingAttachments = _selectedCredential.Attachments != null
            ? new List<AttachmentItem>(_selectedCredential.Attachments)
            : new List<AttachmentItem>();
        UpdateEditAttachmentsDisplay();

        OverlayCredentialEditor.Visibility = Visibility.Visible;
    }

    private void BtnDetailFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;

        string targetId = _selectedCredential.Id;
        _credentialService.ToggleFavorite(targetId);

        // Immediate visual update of detail button
        UpdateDetailFavoriteState(_selectedCredential);

        // Refresh list and category counts
        LoadCredentials();

        // Ensure detail view reflects updated state
        if (_selectedCredential != null)
        {
            DisplayCredentialDetail(_selectedCredential);
            ToastRequested?.Invoke(
                _selectedCredential.IsFavorite ? "Ditambahkan ke Favorit" : "Dihapus dari Favorit",
                $"'{_selectedCredential.Title}'");
        }
    }

    private void BtnRowStarFav_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button btn && btn.Tag is CredentialItem item)
        {
            _credentialService.ToggleFavorite(item.Id);
            LoadCredentials();
            if (_selectedCredential?.Id == item.Id)
            {
                DisplayCredentialDetail(_selectedCredential);
            }
            ToastRequested?.Invoke(
                item.IsFavorite ? "Ditambahkan ke Favorit" : "Dihapus dari Favorit",
                $"'{item.Title}'");
        }
    }

    private void BtnDetailDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential == null) return;

        if (_selectedCredential.IsDeleted)
        {
            var res = MessageBox.Show($"Hapus '{_selectedCredential.Title}' secara permanen?", "Konfirmasi Hapus", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                _credentialService.Delete(_selectedCredential.Id, permanent: true);
                LoadCredentials();
                ToastRequested?.Invoke("Kredensial Dihapus", "Item dihapus secara permanen.");
            }
        }
        else
        {
            _credentialService.Delete(_selectedCredential.Id, permanent: false);
            LoadCredentials();
            ToastRequested?.Invoke("Dipindahkan ke Tempat Sampah", "Item dapat dipulihkan kapan saja.");
        }
    }

    private int ClipboardTimeout => _settingsManager?.Settings.ClipboardAutoClearSeconds ?? 30;

    private void BtnCopyUsername_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtDetailUsername.Text) && TxtDetailUsername.Text != "-")
        {
            ClipboardService.Copy(TxtDetailUsername.Text, "Username", ClipboardTimeout);
            string note = ClipboardTimeout > 0 ? $" (Dibersihkan dalam {ClipboardTimeout}d)" : "";
            ToastRequested?.Invoke("Username Tersalin", $"{TxtDetailUsername.Text}{note}");
        }
    }

    private void BtnCopyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential != null && !string.IsNullOrEmpty(_selectedCredential.Password))
        {
            ClipboardService.Copy(_selectedCredential.Password, "Kata Sandi", ClipboardTimeout);
            string note = ClipboardTimeout > 0 ? $" Otomatis dibersihkan dalam {ClipboardTimeout} detik." : "";
            ToastRequested?.Invoke("Kata Sandi Tersalin", $"Kata sandi disalin ke papan klip aman.{note}");
        }
    }

    private void BtnCopyTotp_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCredential != null && _selectedCredential.HasTotp)
        {
            string raw = _totpService.GenerateCode(_selectedCredential.TotpSecret);
            ClipboardService.Copy(raw, "Kode TOTP", ClipboardTimeout);
            string note = ClipboardTimeout > 0 ? $" (Dibersihkan dalam {ClipboardTimeout}d)" : "";
            ToastRequested?.Invoke("Kode 2FA Tersalin", $"Token {raw} siap digunakan.{note}");
        }
    }

    private void BtnCredMoreActions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void MenuItemExportJson_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Ekspor Brankas Kata Sandi ke JSON",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = $"whatinsides_credentials_{DateTime.Now:yyyyMMdd}.json"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                _credentialService.ExportJson(sfd.FileName);
                ToastRequested?.Invoke("Ekspor Berhasil", $"Data kata sandi diekspor ke: {Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengekspor: {ex.Message}", "Error Ekspor", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Ekspor Brankas Kata Sandi ke CSV",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"whatinsides_credentials_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                _credentialService.ExportCsv(sfd.FileName);
                ToastRequested?.Invoke("Ekspor Berhasil", $"Data kata sandi diekspor ke: {Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengekspor: {ex.Message}", "Error Ekspor", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuItemImport_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Impor Kata Sandi (JSON / CSV / Bitwarden)",
            Filter = "Semua Format yang Didukung (*.json;*.csv)|*.json;*.csv|JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var (count, msg) = _credentialService.ImportJsonOrCsv(ofd.FileName);
                if (count > 0)
                {
                    LoadCredentials();
                    ToastRequested?.Invoke("Impor Berhasil", msg);
                }
                else
                {
                    MessageBox.Show(msg, "Informasi Impor", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengimpor: {ex.Message}", "Error Impor", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnToggleDetailPassword_Click(object sender, RoutedEventArgs e)
    {
        _isDetailPasswordMasked = !_isDetailPasswordMasked;
        if (_isDetailPasswordMasked)
        {
            TxtDetailPasswordMasked.Visibility = Visibility.Visible;
            TxtDetailPasswordPlain.Visibility = Visibility.Collapsed;
            IconDetailEye.Data = (Geometry)FindResource("IconEye");
        }
        else
        {
            TxtDetailPasswordMasked.Visibility = Visibility.Collapsed;
            TxtDetailPasswordPlain.Visibility = Visibility.Visible;
            IconDetailEye.Data = (Geometry)FindResource("IconEyeOff");
        }
    }

    private void BtnOpenDetailUrl_Click(object sender, RoutedEventArgs e)
    {
        string? url = _selectedCredential?.Url;
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore if browser launch fails
            }
        }
    }

    private void BtnTogglePassGen_Click(object sender, RoutedEventArgs e)
    {
        if (BorderPassGenerator.Visibility == Visibility.Visible)
        {
            BorderPassGenerator.Visibility = Visibility.Collapsed;
            BtnTogglePassGen.Content = "Buka Generator Sandi";
        }
        else
        {
            BorderPassGenerator.Visibility = Visibility.Visible;
            BtnTogglePassGen.Content = "Tutup Generator Sandi";
            RegeneratePasswordPreview();
        }
    }

    private void BtnRegenPassword_Click(object sender, RoutedEventArgs e)
    {
        RegeneratePasswordPreview();
    }

    private void BtnApplyPassword_Click(object sender, RoutedEventArgs e)
    {
        TxtEditCredPassword.Text = TxtGeneratedPreview.Text;
        BorderPassGenerator.Visibility = Visibility.Collapsed;
        BtnTogglePassGen.Content = "Buka Generator Sandi";
    }

    private void CmbGenType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SliderLength == null) return;
        if (CmbGenType.SelectedIndex == 0) // Words
        {
            SliderLength.Minimum = 3;
            SliderLength.Maximum = 8;
            SliderLength.Value = 5;
        }
        else if (CmbGenType.SelectedIndex == 1) // Random
        {
            SliderLength.Minimum = 12;
            SliderLength.Maximum = 36;
            SliderLength.Value = 20;
        }
        else // PIN
        {
            SliderLength.Minimum = 4;
            SliderLength.Maximum = 12;
            SliderLength.Value = 6;
        }
        RegeneratePasswordPreview();
    }

    private void SliderLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSliderBadge != null)
        {
            TxtSliderBadge.Text = ((int)SliderLength.Value).ToString();
        }
        RegeneratePasswordPreview();
    }

    private void CmbSeparator_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RegeneratePasswordPreview();
    }

    private void ChkCapitalize_Click(object sender, RoutedEventArgs e)
    {
        RegeneratePasswordPreview();
    }

    private void RegeneratePasswordPreview()
    {
        if (TxtGeneratedPreview == null || _passwordGenerator == null) return;

        int len = (int)(SliderLength?.Value ?? 5);
        int typeIdx = CmbGenType?.SelectedIndex ?? 0;
        string sep = CmbSeparator?.SelectedIndex switch
        {
            1 => ".",
            2 => "_",
            3 => " ",
            _ => "-"
        };
        bool cap = ChkCapitalize?.IsChecked == true;

        string pass = typeIdx switch
        {
            1 => _passwordGenerator.GenerateRandom(len),
            2 => _passwordGenerator.GeneratePin(len),
            _ => _passwordGenerator.GeneratePassphrase(len, sep, cap)
        };

        TxtGeneratedPreview.Text = pass;
        var (score, rating, colorHex) = _passwordGenerator.EvaluateStrength(pass);
        if (PbPassStrength != null)
        {
            PbPassStrength.Value = score;
            try
            {
                PbPassStrength.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
            }
            catch { }
        }
    }

    private void BtnCancelCredEditor_Click(object sender, RoutedEventArgs e)
    {
        OverlayCredentialEditor.Visibility = Visibility.Collapsed;
    }

    private void BtnSaveCredEditor_Click(object sender, RoutedEventArgs e)
    {
        string title = TxtEditCredTitle.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            MessageBox.Show("Silakan masukkan nama layanan atau judul kredensial.", "Validasi Input", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtEditCredTitle.Focus();
            return;
        }

        CredentialCategory cat = CmbEditCredCategory.SelectedValue is CredentialCategory c ? c : CredentialCategory.Login;

        var tags = TxtEditCredTags.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().StartsWith('#') ? t.Trim() : "#" + t.Trim())
            .ToList();

        string partition = CmbEditCredPartition.SelectedValue as string ?? "D:\\ Data & Games";
        string selectedPreset = CmbEditCredIconPreset.SelectedValue as string ?? "Auto";

        if (_isEditingCred && _editingCredId != null)
        {
            var existing = _credentialService.GetById(_editingCredId);
            if (existing != null)
            {
                existing.Title = title;
                existing.Category = cat;
                existing.IconKey = selectedPreset;
                existing.CustomIconPath = _editingCustomIconPath;
                existing.Username = TxtEditCredUsername.Text.Trim();
                existing.Password = TxtEditCredPassword.Text.Trim();
                existing.TotpSecret = string.IsNullOrWhiteSpace(TxtEditCredTotp.Text) ? null : TxtEditCredTotp.Text.Trim();
                existing.Url = string.IsNullOrWhiteSpace(TxtEditCredUrl.Text) ? null : TxtEditCredUrl.Text.Trim();
                existing.VaultPartition = partition;
                existing.Tags = tags;
                existing.Notes = string.IsNullOrWhiteSpace(TxtEditCredNotes.Text) ? null : TxtEditCredNotes.Text.Trim();

                existing.Attachments = _editingAttachments;
                _credentialService.Update(existing);
            }
        }
        else
        {
            var newItem = new CredentialItem
            {
                Title = title,
                Category = cat,
                IconKey = selectedPreset,
                CustomIconPath = _editingCustomIconPath,
                Username = TxtEditCredUsername.Text.Trim(),
                Password = TxtEditCredPassword.Text.Trim(),
                TotpSecret = string.IsNullOrWhiteSpace(TxtEditCredTotp.Text) ? null : TxtEditCredTotp.Text.Trim(),
                Url = string.IsNullOrWhiteSpace(TxtEditCredUrl.Text) ? null : TxtEditCredUrl.Text.Trim(),
                VaultPartition = partition,
                Tags = tags,
                Notes = string.IsNullOrWhiteSpace(TxtEditCredNotes.Text) ? null : TxtEditCredNotes.Text.Trim(),
                Attachments = _editingAttachments,
                IsFavorite = _selectedCredCategory == CredentialCategory.Favorite
            };

            _credentialService.Add(newItem);
        }

        OverlayCredentialEditor.Visibility = Visibility.Collapsed;
        LoadCredentials();
        ToastRequested?.Invoke("Kredensial Disimpan", $"'{title}' tersimpan aman di brankas.");
    }

    private void UpdateEditAttachmentsDisplay()
    {
        if (LstEditAttachments == null || TxtEditAttachmentsCount == null) return;
        TxtEditAttachmentsCount.Text = $"({_editingAttachments.Count})";
        LstEditAttachments.ItemsSource = null;
        LstEditAttachments.ItemsSource = _editingAttachments;
    }

    private async void BtnEditAddAttachment_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Pilih Berkas untuk Dilampirkan",
            Filter = "Semua Berkas (*.*)|*.*|Dokumen (*.pdf;*.docx;*.txt)|*.pdf;*.docx;*.txt|Kunci & Sertifikat (*.key;*.pem;*.pub;*.crt)|*.key;*.pem;*.pub;*.crt|Gambar (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            string masterPassword = GetMasterPassword();

            foreach (var filePath in ofd.FileNames)
            {
                try
                {
                    string attachmentId = Guid.NewGuid().ToString("N");
                    string encFileName = $"{attachmentId}.enc";
                    string encFilePath = Path.Combine(_credentialService.GetAttachmentsDirectory(), encFileName);

                    using (var inStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var outStream = new FileStream(encFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await CryptoEngine.EncryptStreamAsync(inStream, outStream, masterPassword);
                    }

                    var attachment = new AttachmentItem
                    {
                        Id = attachmentId,
                        FileName = Path.GetFileName(filePath),
                        FileSizeBytes = new FileInfo(filePath).Length,
                        EncryptedFileName = encFileName,
                        CreatedAt = DateTime.Now
                    };

                    _editingAttachments.Add(attachment);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal melampirkan berkas '{Path.GetFileName(filePath)}': {ex.Message}", "Error Enkripsi Lampiran", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            UpdateEditAttachmentsDisplay();
        }
    }

    private void BtnEditRemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AttachmentItem item)
        {
            _editingAttachments.Remove(item);
            UpdateEditAttachmentsDisplay();
        }
    }

    private void TxtEditCredInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CmbEditCredIconPreset?.SelectedIndex == 0)
        {
            UpdateEditIconPreview();
        }
    }

    private void CmbEditCredIconPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateEditIconPreview();
    }

    private void BtnUploadCustomIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Pilih Logo atau Ikon Kustom",
            Filter = "Gambar (*.png;*.jpg;*.jpeg;*.ico;*.svg)|*.png;*.jpg;*.jpeg;*.ico;*.svg|Semua File (*.*)|*.*"
        };

        var parentWin = Window.GetWindow(this);
        bool? result = parentWin != null ? dlg.ShowDialog(parentWin) : dlg.ShowDialog();

        if (result == true)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string iconDir = Path.Combine(appData, "WhatInsides", "CustomIcons");
                Directory.CreateDirectory(iconDir);

                string ext = Path.GetExtension(dlg.FileName);
                string dest = Path.Combine(iconDir, $"{Guid.NewGuid():N}{ext}");
                File.Copy(dlg.FileName, dest, overwrite: true);

                _editingCustomIconPath = dest;
                UpdateEditIconPreview();
                ToastRequested?.Invoke("Icon Kustom Terpasang", Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat ikon: {ex.Message}", "Kesalahan", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnClearCustomIcon_Click(object sender, RoutedEventArgs e)
    {
        _editingCustomIconPath = null;
        if (CmbEditCredIconPreset != null && CmbEditCredIconPreset.Items.Count > 0)
        {
            CmbEditCredIconPreset.SelectedIndex = 0; // Auto
        }
        UpdateEditIconPreview();
    }

    private void UpdateEditIconPreview()
    {
        if (ImgEditIconPreview == null || PathEditIconPreview == null) return;

        if (!string.IsNullOrWhiteSpace(_editingCustomIconPath) && File.Exists(_editingCustomIconPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(_editingCustomIconPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                ImgEditIconPreview.Source = bmp;
                ImgEditIconPreview.Visibility = Visibility.Visible;
                PathEditIconPreview.Visibility = Visibility.Collapsed;
                return;
            }
            catch { }
        }

        ImgEditIconPreview.Visibility = Visibility.Collapsed;
        PathEditIconPreview.Visibility = Visibility.Visible;

        string selectedKey = CmbEditCredIconPreset?.SelectedValue as string ?? "Auto";
        string effectiveBrand = selectedKey;
        if (selectedKey == "Auto")
        {
            CredentialCategory curCat = CmbEditCredCategory?.SelectedValue is CredentialCategory c ? c : CredentialCategory.Login;
            var dummy = new CredentialItem
            {
                Title = TxtEditCredTitle?.Text ?? string.Empty,
                Url = TxtEditCredUrl?.Text ?? string.Empty,
                Username = TxtEditCredUsername?.Text ?? string.Empty,
                Category = curCat
            };
            effectiveBrand = dummy.GetEffectiveBrandKey();
        }

        if (effectiveBrand.Equals("AWS", StringComparison.OrdinalIgnoreCase) || effectiveBrand.Equals("Amazon", StringComparison.OrdinalIgnoreCase))
        {
            string k = effectiveBrand.Equals("AWS", StringComparison.OrdinalIgnoreCase) ? "BrandAWS" : "BrandAmazon";
            if (TryFindResource(k) is Geometry dedicatedGeo && dedicatedGeo != Geometry.Empty)
            {
                PathEditIconPreview.Data = dedicatedGeo;
                PathEditIconPreview.Fill = (Brush)new BrushConverter().ConvertFromString("#FF9900")!;
                return;
            }
        }

        var siEditGeo = Converters.CredentialToIconGeometryConverter.TryGetSimpleIconGeometry(effectiveBrand);
        var faEditGeo = Converters.CredentialToIconGeometryConverter.TryGetFontAwesomeGeometry(effectiveBrand);

        if (siEditGeo != null && siEditGeo != Geometry.Empty)
        {
            PathEditIconPreview.Data = siEditGeo;
            PathEditIconPreview.Fill = Converters.CredentialToIconBrushConverter.GetBrandBrush(effectiveBrand);
        }
        else if (faEditGeo != null && faEditGeo != Geometry.Empty)
        {
            PathEditIconPreview.Data = faEditGeo;
            PathEditIconPreview.Fill = Converters.CredentialToIconBrushConverter.GetBrandBrush(effectiveBrand);
        }
        else if (!string.IsNullOrEmpty(effectiveBrand) && TryFindResource("Brand" + effectiveBrand) is Geometry brandGeo)
        {
            PathEditIconPreview.Data = brandGeo;
            PathEditIconPreview.Fill = Converters.CredentialToIconBrushConverter.GetBrandBrush(effectiveBrand);
        }
        else
        {
            CredentialCategory cat = CmbEditCredCategory?.SelectedValue is CredentialCategory c ? c : CredentialCategory.Login;
            PathEditIconPreview.Data = (Geometry)new Converters.CredentialCategoryToGeometryConverter().Convert(cat, typeof(Geometry), null!, System.Globalization.CultureInfo.InvariantCulture);
            PathEditIconPreview.Fill = (Brush)new Converters.CredentialCategoryToColorConverter().Convert(cat, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void CmbEditCredCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbEditCredCategory?.SelectedValue is CredentialCategory cat)
        {
            UpdateFormFieldsForCategory(cat);
        }
        UpdateEditIconPreview();
    }

    private void UpdateFormFieldsForCategory(CredentialCategory category)
    {
        if (TxtEditCredTitleHeader == null) return;

        string currentPreset = CmbEditCredIconPreset?.SelectedValue as string ?? "Auto";
        UpdateBrandPresetsForCategory(category, currentPreset);

        switch (category)
        {
            case CredentialCategory.Finance:
            case CredentialCategory.PaymentCard:
            case CredentialCategory.BankAccount:
                if (TxtEditCredBrandHeader != null) TxtEditCredBrandHeader.Text = "LOGO BANK / PENERBIT KARTU";
                TxtEditCredTitleHeader.Text = "NAMA BANK / PENERBIT KARTU";
                TxtEditCredTitleHint.Text = "BCA, Mandiri, Visa Platinum...";

                GroupEditUsername.Visibility = Visibility.Visible;
                TxtEditCredUsernameHeader.Text = "NOMOR KARTU / REKENING";
                TxtEditCredUsernameHint.Text = "4000 1234 5678 9010 / No. Rek";

                GroupEditPassword.Visibility = Visibility.Visible;
                TxtEditCredPasswordHeader.Text = "PIN KARTU / EXPIRY & CVV";
                TxtEditCredPasswordHint.Text = "PIN: 123456 | Exp: 08/28 | CVV: 789";
                TxtEditCredPasswordHint.Visibility = Visibility.Collapsed;
                BtnTogglePassGen.Visibility = Visibility.Visible;
                BtnTogglePassGen.Content = "Buka Generator PIN";
                if (CmbGenType != null && CmbGenType.SelectedIndex != 2)
                {
                    CmbGenType.SelectedIndex = 2; // PIN generator
                }

                GroupEditTotp.Visibility = Visibility.Visible;
                TxtEditCredTotpHeader.Text = "KODE CVV / CVC (OPSIONAL)";
                TxtEditCredTotpHint.Text = "3 atau 4 digit di belakang kartu";

                GroupEditUrl.Visibility = Visibility.Visible;
                TxtEditCredUrlHeader.Text = "WEBSITE INTERNET BANKING";
                TxtEditCredUrlHint.Text = "https://klikbca.com";

                TxtEditCredNotesHeader.Text = "CATATAN KEUANGAN & LIMIT";
                TxtEditCredNotesHint.Text = "Limit harian, cabang buka, PIC bank";
                TxtEditCredNotes.Height = 64;
                break;

            case CredentialCategory.Identity:
                if (TxtEditCredBrandHeader != null) TxtEditCredBrandHeader.Text = "IKON IDENTITAS / DOKUMEN";
                TxtEditCredTitleHeader.Text = "JENIS DOKUMEN / IDENTITAS";
                TxtEditCredTitleHint.Text = "KTP RI, Paspor, SIM A, NPWP...";

                GroupEditUsername.Visibility = Visibility.Visible;
                TxtEditCredUsernameHeader.Text = "NOMOR IDENTITAS / NIK / NO. PASPOR";
                TxtEditCredUsernameHint.Text = "3171xxxxxxxxxxxx / B1234567";

                GroupEditPassword.Visibility = Visibility.Visible;
                TxtEditCredPasswordHeader.Text = "MASA BERLAKU / EXPIRY DATE";
                TxtEditCredPasswordHint.Text = "2030-12-31 atau Seumur Hidup";
                TxtEditCredPasswordHint.Visibility = Visibility.Visible;
                BtnTogglePassGen.Visibility = Visibility.Collapsed;
                BorderPassGenerator.Visibility = Visibility.Collapsed;

                GroupEditTotp.Visibility = Visibility.Collapsed;
                GroupEditUrl.Visibility = Visibility.Collapsed;

                TxtEditCredNotesHeader.Text = "DETAIL IDENTITAS LENGKAP";
                TxtEditCredNotesHint.Text = "Alamat, TTL, Golongan Darah, Instansi Penerbit";
                TxtEditCredNotes.Height = 90;
                break;

            case CredentialCategory.DeveloperServer:
            case CredentialCategory.SshToken:
            case CredentialCategory.ServerDatabase:
                if (TxtEditCredBrandHeader != null) TxtEditCredBrandHeader.Text = "LOGO SERVER / CLOUD / DB";
                TxtEditCredTitleHeader.Text = "NAMA HOST / SERVER / LAYANAN DEV";
                TxtEditCredTitleHint.Text = "AWS Prod, VPS Ubuntu SG, GitHub API...";

                GroupEditUsername.Visibility = Visibility.Visible;
                TxtEditCredUsernameHeader.Text = "HOST / USERNAME / ACCESS KEY ID";
                TxtEditCredUsernameHint.Text = "root@192.168.1.1, AKIAIOSFODNN7...";

                GroupEditPassword.Visibility = Visibility.Visible;
                TxtEditCredPasswordHeader.Text = "TOKEN / SECRET KEY / SSH PASSWORD";
                TxtEditCredPasswordHint.Text = "ghp_xxxx, private key, db password";
                TxtEditCredPasswordHint.Visibility = Visibility.Collapsed;
                BtnTogglePassGen.Visibility = Visibility.Visible;
                BtnTogglePassGen.Content = "Buka Generator Kunci";

                GroupEditTotp.Visibility = Visibility.Visible;
                TxtEditCredTotpHeader.Text = "PORT / 2FA AUTHENTICATOR (OPSIONAL)";
                TxtEditCredTotpHint.Text = "22, 5432, atau TOTP Secret Server";

                GroupEditUrl.Visibility = Visibility.Visible;
                TxtEditCredUrlHeader.Text = "ENDPOINT / REPOSITORY / HOST URL";
                TxtEditCredUrlHint.Text = "https://api.github.com / ssh://...";

                TxtEditCredNotesHeader.Text = "KONFIGURASI SERVER & ENV";
                TxtEditCredNotesHint.Text = "SSH Config, ENV variables, Port forwarding";
                TxtEditCredNotes.Height = 80;
                break;

            case CredentialCategory.CryptoSeed:
                if (TxtEditCredBrandHeader != null) TxtEditCredBrandHeader.Text = "LOGO JARINGAN / DOMPET KRIPTO";
                TxtEditCredTitleHeader.Text = "NAMA DOMPET / ASSET KRIPTO";
                TxtEditCredTitleHint.Text = "MetaMask Utama, Ledger ETH, Phantom...";

                GroupEditUsername.Visibility = Visibility.Visible;
                TxtEditCredUsernameHeader.Text = "ALAMAT DOMPET PUBLIK (PUBLIC ADDRESS)";
                TxtEditCredUsernameHint.Text = "0x71C... atau bc1qar0s...";

                GroupEditPassword.Visibility = Visibility.Visible;
                TxtEditCredPasswordHeader.Text = "MNEMONIC SEED PHRASE (12 / 24 KATA)";
                TxtEditCredPasswordHint.Text = "word1 word2 word3 ... word12 (Sangat Rahasia)";
                TxtEditCredPasswordHint.Visibility = Visibility.Visible;
                BtnTogglePassGen.Visibility = Visibility.Collapsed;
                BorderPassGenerator.Visibility = Visibility.Collapsed;

                GroupEditTotp.Visibility = Visibility.Collapsed;

                GroupEditUrl.Visibility = Visibility.Visible;
                TxtEditCredUrlHeader.Text = "BLOCKCHAIN EXPLORER / DAPP URL";
                TxtEditCredUrlHint.Text = "https://etherscan.io, solscan.io...";

                TxtEditCredNotesHeader.Text = "DERIVATION PATH & CATATAN JARINGAN";
                TxtEditCredNotesHint.Text = "m/44'/60'/0'/0/0, Network: Arbitrum One";
                TxtEditCredNotes.Height = 80;
                break;

            case CredentialCategory.SecureNote:
            case CredentialCategory.SoftwareLicense:
                if (TxtEditCredBrandHeader != null) TxtEditCredBrandHeader.Text = "IKON LISENSI / CATATAN";
                TxtEditCredTitleHeader.Text = "JUDUL CATATAN / NAMA PRODUK LISENSI";
                TxtEditCredTitleHint.Text = "Lisensi Windows 11, Kode Darurat Bitwarden...";

                GroupEditUsername.Visibility = Visibility.Visible;
                TxtEditCredUsernameHeader.Text = "ATAS NAMA / PEMEGANG LISENSI (OPSIONAL)";
                TxtEditCredUsernameHint.Text = "Nama terdaftar / email pemilik";

                GroupEditPassword.Visibility = Visibility.Visible;
                TxtEditCredPasswordHeader.Text = "PRODUCT KEY / SERIAL NUMBER / KUNCI";
                TxtEditCredPasswordHint.Text = "XXXXX-XXXXX-XXXXX-XXXXX-XXXXX";
                TxtEditCredPasswordHint.Visibility = Visibility.Visible;
                BtnTogglePassGen.Visibility = Visibility.Collapsed;
                BorderPassGenerator.Visibility = Visibility.Collapsed;

                GroupEditTotp.Visibility = Visibility.Collapsed;
                GroupEditUrl.Visibility = Visibility.Collapsed;

                TxtEditCredNotesHeader.Text = "ISI CATATAN RAHASIA TERENKRIPSI";
                TxtEditCredNotesHint.Text = "Instruksi aktivasi atau catatan bebas";
                TxtEditCredNotes.Height = 110;
                break;

            default: // Login, All, Favorite, Trash, Email, Membership, etc.
                if (TxtEditCredBrandHeader != null) TxtEditCredBrandHeader.Text = "LOGO BRAND / LAYANAN";
                TxtEditCredTitleHeader.Text = "NAMA LAYANAN / APLIKASI";
                TxtEditCredTitleHint.Text = "Google, Netflix, GitHub, Steam...";

                GroupEditUsername.Visibility = Visibility.Visible;
                TxtEditCredUsernameHeader.Text = "NAMA PENGGUNA / EMAIL";
                TxtEditCredUsernameHint.Text = "nama@email.com atau username";

                GroupEditPassword.Visibility = Visibility.Visible;
                TxtEditCredPasswordHeader.Text = "KATA SANDI";
                TxtEditCredPasswordHint.Text = "Rahasia";
                TxtEditCredPasswordHint.Visibility = Visibility.Collapsed;
                BtnTogglePassGen.Visibility = Visibility.Visible;
                BtnTogglePassGen.Content = "Buka Generator Sandi";

                GroupEditTotp.Visibility = Visibility.Visible;
                TxtEditCredTotpHeader.Text = "KODE RAHASIA 2FA / TOTP (BASE32, OPSIONAL)";
                TxtEditCredTotpHint.Text = "JBSWY3DPEHPK3PXP";

                GroupEditUrl.Visibility = Visibility.Visible;
                TxtEditCredUrlHeader.Text = "WEBSITE / URL LOGIN";
                TxtEditCredUrlHint.Text = "https://accounts.google.com";

                TxtEditCredNotesHeader.Text = "CATATAN TERENKRIPSI";
                TxtEditCredNotesHint.Text = "Pertanyaan keamanan, backup info";
                TxtEditCredNotes.Height = 64;
                break;
        }
    }
}

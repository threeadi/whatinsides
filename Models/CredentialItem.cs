using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WhatInsides.Models;

public class CredentialItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _title = string.Empty;
    private CredentialCategory _category = CredentialCategory.Login;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _totpSecret;
    private string? _url;
    private string? _notes;
    private string _vaultPartition = "D:\\ Data & Games";
    private List<string> _tags = new();
    private bool _isFavorite;
    private bool _isDeleted;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;
    private Dictionary<string, string> _customFields = new();
    private string? _iconKey;
    private string? _customIconPath;
    private List<AttachmentItem> _attachments = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayLetter));
            }
        }
    }

    public CredentialCategory Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CategoryDisplayName));
            }
        }
    }

    public string Username
    {
        get => _username;
        set { if (_username != value) { _username = value; OnPropertyChanged(); } }
    }

    public string Password
    {
        get => _password;
        set { if (_password != value) { _password = value; OnPropertyChanged(); } }
    }

    public string? TotpSecret
    {
        get => _totpSecret;
        set
        {
            if (_totpSecret != value)
            {
                _totpSecret = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasTotp));
            }
        }
    }

    public string? Url
    {
        get => _url;
        set
        {
            if (_url != value)
            {
                _url = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DomainOnly));
            }
        }
    }

    public string? Notes
    {
        get => _notes;
        set { if (_notes != value) { _notes = value; OnPropertyChanged(); } }
    }

    public string VaultPartition
    {
        get => _vaultPartition;
        set { if (_vaultPartition != value) { _vaultPartition = value; OnPropertyChanged(); } }
    }

    public List<string> Tags
    {
        get => _tags;
        set { if (_tags != value) { _tags = value; OnPropertyChanged(); } }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(); } }
    }

    public bool IsDeleted
    {
        get => _isDeleted;
        set { if (_isDeleted != value) { _isDeleted = value; OnPropertyChanged(); } }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { if (_createdAt != value) { _createdAt = value; OnPropertyChanged(); } }
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set { if (_updatedAt != value) { _updatedAt = value; OnPropertyChanged(); } }
    }

    public Dictionary<string, string> CustomFields
    {
        get => _customFields;
        set { if (_customFields != value) { _customFields = value; OnPropertyChanged(); } }
    }

    public string? IconKey
    {
        get => _iconKey;
        set { if (_iconKey != value) { _iconKey = value; OnPropertyChanged(); } }
    }

    public string? CustomIconPath
    {
        get => _customIconPath;
        set { if (_customIconPath != value) { _customIconPath = value; OnPropertyChanged(); } }
    }

    public List<AttachmentItem> Attachments
    {
        get => _attachments;
        set
        {
            if (_attachments != value)
            {
                _attachments = value ?? new();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachments));
                OnPropertyChanged(nameof(AttachmentsCount));
            }
        }
    }

    public bool HasAttachments => _attachments.Count > 0;

    public int AttachmentsCount => _attachments.Count;

    public string DisplayLetter => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[..1].ToUpperInvariant();

    public bool HasTotp => !string.IsNullOrWhiteSpace(TotpSecret);

    public string CategoryDisplayName => Category switch
    {
        CredentialCategory.Login or CredentialCategory.Password => "Akun & Login",
        CredentialCategory.Finance or CredentialCategory.PaymentCard or CredentialCategory.BankAccount => "Keuangan & Kartu",
        CredentialCategory.Identity => "Identitas & Dokumen",
        CredentialCategory.SecureNote or CredentialCategory.SoftwareLicense => "Catatan & Lisensi",
        CredentialCategory.DeveloperServer or CredentialCategory.SshToken or CredentialCategory.ServerDatabase => "Developer & Server",
        CredentialCategory.CryptoSeed => "Kripto & Seed",
        CredentialCategory.Trash => "Tempat Sampah",
        _ => "Item"
    };

    public string DomainOnly
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return string.Empty;
            try
            {
                var uri = new Uri(Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? Url : "https://" + Url);
                return uri.Host;
            }
            catch
            {
                return Url;
            }
        }
    }

    public string GetEffectiveBrandKey()
    {
        if (!string.IsNullOrWhiteSpace(IconKey) && !IconKey.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return IconKey;

        string combined = $"{Title} {DomainOnly} {Url} {Username}".ToLowerInvariant();

        // 1. Indonesian & Global Banking / Cards / FinTech
        if (combined.Contains("bca") || combined.Contains("klikbca") || combined.Contains("central asia")) return "BCA";
        if (combined.Contains("mandiri") || combined.Contains("livin")) return "Mandiri";
        if (combined.Contains("bni") || combined.Contains("negara indonesia")) return "BNI";
        if (combined.Contains("bri") || combined.Contains("brimo") || combined.Contains("rakyat indonesia")) return "BRI";
        if (combined.Contains("bsi") || combined.Contains("syariah indonesia")) return "BSI";
        if (combined.Contains("cimb") || combined.Contains("octo")) return "CIMB";
        if (combined.Contains("jago")) return "BankJago";
        if (combined.Contains("jenius") || combined.Contains("btpn")) return "Jenius";
        if (combined.Contains("permata")) return "Permata";
        if (combined.Contains("visa")) return "Visa";
        if (combined.Contains("mastercard")) return "Mastercard";
        if (combined.Contains("paypal")) return "PayPal";
        if (combined.Contains("stripe")) return "Stripe";
        if (combined.Contains("wise") || combined.Contains("transferwise")) return "Wise";
        if (combined.Contains("gopay") || combined.Contains("gojek")) return "GoPay";
        if (combined.Contains("ovo")) return "OVO";
        if (combined.Contains("dana")) return "Dana";
        if (combined.Contains("shopeepay") || combined.Contains("shopee")) return "ShopeePay";

        // 2. Gaming & Entertainment
        if (combined.Contains("valorant")) return "Valorant";
        if (combined.Contains("riot")) return "RiotGames";
        if (combined.Contains("steam")) return "Steam";
        if (combined.Contains("epic")) return "EpicGames";
        if (combined.Contains("playstation") || combined.Contains("psn")) return "PlayStation";
        if (combined.Contains("xbox")) return "Xbox";
        if (combined.Contains("nintendo")) return "Nintendo";
        if (combined.Contains("discord")) return "Discord";
        if (combined.Contains("spotify")) return "Spotify";
        if (combined.Contains("netflix")) return "Netflix";
        if (combined.Contains("youtube")) return "YouTube";
        if (combined.Contains("twitch")) return "Twitch";

        // 3. Cloud / Developer / Infrastructure / Database
        if (combined.Contains("aws") || combined.Contains("amazon web")) return "AWS";
        if (combined.Contains("gcp") || combined.Contains("google cloud")) return "GoogleCloud";
        if (combined.Contains("azure")) return "Azure";
        if (combined.Contains("github")) return "GitHub";
        if (combined.Contains("gitlab")) return "GitLab";
        if (combined.Contains("bitbucket")) return "Bitbucket";
        if (combined.Contains("docker")) return "Docker";
        if (combined.Contains("kubernetes") || combined.Contains("k8s")) return "Kubernetes";
        if (combined.Contains("postgres")) return "PostgreSQL";
        if (combined.Contains("mysql")) return "MySQL";
        if (combined.Contains("mariadb")) return "MariaDB";
        if (combined.Contains("redis")) return "Redis";
        if (combined.Contains("mongo")) return "MongoDB";
        if (combined.Contains("sqlite")) return "SQLite";
        if (combined.Contains("cloudflare")) return "Cloudflare";
        if (combined.Contains("digitalocean")) return "DigitalOcean";
        if (combined.Contains("vercel")) return "Vercel";
        if (combined.Contains("supabase")) return "Supabase";
        if (combined.Contains("firebase")) return "Firebase";
        if (combined.Contains("ubuntu")) return "Ubuntu";
        if (combined.Contains("debian")) return "Debian";
        if (combined.Contains("linux")) return "Linux";
        if (combined.Contains("nginx")) return "NGINX";
        if (combined.Contains("apache")) return "Apache";

        // 4. Crypto / Web3
        if (combined.Contains("bitcoin") || combined.Contains("btc")) return "Bitcoin";
        if (combined.Contains("ethereum") || combined.Contains("eth")) return "Ethereum";
        if (combined.Contains("solana") || combined.Contains("sol")) return "Solana";
        if (combined.Contains("binance") || combined.Contains("bnb")) return "Binance";
        if (combined.Contains("polygon") || combined.Contains("matic")) return "Polygon";
        if (combined.Contains("tether") || combined.Contains("usdt")) return "Tether";
        if (combined.Contains("metamask")) return "MetaMask";
        if (combined.Contains("phantom")) return "Phantom";
        if (combined.Contains("ledger")) return "Ledger";
        if (combined.Contains("trezor")) return "Trezor";
        if (combined.Contains("coinbase")) return "Coinbase";
        if (combined.Contains("kraken")) return "Kraken";
        if (combined.Contains("bybit")) return "Bybit";
        if (combined.Contains("uniswap")) return "Uniswap";
        if (combined.Contains("cardano") || combined.Contains("ada")) return "Cardano";
        if (combined.Contains("ripple") || combined.Contains("xrp")) return "Ripple";
        if (combined.Contains("doge")) return "Dogecoin";

        // 5. Software Licenses & Productivity Tools
        if (combined.Contains("windows")) return "Windows";
        if (combined.Contains("office") || combined.Contains("m365")) return "Microsoft";
        if (combined.Contains("adobe") || combined.Contains("photoshop") || combined.Contains("illustrator") || combined.Contains("premiere")) return "Adobe";
        if (combined.Contains("jetbrains") || combined.Contains("intellij") || combined.Contains("rider")) return "JetBrains";
        if (combined.Contains("figma")) return "Figma";
        if (combined.Contains("notion")) return "Notion";
        if (combined.Contains("bitwarden")) return "Bitwarden";
        if (combined.Contains("1password")) return "OnePassword";
        if (combined.Contains("autodesk") || combined.Contains("autocad")) return "Autodesk";

        // 6. Identity & Documents
        if (combined.Contains("paspor") || combined.Contains("passport") || combined.Contains("imigrasi")) return "Passport";
        if (combined.Contains("ktp") || combined.Contains("nik") || combined.Contains("kependudukan") || combined.Contains("dukcapil")) return "IdCard";
        if (combined.Contains("sim ") || combined.Contains("surat izin mengemudi") || combined.Contains("driving license")) return "DrivingLicense";
        if (combined.Contains("npwp") || combined.Contains("pajak")) return "Tax";
        if (combined.Contains("bpjs")) return "Hospital";
        if (combined.Contains("ijazah") || combined.Contains("sertifikat") || combined.Contains("diploma")) return "Certificate";
        if (combined.Contains("keluarga") || combined.Contains("kk")) return "Family";

        // 7. Tech Giants & Social Media
        if (combined.Contains("google") || combined.Contains("gmail")) return "Google";
        if (combined.Contains("microsoft") || combined.Contains("outlook") || combined.Contains("live.com")) return "Microsoft";
        if (combined.Contains("apple") || combined.Contains("icloud")) return "Apple";
        if (combined.Contains("twitter") || combined.Contains("x.com")) return "X";
        if (combined.Contains("instagram")) return "Instagram";
        if (combined.Contains("tiktok")) return "TikTok";
        if (combined.Contains("telegram")) return "Telegram";
        if (combined.Contains("whatsapp")) return "WhatsApp";
        if (combined.Contains("reddit")) return "Reddit";
        if (combined.Contains("openai") || combined.Contains("chatgpt")) return "OpenAI";

        // 8. Category-specific context fallbacks
        switch (Category)
        {
            case CredentialCategory.Finance:
            case CredentialCategory.PaymentCard:
            case CredentialCategory.BankAccount:
                if (combined.Contains("kartu") || combined.Contains("card")) return "CreditCard";
                if (combined.Contains("bank") || combined.Contains("rekening")) return "University";
                break;
            case CredentialCategory.Identity:
                return "IdCard";
            case CredentialCategory.DeveloperServer:
            case CredentialCategory.SshToken:
            case CredentialCategory.ServerDatabase:
                if (combined.Contains("db") || combined.Contains("database") || combined.Contains("sql")) return "Database";
                if (combined.Contains("ssh") || combined.Contains("terminal") || combined.Contains("console")) return "Terminal";
                return "Server";
            case CredentialCategory.CryptoSeed:
                return "Bitcoin";
            case CredentialCategory.SecureNote:
            case CredentialCategory.SoftwareLicense:
                if (combined.Contains("key") || combined.Contains("license") || combined.Contains("serial")) return "Key";
                return "FileLines";
        }

        // 9. Automatic detection across all 3,300+ SimpleIcons brands
        var words = $"{Title} {DomainOnly}".Split(new[] { ' ', '.', '-', '_', '/', ':', '@' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var w in words)
        {
            if (w.Length >= 3 && Enum.TryParse<MahApps.Metro.IconPacks.PackIconSimpleIconsKind>(w, ignoreCase: true, out var kind))
            {
                return kind.ToString();
            }
        }

        return string.Empty;
    }
}

# WhatInsides 🔒

> **Modern In-Place Privacy Vault & File Locker for Windows**  
> Dibangun dengan **.NET 10 (WPF)** dengan estetika minimalis modern bergaya *Bun.sh / Linear*, dirancang untuk mengamankan berkas dan folder sensitif langsung di tempat asalnya tanpa perlu dipindahkan ke partisi khusus.

---

## ⚡ Fitur Utama

### 1. In-Place Locking (Enkripsi Langsung di Lokasi Asal)
Tidak perlu memindahkan berkas ke folder brankas khusus atau virtual disk lambat:
* **Mode Cepat (FastHide)**: Penyembunyian instan dengan manipulasi atribut sistem tingkat lanjut (<0.1 detik). Berkas langsung hilang dari Windows Explorer tanpa proses salin ulang.
* **Mode Enkripsi (AES-256)**: Mengemas berkas/folder menjadi kontainer terenkripsi `.wisafe` menggunakan **AES-256-CBC** dan derivasi kunci **PBKDF2 (SHA-256)** dengan verifikasi hash.
* **DoD 5220.22-M Secure Shredding**: Berkas asli dihancurkan dengan overwrite multi-pass acak sehingga mustahil dipulihkan dengan software recovery.

### 2. Integrasi Windows Explorer (Shell Context Menu)
* Klik kanan pada berkas atau folder apa pun di Windows Explorer untuk mengunci langsung melalui menu konteks **"Kunci dengan WhatInsides"**.
* Mendukung pemindaian ACL dan verifikasi integritas HMAC berkas terkunci langsung dari tab Integrasi Shell.

### 3. Otomatisasi & Proteksi Sesi
* **Inactivity Auto-Lock**: Memantau aktivitas keyboard dan mouse pengguna via Win32 API `GetLastInputInfo`. Vault otomatis terkunci jika pengguna tidak aktif (1, 5, 15 menit).
* **Auto-Relock on Sleep / Win+L**: Mengunci vault secara otomatis saat laptop/PC masuk mode tidur (*suspend*) atau layar terkunci.
* **Auto-Relock on Minimize**: Mengunci vault seketika saat jendela diminimalkan untuk perlindungan privasi instan dari orang sekitar.

### 4. Background Mode & System Tray
* Ikon brankas aktif di Windows System Tray (area notifikasi pojok kanan bawah dekat jam).
* Opsi **Jalankan di background (System Tray)**: Sembunyikan aplikasi dari taskbar saat diminimalkan dan akses cepat melalui menu klik kanan tray (**Buka Dashboard**, **Kunci Vault**, **Keluar**).

### 5. Integrasi Cloud & Google Drive
* Deteksi otomatis instalasi **Google Drive for Desktop** lokal.
* **Auto-Upload Pasca-Penguncian**: Snapshot kontainer terenkripsi `.wisafe` otomatis dicadangkan ke Google Drive atau folder cadangan kustom begitu penguncian selesai.
* **Zero-Knowledge End-to-End**: Hanya kontainer `.wisafe` yang telah terenkripsi AES-256 yang diunggah ke cloud; pihak cloud tidak dapat membaca nama dan isi dokumen.
* **Filter Wi-Fi Only**: Menghemat kuota dengan menunda unggahan otomatis jika perangkat menggunakan koneksi berbayar/seluler.

### 6. Filter Disk & Alokasi Partisi In-Place
* Filter tabel berkas dinamis berdasarkan drive partisi asal (`[C:]`, `[D:]`, `[E:]`, dsb.) dengan *pill chips* dan dropdown.
* Monitor overhead dan kapasitas ruang terpakai drive secara real-time.

### 7. Tema & Navigasi Cepat
* Dukungan penuh **Tema Gelap (Dark Mode)** dan **Tema Terang (Light Mode)** yang tersinkronisasi ke Lock Screen.
* Kotak pencarian global dengan pintasan keyboard **`Ctrl + K`** dan debounce pencarian cepat.

---

## 🛡️ Spesifikasi Keamanan & Kriptografi

| Komponen | Spesifikasi Teknis |
| :--- | :--- |
| **Algoritma Simetris** | AES-256-CBC (Advanced Encryption Standard) |
| **Derivasi Kunci (KDF)** | PBKDF2 dengan HMAC-SHA256 (100.000+ iterasi & salt acak) |
| **Penyimpanan Kunci** | Windows DPAPI (*Data Protection API*) terikat ke akun pengguna |
| **Penghancuran Berkas** | Standar DoD 5220.22-M Sanitization (Multi-Pass Overwrite) |
| **Penyimpanan Pengaturan**| JSON lokal terisolasi di `%LOCALAPPDATA%\WhatInsides` |

---

## ⌨️ Pintasan Keyboard (Keyboard Shortcuts)

* **`Ctrl + K`** atau **`Ctrl + F`**: Fokus instan ke kotak pencarian global.
* **`Enter`** (pada pencarian): Terapkan filter dan sortir seketika.
* **`Esc`**: Tutup modal atau dialog aktif.

---

## 🚀 Memulai (Getting Started)

### Prasyarat
* **Windows 10 / 11** (64-bit)
* **.NET 10 SDK** (atau .NET 10 Desktop Runtime)

### Menjalankan Aplikasi
```bash
# Clone atau buka repositori
cd whatinsides

# Jalankan aplikasi langsung
dotnet run --project WhatInsides.csproj
```

### Menjalankan Pengujian (Unit Tests)
```bash
# Jalankan seluruh rangkaian test suite (43 unit tests)
dotnet test tests/WhatInsides.Tests/WhatInsides.Tests.csproj
```

---

## 📂 Struktur Proyek

```text
whatinsides/
├── Assets/                 # Ikon vektor GoogleIcons, app.ico, app.png
├── Converters/             # WPF Value Converters
├── Fonts/                  # Bundled TrueType Fonts (Geist, Geist Mono, JetBrains Mono)
├── Models/                 # Model data (AppSettings, LockedItemModel, dll)
├── Services/               # Layanan inti:
│   ├── CryptoEngine.cs         # Enkripsi & dekripsi AES-256
│   ├── DirectLockService.cs    # Orkestrasi penguncian in-place
│   ├── FileShredder.cs         # Penghancur DoD 5220.22-M
│   ├── LockRegistryService.cs  # Registry berkas terkunci
│   ├── SettingsManager.cs      # Manajemen konfigurasi appsettings
│   ├── ShellContextMenuService.cs # Integrasi menu klik kanan Windows
│   ├── CloudDetectionService.cs# Deteksi Google Drive & cloud
│   ├── TrayIconService.cs      # Integrasi Windows System Tray
│   └── ThemeManager.cs         # Pengatur tema Dark/Light
├── Themes/                 # Sumber daya XAML (Styles.xaml, Icons.xaml)
├── tests/                  # Unit test suite xUnit (43 tests)
├── MainWindow.xaml         # Antarmuka utama aplikasi
├── LockWindow.xaml         # Layar kunci master password
└── WhatInsides.csproj      # File proyek .NET 10 WPF
```

---

## 📄 Lisensi
Proyek ini dilisensikan di bawah lisensi pribadi / tertutup untuk pengembangan WhatInsides. Seluruh hak cipta dilindungi undang-undang.

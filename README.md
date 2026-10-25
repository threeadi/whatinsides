# WhatInsides 🔒

> **Modern Privacy Vault, Credential Manager & File Locker for Windows**  
> Dibangun dengan **.NET 10 (WPF)** dan **C# 14** dengan estetika minimalis modern bergaya *Bun.sh / Linear*, dirancang untuk mengamankan kredensial akun, kode 2FA TOTP, lampiran dokumen rahasia, serta berkas/folder langsung di tempat asalnya (*in-place*) tanpa perlu virtual disk lambat.

---

## ⚡ Fitur Utama

### 1. Brankas Kredensial & Pengelola Akun (Credential Vault)
* **Kategori Terorganisir**: Mendukung Login/Password Akun, Kartu Bank & Rekening Keuangan, Kartu Identitas, Seed Phrase Kripto, Catatan Rahasia & Lisensi Software, Kunci SSH & Server/Database, serta Tempat Sampah.
* **3.300+ Brand Vector Icons**: Integrasi otomatis SimpleIcons & FontAwesome untuk ribuan logo resmi (Google, GitHub, AWS, BCA, Mandiri, Docker, Discord, OpenAI, dll.) lengkap dengan warna khas brand.
* **TOTP 2FA Authenticator Bawaan**: Penghitung mundur 30 detik real-time dengan ring visual dan salin instan kode 2FA untuk login tanpa repot membuka ponsel.
* **Generator Password Kriptografis**:
  * Pilihan mode: Random String, Diceware Passphrase kata manusia yang mudah diingat, dan PIN angka.
  * Kustomisasi panjang karakter, simbol, angka, huruf besar/kecil, serta kalkulator entropi visual.
* **Lampiran Berkas Terenkripsi (Secure File Attachments)**:
  * Melampirkan dokumen penting (PDF kontrak, kunci privat `.pem`/`.key`, foto KTP, backup seed) langsung ke akun terkait.
  * Dienkripsi penuh dengan AES-256 sebelum disimpan di storage lokal dan dapat diekspor kembali kapan saja.

### 2. Autentikasi Biometrik Windows Hello
* **Sign In dengan Biometrik**: Masuk ke aplikasi dan otorisasi akses menggunakan **Sidik Jari (Fingerprint)**, **Pengenalan Wajah (Face Recognition)**, atau **Windows Hello PIN** via WinRT API native.
* **Fallback Master Password**: Transisi mulus ke Master Password jika perangkat tidak memiliki sensor biometrik atau pengguna memilih input manual.

### 3. Global Auto-Type & Quick Autofill Spotlight
* **`Ctrl + Alt + A` (Auto-Type Cepat)**: Mengisi username dan password secara otomatis ke jendela aplikasi atau browser yang sedang aktif dengan simulasi ketukan keyboard aman (*Unicode SendInput*).
* **`Ctrl + Shift + Space` (Spotlight Quick Picker)**: Membuka jendela pencarian melayang (*floating spotlight*) di atas aplikasi aktif untuk memilih akun secara instan.
* **Pencocokan Target Pintar**: Otomatis mendeteksi browser (membersihkan suffix *Chrome, Edge, Firefox, Brave, Opera*), mencocokkan judul tab, nama proses, domain URL, tag, dan preferensi akun favorit.

### 4. Integrasi Agen SSH (SSH Agent Access Control)
* Prompt interaktif desktop (`SshAccessPromptWindow`) setiap kali terminal atau tool pengembang meminta kunci SSH.
* Opsi persetujuan sementara per sesi (*Allow Once*) atau ingat aplikasi (*Remember for this app*).

### 5. In-Place Locking & Virtual Multi-Vault
* **FastHide (Mode Cepat)**: Penyembunyian instan dengan manipulasi atribut sistem dan disguise CLSID tingkat lanjut (<0.1 detik). Berkas langsung hilang dari Windows Explorer tanpa proses salin ulang.
* **AES-256 In-Place Container**: Mengemas berkas/folder menjadi kontainer terenkripsi `.wisafe` menggunakan **AES-256-CBC** dan derivasi kunci **PBKDF2 (SHA-256)** dengan verifikasi hash.
* **Virtual Multi-Vault**: Mengelola banyak brankas independen dengan virtual mount directory terenkripsi real-time, grafik simulasi kecepatan I/O, dan penghancur berkas permanen **DoD 5220.22-M**.

### 6. Integrasi Windows Explorer (Shell Context Menu)
* Klik kanan pada berkas atau folder apa pun di Windows Explorer untuk mengunci langsung melalui menu konteks **"Kunci dengan WhatInsides"**.
* Mendukung pemindaian ACL dan verifikasi integritas HMAC berkas terkunci.

### 7. Otomatisasi & Proteksi Sesi
* **Proteksi Layar & Anti-Screenshot**: Proteksi tingkat OS via Win32 `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`. Mencegah Snipping Tool, screenshot PrtScn, screen share rapat (Zoom, Teams, Discord, Google Meet), serta malware perekam layar menangkap jendela brankas.
* **Inactivity Auto-Lock**: Memantau aktivitas keyboard dan mouse via Win32 API `GetLastInputInfo`. Vault otomatis terkunci jika pengguna tidak aktif (1, 5, 15 menit).
* **Auto-Relock on Sleep / Win+L**: Mengunci vault secara otomatis saat laptop/PC masuk mode tidur (*suspend*) atau layar terkunci.
* **Auto-Relock on Minimize**: Mengunci vault seketika saat jendela diminimalkan ke taskbar atau System Tray.

### 8. CLI Companion Tool (`wi.exe`)
Aplikasi dilengkapi dengan CLI pendamping mandiri di folder `cli/` untuk integrasi alur kerja terminal pengembang:

```bash
# Menampilkan status WhatInsides & ketersediaan Windows Hello
wi status

# Autentikasi sesi CLI dengan biometrik Windows Hello
wi signin

# Mengambil kredensial atau salin password ke clipboard
wi get github --password --copy

# Menghasilkan kode TOTP 2FA terkini untuk terminal login
wi totp github --copy

# Menampilkan atau mengekspor berkas lampiran akun
wi files aws
wi export-file aws id_rsa.pub ./id_rsa.pub

# Menjalankan perintah terminal dengan kredensial terinjeksi sebagai env vars
wi run -- npm start

# Kunci atau buka berkas/folder langsung dari terminal
wi lock C:\Proyek\data.json -p Rahasia123!
wi unlock C:\Proyek\data.json.wisafe -p Rahasia123!
wi list
```

---

## 🛡️ Spesifikasi Keamanan & Kriptografi

| Komponen | Spesifikasi Teknis |
| :--- | :--- |
| **Algoritma Simetris** | AES-256 (CBC / GCM) & XChaCha20-Poly1305 |
| **Derivasi KDF** | PBKDF2 dengan HMAC-SHA256 (100.000+ iterasi) & Argon2id |
| **Proteksi Layar** | Win32 `SetWindowDisplayAffinity` (`WDA_EXCLUDEFROMCAPTURE` / `WDA_MONITOR`) |
| **Biometrik** | Windows Hello WinRT (`KeyCredentialManager` & `UserConsentVerifier`) |
| **Penyimpanan Kunci** | Windows DPAPI (*Data Protection API*) terikat ke akun pengguna |
| **Penghancuran Berkas** | Standar DoD 5220.22-M Sanitization (Multi-Pass Overwrite) |
| **Penyimpanan Pengaturan**| JSON lokal terisolasi di `%LOCALAPPDATA%\WhatInsides` |

---

## ⌨️ Pintasan Keyboard (Global Hotkeys)

* **`Ctrl + Alt + A`**: Global Auto-Type kredensial ke jendela aktif.
* **`Ctrl + Shift + Space`**: Buka Spotlight Quick Autofill Picker.
* **`Ctrl + K`** / **`Ctrl + F`**: Fokus ke kotak pencarian global aplikasi.
* **`Esc`**: Tutup modal atau dialog aktif.

---

## 🚀 Memulai (Getting Started)

### Prasyarat
* **Windows 10 / 11** (64-bit)
* **.NET 10 SDK** (versi 10.0+)

### Menjalankan Aplikasi Desktop (WPF)
```bash
# Buka repositori
cd whatinsides

# Jalankan aplikasi utama
dotnet run --project WhatInsides.csproj
```

### Menjalankan CLI Tool
```bash
# Jalankan CLI tool
dotnet run --project cli/WhatInsides.Cli.csproj -- status
```

### Menjalankan Pengujian & Coverage (Unit Tests)
Proyek ini memiliki pengujian komprehensif sebanyak **236 tests** dengan tingkat kelulusan 100% dan cakupan >80% pada seluruh Core Services:

```bash
# Menjalankan semua unit tests
dotnet test tests/WhatInsides.Tests/WhatInsides.Tests.csproj

# Menjalankan pengujian lengkap dengan laporan Code Coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📂 Struktur Proyek

```text
whatinsides/
├── Assets/                 # Ikon vektor, brand assets, app.ico, app.png
├── Converters/             # WPF Value Converters (Brushes, Geometries, Category)
├── Fonts/                  # Bundled TrueType Fonts (Geist, Geist Mono, JetBrains Mono)
├── Models/                 # Entitas data (CredentialItem, AttachmentItem, VaultModel, AppSettings)
├── Services/               # Layanan inti & business logic:
│   ├── CredentialVaultService.cs   # Penyimpanan & enkripsi kredensial akun
│   ├── AutoTypeService.cs          # Engine Auto-Type & pencocokan target window
│   ├── BiometricService.cs         # Integrasi Windows Hello WinRT API
│   ├── TotpService.cs              # Generator kode 2FA RFC 6238
│   ├── PasswordGeneratorService.cs # Generator password & passphrase Diceware
│   ├── SshAgentService.cs          # SSH agent controller & prompt
│   ├── VaultManager.cs             # Manajemen brankas AES-256
│   ├── VaultFileSystemService.cs   # Virtual filesystem & mount watcher
│   ├── MultiVaultManager.cs        # Orkestrator multi-brankas
│   ├── NtfsLockerService.cs        # In-place NTFS locker & FastHide
│   ├── DirectLockService.cs        # Orkestrasi penguncian berkas langsung
│   ├── FileShredder.cs             # Penghancur berkas DoD 5220.22-M
│   ├── LockRegistryService.cs      # Registri status berkas terkunci
│   ├── SettingsManager.cs          # Konfigurasi aplikasi & DPAPI password
│   ├── ShellContextMenuService.cs  # Integrasi klik kanan Windows Explorer
│   ├── CloudDetectionService.cs    # Deteksi Google Drive & cloud providers
│   ├── ClipboardService.cs         # Clipboard auto-clear scheduler
│   ├── TrayIconService.cs          # Integrasi Windows System Tray
│   └── ThemeManager.cs             # Sinkronisasi tema Dark/Light
├── Themes/                 # XAML Resources (Styles.xaml, Icons.xaml, BrandIcons.xaml)
├── Views/                  # Antarmuka modular:
│   ├── Credentials/        # CredentialView, QuickAutofillWindow, SshAccessPromptWindow
│   ├── Dashboard/          # FileVaultView
│   └── Settings/           # SettingsView
├── cli/                    # WhatInsides CLI Companion Tool (wi.exe)
├── tests/                  # xUnit Test Suite komprehensif (231 passing tests)
├── WhatInsides.csproj      # File proyek .NET 10 WPF
└── WhatInsides.slnx        # Solution file modern XML
```

---

## 📄 Lisensi
Proyek ini dilisensikan di bawah lisensi pribadi / tertutup untuk pengembangan WhatInsides. Seluruh hak cipta dilindungi undang-undang.

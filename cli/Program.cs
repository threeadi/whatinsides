using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;

namespace WhatInsides.Cli;

public class Program
{
    private static readonly SettingsManager SettingsMgr = new();
    private static readonly LockRegistryService RegistrySvc = new();
    private static readonly CredentialVaultService CredentialSvc = new();
    private static readonly TotpService TotpSvc = new();

    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        string[] cmdArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "status" => HandleStatus(),
                "signin" or "auth" => await HandleSigninAsync(),
                "list" or "ls" => HandleList(),
                "lock" => await HandleLockAsync(cmdArgs),
                "unlock" => await HandleUnlockAsync(cmdArgs),
                "get" => await HandleGetAsync(cmdArgs),
                "totp" => HandleTotp(cmdArgs),
                "run" => await HandleRunAsync(cmdArgs),
                "attach" => await HandleAttachAsync(cmdArgs),
                "files" or "attachments" => HandleFiles(cmdArgs),
                "export-file" or "export-attachment" => await HandleExportFileAsync(cmdArgs),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
================================================================
  WhatInsides CLI (wi) - Developer & Vault Security Tool
================================================================
USAGE:
  wi <command> [arguments] [options]

COMMANDS:
  signin, auth                 Authenticate with Windows Hello (Biometrics / PIN)
  lock <path>                  Encrypt and lock a file or directory
  unlock <path>                Decrypt and restore a locked .wisafe file
  list, ls                     Display all registered locked files and folders
  get <query>                  Retrieve credentials from the secure vault (Biometrics)
  totp <query>                 Generate a real-time 2FA / TOTP authentication token
  run -- <command> [args]      Authorize with biometrics & inject secrets into any CLI
  attach <query> <file-path>   Encrypt and attach a file to an existing credential
  files <query>                List encrypted attachments for a credential
  export-file <q> <id> <out>   Decrypt and export an attached file
  status                       Display current vault statistics and security settings
  help, -h, --help             Show this documentation

OPTIONS:
  -p, --password <pass>        Master password (prompted securely if not supplied)
  --type <aes|fast>            Locking algorithm: 'aes' (AES-256) or 'fast' (FastHide)
  --shred                      Securely overwrite and shred original unencrypted files
  -o, --out <path>             Custom destination output path when unlocking
  --password                   (with 'get') Output ONLY the secret password
  --username                   (with 'get') Output ONLY the username
  --totp                       (with 'get') Output ONLY the 2FA code
  --copy                       Copy retrieved password/TOTP directly to Windows clipboard
");
    }

    private static int HandleStatus()
    {
        Console.WriteLine("\n--- WHATINSIDES VAULT STATUS ---");
        Console.WriteLine($"Master Password Configured : {(SettingsMgr.Settings.IsPasswordConfigured ? "YES" : "NO (Default)")}");
        Console.WriteLine($"Crypto Algorithm           : {SettingsMgr.Settings.CryptoAlgorithm}");
        Console.WriteLine($"KDF Algorithm              : {SettingsMgr.Settings.KdfAlgorithm}");
        Console.WriteLine($"Auto-Lock Inactivity       : {(SettingsMgr.Settings.AutoLockMinutes > 0 ? $"{SettingsMgr.Settings.AutoLockMinutes} minutes" : "Disabled")}");
        Console.WriteLine($"Clipboard Auto-Clear       : {(SettingsMgr.Settings.ClipboardAutoClearSeconds > 0 ? $"{SettingsMgr.Settings.ClipboardAutoClearSeconds}s" : "Disabled")}");
        Console.WriteLine($"Registered Locked Items    : {RegistrySvc.LockedItems.Count}");
        Console.WriteLine($"Stored Credentials         : {CredentialSvc.GetAll().Count(x => !x.IsDeleted)} items");
        Console.WriteLine($"Explorer Shell Integration : {(ShellContextMenuService.IsContextMenuRegistered() ? "ENABLED" : "DISABLED")}");

        var (bioAvailable, bioMsg) = BiometricService.CheckAvailabilityAsync().GetAwaiter().GetResult();
        Console.WriteLine($"Windows Hello Biometrics   : {(bioAvailable ? "READY (Fingerprint / Face / PIN)" : "UNAVAILABLE (" + bioMsg + ")")}");
        Console.WriteLine("--------------------------------\n");
        return 0;
    }

    private static async Task<int> HandleSigninAsync()
    {
        ConsoleHelper.WriteInfo("Memeriksa kesiapan Windows Hello...");
        var (available, msg) = await BiometricService.CheckAvailabilityAsync();
        if (!available)
        {
            ConsoleHelper.WriteWarning($"Biometrik tidak tersedia: {msg}");
            string pass = ConsoleHelper.ReadPassword("Gunakan Master Password: ");
            if (SettingsMgr.VerifyPassword(pass))
            {
                ConsoleHelper.WriteSuccess("Autentikasi Master Password berhasil!");
                return 0;
            }
            ConsoleHelper.WriteError("Password salah!");
            return 1;
        }

        ConsoleHelper.WriteInfo("Silakan lakukan verifikasi biometrik pada prompt Windows Hello...");
        var (success, resultMsg) = await BiometricService.RequestConsentAsync("WhatInsides CLI: Sign in ke terminal");
        if (success)
        {
            ConsoleHelper.WriteSuccess("Verifikasi biometrik berhasil! Sesi CLI terautentikasi.");
            return 0;
        }

        ConsoleHelper.WriteError($"Autentikasi gagal: {resultMsg}");
        return 1;
    }

    private static int HandleList()
    {
        RegistrySvc.LoadRegistry();
        var items = RegistrySvc.LockedItems.ToList();
        if (items.Count == 0)
        {
            ConsoleHelper.WriteInfo("Tidak ada berkas yang terkunci di dalam registri WhatInsides.");
            return 0;
        }

        Console.WriteLine("\nDAFTAR BERKAS TERKUNCI:");
        Console.WriteLine(new string('-', 88));
        Console.WriteLine($"{"Tipe",-10} | {"Nama Asli",-28} | {"Status",-8} | {"Lokasi Kontainer"}");
        Console.WriteLine(new string('-', 88));

        foreach (var item in items)
        {
            string type = item.LockType ?? "Aes256";
            string name = Path.GetFileName(item.OriginalPath ?? string.Empty);
            if (name.Length > 26) name = name[..23] + "...";
            string status = item.IsLocked ? "LOCKED" : "UNLOCKED";
            string loc = item.CurrentPath ?? "-";
            if (loc.Length > 36) loc = "..." + loc[^33..];

            Console.WriteLine($"{type,-10} | {name,-28} | {status,-8} | {loc}");
        }
        Console.WriteLine(new string('-', 88));
        Console.WriteLine($"Total: {items.Count} berkas/folder terkunci.\n");
        return 0;
    }

    private static async Task<int> HandleLockAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleHelper.WriteError("Target berkas atau folder belum ditentukan.\nContoh: wi lock C:\\Secret\\data.txt");
            return 1;
        }

        string targetPath = Path.GetFullPath(args[0]);
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            ConsoleHelper.WriteError($"Target tidak ditemukan: {targetPath}");
            return 1;
        }

        string lockType = "Aes256";
        bool shred = false;
        string? password = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "-p" or "--password" && i + 1 < args.Length)
            {
                password = args[++i];
            }
            else if (args[i] is "--type" && i + 1 < args.Length)
            {
                string t = args[++i].ToLowerInvariant();
                lockType = t is "fast" or "fasthide" ? "FastHide" : "Aes256";
            }
            else if (args[i] is "--shred")
            {
                shred = true;
            }
        }

        password = ResolveMasterPassword(password);
        if (string.IsNullOrEmpty(password))
        {
            ConsoleHelper.WriteError("Password tidak boleh kosong.");
            return 1;
        }

        ConsoleHelper.WriteInfo($"Mengunci '{Path.GetFileName(targetPath)}' [{lockType}]...");
        var lockedItem = await RegistrySvc.LockItemAsync(targetPath, lockType, password, shred);
        ConsoleHelper.WriteSuccess($"Berhasil dikunci: {lockedItem.CurrentPath}");
        return 0;
    }

    private static async Task<int> HandleUnlockAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleHelper.WriteError("Target berkas .wisafe belum ditentukan.\nContoh: wi unlock C:\\Secret\\data.txt.wisafe");
            return 1;
        }

        string targetPath = Path.GetFullPath(args[0]);
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            ConsoleHelper.WriteError($"Berkas tidak ditemukan: {targetPath}");
            return 1;
        }

        string? password = null;
        string? outDir = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "-p" or "--password" && i + 1 < args.Length)
            {
                password = args[++i];
            }
            else if (args[i] is "-o" or "--out" && i + 1 < args.Length)
            {
                outDir = Path.GetFullPath(args[++i]);
            }
        }

        password = ResolveMasterPassword(password);
        if (string.IsNullOrEmpty(password))
        {
            ConsoleHelper.WriteError("Password tidak boleh kosong.");
            return 1;
        }

        ConsoleHelper.WriteInfo($"Mendekripsi '{Path.GetFileName(targetPath)}'...");
        RegistrySvc.LoadRegistry();
        var registered = RegistrySvc.LockedItems.FirstOrDefault(x => string.Equals(x.CurrentPath, targetPath, StringComparison.OrdinalIgnoreCase));
        string unlockedPath;
        if (registered != null)
        {
            await RegistrySvc.UnlockPermanentAsync(registered, password);
            unlockedPath = registered.OriginalPath ?? Path.ChangeExtension(targetPath, null);
        }
        else
        {
            string targetDir = outDir ?? Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;
            unlockedPath = await DirectLockService.DecryptDirectAsync(targetPath, password, targetDir);
        }

        ConsoleHelper.WriteSuccess($"Berhasil dibuka ke: {unlockedPath}");
        return 0;
    }

    private static async Task<int> HandleGetAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleHelper.WriteError("Kueri pencarian belum ditentukan.\nContoh: wi get github.com");
            return 1;
        }

        string query = args[0];
        bool onlyPassword = args.Contains("--password");
        bool onlyUsername = args.Contains("--username");
        bool onlyTotp = args.Contains("--totp");
        bool copyToClipboard = args.Contains("--copy");

        string? passwordArg = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "-p" && i + 1 < args.Length)
            {
                passwordArg = args[++i];
            }
        }

        CredentialSvc.Load();
        var matches = CredentialSvc.Filter(CredentialCategory.All, query).ToList();
        if (matches.Count == 0)
        {
            ConsoleHelper.WriteWarning($"Tidak ada kredensial yang cocok dengan kueri '{query}'.");
            return 1;
        }

        var item = matches.First();

        if (SettingsMgr.Settings.IsPasswordConfigured)
        {
            bool authenticated = await AuthenticateUserAsync($"Akses kredensial '{item.Title}'", passwordArg);
            if (!authenticated)
            {
                ConsoleHelper.WriteError("Otorisasi gagal! Akses ditolak.");
                return 1;
            }
        }

        if (onlyPassword)
        {
            Console.WriteLine(item.Password);
            if (copyToClipboard) ClipboardService.Copy(item.Password, "Password", SettingsMgr.Settings.ClipboardAutoClearSeconds);
            return 0;
        }

        if (onlyUsername)
        {
            Console.WriteLine(item.Username);
            if (copyToClipboard) ClipboardService.Copy(item.Username, "Username", SettingsMgr.Settings.ClipboardAutoClearSeconds);
            return 0;
        }

        if (onlyTotp)
        {
            string code = TotpSvc.GenerateCode(item.TotpSecret);
            Console.WriteLine(code);
            if (copyToClipboard) ClipboardService.Copy(code, "TOTP", SettingsMgr.Settings.ClipboardAutoClearSeconds);
            return 0;
        }

        Console.WriteLine("\n--- DETAIL KREDENSIAL ---");
        Console.WriteLine($"Judul     : {item.Title}");
        Console.WriteLine($"Kategori  : {item.Category}");
        Console.WriteLine($"Username  : {item.Username}");
        Console.WriteLine($"Password  : {item.Password}");
        if (!string.IsNullOrEmpty(item.Url)) Console.WriteLine($"URL       : {item.Url}");
        if (!string.IsNullOrEmpty(item.TotpSecret))
        {
            string currentTotp = TotpSvc.GenerateCode(item.TotpSecret);
            int rem = TotpSvc.GetRemainingSeconds();
            Console.WriteLine($"2FA/TOTP  : {TotpSvc.FormatCodeWithSpace(currentTotp)} ({rem} detik tersisa)");
        }
        if (item.Tags.Count > 0) Console.WriteLine($"Tags      : {string.Join(", ", item.Tags)}");
        if (!string.IsNullOrEmpty(item.Notes)) Console.WriteLine($"Catatan   : {item.Notes}");
        Console.WriteLine("-------------------------\n");

        if (copyToClipboard)
        {
            ClipboardService.Copy(item.Password, "Password", SettingsMgr.Settings.ClipboardAutoClearSeconds);
            ConsoleHelper.WriteSuccess("Password disalin ke clipboard!");
        }

        return 0;
    }

    private static int HandleTotp(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleHelper.WriteError("Nama akun/layanan belum ditentukan.\nContoh: wi totp github");
            return 1;
        }

        CredentialSvc.Load();
        string query = args[0];
        bool copy = args.Contains("--copy");
        var item = CredentialSvc.Filter(CredentialCategory.All, query).FirstOrDefault(x => !string.IsNullOrEmpty(x.TotpSecret));

        if (item == null)
        {
            ConsoleHelper.WriteWarning($"Tidak ada akun dengan konfigurasi TOTP 2FA untuk kueri '{query}'.");
            return 1;
        }

        string code = TotpSvc.GenerateCode(item.TotpSecret);
        int rem = TotpSvc.GetRemainingSeconds();
        Console.WriteLine($"{code} ({rem}s tersisa - {item.Title})");

        if (copy)
        {
            ClipboardService.Copy(code, "TOTP", SettingsMgr.Settings.ClipboardAutoClearSeconds);
            ConsoleHelper.WriteSuccess("Kode 2FA disalin ke clipboard!");
        }
        return 0;
    }

    private static async Task<int> HandleRunAsync(string[] args)
    {
        int separatorIdx = Array.IndexOf(args, "--");
        if (separatorIdx < 0 || separatorIdx == args.Length - 1)
        {
            ConsoleHelper.WriteError("Gunakan separator '--' untuk menjalankan perintah.\nContoh: wi run -- npm start");
            return 1;
        }

        string cmd = args[separatorIdx + 1];
        string cmdArguments = string.Join(" ", args.Skip(separatorIdx + 2));

        if (SettingsMgr.Settings.IsPasswordConfigured)
        {
            bool authenticated = await AuthenticateUserAsync($"Jalankan '{cmd}' dengan secrets WhatInsides", null);
            if (!authenticated)
            {
                ConsoleHelper.WriteError("Otorisasi biometrik/password dibatalkan. Perintah tidak dijalankan.");
                return 1;
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = cmd,
            Arguments = cmdArguments,
            UseShellExecute = false
        };

        CredentialSvc.Load();
        var activeCreds = CredentialSvc.GetAll().Where(x => !x.IsDeleted).ToList();
        foreach (var c in activeCreds)
        {
            string cleanTitle = new string(c.Title.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (!string.IsNullOrEmpty(cleanTitle))
            {
                psi.EnvironmentVariables[$"WI_{cleanTitle}_USERNAME"] = c.Username;
                psi.EnvironmentVariables[$"WI_{cleanTitle}_PASSWORD"] = c.Password;
            }
        }

        ConsoleHelper.WriteInfo($"Menjalankan '{cmd} {cmdArguments}' dengan Secrets Environment WhatInsides...");
        using var process = Process.Start(psi);
        if (process == null)
        {
            ConsoleHelper.WriteError($"Gagal memulai proses '{cmd}'.");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static async Task<int> HandleAttachAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ConsoleHelper.WriteError("Penggunaan: wi attach <query> <file-path> [-p <password>]");
            return 1;
        }

        string query = args[0];
        string filePath = Path.GetFullPath(args[1]);

        if (!File.Exists(filePath))
        {
            ConsoleHelper.WriteError($"Berkas tidak ditemukan: {filePath}");
            return 1;
        }

        CredentialSvc.Load();
        var results = CredentialSvc.Filter(CredentialCategory.All, query).ToList();
        if (results.Count == 0)
        {
            ConsoleHelper.WriteWarning($"Tidak ada kredensial yang cocok dengan query '{query}'.");
            return 1;
        }

        var target = results[0];
        string? passArg = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] is "-p" or "--password" && i + 1 < args.Length)
            {
                passArg = args[i + 1];
            }
        }

        string password = ResolveMasterPassword(passArg);
        if (!SettingsMgr.VerifyPassword(password))
        {
            ConsoleHelper.WriteError("Master password salah!");
            return 1;
        }

        ConsoleHelper.WriteInfo($"Mengenkripsi dan melampirkan '{Path.GetFileName(filePath)}' ke '{target.Title}'...");
        var attachment = await CredentialSvc.AddAttachmentAsync(target.Id, filePath, password);

        ConsoleHelper.WriteSuccess($"[OK] Lampiran berhasil disimpan!");
        Console.WriteLine($"  ID Lampiran : {attachment.Id}");
        Console.WriteLine($"  Nama Berkas : {attachment.FileName}");
        Console.WriteLine($"  Ukuran      : {attachment.FormattedSize}");
        Console.WriteLine($"  Enkripsi    : AES-256-CBC with PBKDF2 HMAC");
        return 0;
    }

    private static int HandleFiles(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleHelper.WriteError("Penggunaan: wi files <query>");
            return 1;
        }

        CredentialSvc.Load();
        string query = args[0];
        var results = CredentialSvc.Filter(CredentialCategory.All, query).ToList();
        if (results.Count == 0)
        {
            ConsoleHelper.WriteWarning($"Tidak ada kredensial yang cocok dengan query '{query}'.");
            return 1;
        }

        var target = results[0];
        Console.WriteLine($"\n--- LAMPIRAN TERENKRIPSI: {target.Title} ({target.Username}) ---");
        if (target.Attachments.Count == 0)
        {
            Console.WriteLine("Belum ada berkas yang dilampirkan pada akun ini.");
            Console.WriteLine("Gunakan 'wi attach <query> <file-path>' untuk melampirkan berkas.");
        }
        else
        {
            int index = 1;
            foreach (var a in target.Attachments)
            {
                Console.WriteLine($"[{index++}] {a.FileName}");
                Console.WriteLine($"    ID     : {a.Id}");
                Console.WriteLine($"    Ukuran : {a.FormattedSize}");
                Console.WriteLine($"    Waktu  : {a.CreatedAt:dd-MM-yyyy HH:mm:ss}");
                Console.WriteLine();
            }
        }
        Console.WriteLine("--------------------------------------------------\n");
        return 0;
    }

    private static async Task<int> HandleExportFileAsync(string[] args)
    {
        if (args.Length < 3)
        {
            ConsoleHelper.WriteError("Penggunaan: wi export-file <query> <attachment-id-or-filename> <output-path> [-p <password>]");
            return 1;
        }

        string query = args[0];
        string fileIdentifier = args[1];
        string outputPath = Path.GetFullPath(args[2]);

        CredentialSvc.Load();
        var results = CredentialSvc.Filter(CredentialCategory.All, query).ToList();
        if (results.Count == 0)
        {
            ConsoleHelper.WriteWarning($"Tidak ada kredensial yang cocok dengan query '{query}'.");
            return 1;
        }

        var target = results[0];
        var attachment = target.Attachments.FirstOrDefault(a => 
            a.Id.Equals(fileIdentifier, StringComparison.OrdinalIgnoreCase) ||
            a.FileName.Equals(fileIdentifier, StringComparison.OrdinalIgnoreCase));

        if (attachment == null)
        {
            ConsoleHelper.WriteError($"Lampiran '{fileIdentifier}' tidak ditemukan pada '{target.Title}'.");
            return 1;
        }

        string? passArg = null;
        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] is "-p" or "--password" && i + 1 < args.Length)
            {
                passArg = args[i + 1];
            }
        }

        string password = ResolveMasterPassword(passArg);
        if (!SettingsMgr.VerifyPassword(password))
        {
            ConsoleHelper.WriteError("Master password salah!");
            return 1;
        }

        ConsoleHelper.WriteInfo($"Mendekripsi lampiran '{attachment.FileName}' ke '{outputPath}'...");
        await CredentialSvc.ExportAttachmentAsync(target.Id, attachment.Id, outputPath, password);
        ConsoleHelper.WriteSuccess($"[OK] Berkas berhasil didekripsi dan diekspor ke: {outputPath}");
        return 0;
    }

    private static async Task<bool> AuthenticateUserAsync(string actionDescription, string? passArg = null)
    {
        if (!string.IsNullOrEmpty(passArg))
        {
            return SettingsMgr.VerifyPassword(passArg);
        }

        var (bioAvailable, _) = await BiometricService.CheckAvailabilityAsync();
        if (bioAvailable)
        {
            ConsoleHelper.WriteInfo("Meminta otorisasi Windows Hello (Biometrik / PIN)...");
            var (bioSuccess, bioMsg) = await BiometricService.RequestConsentAsync($"WhatInsides: {actionDescription}");
            if (bioSuccess)
            {
                ConsoleHelper.WriteSuccess("Verifikasi biometrik berhasil.");
                return true;
            }
            ConsoleHelper.WriteWarning($"{bioMsg} Mengalihkan ke Master Password...");
        }

        string pass = ConsoleHelper.ReadPassword("Masukkan Master Password: ");
        return SettingsMgr.VerifyPassword(pass);
    }

    private static string ResolveMasterPassword(string? passArg)
    {
        if (!string.IsNullOrEmpty(passArg))
            return passArg;

        return ConsoleHelper.ReadPassword();
    }

    private static int HandleUnknownCommand(string cmd)
    {
        ConsoleHelper.WriteError($"Perintah tidak dikenal: '{cmd}'");
        Console.WriteLine("Ketik 'wi help' atau 'wi --help' untuk melihat daftar perintah yang tersedia.");
        return 1;
    }
}

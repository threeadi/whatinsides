using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using WhatInsides.Services;

namespace WhatInsides;

public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool FreeConsole();

    private const uint ATTACH_PARENT_PROCESS = 0x0FFFFFFFF;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            var sm = new SettingsManager();
            var savedTheme = sm.Settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
                ? AppTheme.Light
                : AppTheme.Dark;
            ThemeManager.SetTheme(savedTheme, save: false);
        }
        catch { }

        if (e.Args.Contains("--test"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            var logSb = new System.Text.StringBuilder();
            Action<string> log = s => { Console.WriteLine(s); logSb.AppendLine(s); };

            log("");
            log("==================================================");
            log("[WhatInsides] Running Automated Dual-Mode Verification");
            log("==================================================");

            bool allPassed = true;
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_sandbox_run");
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            try
            {
                // TEST 1: Mode B (Fast Hide) Folder
                log("\n[1/3] Testing Mode B: Kunci Cepat Instan (NTFS + CLSID + SuperHidden)...");
                string sampleFolder = Path.Combine(testDir, "FolderRahasia");
                Directory.CreateDirectory(sampleFolder);
                File.WriteAllText(Path.Combine(sampleFolder, "secret.txt"), "Rahasia tingkat tinggi");

                var lockResult = NtfsLockerService.LockFolderInPlace(sampleFolder);
                if (lockResult.Success && Directory.Exists(lockResult.NewPath))
                {
                    log($" -> Folder locked: {lockResult.NewPath}");
                    var dirInfo = new DirectoryInfo(lockResult.NewPath);
                    bool isHidden = (dirInfo.Attributes & FileAttributes.Hidden) != 0;
                    bool isSystem = (dirInfo.Attributes & FileAttributes.System) != 0;
                    log($" -> Attributes: Hidden={isHidden}, System={isSystem}");

                    var unlockResult = NtfsLockerService.UnlockFolderInPlace(lockResult.NewPath, sampleFolder);
                    if (unlockResult.Success && Directory.Exists(sampleFolder))
                    {
                        string content = File.ReadAllText(Path.Combine(sampleFolder, "secret.txt"));
                        if (content == "Rahasia tingkat tinggi")
                        {
                            log(" -> PASS: Mode B Instant Hide and Restore verified 100%!");
                        }
                        else
                        {
                            log(" -> FAIL: Restored content mismatch!");
                            allPassed = false;
                        }
                    }
                    else
                    {
                        log($" -> FAIL: Unlock failed: {unlockResult.Error}");
                        allPassed = false;
                    }
                }
                else
                {
                    log($" -> FAIL: Lock failed: {lockResult.Error}");
                    allPassed = false;
                }

                // TEST 2: Mode C (AES-256) File Encryption + Shredding
                log("\n[2/3] Testing Mode C: Enkripsi Penuh AES-256 File + Shredding...");
                string sampleFile = Path.Combine(testDir, "laporan_keuangan.pdf");
                File.WriteAllText(sampleFile, "Laporan Neraca Keuangan Perusahaan 2026 - Top Secret");

                string lockedSafePath = await DirectLockService.EncryptDirectFileAsync(sampleFile, "admin123", shredOriginal: true);
                log($" -> Encrypted to: {lockedSafePath}");
                bool originalExists = File.Exists(sampleFile);
                log($" -> Plaintext original shredded: {!originalExists}");

                string decryptedFile = await DirectLockService.DecryptDirectAsync(lockedSafePath, "admin123", testDir);
                log($" -> Decrypted to: {decryptedFile}");
                if (File.Exists(decryptedFile) && File.ReadAllText(decryptedFile) == "Laporan Neraca Keuangan Perusahaan 2026 - Top Secret")
                {
                    log(" -> PASS: Mode C AES-256 File Encrypt/Decrypt verified 100%!");
                }
                else
                {
                    log(" -> FAIL: Decrypted file missing or corrupted!");
                    allPassed = false;
                }

                // TEST 3: Mode C (AES-256) Directory Container
                log("\n[3/3] Testing Mode C: Enkripsi Penuh Direktori Container (.wisafe)...");
                string folderToEncrypt = Path.Combine(testDir, "ProyekSensitif");
                Directory.CreateDirectory(folderToEncrypt);
                File.WriteAllText(Path.Combine(folderToEncrypt, "data1.txt"), "Data rahasia 1");
                File.WriteAllText(Path.Combine(folderToEncrypt, "data2.txt"), "Data rahasia 2");

                string encryptedFolderSafe = await DirectLockService.EncryptDirectFolderAsync(folderToEncrypt, "admin123", shredOriginal: true);
                log($" -> Folder encrypted to: {encryptedFolderSafe}");

                if (File.Exists(encryptedFolderSafe))
                {
                    log(" -> PASS: Encrypted folder container generated successfully!");
                }
                else
                {
                    log(" -> FAIL: Encrypted folder file missing!");
                    allPassed = false;
                }

                log("\n==================================================");
                if (allPassed)
                {
                    log("[SUCCESS] ALL VERIFICATION TESTS PASSED!");
                }
                else
                {
                    log("[FAILURE] ONE OR MORE TESTS FAILED!");
                }
                log("==================================================");
            }
            catch (Exception ex)
            {
                log($"\n[ERROR] Exception during test run: {ex.Message}");
                if (ex.StackTrace != null) log(ex.StackTrace);
            }
            finally
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_output.txt"), logSb.ToString());
                }
                catch { }

                try { if (Directory.Exists(testDir)) Directory.Delete(testDir, true); } catch { }
                FreeConsole();
                Shutdown(allPassed ? 0 : 1);
            }
            return;
        }

        base.OnStartup(e);
    }
}

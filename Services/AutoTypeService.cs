using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using WhatInsides.Models;

namespace WhatInsides.Services;

public enum AutoTypeSequence
{
    UsernameTabPassword,
    UsernameTabPasswordEnter,
    PasswordOnly,
    PasswordEnter,
    TotpOnly
}

public sealed class AutoTypeService : IDisposable
{
    private readonly CredentialVaultService _vaultService;
    private readonly SettingsManager _settingsManager;
    private readonly Func<string?> _getMasterPassword;
    private readonly TotpService _totpService;

    private IntPtr _registeredHwnd = IntPtr.Zero;
    private bool _isDisposed = false;

    public const int HOTKEY_AUTOTYPE_ID = 9001;
    public const int HOTKEY_SPOTLIGHT_ID = 9002;

    public event Action<TargetWindowInfo, List<CredentialItem>>? RequestAutofillPicker;
    public event Action<string, string>? ToastNotification;

    #region Win32 API Interop

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint VK_TAB = 0x09;
    private const uint VK_RETURN = 0x0D;
    private const uint VK_ESCAPE = 0x1B;
    private const uint VK_BACK = 0x08;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

    #endregion

    public AutoTypeService(
        CredentialVaultService vaultService,
        SettingsManager settingsManager,
        Func<string?> getMasterPassword,
        TotpService totpService)
    {
        _vaultService = vaultService;
        _settingsManager = settingsManager;
        _getMasterPassword = getMasterPassword;
        _totpService = totpService;
    }

    public bool RegisterGlobalHotkeys(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return false;
        _registeredHwnd = windowHandle;

        bool reg1 = RegisterHotKey(windowHandle, HOTKEY_AUTOTYPE_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x41); // Ctrl + Alt + A
        bool reg2 = RegisterHotKey(windowHandle, HOTKEY_SPOTLIGHT_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 0x20); // Ctrl + Shift + Space

        return reg1 || reg2;
    }

    public void UnregisterGlobalHotkeys()
    {
        if (_registeredHwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_registeredHwnd, HOTKEY_AUTOTYPE_ID);
            UnregisterHotKey(_registeredHwnd, HOTKEY_SPOTLIGHT_ID);
            _registeredHwnd = IntPtr.Zero;
        }
    }

    public TargetWindowInfo GetForegroundTargetWindow(IntPtr excludeHwnd)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == excludeHwnd)
        {
            return new TargetWindowInfo(IntPtr.Zero, string.Empty, string.Empty);
        }

        var sb = new StringBuilder(512);
        GetWindowText(fg, sb, sb.Capacity);
        string title = sb.ToString();

        string processName = string.Empty;
        GetWindowThreadProcessId(fg, out uint pid);
        if (pid != 0)
        {
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch { }
        }

        return new TargetWindowInfo(fg, title, processName);
    }

    public List<CredentialItem> FindMatchingCredentials(TargetWindowInfo target)
    {
        var all = _vaultService.GetAll();
        if (all.Count == 0) return new List<CredentialItem>();

        string cleanTitle = CleanWindowTitle(target.Title);
        string process = target.ProcessName.ToLowerInvariant();
        string windowFull = $"{cleanTitle} {process}".ToLowerInvariant();

        var matches = new List<(CredentialItem item, int score)>();

        foreach (var item in all)
        {
            int score = CalculateMatchScore(item, cleanTitle, process, windowFull);
            if (score > 0)
            {
                matches.Add((item, score));
            }
        }

        return matches
            .OrderByDescending(m => m.score)
            .ThenByDescending(m => m.item.IsFavorite)
            .ThenBy(m => m.item.Title)
            .Select(m => m.item)
            .ToList();
    }

    public static string CleanWindowTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return string.Empty;

        // Strip common browser window suffixes to extract web page or app title
        string[] browserSuffixes = {
            " - Google Chrome",
            " - Mozilla Firefox",
            " - Microsoft Edge",
            " - Brave",
            " - Opera",
            " - Vivaldi"
        };

        string title = rawTitle.Trim();
        foreach (var s in browserSuffixes)
        {
            if (title.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^s.Length].Trim();
                break;
            }
        }

        return title.Trim();
    }

    public static int CalculateMatchScore(CredentialItem item, string cleanTitle, string process, string windowFull)
    {
        int score = 0;
        string itemTitle = item.Title.Trim().ToLowerInvariant();
        string domain = item.DomainOnly.Trim().ToLowerInvariant();
        string url = (item.Url ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(itemTitle) && string.IsNullOrEmpty(domain)) return 0;

        // Exact Title Match with process or window
        if (!string.IsNullOrEmpty(itemTitle))
        {
            if (process.Equals(itemTitle, StringComparison.OrdinalIgnoreCase)) score += 100;
            if (cleanTitle.Equals(itemTitle, StringComparison.OrdinalIgnoreCase)) score += 90;
            if (windowFull.Contains(itemTitle)) score += 60;
            if (itemTitle.Contains(process) && process.Length >= 3) score += 50;
        }

        // Domain / URL matching
        if (!string.IsNullOrEmpty(domain) && domain.Length >= 3)
        {
            string cleanDomain = domain.Replace(".com", "").Replace(".co.id", "").Replace(".app", "").Replace(".org", "");
            if (windowFull.Contains(domain)) score += 80;
            else if (windowFull.Contains(cleanDomain)) score += 70;
        }

        // Tag matching
        if (item.Tags.Count > 0)
        {
            foreach (var t in item.Tags)
            {
                string tagClean = t.TrimStart('#').ToLowerInvariant();
                if (tagClean.Length >= 3 && windowFull.Contains(tagClean))
                {
                    score += 40;
                }
            }
        }

        // Brand preset matching
        string effectiveBrand = item.GetEffectiveBrandKey().ToLowerInvariant();
        if (!string.IsNullOrEmpty(effectiveBrand) && effectiveBrand != "auto")
        {
            if (windowFull.Contains(effectiveBrand)) score += 50;
            if (process.Contains(effectiveBrand)) score += 60;
        }

        // Slight bonus for favorites
        if (item.IsFavorite && score > 0) score += 5;

        return score;
    }

    public async Task<bool> PerformAutoTypeAsync(
        IntPtr targetHwnd,
        CredentialItem item,
        AutoTypeSequence sequence = AutoTypeSequence.UsernameTabPassword)
    {
        if (targetHwnd == IntPtr.Zero || item == null) return false;

        // 1. Bring target application window to foreground
        SetForegroundWindow(targetHwnd);
        await Task.Delay(140); // Allow OS to switch input focus

        // 2. Build keystroke stream based on requested sequence
        switch (sequence)
        {
            case AutoTypeSequence.UsernameTabPassword:
                await TypeTextAsync(item.Username);
                await SendVirtualKeyAsync(VK_TAB);
                await Task.Delay(60);
                await TypeTextAsync(item.Password);
                break;

            case AutoTypeSequence.UsernameTabPasswordEnter:
                await TypeTextAsync(item.Username);
                await SendVirtualKeyAsync(VK_TAB);
                await Task.Delay(60);
                await TypeTextAsync(item.Password);
                await Task.Delay(60);
                await SendVirtualKeyAsync(VK_RETURN);
                break;

            case AutoTypeSequence.PasswordOnly:
                await TypeTextAsync(item.Password);
                break;

            case AutoTypeSequence.PasswordEnter:
                await TypeTextAsync(item.Password);
                await Task.Delay(60);
                await SendVirtualKeyAsync(VK_RETURN);
                break;

            case AutoTypeSequence.TotpOnly:
                if (item.HasTotp)
                {
                    string code = _totpService.GenerateCode(item.TotpSecret);
                    await TypeTextAsync(code);
                }
                break;
        }

        return true;
    }

    private static async Task TypeTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        foreach (char c in text)
        {
            var down = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var up = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
            await Task.Delay(16); // Safe typing cadence (approx 60 WPM keystroke spacing)
        }
    }

    private static async Task SendVirtualKeyAsync(uint vk)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
        await Task.Delay(30);
    }

    public void HandleHotkeyMessage(int hotkeyId, IntPtr appMainWindowHandle)
    {
        var target = GetForegroundTargetWindow(appMainWindowHandle);
        var matches = FindMatchingCredentials(target);

        if (hotkeyId == HOTKEY_AUTOTYPE_ID && matches.Count == 1 && target.Hwnd != IntPtr.Zero)
        {
            // Direct 1-to-1 Match: Perform instant Auto-Type!
            var singleMatch = matches[0];
            _ = Task.Run(async () =>
            {
                await PerformAutoTypeAsync(target.Hwnd, singleMatch, AutoTypeSequence.UsernameTabPassword);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ToastNotification?.Invoke("Autofill Berhasil", $"Kredensial '{singleMatch.Title}' diisi ke {target.ProcessName}.");
                });
            });
        }
        else
        {
            // 0 matches, or >1 matches, or Spotlight hotkey: Show Quick Autofill Picker
            RequestAutofillPicker?.Invoke(target, matches);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            UnregisterGlobalHotkeys();
            _isDisposed = true;
        }
    }
}

public record TargetWindowInfo(IntPtr Hwnd, string Title, string ProcessName)
{
    public bool HasTarget => Hwnd != IntPtr.Zero && (!string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(ProcessName));
    public string DisplaySummary => string.IsNullOrEmpty(Title) ? ProcessName : (string.IsNullOrEmpty(ProcessName) ? Title : $"{ProcessName} ({Title})");
}

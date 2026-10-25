using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;

namespace WhatInsides.Services;

public static class BiometricService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetConsoleWindow();

    /// <summary>
    /// Checks whether Windows Hello (Biometrics: Fingerprint / Facial Recognition / PIN) is available on this PC.
    /// </summary>
    public static async Task<(bool Available, string Status)> CheckAvailabilityAsync()
    {
        try
        {
            var ucvResult = await UserConsentVerifier.CheckAvailabilityAsync();
            return ucvResult switch
            {
                UserConsentVerifierAvailability.Available => (true, "Windows Hello (Biometrik / PIN) Siap Digunakan"),
                UserConsentVerifierAvailability.DeviceNotPresent => (false, "Sensor biometrik tidak ditemukan pada perangkat ini."),
                UserConsentVerifierAvailability.NotConfiguredForUser => (false, "Windows Hello belum dikonfigurasi di Windows Settings."),
                UserConsentVerifierAvailability.DisabledByPolicy => (false, "Windows Hello dinonaktifkan oleh kebijakan sistem (Group Policy)."),
                UserConsentVerifierAvailability.DeviceBusy => (false, "Sensor biometrik sedang sibuk."),
                _ => (false, $"Status: {ucvResult}")
            };
        }
        catch (Exception ex)
        {
            return (false, $"Gagal memeriksa biometrik: {ex.Message}");
        }
    }

    /// <summary>
    /// Prompts the user with native Windows Hello (Fingerprint / Face / PIN) dialog attached to the window.
    /// Returns true if verified successfully.
    /// </summary>
    public static async Task<(bool Success, string Message)> RequestConsentAsync(
        string prompt = "WhatInsides: Otorisasi akses ke brankas rahasia",
        IntPtr windowHandle = default)
    {
        try
        {
            IntPtr hwnd = windowHandle;
            if (hwnd == IntPtr.Zero) hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) hwnd = GetConsoleWindow();

            UserConsentVerificationResult verificationResult;
            if (hwnd != IntPtr.Zero)
            {
                verificationResult = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(hwnd, prompt);
            }
            else
            {
                verificationResult = await UserConsentVerifier.RequestVerificationAsync(prompt);
            }

            return verificationResult switch
            {
                UserConsentVerificationResult.Verified => (true, "Verifikasi biometrik berhasil."),
                UserConsentVerificationResult.Canceled => (false, "Verifikasi dibatalkan oleh pengguna."),
                UserConsentVerificationResult.DeviceNotPresent => (false, "Sensor biometrik tidak tersedia."),
                UserConsentVerificationResult.NotConfiguredForUser => (false, "Windows Hello belum dikonfigurasi di Windows Settings."),
                UserConsentVerificationResult.DeviceBusy => (false, "Sensor biometrik sedang sibuk."),
                _ => (false, "Verifikasi biometrik gagal.")
            };
        }
        catch (Exception ex)
        {
            string detail = string.IsNullOrWhiteSpace(ex.Message)
                ? $"Kode 0x{ex.HResult:X8} ({ex.GetType().Name})"
                : ex.Message;
            return (false, $"Error saat memanggil Windows Hello: {detail}");
        }
    }
}

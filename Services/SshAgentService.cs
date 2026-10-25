using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WhatInsides.Models;
using WhatInsides.Views.Credentials;

namespace WhatInsides.Services;

public sealed class SshAgentService
{
    private readonly HashSet<string> _approvedSessions = new(StringComparer.OrdinalIgnoreCase);
    private bool _approveAllApplications = false;

    public event Action<string, string>? ToastNotification;

    public bool IsApproved(string appName)
    {
        return _approveAllApplications || _approvedSessions.Contains(appName);
    }

    public void RevokeAllApprovals()
    {
        _approveAllApplications = false;
        _approvedSessions.Clear();
    }

    public async Task<bool> RequestAccessAsync(
        string appName,
        string keyTitle,
        string keyType = "id_ed25519",
        string? fingerprint = null)
    {
        if (IsApproved(appName))
        {
            return true;
        }

        string effectiveFingerprint = string.IsNullOrWhiteSpace(fingerprint)
            ? GenerateMockFingerprint(keyTitle)
            : fingerprint;

        var tcs = new TaskCompletionSource<bool>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var prompt = new SshAccessPromptWindow(appName, keyTitle, keyType, effectiveFingerprint);
            
            prompt.AccessDecided += (approved, rememberAll) =>
            {
                if (approved)
                {
                    if (rememberAll)
                    {
                        _approveAllApplications = true;
                    }
                    else
                    {
                        _approvedSessions.Add(appName);
                    }
                }
                tcs.TrySetResult(approved);
            };

            prompt.Show();
            prompt.Activate();
            prompt.Focus();
        });

        bool result = await tcs.Task;

        if (result)
        {
            ToastNotification?.Invoke("Akses SSH Diberikan", $"'{appName}' diizinkan menggunakan '{keyTitle}'.");
        }
        else
        {
            ToastNotification?.Invoke("Akses SSH Ditolak", $"Permintaan akses dari '{appName}' dibatalkan.");
        }

        return result;
    }

    public async Task<bool> SimulateAccessRequestAsync(CredentialItem? item = null, string? appName = null)
    {
        string targetApp = string.IsNullOrWhiteSpace(appName) ? "Windows Terminal" : appName;
        string keyTitle = item?.Title ?? "GitHub SSH Key";
        string keyType = item?.Category == CredentialCategory.DeveloperServer ? "id_ed25519" : "id_rsa";
        string fingerprint = GenerateMockFingerprint(keyTitle + (item?.Username ?? "git"));

        return await RequestAccessAsync(targetApp, keyTitle, keyType, fingerprint);
    }

    private static string GenerateMockFingerprint(string seed)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        string b64 = Convert.ToBase64String(hash).TrimEnd('=');
        if (b64.Length > 24) b64 = b64[..24];
        return $"SHA256:{b64}...";
    }
}

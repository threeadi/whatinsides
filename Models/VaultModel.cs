using System;
using System.IO;

namespace WhatInsides.Models;

public class VaultModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Secure Vault";
    public string EncryptedStoragePath { get; set; } = string.Empty;
    public string? UnlockedDirectory { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.Now;
    public string EncryptionAlgorithm { get; set; } = "AES-256-GCM";
    public string KdfAlgorithm { get; set; } = "Argon2id (PBKDF2-SHA256)";
    public bool IsLocked { get; set; } = true;
    public int FileCount { get; set; } = 0;
    public long TotalBytes { get; set; } = 0;
    public string CloudProvider { get; set; } = "None"; // "None", "OneDrive", "Google Drive"

    public string PasswordSalt { get; set; } = string.Empty;
    public string PasswordVerifier { get; set; } = string.Empty;
    public string PasswordHint { get; set; } = string.Empty;
    public string KeyFileHash { get; set; } = string.Empty; // For key file authentication

    public string FormattedLastActivity => GetRelativeTime(LastActivity);
    public string FormattedTotalSize => FormatBytes(TotalBytes);

    private static string GetRelativeTime(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"Today, {time:h:mm tt}";
        if (diff.TotalDays < 1) return $"Today, {time:h:mm tt}";
        if (diff.TotalDays < 2) return $"Yesterday, {time:h:mm tt}";
        return time.ToString("MMM dd, yyyy");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
            if (counter >= suffixes.Length - 1) break;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

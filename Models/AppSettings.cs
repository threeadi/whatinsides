using System;

namespace WhatInsides.Models;

public class AppSettings
{
    public string PasswordSalt { get; set; } = string.Empty;
    public string PasswordVerifier { get; set; } = string.Empty; // Hash used to verify master password
    public string PasswordHint { get; set; } = string.Empty;
    public string BiometricKeyBlob { get; set; } = string.Empty;
    public bool IsPasswordConfigured { get; set; } = false;

    public int AutoLockMinutes { get; set; } = 5; // 0 = off, 1, 5, 15, 30
    public bool SecureShredOnLock { get; set; } = true;
    public int ShredPassCount { get; set; } = 1;
    public string CustomVaultPath { get; set; } = string.Empty;
    public bool MinimizeToTray { get; set; } = false;
    public bool AutoLockOnMinimize { get; set; } = false;
    public bool LockOnSleep { get; set; } = true;
    public string CryptoAlgorithm { get; set; } = "AES-256-GCM";
    public string KdfAlgorithm { get; set; } = "Argon2id";
    public string Theme { get; set; } = "Dark";
    public int ClipboardAutoClearSeconds { get; set; } = 30; // 0 = never, 15, 30, 60
    public bool PreventScreenCapture { get; set; } = true;

    public bool CloudAutoUpload { get; set; } = true;
    public string CloudBackupFolder { get; set; } = string.Empty;
    public bool CloudSyncWifiOnly { get; set; } = true;
    public bool CloudZeroKnowledgeE2E { get; set; } = true;
    public bool CloudBackupCredentialsVault { get; set; } = true;
}

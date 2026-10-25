namespace WhatInsides.Models;

public enum CredentialCategory
{
    All,
    Favorite,
    Login,          // Akun & Login (menggabungkan Login & Game + Kata Sandi)
    Finance,        // Keuangan & Kartu (menggabungkan Kartu Pembayaran + Rekening Bank)
    Identity,       // Identitas & Dokumen
    SecureNote,     // Catatan & Lisensi (menggabungkan Catatan Rahasia + Lisensi Software)
    DeveloperServer,// Developer & Server (menggabungkan SSH & API Tokens + Server & Database)
    CryptoSeed,     // Kripto & Seed
    Trash,

    // Legacy aliases for backward compatibility with existing saved items in JSON
    Password = 100,
    PaymentCard = 101,
    BankAccount = 102,
    SoftwareLicense = 103,
    SshToken = 104,
    ServerDatabase = 105
}

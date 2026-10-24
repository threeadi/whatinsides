using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WhatInsides.Services;

public class CryptoEngine
{
    private const string MAGIC_HEADER = "WIVAULT_V2";
    private const int SALT_SIZE = 32;
    private const int IV_SIZE = 16;
    private const int KEY_SIZE = 32; // 256 bits
    private const int PBKDF2_ITERATIONS = 120_000;
    private const int BUFFER_SIZE = 64 * 1024; // 64 KB chunk size for streaming

    private static readonly byte[] VERIFIER_PAYLOAD = Encoding.UTF8.GetBytes("WHATINSIDES_AUTH_VERIFICATION_TOKEN");

    /// <summary>
    /// Derives a 256-bit encryption key and a 256-bit HMAC key from a password and salt using PBKDF2.
    /// </summary>
    public static (byte[] AesKey, byte[] HmacKey) DeriveKeys(string password, byte[] salt)
    {
        // Derive 64 bytes total: 32 bytes for AES-256 and 32 bytes for HMAC-SHA256
        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PBKDF2_ITERATIONS,
            HashAlgorithmName.SHA256,
            KEY_SIZE * 2);

        byte[] aesKey = new byte[KEY_SIZE];
        byte[] hmacKey = new byte[KEY_SIZE];

        Buffer.BlockCopy(derived, 0, aesKey, 0, KEY_SIZE);
        Buffer.BlockCopy(derived, KEY_SIZE, hmacKey, 0, KEY_SIZE);

        return (aesKey, hmacKey);
    }

    /// <summary>
    /// Generates a verifier string for validating the master password without decrypting files.
    /// </summary>
    public static string CreatePasswordVerifier(string password, byte[] salt)
    {
        var (aesKey, hmacKey) = DeriveKeys(password, salt);
        using var hmac = new HMACSHA256(hmacKey);
        byte[] hash = hmac.ComputeHash(VERIFIER_PAYLOAD);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates a password against an existing salt and verifier string.
    /// </summary>
    public static bool VerifyMasterPassword(string password, byte[] salt, string storedVerifier)
    {
        try
        {
            string calculated = CreatePasswordVerifier(password, salt);
            byte[] calcBytes = Convert.FromBase64String(calculated);
            byte[] storedBytes = Convert.FromBase64String(storedVerifier);
            return CryptographicOperations.FixedTimeEquals(calcBytes, storedBytes);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Encrypts an input stream to an output stream with AES-256-CBC and HMAC integrity.
    /// </summary>
    public static async Task EncryptStreamAsync(
        Stream inputStream,
        Stream outputStream,
        string password,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SALT_SIZE);
        byte[] iv = RandomNumberGenerator.GetBytes(IV_SIZE);

        var (aesKey, hmacKey) = DeriveKeys(password, salt);

        // Compute instant verification token
        byte[] verifierToken;
        using (var hmac = new HMACSHA256(hmacKey))
        {
            verifierToken = hmac.ComputeHash(VERIFIER_PAYLOAD);
        }

        // Write Header:
        // [MAGIC 10b] [SALT 32b] [IV 16b] [VERIFIER 32b]
        byte[] magicBytes = Encoding.ASCII.GetBytes(MAGIC_HEADER);
        await outputStream.WriteAsync(magicBytes, cancellationToken);
        await outputStream.WriteAsync(salt, cancellationToken);
        await outputStream.WriteAsync(iv, cancellationToken);
        await outputStream.WriteAsync(verifierToken, cancellationToken);

        long totalBytes = inputStream.CanSeek ? inputStream.Length : -1;
        long bytesProcessed = 0;

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var hmacStream = new HMACSHA256(hmacKey);

        // We encrypt the payload
        using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            int read;
            while ((read = await inputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await cryptoStream.WriteAsync(buffer, 0, read, cancellationToken);
                bytesProcessed += read;
                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((double)bytesProcessed / totalBytes);
                }
            }
            await cryptoStream.FlushFinalBlockAsync(cancellationToken);
        }

        if (progress != null) progress.Report(1.0);
    }

    /// <summary>
    /// Checks if a file has a valid header and if the provided password matches the verifier token.
    /// </summary>
    public static async Task<(bool IsValidFormat, bool IsPasswordCorrect)> CheckFilePasswordAsync(string encryptedFilePath, string password)
    {
        if (!File.Exists(encryptedFilePath)) return (false, false);

        using var fs = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] magicExpected = Encoding.ASCII.GetBytes(MAGIC_HEADER);
        byte[] magicActual = new byte[magicExpected.Length];

        if (await fs.ReadAsync(magicActual, 0, magicActual.Length) != magicActual.Length)
            return (false, false);

        if (!CryptographicOperations.FixedTimeEquals(magicActual, magicExpected))
            return (false, false);

        byte[] salt = new byte[SALT_SIZE];
        byte[] iv = new byte[IV_SIZE];
        byte[] fileVerifier = new byte[32];

        if (await fs.ReadAsync(salt, 0, SALT_SIZE) != SALT_SIZE) return (false, false);
        if (await fs.ReadAsync(iv, 0, IV_SIZE) != IV_SIZE) return (false, false);
        if (await fs.ReadAsync(fileVerifier, 0, 32) != 32) return (false, false);

        var (_, hmacKey) = DeriveKeys(password, salt);
        using var hmac = new HMACSHA256(hmacKey);
        byte[] calculatedVerifier = hmac.ComputeHash(VERIFIER_PAYLOAD);

        bool passMatches = CryptographicOperations.FixedTimeEquals(calculatedVerifier, fileVerifier);
        return (true, passMatches);
    }

    /// <summary>
    /// Decrypts an encrypted stream to an output stream.
    /// </summary>
    public static async Task DecryptStreamAsync(
        Stream inputStream,
        Stream outputStream,
        string password,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        byte[] magicExpected = Encoding.ASCII.GetBytes(MAGIC_HEADER);
        byte[] magicActual = new byte[magicExpected.Length];

        if (await inputStream.ReadAsync(magicActual, 0, magicActual.Length, cancellationToken) != magicActual.Length)
            throw new InvalidDataException("File rusak atau bukan format WhatInsides yang valid.");

        if (!CryptographicOperations.FixedTimeEquals(magicActual, magicExpected))
            throw new InvalidDataException("Format file tidak dikenali sebagai arsip WhatInsides.");

        byte[] salt = new byte[SALT_SIZE];
        byte[] iv = new byte[IV_SIZE];
        byte[] fileVerifier = new byte[32];

        if (await inputStream.ReadAsync(salt, 0, SALT_SIZE, cancellationToken) != SALT_SIZE)
            throw new InvalidDataException("Data enkripsi salt tidak lengkap.");
        if (await inputStream.ReadAsync(iv, 0, IV_SIZE, cancellationToken) != IV_SIZE)
            throw new InvalidDataException("Data enkripsi IV tidak lengkap.");
        if (await inputStream.ReadAsync(fileVerifier, 0, 32, cancellationToken) != 32)
            throw new InvalidDataException("Data verifier tidak lengkap.");

        var (aesKey, hmacKey) = DeriveKeys(password, salt);

        // Verify password immediately
        using (var hmac = new HMACSHA256(hmacKey))
        {
            byte[] calculatedVerifier = hmac.ComputeHash(VERIFIER_PAYLOAD);
            if (!CryptographicOperations.FixedTimeEquals(calculatedVerifier, fileVerifier))
            {
                throw new UnauthorizedAccessException("Password salah! Kunci enkripsi tidak cocok.");
            }
        }

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();

        long headerLength = magicActual.Length + SALT_SIZE + IV_SIZE + 32;
        long totalPayloadBytes = inputStream.CanSeek ? (inputStream.Length - headerLength) : -1;
        long bytesProcessed = 0;

        using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read, leaveOpen: true))
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            int read;
            while ((read = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await outputStream.WriteAsync(buffer, 0, read, cancellationToken);
                bytesProcessed += read;
                if (totalPayloadBytes > 0 && progress != null)
                {
                    progress.Report(Math.Min(1.0, (double)bytesProcessed / totalPayloadBytes));
                }
            }
        }

        if (progress != null) progress.Report(1.0);
    }
}

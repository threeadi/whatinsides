using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class CryptoEngineTests
{
    [Fact]
    public void DeriveKeys_GeneratesConsistentKeys_ForSamePasswordAndSalt()
    {
        string password = "StrongMasterPassword!@#123";
        byte[] salt = new byte[32];
        salt[0] = 0xAA;
        salt[31] = 0xBB;

        var (aesKey1, hmacKey1) = CryptoEngine.DeriveKeys(password, salt);
        var (aesKey2, hmacKey2) = CryptoEngine.DeriveKeys(password, salt);

        Assert.Equal(32, aesKey1.Length);
        Assert.Equal(32, hmacKey1.Length);
        Assert.Equal(aesKey1, aesKey2);
        Assert.Equal(hmacKey1, hmacKey2);
    }

    [Fact]
    public void DeriveKeys_GeneratesDifferentKeys_ForDifferentSalts()
    {
        string password = "MasterPassword";
        byte[] salt1 = new byte[32];
        salt1[0] = 0x01;
        byte[] salt2 = new byte[32];
        salt2[0] = 0x02;

        var (aesKey1, _) = CryptoEngine.DeriveKeys(password, salt1);
        var (aesKey2, _) = CryptoEngine.DeriveKeys(password, salt2);

        Assert.NotEqual(aesKey1, aesKey2);
    }

    [Fact]
    public void VerifyMasterPassword_WithCorrectPassword_ReturnsTrue()
    {
        string password = "SecretMasterPassword";
        byte[] salt = RandomNumberGenerator.GetBytes(32);

        string verifier = CryptoEngine.CreatePasswordVerifier(password, salt);
        bool isValid = CryptoEngine.VerifyMasterPassword(password, salt, verifier);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyMasterPassword_WithWrongPassword_ReturnsFalse()
    {
        string password = "SecretMasterPassword";
        byte[] salt = RandomNumberGenerator.GetBytes(32);

        string verifier = CryptoEngine.CreatePasswordVerifier(password, salt);
        bool isValid = CryptoEngine.VerifyMasterPassword("WrongPassword123", salt, verifier);

        Assert.False(isValid);
    }

    [Fact]
    public async Task EncryptAndDecryptStream_Roundtrip_RestoresExactData()
    {
        string password = "VaultKey9988!";
        string originalText = "WhatInsides Confidential Vault Document - Top Secret Data 2026";
        byte[] originalBytes = Encoding.UTF8.GetBytes(originalText);

        using var plainInputStream = new MemoryStream(originalBytes);
        using var cipherStream = new MemoryStream();

        // 1. Encrypt
        await CryptoEngine.EncryptStreamAsync(plainInputStream, cipherStream, password);
        byte[] cipherBytes = cipherStream.ToArray();

        // Cipher data must be larger than plaintext due to magic header (10b) + salt (32b) + IV (16b) + verifier (32b) + padding
        Assert.True(cipherBytes.Length > originalBytes.Length);

        // 2. Decrypt
        cipherStream.Position = 0;
        using var decryptedStream = new MemoryStream();
        await CryptoEngine.DecryptStreamAsync(cipherStream, decryptedStream, password);

        string recoveredText = Encoding.UTF8.GetString(decryptedStream.ToArray());
        Assert.Equal(originalText, recoveredText);
    }

    [Fact]
    public async Task EncryptAndDecrypt_WithLargeData_ReportsProgressAndMatchesContent()
    {
        string password = "LargePayloadPassword";
        // Create 256 KB random payload
        byte[] payload = new byte[256 * 1024];
        RandomNumberGenerator.Fill(payload);

        using var plainInputStream = new MemoryStream(payload);
        using var cipherStream = new MemoryStream();

        double lastProgress = 0;
        var progress = new Progress<double>(p => lastProgress = p);

        await CryptoEngine.EncryptStreamAsync(plainInputStream, cipherStream, password, progress);
        Assert.True(lastProgress >= 0.99);

        // Decrypt
        cipherStream.Position = 0;
        using var decryptedStream = new MemoryStream();
        await CryptoEngine.DecryptStreamAsync(cipherStream, decryptedStream, password);

        Assert.Equal(payload, decryptedStream.ToArray());
    }

    [Fact]
    public async Task DecryptStream_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        string password = "CorrectPassword123";
        byte[] data = Encoding.UTF8.GetBytes("Important Database Backup Content");

        using var plainInputStream = new MemoryStream(data);
        using var cipherStream = new MemoryStream();
        await CryptoEngine.EncryptStreamAsync(plainInputStream, cipherStream, password);

        cipherStream.Position = 0;
        using var decryptedStream = new MemoryStream();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await CryptoEngine.DecryptStreamAsync(cipherStream, decryptedStream, "IncorrectPassword");
        });
    }

    [Fact]
    public async Task DecryptStream_WithCorruptedHeader_ThrowsInvalidDataException()
    {
        byte[] corruptedData = new byte[128];
        RandomNumberGenerator.Fill(corruptedData);

        using var inputStream = new MemoryStream(corruptedData);
        using var outputStream = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await CryptoEngine.DecryptStreamAsync(inputStream, outputStream, "AnyPassword");
        });
    }

    [Fact]
    public async Task CheckFilePasswordAsync_ValidatesFileHeaderAndPasswordAccurately()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"wi_test_check_{Guid.NewGuid():N}.wisafe");
        try
        {
            string password = "CheckPassword123";
            byte[] content = Encoding.UTF8.GetBytes("Check File Password Payload");

            using (var plainStream = new MemoryStream(content))
            using (var fs = File.Create(tempFile))
            {
                await CryptoEngine.EncryptStreamAsync(plainStream, fs, password);
            }

            // Check correct password
            var (isValidFormat1, isPasswordCorrect1) = await CryptoEngine.CheckFilePasswordAsync(tempFile, password);
            Assert.True(isValidFormat1);
            Assert.True(isPasswordCorrect1);

            // Check wrong password
            var (isValidFormat2, isPasswordCorrect2) = await CryptoEngine.CheckFilePasswordAsync(tempFile, "WrongPassword");
            Assert.True(isValidFormat2);
            Assert.False(isPasswordCorrect2);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

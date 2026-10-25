using System;
using System.Security.Cryptography;

namespace WhatInsides.Services;

public class TotpService
{
    private const int TimeStepSeconds = 30;
    private const int Digits = 6;

    public string GenerateCode(string? base32Secret)
    {
        if (string.IsNullOrWhiteSpace(base32Secret))
            return "------";

        try
        {
            byte[] key = Base32Decode(base32Secret.Trim().Replace(" ", "").ToUpperInvariant());
            if (key.Length == 0) return "------";

            long timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;
            byte[] timeBytes = BitConverter.GetBytes(timeStep);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timeBytes);
            }

            using var hmac = new HMACSHA1(key);
            byte[] hash = hmac.ComputeHash(timeBytes);

            int offset = hash[^1] & 0x0F;
            int binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            int otp = binaryCode % 1000000;
            return otp.ToString("D6");
        }
        catch
        {
            return "------";
        }
    }

    public string FormatCodeWithSpace(string rawCode)
    {
        if (rawCode.Length == 6)
        {
            return $"{rawCode[..3]} {rawCode[3..]}";
        }
        return rawCode;
    }

    public int GetRemainingSeconds()
    {
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int remainder = (int)(seconds % TimeStepSeconds);
        return TimeStepSeconds - remainder;
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new System.Collections.Generic.List<byte>();

        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in input)
        {
            if (c == '=') break; // padding
            int val = alphabet.IndexOf(c);
            if (val < 0) continue; // ignore whitespace/dashes

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)(buffer >> bitsLeft));
            }
        }

        return output.ToArray();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WhatInsides.Services;

public class PasswordGeneratorService
{
    private static readonly string[] WordList = new[]
    {
        "pulsar", "proven", "english", "dipper", "fay", "cosmic", "falcon", "orbit", "horizon", "anchor",
        "velvet", "cipher", "matrix", "shield", "beacon", "aurora", "breeze", "canyon", "crystal", "delta",
        "echo", "ember", "frost", "galaxy", "harbor", "haven", "island", "jungle", "lagoon", "lunar",
        "meadow", "meteor", "nebula", "nexus", "oasis", "ocean", "phoenix", "planet", "prism", "quartz",
        "radiant", "ripple", "safari", "shadow", "sierra", "solar", "spark", "summit", "timber", "titan",
        "valley", "vector", "vertex", "vessel", "vortex", "walnut", "willow", "zenith", "zephyr", "glacier"
    };

    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    public string GeneratePassphrase(int wordCount = 5, string separator = "-", bool capitalize = false)
    {
        wordCount = Math.Clamp(wordCount, 3, 10);
        var selectedWords = new List<string>(wordCount);

        for (int i = 0; i < wordCount; i++)
        {
            int index = RandomNumberGenerator.GetInt32(WordList.Length);
            string word = WordList[index];
            if (capitalize && word.Length > 0)
            {
                word = char.ToUpperInvariant(word[0]) + word[1..];
            }
            selectedWords.Add(word);
        }

        return string.Join(separator, selectedWords);
    }

    public string GenerateRandom(int length = 20, bool useUpper = true, bool useLower = true, bool useDigits = true, bool useSymbols = true)
    {
        length = Math.Clamp(length, 8, 64);
        var poolBuilder = new StringBuilder();
        if (useLower) poolBuilder.Append(LowerChars);
        if (useUpper) poolBuilder.Append(UpperChars);
        if (useDigits) poolBuilder.Append(DigitChars);
        if (useSymbols) poolBuilder.Append(SymbolChars);

        string pool = poolBuilder.Length > 0 ? poolBuilder.ToString() : LowerChars + DigitChars;
        var result = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            int idx = RandomNumberGenerator.GetInt32(pool.Length);
            result.Append(pool[idx]);
        }

        return result.ToString();
    }

    public string GeneratePin(int length = 6)
    {
        length = Math.Clamp(length, 4, 12);
        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            result.Append(RandomNumberGenerator.GetInt32(10));
        }
        return result.ToString();
    }

    public (int ScorePercent, string Rating, string ColorHex) EvaluateStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (0, "Kosong", "#71717A");

        int score = 0;
        if (password.Length >= 8) score += 20;
        if (password.Length >= 14) score += 20;
        if (password.Length >= 20) score += 15;
        if (password.Length >= 28) score += 15;

        bool hasLower = password.Any(char.IsLower);
        bool hasUpper = password.Any(char.IsUpper);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

        int variety = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
        score += variety * 10;

        if (password.Contains('-') || password.Contains('_') || password.Contains('.'))
        {
            score += 5; // Passphrase separator bonus
        }

        score = Math.Clamp(score, 10, 100);

        if (score >= 80) return (score, "Sangat Kuat", "#34D399"); // Green
        if (score >= 60) return (score, "Kuat", "#60CDFF");       // Cyan
        if (score >= 40) return (score, "Cukup", "#FBBF24");      // Amber
        return (score, "Lemah", "#F87171");                       // Red
    }
}

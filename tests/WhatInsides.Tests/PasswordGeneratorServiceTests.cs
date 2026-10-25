using System;
using System.Linq;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class PasswordGeneratorServiceTests
{
    private readonly PasswordGeneratorService _generator = new();

    [Fact]
    public void GeneratePassphrase_DefaultOptions_Returns5WordsSeparatedByDash()
    {
        string passphrase = _generator.GeneratePassphrase(5, "-", false);

        Assert.NotNull(passphrase);
        string[] parts = passphrase.Split('-');
        Assert.Equal(5, parts.Length);
        Assert.All(parts, word => Assert.False(string.IsNullOrWhiteSpace(word)));
    }

    [Fact]
    public void GeneratePassphrase_CapitalizeTrue_CapitalizesEachWord()
    {
        string passphrase = _generator.GeneratePassphrase(4, ".", true);

        string[] parts = passphrase.Split('.');
        Assert.Equal(4, parts.Length);
        Assert.All(parts, word => Assert.True(char.IsUpper(word[0])));
    }

    [Fact]
    public void GeneratePassphrase_ClampsWordCount()
    {
        string tooSmall = _generator.GeneratePassphrase(1);
        Assert.Equal(3, tooSmall.Split('-').Length);

        string tooLarge = _generator.GeneratePassphrase(99);
        Assert.Equal(10, tooLarge.Split('-').Length);
    }

    [Fact]
    public void GenerateRandom_ReturnsExactRequestedLength()
    {
        string pass = _generator.GenerateRandom(24, true, true, true, true);

        Assert.Equal(24, pass.Length);
    }

    [Fact]
    public void GenerateRandom_DigitsOnly_ContainsOnlyDigits()
    {
        string digits = _generator.GenerateRandom(16, false, false, true, false);

        Assert.Equal(16, digits.Length);
        Assert.All(digits, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GeneratePin_ReturnsNumericStringOfRequestedLength()
    {
        string pin = _generator.GeneratePin(6);

        Assert.Equal(6, pin.Length);
        Assert.All(pin, c => Assert.True(char.IsDigit(c)));
    }

    [Theory]
    [InlineData("", 0, "Kosong")]
    [InlineData("abc", 10, "Lemah")]
    [InlineData("pass1234", 40, "Cukup")]
    [InlineData("Pass123!", 60, "Kuat")]
    [InlineData("pulsar-proven-english-dipper-fay", 80, "Sangat Kuat")]
    public void EvaluateStrength_CalculatesScoreAndRating(string password, int minScore, string expectedRating)
    {
        var (score, rating, color) = _generator.EvaluateStrength(password);

        Assert.True(score >= minScore, $"Expected score >= {minScore}, got {score}");
        Assert.Equal(expectedRating, rating);
        Assert.StartsWith("#", color);
    }
}

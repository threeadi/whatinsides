using System;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class TotpServiceTests
{
    private readonly TotpService _totpService = new();

    [Fact]
    public void GenerateCode_NullOrEmptySecret_ReturnsDashes()
    {
        Assert.Equal("------", _totpService.GenerateCode(null));
        Assert.Equal("------", _totpService.GenerateCode("   "));
        Assert.Equal("------", _totpService.GenerateCode(""));
    }

    [Fact]
    public void GenerateCode_ValidSecret_ReturnsSixDigitNumericString()
    {
        // JBSWY3DPEHPK3PXP is standard base32 test key for "Hello!\xDE\xAD\xBE\xEF"
        string code = _totpService.GenerateCode("JBSWY3DPEHPK3PXP");

        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GenerateCode_WithSpacesAndLowerCase_SanitizesAndGenerates()
    {
        string code1 = _totpService.GenerateCode("jbswy3dp ehpk3pxp");

        Assert.Equal(6, code1.Length);
        Assert.All(code1, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void FormatCodeWithSpace_SixDigits_SplitsInMiddle()
    {
        string formatted = _totpService.FormatCodeWithSpace("739104");
        Assert.Equal("739 104", formatted);
    }

    [Fact]
    public void FormatCodeWithSpace_NonSixDigits_ReturnsUnchanged()
    {
        Assert.Equal("123", _totpService.FormatCodeWithSpace("123"));
        Assert.Equal("1234567", _totpService.FormatCodeWithSpace("1234567"));
        Assert.Equal("", _totpService.FormatCodeWithSpace(""));
    }

    [Fact]
    public void GetRemainingSeconds_ReturnsWithinValidRange()
    {
        int remaining = _totpService.GetRemainingSeconds();
        Assert.InRange(remaining, 1, 30);
    }
}

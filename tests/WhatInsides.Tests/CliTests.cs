using System;
using System.IO;
using System.Threading.Tasks;
using WhatInsides.Cli;
using Xunit;

namespace WhatInsides.Tests;

public class CliTests
{
    [Fact]
    public async Task Cli_HelpCommand_ReturnsZero()
    {
        int exitCode = await Program.Main(new[] { "help" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Cli_StatusCommand_ReturnsZero()
    {
        int exitCode = await Program.Main(new[] { "status" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Cli_ListCommand_ReturnsZero()
    {
        int exitCode = await Program.Main(new[] { "list" });
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Cli_UnknownCommand_ReturnsNonZero()
    {
        int exitCode = await Program.Main(new[] { "unknown_foo_bar" });
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Cli_LockAndUnlock_FileRoundtrip_Succeeds()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wi_cli_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "document.txt");
        string originalContent = "Confidential Top Secret Data 2026";
        await File.WriteAllTextAsync(testFile, originalContent);

        try
        {
            string password = "TestPassword123!";

            // 1. Lock file
            int lockExit = await Program.Main(new[] { "lock", testFile, "-p", password });
            Assert.Equal(0, lockExit);

            string lockedFile = testFile + ".wisafe";
            Assert.True(File.Exists(lockedFile));

            // 2. Unlock file
            int unlockExit = await Program.Main(new[] { "unlock", lockedFile, "-p", password });
            Assert.Equal(0, unlockExit);

            Assert.True(File.Exists(testFile));
            string restored = await File.ReadAllTextAsync(testFile);
            Assert.Equal(originalContent, restored);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Cli_TotpCommand_WithExistingAccount_ReturnsZero()
    {
        var svc = new WhatInsides.Services.CredentialVaultService();
        var item = new WhatInsides.Models.CredentialItem
        {
            Title = "CliTestAccountTotp",
            Username = "testuser",
            TotpSecret = "JBSWY3DPEHPK3PXP"
        };
        svc.Add(item);

        try
        {
            int exitCode = await Program.Main(new[] { "totp", "CliTestAccountTotp" });
            Assert.Equal(0, exitCode);
        }
        finally
        {
            svc.Delete(item.Id, permanent: true);
        }
    }

    [Fact]
    public async Task Cli_FilesCommand_WithExistingAccount_ReturnsZero()
    {
        var svc = new WhatInsides.Services.CredentialVaultService();
        var item = new WhatInsides.Models.CredentialItem
        {
            Title = "CliTestAccountFiles",
            Username = "testuser"
        };
        svc.Add(item);

        try
        {
            int exitCode = await Program.Main(new[] { "files", "CliTestAccountFiles" });
            Assert.Equal(0, exitCode);
        }
        finally
        {
            svc.Delete(item.Id, permanent: true);
        }
    }

    [Fact]
    public async Task Cli_GetCommand_WhenItemMissing_ReturnsOne()
    {
        int exitCode = await Program.Main(new[] { "get", "NonExistentServiceQuery_99999" });
        Assert.Equal(1, exitCode);
    }
}

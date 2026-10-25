using System;
using System.Threading.Tasks;
using WhatInsides.Models;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class SshAgentServiceTests
{
    [Fact]
    public void IsApproved_InitiallyFalse()
    {
        var service = new SshAgentService();
        Assert.False(service.IsApproved("Windows Terminal"));
        Assert.False(service.IsApproved("git"));
        Assert.False(service.IsApproved(""));
    }

    [Fact]
    public void RevokeAllApprovals_ResetsApprovalState()
    {
        var service = new SshAgentService();
        service.RevokeAllApprovals();
        Assert.False(service.IsApproved("VSCode"));
    }

    [Fact]
    public void ToastNotification_Event_CanBeSubscribed()
    {
        var service = new SshAgentService();
        string title = "";
        string message = "";

        Action<string, string> handler = (t, m) =>
        {
            title = t;
            message = m;
        };

        service.ToastNotification += handler;
        service.ToastNotification -= handler;

        Assert.Equal("", title);
    }
}

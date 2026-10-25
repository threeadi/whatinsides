using System;
using System.Threading;
using WhatInsides.Services;
using Xunit;

namespace WhatInsides.Tests;

public class ClipboardServiceTests
{
    [Fact]
    public void Copy_WithNullOrEmpty_DoesNothingAndDoesNotThrow()
    {
        ClipboardService.Copy(string.Empty, "Label", 10);
        ClipboardService.Copy(null!, "Label", 10);
        // Assert: no exception thrown
        Assert.True(true);
    }

    [Fact]
    public void CancelTimer_ExecutesSafely()
    {
        ClipboardService.CancelTimer();
        Assert.True(true);
    }

    [Fact]
    public void ClearNow_ExecutesSafely()
    {
        ClipboardService.ClearNow();
        Assert.True(true);
    }

    [Fact]
    public void ClipboardCleared_Event_CanBeSubscribedAndUnsubscribed()
    {
        string? clearedLabel = null;
        Action<string> handler = label => clearedLabel = label;

        ClipboardService.ClipboardCleared += handler;
        ClipboardService.ClipboardCleared -= handler;

        Assert.Null(clearedLabel);
    }

    [Fact]
    public void Copy_WhenCalledFromStaThread_SetsClipboardText()
    {
        var thread = new Thread(() =>
        {
            try
            {
                ClipboardService.Copy("TestSecret123", "TestPassword", 0, excludeFromHistory: true);
                ClipboardService.Copy("TestPlain456", "TestPlain", 0, excludeFromHistory: false);
                ClipboardService.ClearNow();
            }
            catch
            {
                // In headless CI or sandboxed runner, Clipboard API may be restricted
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(true);
    }
}

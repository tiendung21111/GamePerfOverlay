using System;
using System.Threading;
using Xunit;

namespace GamePerfOverlay.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void Mutex_WhenHeldByFirstInstance_SecondInstanceDetectsConflict()
    {
        string uniqueMutexName = @"Local\Test_SingleInstance_" + Guid.NewGuid().ToString("N");

        using var firstMutex = new Mutex(true, uniqueMutexName, out bool firstCreated);
        Assert.True(firstCreated);

        using var secondMutex = new Mutex(true, uniqueMutexName, out bool secondCreated);
        Assert.False(secondCreated);

        firstMutex.ReleaseMutex();
    }

    [Fact]
    public void SingleInstanceMutexName_HasValidPrefixAndIdentifier()
    {
        Assert.False(string.IsNullOrWhiteSpace(App.SingleInstanceMutexName));
        Assert.StartsWith(@"Local\", App.SingleInstanceMutexName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Game Hub & Launcher", Views.LauncherWindow.WindowTitle);
    }
}

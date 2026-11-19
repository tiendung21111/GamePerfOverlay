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

    [Fact]
    public void SingleInstanceEventName_HasValidPrefixAndIdentifier()
    {
        Assert.False(string.IsNullOrWhiteSpace(App.SingleInstanceEventName));
        Assert.StartsWith(@"Local\", App.SingleInstanceEventName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventWaitHandle_CanSignalBetweenInstances()
    {
        string uniqueEventName = @"Local\Test_SingleInstanceEvent_" + Guid.NewGuid().ToString("N");

        using var receiverEvent = new EventWaitHandle(false, EventResetMode.AutoReset, uniqueEventName);

        // Simulate secondary instance opening and signaling
        using var senderEvent = EventWaitHandle.OpenExisting(uniqueEventName);
        bool signaled = senderEvent.Set();
        Assert.True(signaled);

        // Receiver should receive the pulse
        bool received = receiverEvent.WaitOne(TimeSpan.FromSeconds(2));
        Assert.True(received);
    }
}

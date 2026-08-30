// SPDX-License-Identifier: MIT

using System;
using TrackMeUp.Runtime;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class TrackingStartupPolicyTests
{
    [Fact]
    public void PersistedStartOnLaunch_StartsWithoutCommandLineSwitch()
    {
        var options = LaunchOptions.Parse([]);
        var settings = new AppSettings(StartTrackingOnLaunch: true);

        Assert.True(TrackingStartupPolicy.ShouldStart(options, settings));
    }

    [Fact]
    public void ExplicitPausedSwitch_OverridesAutomaticAndExplicitStartRequests()
    {
        var options = LaunchOptions.Parse(["--start-tracking", "--paused"]);
        var settings = new AppSettings(StartTrackingOnLaunch: true);

        Assert.False(TrackingStartupPolicy.ShouldStart(options, settings));
    }

    [Fact]
    public void NoLaunchRequest_RemainsPaused()
    {
        var options = LaunchOptions.Parse([]);
        var settings = new AppSettings(StartTrackingOnLaunch: false);

        Assert.False(TrackingStartupPolicy.ShouldStart(options, settings));
    }

    [Fact]
    public void SafeMode_SuppressesPersistedStartOnLaunch()
    {
        var options = LaunchOptions.Parse(["--safe-mode"]);
        var settings = new AppSettings(StartTrackingOnLaunch: true);

        Assert.False(TrackingStartupPolicy.ShouldStart(options, settings));
    }
}

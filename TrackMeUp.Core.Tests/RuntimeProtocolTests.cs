using System;
using TrackMeUp.Runtime;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class RuntimeProtocolTests
{
    [Fact]
    public void EndpointNames_AreStableAndDoNotExposeInstallationId()
    {
        var endpoint = RuntimeProtocol.CreateEndpoint("machine-private-installation-id");

        Assert.StartsWith("Local\\TrackMeUp.Runtime.", endpoint.MutexName);
        Assert.StartsWith("TrackMeUp.Runtime.", endpoint.PipeName);
        Assert.DoesNotContain("machine-private-installation-id", endpoint.MutexName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LaunchOptions_StripsCliSwitchFromCommandArguments()
    {
        var options = LaunchOptions.Parse(["-cli", "--language", "it", "status"]);

        Assert.Equal(LaunchMode.Cli, options.Mode);
        Assert.Equal("it", options.Language);
        Assert.Equal(["status"], options.RemainingArguments);
    }
}

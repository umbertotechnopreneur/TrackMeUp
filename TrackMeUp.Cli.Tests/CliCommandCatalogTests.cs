using System;
using TrackMeUp.Cli;
using Xunit;

namespace TrackMeUp.Cli.Tests;

public sealed class CliCommandCatalogTests
{
    [Theory]
    [InlineData("/status", "status")]
    [InlineData("/tracking", "tracking")]
    [InlineData("/?", "help")]
    public void Normalize_RemovesOnlyTheCommandSlash(string input, string expected)
    {
        var normalized = CliCommandCatalog.Normalize([input, "value/with/slash"]);

        Assert.Equal(expected, normalized[0]);
        Assert.Equal("value/with/slash", normalized[1]);
    }

    [Theory]
    [InlineData(new[] { "/help" }, null)]
    [InlineData(new[] { "/help", "/config" }, "config")]
    [InlineData(new[] { "/tracking", "--help" }, "tracking")]
    [InlineData(new[] { "/settings", "help" }, "settings")]
    public void TryGetHelpTopic_AcceptsGeneralAndCommandForms(string[] arguments, string? expectedTopic)
    {
        Assert.True(CliCommandCatalog.TryGetHelpTopic(arguments, out var topic));
        Assert.Equal(expectedTopic, topic);
    }

    [Fact]
    public void SettingsAlias_ResolvesToConfigHelp()
    {
        Assert.True(CliCommandCatalog.TryGet("settings", out var command));
        Assert.Equal("config", command!.Name);
    }

    [Fact]
    public void HelpWordAsOptionValue_IsNotTreatedAsCommandHelp()
    {
        Assert.False(CliCommandCatalog.TryGetHelpTopic(["focus", "start", "--objective", "help"], out _));
    }
}

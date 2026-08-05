using System;
using TrackMeUp.Cli;
using Xunit;

namespace TrackMeUp.Cli.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void DefaultLanguage_FollowsTheSystemAndCanBeSelectedExplicitly()
    {
        Assert.Equal("system", CliOptions.Parse(["status"], redirected: false).Language);
        Assert.Equal("system", CliOptions.Parse(["--language", "system", "status"], redirected: false).Language);
    }

    [Fact]
    public void JsonMode_DisablesColorAndAnimationAndRetainsCommand()
    {
        var options = CliOptions.Parse(["--json", "--language", "it", "status"], redirected: false);

        Assert.Equal(CliFormat.Json, options.Format);
        Assert.True(options.NoColor);
        Assert.True(options.NoAnimation);
        Assert.Equal("it", options.Language);
        Assert.Equal(["status"], options.CommandArguments);
    }

    [Fact]
    public void UnsupportedLanguage_IsRejectedBeforeCommandDispatch()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--language", "pt", "status"], redirected: false));
    }

    [Theory]
    [InlineData("--format")]
    [InlineData("--language")]
    [InlineData("--timeout")]
    public void GlobalOptionWithoutValue_IsRejected(string option)
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse([option], redirected: false));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    [InlineData("later")]
    public void InvalidTimeout_IsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--timeout", value, "status"], redirected: false));
    }

    [Theory]
    [InlineData("privacy.blocked", 5)]
    [InlineData("ai.disabled", 6)]
    [InlineData("operation.cancelled", 130)]
    public void ExitCodeMapper_UsesStableCodes(string code, int expected) => Assert.Equal(expected, ExitCodeMapper.Map(code));
}

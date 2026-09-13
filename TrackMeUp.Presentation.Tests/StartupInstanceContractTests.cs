// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using TrackMeUp.Runtime;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class StartupInstanceContractTests
{
    [Fact]
    public void DesktopStartup_RedirectsLongLivedActivationsBeforeCreatingWinUi()
    {
        var project = File.ReadAllText(RepositoryFile("TrackMeUp", "TrackMeUp.csproj"));
        var program = File.ReadAllText(RepositoryFile("TrackMeUp", "Program.cs"));
        var app = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));

        Assert.Contains("DISABLE_XAML_GENERATED_MAIN", project, StringComparison.Ordinal);
        Assert.Contains("AppInstance.FindOrRegisterForKey(MainInstanceKey)", program, StringComparison.Ordinal);
        Assert.Contains("await mainInstance.RedirectActivationToAsync(activation);", program, StringComparison.Ordinal);
        Assert.Contains("return LaunchOptions.Parse(arguments).Mode is not (LaunchMode.Cli or LaunchMode.Help or LaunchMode.Version);", program, StringComparison.Ordinal);
        Assert.True(
            program.IndexOf("RedirectActivationToAsync", StringComparison.Ordinal)
            < program.IndexOf("Application.Start", StringComparison.Ordinal));
        Assert.Contains("mainInstance.Activated += MainInstance_Activated;", program, StringComparison.Ordinal);
        Assert.Contains("HandleRedirectedActivation", app, StringComparison.Ordinal);
        Assert.Contains("_window.ShowFlyout();", app, StringComparison.Ordinal);
        Assert.Contains("WindowsLaunchArguments.Parse(launch.Arguments", app, StringComparison.Ordinal);
        Assert.Contains("case LaunchMode.Reports:", app, StringComparison.Ordinal);
        Assert.Contains("lock (ActivationGate)", program, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (ArgumentException)", program, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("reports --theme dark")]
    [InlineData("\"C:\\Synthetic Apps\\TrackMeUp.exe\" reports --theme dark")]
    public void RedirectedReports_KeepModeAndTheme(string arguments)
    {
        var options = WindowsLaunchArguments.Parse(arguments, "TrackMeUp.exe");

        Assert.Equal(LaunchMode.Reports, options.Mode);
        Assert.Equal("dark", options.Theme);
        Assert.Empty(options.RemainingArguments);
    }

    [Theory]
    [InlineData("--ui --paused --start-tracking --language \"it-IT\"")]
    [InlineData("TrackMeUp.exe --ui --safe-mode --start-tracking --language \"it-IT\"")]
    public void RedirectedUi_PreservesFlagsThatSuppressAutomaticTracking(string arguments)
    {
        var options = WindowsLaunchArguments.Parse(arguments, "TrackMeUp.exe");

        Assert.Equal(LaunchMode.Ui, options.Mode);
        Assert.Equal("it-IT", options.Language);
        Assert.False(TrackingStartupPolicy.ShouldStart(options, new AppSettings(StartTrackingOnLaunch: true)));
    }

    [Theory]
    [InlineData("--background")]
    [InlineData("TrackMeUp.exe --background")]
    public void RedirectedBackground_RemainsHeadless(string arguments)
    {
        Assert.Equal(LaunchMode.Background, WindowsLaunchArguments.Parse(arguments, "TrackMeUp.exe").Mode);
    }

    [Fact]
    public void EmptyActivation_DoesNotReuseHostArguments()
    {
        var options = WindowsLaunchArguments.Parse(string.Empty, "TrackMeUp.exe");

        Assert.Equal(LaunchMode.Ui, options.Mode);
        Assert.Empty(options.RemainingArguments);
    }

    [Fact]
    public void InvalidActivationLanguage_FailsBeforeApplyingDefaults()
    {
        Assert.Throws<ArgumentException>(() => WindowsLaunchArguments.Parse("--language invalid", "TrackMeUp.exe"));
    }

    private static string RepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TrackMeUp.slnx")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            }
        }

        throw new DirectoryNotFoundException("Could not locate the TrackMeUp repository root.");
    }
}

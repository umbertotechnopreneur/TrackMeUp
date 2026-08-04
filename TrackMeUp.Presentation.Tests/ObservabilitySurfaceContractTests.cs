using System;
using System.IO;
using System.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards privacy and shutdown guarantees in the Windows composition root.</summary>
public sealed class ObservabilitySurfaceContractTests
{
    /// <summary>Ensures launch telemetry cannot regress to logging raw paths, arguments, or installation IDs.</summary>
    [Fact]
    public void AppLogging_DoesNotIncludeSensitiveLaunchValues()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));

        Assert.Contains("Launch requested. Mode={Mode}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BaseDirectory={BaseDirectory}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Arguments={Arguments}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallationId={InstallationId}", source, StringComparison.Ordinal);
    }

    /// <summary>Ensures Sentry remains optional, private by default, and bounded during shutdown.</summary>
    [Fact]
    public void LoggingBootstrapper_SentryIsOptionalPrivateAndBounded()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Runtime", "LoggingBootstrapper.cs"));
        var configurationSource = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "ObservabilityConfiguration.cs"));

        Assert.Contains("ReadEnvironmentVariable(\"TRACKMEUP_SENTRY_DSN\")", configurationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable", source, StringComparison.Ordinal);
        Assert.Contains("options.SendDefaultPii = false", source, StringComparison.Ordinal);
        Assert.Contains("BuildServiceProvider(sentryDsn: null", source, StringComparison.Ordinal);
        Assert.Contains("new ObservabilityHealth(", source, StringComparison.Ordinal);
        Assert.Contains("SentrySdk.FlushAsync(ShutdownTimeout)", source, StringComparison.Ordinal);
        Assert.Contains("options.ShutdownTimeout = ShutdownTimeout", source, StringComparison.Ordinal);
        Assert.Contains("options.SetBeforeSend", source, StringComparison.Ordinal);
        Assert.Contains("options.SetBeforeBreadcrumb", source, StringComparison.Ordinal);
        Assert.Contains("new Breadcrumb(", source, StringComparison.Ordinal);
        Assert.Contains("new Dictionary<string, string>()", source, StringComparison.Ordinal);
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

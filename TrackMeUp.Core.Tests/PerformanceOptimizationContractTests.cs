using System;
using System.IO;
using System.Linq;
using Xunit;

namespace TrackMeUp.Core.Tests;

/// <summary>Guards runtime ownership of cached settings and lightweight score telemetry.</summary>
public sealed class PerformanceOptimizationContractTests
{
    [Fact]
    public void RuntimeApplication_UsesSnapshotAfterConstructionAndPersistsBeforeReplacingIt()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "TrackMeUpApplication.cs"));
        var applicationStart = source.IndexOf("public sealed class TrackMeUpApplication", StringComparison.Ordinal);
        Assert.True(applicationStart >= 0);
        var applicationSource = source[applicationStart..];

        Assert.DoesNotContain("_store.LoadSettings()", applicationSource, StringComparison.Ordinal);
        var persistStart = applicationSource.IndexOf("private void PersistSettings", StringComparison.Ordinal);
        var persistEnd = applicationSource.IndexOf("private PendingManualScreenshotState", persistStart, StringComparison.Ordinal);
        var persistSource = applicationSource[persistStart..persistEnd];
        Assert.True(
            persistSource.IndexOf("_store.SaveSettings(settings);", StringComparison.Ordinal)
            < persistSource.IndexOf("_settingsSnapshot.Replace(settings);", StringComparison.Ordinal));
    }

    [Fact]
    public void MinuteSampler_ContainsOnlyCpuAndGpuUsageInterop()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Infrastructure", "Services", "SystemUsageSampler.cs"));

        Assert.Contains("GetSystemTimes", source, StringComparison.Ordinal);
        Assert.Contains("GPU Engine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagementObjectSearcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriveInfo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkInterface", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemSnapshotService", source, StringComparison.Ordinal);
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

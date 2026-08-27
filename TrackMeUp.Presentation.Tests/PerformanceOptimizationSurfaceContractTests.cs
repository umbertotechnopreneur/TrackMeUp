using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards the passive, lazy, bounded-memory presentation optimizations.</summary>
public sealed class PerformanceOptimizationSurfaceContractTests
{
    [Fact]
    public void MainPreview_UsesBoundedDecodeAndReleasesInvalidSource()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains("private const int ScreenshotPreviewDecodePixelWidth = 384;", source, StringComparison.Ordinal);
        Assert.Contains("DecodePixelWidth = ScreenshotPreviewDecodePixelWidth", source, StringComparison.Ordinal);
        Assert.Contains("LastScreenshotImage.Source = null;", source, StringComparison.Ordinal);
        Assert.Contains("_latestScreenshotCapturedAt == capturedAt", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HeavyPagesAndOperationDetails_AreConstructedOnlyOnDemand()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));
        var operationsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));

        Assert.Contains(main.Descendants(), element => element.Name.LocalName == "ContentPresenter" && HasName(element, "OptionsHost"));
        Assert.Contains(main.Descendants(), element => element.Name.LocalName == "ContentPresenter" && HasName(element, "OperationsHost"));
        Assert.DoesNotContain(main.Descendants(), element => element.Name.LocalName is "OptionsControl" or "OperationsControl");
        Assert.Contains("private Task EnsureOptionsAsync()", mainSource, StringComparison.Ordinal);
        Assert.Contains("private Task EnsureOperationsAsync()", mainSource, StringComparison.Ordinal);
        Assert.Contains("options.InitializeAsync(_application, AiState, _surfaceLifetime.Token)", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public async void Initialize", File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs")), StringComparison.Ordinal);

        string[] detailHosts = ["SnapshotAiHost", "ReportsHost", "PrivacyHost", "RetentionHost", "PluginsHost", "InstallationTransferHost"];
        Assert.All(detailHosts, name => Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "ContentPresenter" && HasName(element, name)));
        Assert.DoesNotContain(
            operations.Descendants(),
            element => element.Name.LocalName.EndsWith("OperationsControl", StringComparison.Ordinal));
        Assert.Contains("private FrameworkElement EnsureSection", operationsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainDashboardSubscription_FollowsWindowVisibility()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains("&& _appWindow.IsVisible", source, StringComparison.Ordinal);
        Assert.Contains("if (args.DidVisibilityChange)", source, StringComparison.Ordinal);
        Assert.Contains("_dashboardSubscription.Dispose();", source, StringComparison.Ordinal);
    }

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);

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

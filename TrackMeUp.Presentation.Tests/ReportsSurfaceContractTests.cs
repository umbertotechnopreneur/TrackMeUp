// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards the native reports shell and its passive WebView boundary.</summary>
public sealed class ReportsSurfaceContractTests
{
    [Fact]
    public void ReportsWindow_ProvidesAcrylicHeaderFiltersAndWebView()
    {
        var reports = XDocument.Load(RepositoryFile("TrackMeUp", "ReportsWindow.xaml"));

        Assert.Contains(reports.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(reports.Descendants(), element => element.Name.LocalName == "WebView2");
        Assert.Contains(reports.Descendants(), element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "TitleBarDragRegion"));
        Assert.Contains(reports.Descendants(), element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "RefreshReportButton"));
        Assert.Contains(reports.Descendants(), element => element.Attribute("Tag")?.Value == "Reports.Motto");
        Assert.Contains(reports.Descendants(), element => element.Attribute("Tag")?.Value == "custom");
        Assert.Contains(reports.Descendants(), element => element.Attribute("Tag")?.Value == "hourOfWeek");

        var filters = reports.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Descendants().Any(descendant => descendant.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "RangeComboBox")));
        Assert.Equal("1", filters.Attribute("Grid.Column")?.Value);
        Assert.Equal("Right", filters.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Bottom", filters.Attribute("VerticalAlignment")?.Value);
        Assert.True(filters.Parent is { } header && header.Elements().Any(element => element.Name.LocalName == "Grid.ColumnDefinitions"));
    }

    [Fact]
    public void ReportsCodeBehind_UsesTypedFacadeAndPackagedAssetsWithoutFallbackServer()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));

        Assert.Contains("_viewModel.LoadAsync", source, StringComparison.Ordinal);
        Assert.Contains("SetVirtualHostNameToFolderMapping", source, StringComparison.Ordinal);
        Assert.Contains("PostWebMessageAsJson", source, StringComparison.Ordinal);
        Assert.Contains("report.ready", source, StringComparison.Ordinal);
        Assert.Contains("report.theme.set", source, StringComparison.Ordinal);
        Assert.Contains("report.theme.state", source, StringComparison.Ordinal);
        Assert.Contains("report.theme.error", source, StringComparison.Ordinal);
        Assert.Contains("JsonValueKind.String", source, StringComparison.Ordinal);
        Assert.Contains("RequestedTheme", source, StringComparison.Ordinal);
        Assert.Contains("WebMessageReceived", source, StringComparison.Ordinal);
        Assert.Contains("reports.trackmeup.local", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToString", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Kestrel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpListener", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TrackMeUpApplicationFactory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsThemeAndCache_UseApplicationSettingsAndExplicitFreshnessBoundaries()
    {
        var reports = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));
        var app = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var webTheme = File.ReadAllText(RepositoryFile("TrackMeUp.Reports.Web", "src", "themePreference.ts"));

        Assert.Contains("SnapshotCacheTtl", reports, StringComparison.Ordinal);
        Assert.Contains("InvalidateReportCache", reports, StringComparison.Ordinal);
        Assert.Contains("PatchSettingsAsync", reports, StringComparison.Ordinal);
        Assert.Contains("GetSettingsAsync", reports, StringComparison.Ordinal);
        Assert.Contains("ExtendsContentIntoTitleBar = true", reports, StringComparison.Ordinal);
        Assert.Contains("SetTitleBar(TitleBarDragRegion)", reports, StringComparison.Ordinal);
        Assert.Contains("StartReports(options)", app, StringComparison.Ordinal);
        Assert.Contains("options.Theme", app, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", webTheme, StringComparison.Ordinal);
        Assert.DoesNotContain("trackmeup.reports.theme", webTheme, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationOwnsSharedFacadeLifetimeInsteadOfIndividualWindows()
    {
        var app = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var reports = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));

        Assert.Contains("_applicationFacade.DisposeAsync()", app, StringComparison.Ordinal);
        Assert.Contains("ReportsRequested", app, StringComparison.Ordinal);
        Assert.DoesNotContain("_application.DisposeAsync()", main, StringComparison.Ordinal);
        Assert.DoesNotContain("_application.DisposeAsync()", reports, StringComparison.Ordinal);
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

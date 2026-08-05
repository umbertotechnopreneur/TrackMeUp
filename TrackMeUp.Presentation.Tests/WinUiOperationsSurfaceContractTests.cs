using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards complete, passive access to operational use cases from the WinUI frontend.</summary>
public sealed class WinUiOperationsSurfaceContractTests
{
    /// <summary>Ensures the dense operational surface remains integrated and usable at narrow widths.</summary>
    [Fact]
    public void OperationsSurface_IsIntegratedScrollableAndAdaptive()
    {
        var mainWindow = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));

        Assert.Contains(mainWindow.Descendants(), element => element.Name.LocalName == "OperationsControl");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "InfoBar");
    }

    /// <summary>Ensures every requested operation remains delegated through the shared facade.</summary>
    [Fact]
    public void OperationsCodeBehind_InvokesCompleteFacadeSurfaceWithoutDirectIo()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));
        string[] requiredFacadeCalls =
        [
            "GetRuntimeHealthAsync",
            "CaptureSystemSnapshotAsync",
            "CaptureScreenshotAsync",
            "GetLatestScreenshotAsync",
            "OpenScreenshotFolderAsync",
            "AnalyzeCurrentActivityAsync",
            "StartFocusSessionAsync",
            "GetFocusSessionAsync",
            "StopFocusSessionAsync",
            "GenerateTodayReportAsync",
            "GenerateDailyDigestAsync",
            "OpenReportsFolderAsync",
            "GetPrivacyRulesAsync",
            "AddPrivacyRuleAsync",
            "RemovePrivacyRuleAsync",
            "TestCurrentPrivacyAsync",
            "GetRetentionStatusAsync",
            "PreviewRetentionAsync",
            "RunRetentionAsync",
            "GetPluginsAsync",
            "SetPluginEnabledAsync"
        ];

        Assert.All(requiredFacadeCalls, call => Assert.Contains(call, source, StringComparison.Ordinal));
        Assert.DoesNotContain("System.IO", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Registry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScreenCaptureService", source, StringComparison.Ordinal);
    }

    /// <summary>Ensures destructive retention execution cannot be triggered without a safe-default dialog.</summary>
    [Fact]
    public void RetentionExecution_RequiresExplicitUiAndApplicationConfirmation()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));

        Assert.Contains("new ContentDialog", source, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Close", source, StringComparison.Ordinal);
        Assert.Contains("dialog.ShowAsync() != ContentDialogResult.Primary", source, StringComparison.Ordinal);
        Assert.Contains("new RetentionRequest(Execute: true, Confirmed: true)", source, StringComparison.Ordinal);
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

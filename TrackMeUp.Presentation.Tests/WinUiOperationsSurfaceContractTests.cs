using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards complete, passive access to operational use cases from the WinUI frontend.</summary>
public sealed class WinUiOperationsSurfaceContractTests
{
    /// <summary>Guards the passive About diagnostics surface and its shared-runtime route.</summary>
    [Fact]
    public void AboutDiagnostics_UseFacadeRuntimeAndRedactedShareInfrastructure()
    {
        var about = File.ReadAllText(RepositoryFile("TrackMeUp", "AboutWindow.xaml.cs"));
        var runtime = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Runtime", "RuntimeHost.cs"));
        var logs = File.ReadAllText(RepositoryFile("TrackMeUp", "Services", "ApplicationLogService.cs"));

        Assert.Contains("_application.OpenApplicationLogAsync", about, StringComparison.Ordinal);
        Assert.Contains("_application.ShareApplicationLogAsync", about, StringComparison.Ordinal);
        Assert.Contains("_application.OpenProductLinkAsync", about, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", about, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", about, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics.log.open\"", runtime, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics.log.share\"", runtime, StringComparison.Ordinal);
        Assert.Contains("\"product.link.open\"", runtime, StringComparison.Ordinal);
        Assert.Contains("MaximumSharedSourceBytes", logs, StringComparison.Ordinal);
        Assert.Contains("CreateRedactedExport", logs, StringComparison.Ordinal);
        Assert.Contains("RedactForSharing", logs, StringComparison.Ordinal);
    }

    /// <summary>Ensures the dense operational surface remains integrated and usable at narrow widths.</summary>
    [Fact]
    public void OperationsSurface_IsIntegratedScrollableAndAdaptive()
    {
        var mainWindow = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));
        var privacy = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "PrivacyOperationsControl.xaml"));
        var retention = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "RetentionOperationsControl.xaml"));
        var plugins = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "PluginOperationsControl.xaml"));

        Assert.Contains(mainWindow.Descendants(), element => element.Name.LocalName == "OperationsControl");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "InfoBar");
        Assert.Equal(3, operations.Descendants().Count(element => element.Name.LocalName == "ToggleButton" && element.Attribute("Tag")?.Value.StartsWith("Operations.Section.", StringComparison.Ordinal) == true));
        Assert.Contains(operations.Descendants(), element => HasName(element, "RuntimeCapabilitiesList"));
        Assert.Contains(operations.Descendants(), element => HasName(element, "SystemDisksList"));
        Assert.All(new[] { privacy, retention, plugins }, document => Assert.Contains(document.Descendants(), element => element.Name.LocalName == "InfoBar"));
        Assert.All(new[] { privacy, retention, plugins }, document => Assert.Contains(document.Descendants(), element => element.Attribute("Tag")?.Value?.EndsWith(".Description", StringComparison.Ordinal) == true));
    }

    /// <summary>Ensures every requested operation remains delegated through the shared facade.</summary>
    [Fact]
    public void OperationsCodeBehind_InvokesCompleteFacadeSurfaceWithoutDirectIo()
    {
        var sources = new[]
        {
            "OperationsControl.xaml.cs",
            "PrivacyOperationsControl.xaml.cs",
            "RetentionOperationsControl.xaml.cs",
            "PluginOperationsControl.xaml.cs"
        }.Select(file => File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", file))).ToArray();
        var source = string.Join(Environment.NewLine, sources);
        string[] requiredFacadeCalls =
        [
            "GetRuntimeHealthAsync",
            "CaptureSystemSnapshotAsync",
            "CaptureScreenshotAsync",
            "GetLatestScreenshotAsync",
            "OpenScreenshotFolderAsync",
            "AnalyzeCurrentActivityAsync",
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
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "RetentionOperationsControl.xaml.cs"));

        Assert.Contains("Dialogs.ConfirmAsync(", source, StringComparison.Ordinal);
        Assert.Contains("MicaDialogRequest.Confirmation(", source, StringComparison.Ordinal);
        Assert.Contains("if (!confirmed)", source, StringComparison.Ordinal);
        Assert.Contains("new RetentionRequest(Execute: true, Confirmed: true)", source, StringComparison.Ordinal);
    }

    /// <summary>Prevents view code from bypassing the single queued dialog engine.</summary>
    [Fact]
    public void WindowViews_DoNotConstructAdHocSystemOrContentDialogs()
    {
        var trackMeUpDirectory = Path.GetDirectoryName(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"))!;
        var sources = Directory.EnumerateFiles(trackMeUpDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var path in sources)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("new ContentDialog", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MessageBox.Show", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new MessageDialog", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Guards the Acrylic, safe-cancel and cross-process notification contracts.</summary>
    [Fact]
    public void DialogEngine_IsAcrylicQueuedAccessibleAndFacadeBacked()
    {
        var service = File.ReadAllText(RepositoryFile("TrackMeUp", "MicaDialogService.cs"));
        var dialog = File.ReadAllText(RepositoryFile("TrackMeUp", "MicaDialogWindow.xaml.cs"));
        var dialogXaml = XDocument.Load(RepositoryFile("TrackMeUp", "MicaDialogWindow.xaml"));
        var connectionDialog = File.ReadAllText(RepositoryFile("TrackMeUp", "AiConnectionTestDialogWindow.xaml.cs"));
        var connectionDialogXaml = XDocument.Load(RepositoryFile("TrackMeUp", "AiConnectionTestDialogWindow.xaml"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var app = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var schedule = File.ReadAllText(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml.cs"));

        Assert.Contains("SemaphoreSlim", service, StringComparison.Ordinal);
        Assert.Contains("AccentColor", service, StringComparison.Ordinal);
        Assert.Contains("DisableDialogPeerWindows(dialog.WindowHandle)", service, StringComparison.Ordinal);
        Assert.Contains("RestoreDialogPeerWindows(disabledPeerWindows)", service, StringComparison.Ordinal);
        Assert.Contains("EnumThreadWindows", service, StringComparison.Ordinal);
        Assert.Contains("EnableWindow(windowHandle, false)", service, StringComparison.Ordinal);
        Assert.Contains("EnableWindow(windowHandle, true)", service, StringComparison.Ordinal);
        Assert.Contains("return await ShowAsync(application, owner, request, theme) == MicaDialogResult.Primary;", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SavePlacementAsync", service, StringComparison.Ordinal);
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(dialogXaml.Descendants(), element =>
            HasName(element, "RootGrid") && element.Attribute("Background")?.Value == "Transparent");
        Assert.DoesNotContain(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Background")?.Value?.Contains("LayerFillColorDefaultBrush", StringComparison.Ordinal) == true);
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "Rectangle"
            && HasName(element, "AccentVeil")
            && element.Attribute("IsHitTestVisible")?.Value == "False");
        Assert.DoesNotContain(dialogXaml.Descendants(), element => HasName(element, "AccentIconSurface"));
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "FontIcon"
            && HasName(element, "SeverityIcon")
            && element.Attribute("FontSize")?.Value == "30");
        Assert.DoesNotContain(dialogXaml.Descendants(), element => element.Attribute("Style")?.Value == "{StaticResource DialogActionButtonStyle}");
        Assert.Contains(dialogXaml.Descendants(), element => HasName(element, "PrimaryButton") && element.Attribute("Style")?.Value == "{StaticResource AccentButtonStyle}");
        Assert.Contains("ExtendsContentIntoTitleBar = true;", dialog, StringComparison.Ordinal);
        Assert.Contains("SetWindowLongPtr", dialog, StringComparison.Ordinal);
        Assert.Contains("HwndTopMost", dialog, StringComparison.Ordinal);
        Assert.Contains("SetWindowPos(_windowHandle, HwndTopMost", dialog, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Dialog", dialog, StringComparison.Ordinal);
        Assert.Contains("await _placement.RestoreAndCenterAsync(RootGrid, CancellationToken.None);", dialog, StringComparison.Ordinal);
        Assert.Contains("Closed += (_, _) => _completion.TrySetResult(_result);", dialog, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName", dialog, StringComparison.Ordinal);
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "ScrollViewer"
            && HasName(element, "MessageScrollViewer")
            && element.Attribute("VerticalScrollBarVisibility")?.Value == "Hidden"
            && element.Attribute("VerticalScrollMode")?.Value == "Disabled");
        Assert.Contains(dialogXaml.Descendants(), element =>
            HasName(element, "DialogMessageText") && element.Attribute("IsTextSelectionEnabled")?.Value == "True");
        Assert.Contains("MessageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;", dialog, StringComparison.Ordinal);
        Assert.Contains("DialogBody.Measure", dialog, StringComparison.Ordinal);
        Assert.Contains("LogicalMaximumHeight", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("LogicalInformationHeight", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("LogicalConfirmationHeight", dialog, StringComparison.Ordinal);
        Assert.Contains("await _placement.SaveAsync(CancellationToken.None);", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("PrimaryButton.Background", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetContrastingForeground", dialog, StringComparison.Ordinal);
        Assert.Contains("AccentVeil.Fill = CreateAccentVeil(accent, theme);", dialog, StringComparison.Ordinal);
        Assert.Contains("new RadialGradientBrush", dialog, StringComparison.Ordinal);
        Assert.Contains("ElementTheme.Dark => (byte)30", dialog, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Escape", dialog, StringComparison.Ordinal);
        Assert.Contains(dialogXaml.Descendants(), element => element.Attribute("AutomationProperties.LiveSetting")?.Value == "Assertive");
        Assert.Contains("DrainApplicationNotificationsAsync", main, StringComparison.Ordinal);
        Assert.Contains("Enabled: true, HasKey: false", main, StringComparison.Ordinal);
        Assert.Contains("private readonly MicaDialogService _dialogs = new();", app, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ConfirmAsync(", schedule, StringComparison.Ordinal);
        Assert.Contains(connectionDialogXaml.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(connectionDialogXaml.Descendants(), element => HasName(element, "TerminalScrollViewer"));
        Assert.Contains(connectionDialogXaml.Descendants(), element => HasName(element, "TerminalText") && element.Attribute("FontFamily")?.Value == "Cascadia Mono");
        Assert.Contains("AiConnectionTestProtocol.Prompt", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("AppendTerminalAsync", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("RestoreAndCenterAsync", connectionDialog, StringComparison.Ordinal);
    }

    private static bool HasName(XElement element, string value)
        => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == value);

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

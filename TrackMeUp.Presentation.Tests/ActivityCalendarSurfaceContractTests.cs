using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class ActivityCalendarSurfaceContractTests
{
    [Fact]
    public void ActivityCalendar_UsesNativeCalendarAndAggregateApplicationFacade()
    {
        var dialog = XDocument.Load(RepositoryFile("TrackMeUp", "ActivityCalendarDialogWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "ActivityCalendarDialogWindow.xaml.cs"));
        var calendar = dialog.Descendants().Single(element => HasName(element, "ActivityCalendarView"));
        var root = dialog.Descendants().Single(element => HasName(element, "RootGrid"));

        Assert.Equal("CalendarView", calendar.Name.LocalName);
        Assert.Equal("Single", calendar.Attribute("SelectionMode")?.Value);
        Assert.Equal("ActivityCalendarView_SelectedDatesChanged", calendar.Attribute("SelectedDatesChanged")?.Value);
        Assert.Equal("ActivityCalendarView_CalendarViewDayItemChanging", calendar.Attribute("CalendarViewDayItemChanging")?.Value);
        Assert.Equal("Transparent", root.Attribute("Background")?.Value);
        Assert.Contains(dialog.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(dialog.Descendants(), element => HasName(element, "ScoreValueText"));
        Assert.Contains(dialog.Descendants(), element => HasName(element, "ActiveTimeValueText"));
        Assert.Contains(dialog.Descendants(), element => HasName(element, "IdleTimeValueText"));
        Assert.Contains(dialog.Descendants(), element => HasName(element, "TrackedTimeValueText"));
        Assert.Contains(dialog.Descendants(), element => HasName(element, "KeyPressesValueText"));
        Assert.Contains(dialog.Descendants(), element => HasName(element, "MouseClicksValueText"));
        Assert.DoesNotContain(dialog.Descendants(), element => element.Name.LocalName is "WebView2" or "Frame");

        Assert.Contains("_application.GetReportAsync", source, StringComparison.Ordinal);
        Assert.Contains("new ReportQuery(from, today, string.Empty, ReportView.Calendar)", source, StringComparison.Ordinal);
        Assert.Contains("today.AddDays(-365)", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("TryApplySnapshot(result.Value)", StringComparison.Ordinal) <
            source.IndexOf("ActivityCalendarView.MinDate = ToCalendarDate(from)", StringComparison.Ordinal));
        Assert.Contains("ExpectedReportContractVersion = 4", source, StringComparison.Ordinal);
        Assert.Contains("cell.ActivityScore", source, StringComparison.Ordinal);
        Assert.Contains("SetDensityColors", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.ActivityCalendar", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(args.Item", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(args.Item", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sqlite", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ActivityMenu_OpensCalendarThroughSharedDialogCoordinator()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var dialogs = File.ReadAllText(RepositoryFile("TrackMeUp", "MicaDialogService.cs"));
        var menuItem = main.Descendants().Single(element => HasName(element, "ActivityCalendarMenuItem"));

        Assert.Equal("ActivityCalendar.MenuTitle", menuItem.Attribute("Tag")?.Value);
        Assert.Equal("ActivityCalendarMenuItem_Click", menuItem.Attribute("Click")?.Value);
        Assert.Contains(menuItem.Ancestors(), element => HasName(element, "ActivityMenu"));
        Assert.Contains("ActivityCalendarMenuItem.Text = T(\"ActivityCalendar.MenuTitle\")", mainSource, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowActivityCalendarAsync(_application, this, RootGrid.RequestedTheme, _strings)", mainSource, StringComparison.Ordinal);
        Assert.Contains("internal async Task ShowActivityCalendarAsync", dialogs, StringComparison.Ordinal);
        Assert.Contains("new ActivityCalendarDialogWindow(application, theme, strings, ownerAppWindow, ownerHandle)", dialogs, StringComparison.Ordinal);
        Assert.Contains("await _queue.WaitAsync()", dialogs, StringComparison.Ordinal);
        Assert.Contains("DisableDialogPeerWindows(dialog.WindowHandle)", dialogs, StringComparison.Ordinal);
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

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);
}

// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards the responsive and input contracts of the weekly hours editor.</summary>
public sealed class WeeklyHoursEditorInteractionContractTests
{
    /// <summary>Ensures cells are native empty toggles and no overlay prevents tap or keyboard input.</summary>
    [Fact]
    public void CellsUseNativeToggleInputWithoutAnInteractionOverlayOrInactiveGlyph()
    {
        var editor = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml.cs"));

        Assert.DoesNotContain(editor.Descendants(), element => element.Name.LocalName == "Ellipse");
        Assert.DoesNotContain(editor.Descendants(), element => HasName(element, "GridInteractionSurface"));
        Assert.Contains(editor.Descendants(), element => HasName(element, "SelectionIndicator"));
        Assert.Contains("UseSystemFocusVisuals", editor.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("IsHitTestVisible = false", source, StringComparison.Ordinal);
        Assert.Contains("PointerDeviceType.Touch", source, StringComparison.Ordinal);
        Assert.Contains("slot.ReleasePointerCapture(e.Pointer)", source, StringComparison.Ordinal);
        Assert.Contains("DaysHost.CapturePointer(e.Pointer)", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("DaysHost.CapturePointer(e.Pointer)", StringComparison.Ordinal)
            < source.IndexOf("private void DaysHost_PointerMoved", StringComparison.Ordinal));
        Assert.Contains("if (!_isDragging && _dragSelectionValue.HasValue)", source, StringComparison.Ordinal);
    }

    /// <summary>Ensures the selection fill follows the combined states used by the WinUI ToggleButton runtime.</summary>
    [Fact]
    public void CellsUseTheWinUiToggleButtonCommonStateContract()
    {
        var editor = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml"));
        var template = editor.Descendants().Single(element =>
            element.Name.LocalName == "ControlTemplate"
            && element.Attribute("TargetType")?.Value == "ToggleButton");
        var stateGroup = template.Descendants().Single(element => element.Name.LocalName == "VisualStateGroup");
        var stateNames = stateGroup.Elements()
            .Where(element => element.Name.LocalName == "VisualState")
            .Select(element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Name").Value)
            .ToArray();

        Assert.Equal("CommonStates", stateGroup.Attributes().Single(attribute => attribute.Name.LocalName == "Name").Value);
        Assert.Contains("Normal", stateNames);
        Assert.Contains("PointerOver", stateNames);
        Assert.Contains("Pressed", stateNames);
        Assert.Contains("Disabled", stateNames);
        Assert.Contains("Checked", stateNames);
        Assert.Contains("CheckedPointerOver", stateNames);
        Assert.Contains("CheckedPressed", stateNames);
        Assert.Contains("CheckedDisabled", stateNames);
        Assert.DoesNotContain("Unchecked", stateNames);
    }

    /// <summary>Ensures day cards reflow and drag hit testing follows each arranged single-column hourly grid.</summary>
    [Fact]
    public void DayCardsAndDragMappingFollowTheResponsiveArrangedTimeline()
    {
        var editor = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml.cs"));
        var scroller = editor.Descendants().Single(element => HasName(element, "DaysScrollViewer"));
        var editorGrid = editor.Descendants().Single(element =>
            element.Name.LocalName == "Grid"
            && element.Elements().Any(child => child.Name.LocalName == "ScrollViewer" && HasName(child, "DaysScrollViewer")));
        Assert.Equal("Stretch", scroller.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Equal("Disabled", scroller.Attribute("HorizontalScrollMode")?.Value);
        Assert.Equal("Top", scroller.Attribute("VerticalAlignment")?.Value);
        Assert.Null(scroller.Attribute("MaxHeight"));
        Assert.Equal("Auto", editorGrid.Descendants().First(element => element.Name.LocalName == "RowDefinition").Attribute("Height")?.Value);
        Assert.DoesNotContain("DayColumnWidth", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotHeight", source, StringComparison.Ordinal);
        Assert.Contains("HourRowHeight = 24d", source, StringComparison.Ordinal);
        Assert.Contains("SlotGridColumns = 1", source, StringComparison.Ordinal);
        Assert.Contains("SevenColumnBreakpoint = 780d", source, StringComparison.Ordinal);
        Assert.Contains("FourColumnBreakpoint = 500d", source, StringComparison.Ordinal);
        Assert.Contains("TwoColumnBreakpoint = 300d", source, StringComparison.Ordinal);
        Assert.Contains("ApplyResponsiveLayout", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(card, dayIndex % columnCount)", source, StringComparison.Ordinal);
        Assert.Contains("timeline.TransformToVisual(DaysHost)", source, StringComparison.Ordinal);
        Assert.Contains("SlotsPerHour / SlotGridColumns", source, StringComparison.Ordinal);
        Assert.Contains("ApplyDragPath", source, StringComparison.Ordinal);
        Assert.Contains(editor.Descendants(), element => HasKey(element, "ScheduleDayCardStyle"));
        Assert.Contains(editor.Descendants(), element => HasKey(element, "ScheduleWeekendDayCardStyle"));
    }

    private static string RepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrackMeUp.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, .. segments]);
    }

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);

    private static bool HasKey(XElement element, string key) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == key);
}

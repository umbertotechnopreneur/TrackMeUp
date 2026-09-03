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
        Assert.Contains("DaysHost.CapturePointer(e.Pointer)", source, StringComparison.Ordinal);
    }

    /// <summary>Ensures day columns stretch and drag hit testing reads their arranged widths.</summary>
    [Fact]
    public void DayColumnsAndDragMappingFollowTheArrangedWidth()
    {
        var editor = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml.cs"));
        var dayHosts = editor.Descendants()
            .Where(element => HasName(element, "DaysHeaderHost") || HasName(element, "DaysHost"))
            .ToArray();

        Assert.Equal(2, dayHosts.Length);
        Assert.All(dayHosts, host =>
        {
            var widths = host.Descendants()
                .Where(element => element.Name.LocalName == "ColumnDefinition")
                .Select(element => element.Attribute("Width")?.Value)
                .ToArray();
            Assert.Equal("64", widths[0]);
            Assert.Equal(7, widths.Skip(1).Count(width => width == "*"));
        });
        Assert.DoesNotContain("Width=\"736\"", editor.ToString(), StringComparison.Ordinal);
        var scroller = editor.Descendants().Single(element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal("2", scroller.Attributes().Single(attribute => attribute.Name.LocalName == "Grid.Row").Value);
        Assert.Equal("Stretch", scroller.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Null(scroller.Attribute("MaxHeight"));
        Assert.DoesNotContain("DayColumnWidth", source, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions[candidateDayIndex + 1].ActualWidth", source, StringComparison.Ordinal);
        Assert.Contains("ApplyDragPath", source, StringComparison.Ordinal);
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
}

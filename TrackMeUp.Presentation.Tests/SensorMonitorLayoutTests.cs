// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class SensorMonitorLayoutTests
{
    [Theory]
    [InlineData(476, 100, true)]
    [InlineData(952, 124, false)]
    [InlineData(352, 64, false)]
    public void Rows_ReflowWithinTheirAllocatedHeight(double width, double height, bool stacked)
    {
        var layout = SensorMonitorLayout.ResolveTrack(width, height, true, true);
        Assert.Equal(stacked, layout.Stacked);
        var occupied = layout.Padding * 2 + (layout.Stacked ? 56 : 0) + layout.GraphHeight + 10
            + (layout.ShowCapacityCaption ? 20 : 0) + (layout.ShowSecondary ? 20 : 0);
        Assert.True(occupied <= height, $"Row requires {occupied} but only has {height}.");
        Assert.False(layout.ShowSecondary); // Capacity and its units already occupy the metadata line.
    }

    [Fact]
    public void NormalLaptopViewport_KeepsCpuRamGpuDisksAndBatteryOnOnePage()
    {
        Assert.Equal(5, SensorMonitorLayout.PageSize(360, 5));
        Assert.Equal(4, SensorMonitorLayout.PageSize(240, 4));
        Assert.Equal(5, SensorMonitorLayout.PageSize(240, 5));
        Assert.Equal(5, SensorMonitorLayout.PageSize(240, 8));
        Assert.Equal(0, SensorMonitorLayout.PageSize(240, 0));
    }

    [Fact]
    public void Geometry_RejectsInvalidSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SensorMonitorLayout.ResolveTrack(double.NaN, 100, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => SensorMonitorLayout.PageSize(-1, 4));
    }

    [Fact]
    public void Monitor_UsesBoundedRowsInsteadOfAScrollingStack()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrackMeUp", "SensorsWindow.xaml"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var view = XDocument.Load(Path.Combine(directory.FullName, "TrackMeUp", "SensorsWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var monitor = view.Descendants().Single(element => element.Attribute(x + "Name")?.Value == "MonitorSurface");
        Assert.DoesNotContain(monitor.Descendants(), element => element.Name.LocalName is "ScrollViewer" or "Viewbox");
        Assert.Equal("Grid", monitor.Descendants().Single(element => element.Attribute(x + "Name")?.Value == "TracksHost").Name.LocalName);
    }
}

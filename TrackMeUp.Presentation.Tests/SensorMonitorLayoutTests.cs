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
    [InlineData(476, 192, true)]
    [InlineData(952, 124, false)]
    [InlineData(352, 192, true)]
    public void Rows_ReflowWithinTheirAllocatedHeight(double width, double height, bool stacked)
    {
        var layout = SensorMonitorLayout.ResolveTrack(width, height);
        Assert.Equal(stacked, layout.Stacked);
        var occupied = layout.Padding * 2 + (layout.Stacked ? 124 : 0) + layout.GraphHeight;
        Assert.True(occupied <= height, $"Row requires {occupied} but only has {height}.");
        if (!stacked) Assert.True(layout.NameWidth + layout.ValueWidth + layout.TemperatureWidth + 92 < width);
    }

    [Fact]
    public void NormalLaptopViewport_KeepsCpuRamGpuDisksAndBatteryOnOnePage()
    {
        Assert.Equal(7, SensorMonitorLayout.PageSize(700, 1000, 7));
        Assert.Equal(2, SensorMonitorLayout.PageSize(240, 1000, 8));
        Assert.Equal(3, SensorMonitorLayout.PageSize(700, 476, 7));
        Assert.Equal(1, SensorMonitorLayout.PageSize(100, 476, 7));
        Assert.Equal(0, SensorMonitorLayout.PageSize(240, 1000, 0));
    }

    [Fact]
    public void Geometry_RejectsInvalidSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SensorMonitorLayout.ResolveTrack(double.NaN, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => SensorMonitorLayout.PageSize(-1, 1000, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SensorMonitorLayout.PageSize(100, double.NaN, 4));
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

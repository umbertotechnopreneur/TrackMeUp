using TrackMeUp.Application;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class WindowStateCalculatorTests
{
    [Fact]
    public void WindowStateMinimumSizes_AreSharedByRestoreAndNativeSizing()
    {
        Assert.Equal(new WindowMinimumSize(720, 520), WindowStateService.GetMinimumSize(WindowStateKeys.Reports));
        Assert.Equal(new WindowMinimumSize(760, 540), WindowStateService.GetMinimumSize(WindowStateKeys.Screenshots));
        Assert.Equal(new WindowMinimumSize(420, 360), WindowStateService.GetMinimumSize(WindowStateKeys.Search));
        Assert.Equal(new WindowMinimumSize(620, 480), WindowStateService.GetMinimumSize(WindowStateKeys.Schedule));
        Assert.Equal(new WindowMinimumSize(320, 196), WindowStateService.GetMinimumSize(WindowStateKeys.Dialog));
    }

    [Fact]
    public void ClampToWorkArea_ExpandsTinyPersistedBoundsToWindowMinimum()
    {
        var saved = new WindowState(3800, 2000, 100, 40, @"\\.\DISPLAY1");
        var workArea = new WindowWorkArea(0, 0, 1920, 1080);

        var restored = WindowStateCalculator.ClampToWorkArea(saved, workArea, @"\\.\DISPLAY2", 760, 540);

        Assert.Equal(760, restored.Width);
        Assert.Equal(540, restored.Height);
        Assert.Equal(1160, restored.X);
        Assert.Equal(540, restored.Y);
        Assert.Equal(@"\\.\DISPLAY2", restored.MonitorDeviceName);
    }

    [Fact]
    public void ClampToWorkArea_BoundsMinimumToSmallWorkArea()
    {
        var saved = new WindowState(-400, -200, 100, 40, @"\\.\DISPLAY1");
        var workArea = new WindowWorkArea(10, 20, 300, 180);

        var restored = WindowStateCalculator.ClampToWorkArea(saved, workArea, @"\\.\DISPLAY2", 760, 540);

        Assert.Equal(300, restored.Width);
        Assert.Equal(180, restored.Height);
        Assert.Equal(10, restored.X);
        Assert.Equal(20, restored.Y);
        Assert.Equal(@"\\.\DISPLAY2", restored.MonitorDeviceName);
    }
}

using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ScreenCaptureServiceTests
{
    [Fact]
    public void SelectFocusedDisplayIndex_UsesDisplayWithLargestForegroundIntersection()
    {
        var displays = new[]
        {
            new NativeMethods.Rect { Left = 0, Top = 0, Right = 100, Bottom = 100 },
            new NativeMethods.Rect { Left = 100, Top = 0, Right = 220, Bottom = 100 }
        };
        var foregroundWindow = new NativeMethods.Rect { Left = 90, Top = 10, Right = 180, Bottom = 80 };

        var focusedDisplay = ScreenCaptureService.SelectFocusedDisplayIndex(displays, foregroundWindow);

        Assert.Equal(1, focusedDisplay);
    }
}

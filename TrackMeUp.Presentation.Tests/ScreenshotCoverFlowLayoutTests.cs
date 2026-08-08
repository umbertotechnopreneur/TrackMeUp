using System;
using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class ScreenshotCoverFlowLayoutTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void MidpointPose_SeparatesFacesAndLayersIncomingCoverDeterministically(
        int transitionDirection,
        bool rightCoverIsIncoming)
    {
        var left = ScreenshotCoverFlowLayout.CalculatePose(-0.5d, 1_000d, transitionDirection, reducedMotion: false);
        var right = ScreenshotCoverFlowLayout.CalculatePose(0.5d, 1_000d, transitionDirection, reducedMotion: false);

        Assert.True(right.TranslateX - left.TranslateX >= 500d);
        if (rightCoverIsIncoming)
        {
            Assert.True(right.ZIndex > left.ZIndex);
            Assert.True(right.Depth > left.Depth);
        }
        else
        {
            Assert.True(left.ZIndex > right.ZIndex);
            Assert.True(left.Depth > right.Depth);
        }
    }

    [Fact]
    public void StablePose_KeepsSelectedCoverAboveBothSides()
    {
        var selected = ScreenshotCoverFlowLayout.CalculatePose(0d, 1_000d, 0, reducedMotion: false);
        var left = ScreenshotCoverFlowLayout.CalculatePose(-1d, 1_000d, 0, reducedMotion: false);
        var right = ScreenshotCoverFlowLayout.CalculatePose(1d, 1_000d, 0, reducedMotion: false);

        Assert.True(selected.ZIndex > left.ZIndex);
        Assert.True(selected.ZIndex > right.ZIndex);
        Assert.Equal(left.ZIndex, right.ZIndex);
    }

    [Theory]
    [InlineData(3.5555555556d, 600d, 360d, 600d, 168.75d)]
    [InlineData(0.5625d, 600d, 360d, 202.5d, 360d)]
    [InlineData(1.7777777778d, 600d, 360d, 600d, 337.5d)]
    public void FitPresenter_PreservesWidePortraitAndStandardAspectRatios(
        double aspectRatio,
        double maximumWidth,
        double maximumHeight,
        double expectedWidth,
        double expectedHeight)
    {
        var size = ScreenshotCoverFlowLayout.FitPresenter(aspectRatio, maximumWidth, maximumHeight);

        Assert.Equal(expectedWidth, size.Width, precision: 5);
        Assert.Equal(expectedHeight, size.Height, precision: 5);
        Assert.Equal(aspectRatio, size.Width / size.Height, precision: 5);
    }

    [Theory]
    [InlineData(double.NaN, 1_000d, 0)]
    [InlineData(0d, 0d, 0)]
    [InlineData(0d, 1_000d, 2)]
    public void InvalidPoseInput_FailsFast(double relativePosition, double viewportWidth, int transitionDirection)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScreenshotCoverFlowLayout.CalculatePose(relativePosition, viewportWidth, transitionDirection, reducedMotion: false));

    [Theory]
    [InlineData(0d, 600d, 360d)]
    [InlineData(1.7777777778d, 0d, 360d)]
    [InlineData(1.7777777778d, 600d, double.NaN)]
    public void InvalidPresenterBounds_FailFast(double aspectRatio, double maximumWidth, double maximumHeight)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScreenshotCoverFlowLayout.FitPresenter(aspectRatio, maximumWidth, maximumHeight));
}

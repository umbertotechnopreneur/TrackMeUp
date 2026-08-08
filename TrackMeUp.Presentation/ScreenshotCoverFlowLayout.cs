namespace TrackMeUp.Presentation;

/// <summary>Describes the transform and deterministic visual layer for one cover-flow presenter.</summary>
public readonly record struct ScreenshotCoverFlowPose(
    double TranslateX,
    double TranslateY,
    double Scale,
    double RotationY,
    double Opacity,
    double Depth,
    int ZIndex);

/// <summary>Describes an aspect-correct screenshot presenter size.</summary>
public readonly record struct ScreenshotCoverFlowSize(double Width, double Height);

/// <summary>Calculates collision-free, direction-aware poses for the screenshot Cover Flow.</summary>
public static class ScreenshotCoverFlowLayout
{
    private const int PoolRadius = ScreenshotCoverFlowProjection.StagingRadius;

    private static readonly double[] HorizontalPositionAnchors = [0d, 0.36d, 0.58d, 0.76d];
    private static readonly double[] ScaleAnchors = [1d, 0.76d, 0.60d, 0.48d];
    private static readonly double[] YawAnchors = [0d, 54d, 64d, 70d];
    private static readonly double[] OpacityAnchors = [1d, 0.88d, 0.48d, 0d];
    private static readonly double[] DepthAnchors = [88d, 30d, -8d, -40d];

    /// <summary>
    /// Calculates a presenter pose. The transition direction is -1, 0, or 1 and gives the
    /// incoming presenter a deterministic layer only when two covers are equally central.
    /// </summary>
    /// <param name="relativePosition">Fractional slot position relative to the selected cover.</param>
    /// <param name="viewportWidth">Available horizontal viewport in device-independent pixels.</param>
    /// <param name="transitionDirection">Current travel direction: -1, 0, or 1.</param>
    /// <param name="reducedMotion">Whether Windows animation effects are disabled.</param>
    /// <returns>The transform, opacity, depth, and z-order for the presenter.</returns>
    public static ScreenshotCoverFlowPose CalculatePose(
        double relativePosition,
        double viewportWidth,
        int transitionDirection,
        bool reducedMotion)
    {
        if (!double.IsFinite(relativePosition))
        {
            throw new ArgumentOutOfRangeException(nameof(relativePosition));
        }

        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        }

        if (transitionDirection is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(transitionDirection));
        }

        var distance = Math.Abs(relativePosition);
        var layerBias = transitionDirection != 0 && Math.Sign(relativePosition) == transitionDirection ? 1 : 0;
        var zIndex = Math.Max(0, 10_000 - (int)Math.Round(
            Math.Min(distance, PoolRadius) * 1_000d,
            MidpointRounding.AwayFromZero)) + layerBias;

        if (reducedMotion)
        {
            var translate = relativePosition * viewportWidth * 0.205d;
            var opacity = distance <= ScreenshotCoverFlowProjection.VisibleRadius + 0.5d ? 1d : 0d;
            return new ScreenshotCoverFlowPose(translate, 0d, 1d, 0d, opacity, layerBias * 0.5d, zIndex);
        }

        var direction = Math.Sign(relativePosition);
        var horizontalPosition = InterpolateHorizontalPosition(distance);
        var scale = InterpolateAnchor(ScaleAnchors, distance);
        var yaw = -direction * InterpolateAnchor(YawAnchors, distance);
        var opacityValue = distance >= PoolRadius ? 0d : InterpolateAnchor(OpacityAnchors, distance);
        var depth = InterpolateAnchor(DepthAnchors, distance) + (layerBias * 0.5d);
        var translateY = Math.Min(distance, ScreenshotCoverFlowProjection.VisibleRadius) * 6d;
        return new ScreenshotCoverFlowPose(
            direction * horizontalPosition * viewportWidth,
            translateY,
            scale,
            yaw,
            opacityValue,
            depth,
            zIndex);
    }

    /// <summary>Fits an image inside finite presenter bounds without cropping or changing its aspect ratio.</summary>
    /// <param name="aspectRatio">Source pixel width divided by source pixel height.</param>
    /// <param name="maximumWidth">Maximum presenter width in device-independent pixels.</param>
    /// <param name="maximumHeight">Maximum presenter height in device-independent pixels.</param>
    /// <returns>The largest aspect-correct size that fits inside both bounds.</returns>
    public static ScreenshotCoverFlowSize FitPresenter(
        double aspectRatio,
        double maximumWidth,
        double maximumHeight)
    {
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        }

        if (!double.IsFinite(maximumWidth) || maximumWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        }

        if (!double.IsFinite(maximumHeight) || maximumHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        }

        var width = maximumWidth;
        var height = width / aspectRatio;
        if (height > maximumHeight)
        {
            height = maximumHeight;
            width = height * aspectRatio;
        }

        return new ScreenshotCoverFlowSize(width, height);
    }

    private static double InterpolateHorizontalPosition(double distance)
    {
        var boundedDistance = Math.Clamp(distance, 0d, PoolRadius);
        if (boundedDistance <= 1d)
        {
            // Fan the two central covers apart before the midpoint so their faces meet instead of intersecting.
            return HorizontalPositionAnchors[1] * Math.Sin(boundedDistance * Math.PI / 2d);
        }

        return InterpolateAnchor(HorizontalPositionAnchors, boundedDistance);
    }

    private static double InterpolateAnchor(IReadOnlyList<double> anchors, double distance)
    {
        var boundedDistance = Math.Clamp(distance, 0d, PoolRadius);
        var lowerIndex = Math.Min((int)Math.Floor(boundedDistance), PoolRadius - 1);
        var upperIndex = Math.Min(lowerIndex + 1, PoolRadius);
        var fraction = boundedDistance - lowerIndex;
        return anchors[lowerIndex] + ((anchors[upperIndex] - anchors[lowerIndex]) * fraction);
    }
}

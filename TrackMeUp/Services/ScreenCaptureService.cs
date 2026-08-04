using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace TrackMeUp.Services;

/// <summary>
/// Captures desktop images to WEBP for local history and AI context.
/// </summary>
public sealed class ScreenCaptureService
{
    private static readonly Regex OwnedArtifactName = new(
        "^[0-9a-f]{32}_[0-9]+\\.[0-9]+\\.[0-9]+_(?:monitor-[1-9][0-9]*|active-window)(?:-raw)?\\.(?:webp|png)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
    private readonly string _appVersion;

    /// <summary>
    /// Creates capture service.
    /// </summary>
    /// <param name="appVersion">Optional version suffix used in file naming.</param>
    public ScreenCaptureService(string? appVersion = null)
    {
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? "0.0.0" : appVersion;
    }

    /// <summary>
    /// Compatibility method: captures without watermark and returns only analysis-ready paths.
    /// </summary>
    public IReadOnlyList<string> CaptureAll(string directory)
        => CaptureByMode(directory, "all-screens", includeWatermark: false).AnalysisScreenshotPaths;

    /// <summary>
    /// Compatibility method: captures without watermark and returns only analysis-ready paths.
    /// </summary>
    public IReadOnlyList<string> CaptureByMode(string directory, string captureMode)
        => CaptureByMode(directory, captureMode, includeWatermark: false).AnalysisScreenshotPaths;

    /// <summary>
    /// Captures screenshots and returns both AI-ready and storage-visible artifacts.
    /// </summary>
    /// <param name="directory">Directory where files are written.</param>
    /// <param name="captureMode">Capture mode: all-screens or active-window.</param>
    /// <param name="includeWatermark">If true, stores watermarked files to disk.</param>
    public ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, bool includeWatermark)
    {
        var captureId = Guid.NewGuid().ToString("N");
        return captureMode?.ToLowerInvariant() == "active-window"
            ? CaptureActiveWindow(directory, captureId, includeWatermark)
            : CaptureAllScreens(directory, captureId, includeWatermark);
    }

    /// <summary>Returns whether a path matches the versioned naming contract of a TrackMeUp screenshot artifact.</summary>
    /// <param name="path">Candidate file path.</param>
    /// <returns>True only for files that TrackMeUp capture code can create.</returns>
    public static bool IsOwnedArtifact(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return OwnedArtifactName.IsMatch(Path.GetFileName(path));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Captures all monitors and returns a pair of analysis and storage screenshot lists.
    /// </summary>
    public ScreenshotCaptureResult CaptureAllScreens(string directory, string captureId, bool includeWatermark)
    {
        Directory.CreateDirectory(directory);
        var displays = new List<NativeMethods.Rect>();
        NativeMethods.MonitorEnumProc callback = (IntPtr monitor, IntPtr deviceContext, ref NativeMethods.Rect rect, IntPtr data) =>
        {
            displays.Add(rect);
            return true;
        };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);

        var storage = new List<string>(displays.Count);
        var analysis = new List<string>(displays.Count);
        for (var index = 0; index < displays.Count; index++)
        {
            var paths = CaptureRect(
                directory,
                displays[index],
                $"monitor-{index + 1}",
                captureId,
                includeWatermark,
                $"Monitor {index + 1}");
            storage.AddRange(paths.Stored);
            analysis.AddRange(paths.Analysis);
        }

        return new ScreenshotCaptureResult(captureId, analysis.AsReadOnly(), storage.AsReadOnly());
    }

    /// <summary>
    /// Captures current foreground window and returns a pair of analysis/storage screenshot paths.
    /// </summary>
    public ScreenshotCaptureResult CaptureActiveWindow(string directory, string captureId, bool includeWatermark)
    {
        Directory.CreateDirectory(directory);
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException("No active foreground window found.");
        }

        if (!NativeMethods.GetWindowRect(window, out var rect))
        {
            throw new InvalidOperationException("Unable to read active window bounds.");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Invalid active window size.");
        }

        var paths = CaptureRect(directory, rect, "active-window", captureId, includeWatermark, "Active window");
        return new ScreenshotCaptureResult(captureId, paths.Analysis, paths.Stored);
    }

    private (IReadOnlyList<string> Analysis, IReadOnlyList<string> Stored) CaptureRect(
        string directory,
        NativeMethods.Rect rect,
        string stem,
        string captureId,
        bool includeWatermark,
        string watermarkSuffix)
    {
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var versionedStem = $"{captureId}_{_appVersion}_{stem}";
        var rawPngPath = Path.Combine(directory, $"{versionedStem}.png");
        var rawWebpPath = Path.Combine(directory, $"{versionedStem}-raw.webp");
        var storedPngPath = includeWatermark ? Path.Combine(directory, $"{versionedStem}.png") : rawPngPath;
        var storedWebpPath = Path.Combine(directory, $"{versionedStem}.webp");

        var machine = Environment.MachineName;
        var createdAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var watermarkText = $"{machine}  ·  {createdAt}  ·  {captureId}  ·  {watermarkSuffix}";

        using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            bitmap.Save(rawPngPath, ImageFormat.Png);
        }

        // Keep raw image as analysis source, then optionally render a watermarked visual copy for storage.
        ConvertPngToWebp(rawPngPath, rawWebpPath);
        var storagePath = rawWebpPath;
        if (includeWatermark)
        {
            AddWatermark(rawPngPath, storedPngPath, watermarkText);
            ConvertPngToWebp(storedPngPath, storedWebpPath);
            storagePath = storedWebpPath;
            TryDelete(storedPngPath);
        }

        TryDelete(rawPngPath);

        if (includeWatermark)
        {
            return (new[] { rawWebpPath }, new[] { storagePath });
        }

        return (new[] { rawWebpPath }, new[] { rawWebpPath });
    }

    /// <summary>
    /// Encodes a temporary PNG capture as a compact WEBP artifact.
    /// </summary>
    /// <param name="sourcePng">Temporary PNG capture path.</param>
    /// <param name="outputWebp">Destination WEBP path.</param>
    private static void ConvertPngToWebp(string sourcePng, string outputWebp)
    {
        // Decode and encode locally so no screenshot pixels leave the device during conversion.
        using var source = SKBitmap.Decode(sourcePng)
            ?? throw new InvalidOperationException($"Unable to decode screenshot '{sourcePng}'.");
        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 70);
        using var output = File.Create(outputWebp);
        data.SaveTo(output);
    }

    private static void AddWatermark(string sourcePng, string destinationPng, string watermarkText)
    {
        using var source = new Bitmap(sourcePng);
        using var destination = new Bitmap(source);
        using var graphics = Graphics.FromImage(destination);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
        var textBrush = new SolidBrush(Color.FromArgb(225, 240, 240, 255));
        var shadowBrush = new SolidBrush(Color.FromArgb(170, 5, 5, 5));
        var shadowOffset = new PointF(1f, 1f);
        var margin = 12;
        var textWidth = graphics.MeasureString(watermarkText, font).Width;
        var textHeight = graphics.MeasureString(watermarkText, font).Height;
        var x = Math.Max(margin, destination.Width - textWidth - margin);
        var y = Math.Max(margin, destination.Height - textHeight - margin);

        using var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far };
        var backgroundRect = new RectangleF(
            x - 9,
            y - 5,
            Math.Min(textWidth + 12, destination.Width - 10),
            textHeight + 8);
        using var backBrush = new SolidBrush(Color.FromArgb(130, 0, 0, 0));
        graphics.FillRoundedRectangle(backBrush, backgroundRect, 6f);

        graphics.DrawString(watermarkText, font, shadowBrush, x + shadowOffset.X, y + shadowOffset.Y, format);
        graphics.DrawString(watermarkText, font, textBrush, x, y, format);

        destination.Save(destinationPng, ImageFormat.Png);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for temporary conversion files.
        }
    }
}

internal static class GraphicsExtensions
{
    /// <summary>
    /// Draws rounded filled rectangle for watermark background.
    /// </summary>
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, radius, radius, 180, 90);
        path.AddArc(bounds.Right - radius, bounds.Y, radius, radius, 270, 90);
        path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();

        graphics.FillPath(brush, path);
    }
}

/// <summary>
/// Result of a screenshot pass used by AI and local storage separately.
/// </summary>
public sealed record ScreenshotCaptureResult(string CaptureId, IReadOnlyList<string> AnalysisScreenshotPaths, IReadOnlyList<string> StoredScreenshotPaths)
{
    /// <summary>
    /// Returns all generated files, used for retention and cleanup.
    /// </summary>
    public IReadOnlyList<string> AllScreenshotPaths
        => AnalysisScreenshotPaths.Concat(StoredScreenshotPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

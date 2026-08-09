using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using TrackMeUp.Providers;

namespace TrackMeUp.Services;

/// <summary>Captures one validated snapshot pass for application-layer orchestration.</summary>
public interface IScreenCaptureService
{
    /// <summary>Captures the configured screen scope and returns AI and retained artifacts.</summary>
    ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, bool includeWatermark, string captureOrigin);
}

/// <summary>
/// Captures desktop images to WEBP for local history and AI context.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    private static readonly Regex OwnedArtifactName = new(
        "^[0-9a-f]{32}_[0-9]+\\.[0-9]+\\.[0-9]+_(?:manual|scheduled)_(?:monitor-[1-9][0-9]*|active-window)(?:-raw)?\\.(?:webp|png)$",
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
    /// Captures screenshots and returns both AI-ready and storage-visible artifacts.
    /// </summary>
    /// <param name="directory">Directory where files are written.</param>
    /// <param name="captureMode">Capture mode: all-screens or active-window.</param>
    /// <param name="includeWatermark">If true, stores watermarked files to disk.</param>
    public ScreenshotCaptureResult CaptureByMode(
        string directory,
        string captureMode,
        bool includeWatermark,
        string captureOrigin)
    {
        var captureId = Guid.NewGuid().ToString("N");
        var validatedOrigin = ScreenshotCaptureOrigins.Validate(captureOrigin);
        return captureMode switch
        {
            "active-window" => CaptureActiveWindow(directory, captureId, includeWatermark, validatedOrigin),
            "all-screens" => CaptureAllScreens(directory, captureId, includeWatermark, validatedOrigin),
            _ => throw new ArgumentException("Screenshot capture mode must be 'all-screens' or 'active-window'.", nameof(captureMode))
        };
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

        return OwnedArtifactName.IsMatch(Path.GetFileName(path));
    }

    /// <summary>
    /// Captures all monitors and returns a pair of analysis and storage screenshot lists.
    /// </summary>
    public ScreenshotCaptureResult CaptureAllScreens(string directory, string captureId, bool includeWatermark, string captureOrigin)
    {
        var validatedOrigin = ScreenshotCaptureOrigins.Validate(captureOrigin);
        Directory.CreateDirectory(directory);
        var displays = EnumerateDisplays();
        var foreground = CaptureForegroundTarget();
        var focusedDisplay = ResolveFocusedDisplay(displays, foreground.WindowBounds);
        var focusMetadata = CreateFocusMetadata(focusedDisplay, foreground, focusedDisplay.Stem);
        var captureOrder = displays
            .Where(display => display.Index != focusedDisplay.Index)
            .Append(focusedDisplay);

        var storage = new List<string>(displays.Count);
        var analysis = new List<string>(displays.Count);
        foreach (var display in captureOrder)
        {
            var paths = CaptureRect(
                directory,
                display.Bounds,
                display.Stem,
                captureId,
                includeWatermark,
                display.Name,
                validatedOrigin);

            if (display.Index == focusedDisplay.Index)
            {
                analysis.InsertRange(0, paths.Analysis);
                storage.InsertRange(0, paths.Stored);
                continue;
            }

            analysis.AddRange(paths.Analysis);
            storage.AddRange(paths.Stored);
        }

        return new ScreenshotCaptureResult(
            captureId,
            analysis.AsReadOnly(),
            storage.AsReadOnly(),
            validatedOrigin,
            FocusMetadata: focusMetadata);
    }

    /// <summary>
    /// Captures current foreground window and returns a pair of analysis/storage screenshot paths.
    /// </summary>
    public ScreenshotCaptureResult CaptureActiveWindow(string directory, string captureId, bool includeWatermark, string captureOrigin)
    {
        var validatedOrigin = ScreenshotCaptureOrigins.Validate(captureOrigin);
        Directory.CreateDirectory(directory);
        var foreground = CaptureForegroundTarget();
        var focusedDisplay = ResolveFocusedDisplay(EnumerateDisplays(), foreground.WindowBounds);
        var focusMetadata = CreateFocusMetadata(focusedDisplay, foreground, "active-window");
        var paths = CaptureRect(
            directory,
            foreground.WindowBounds,
            "active-window",
            captureId,
            includeWatermark,
            "Active window",
            validatedOrigin);
        return new ScreenshotCaptureResult(
            captureId,
            paths.Analysis,
            paths.Stored,
            validatedOrigin,
            FocusMetadata: focusMetadata);
    }

    /// <summary>Returns the zero-based display index that contains the largest foreground-window area.</summary>
    /// <param name="displays">Physical display bounds returned by Windows monitor enumeration.</param>
    /// <param name="windowBounds">Physical foreground window bounds.</param>
    /// <returns>The index of the display with the largest intersection.</returns>
    internal static int SelectFocusedDisplayIndex(IReadOnlyList<NativeMethods.Rect> displays, NativeMethods.Rect windowBounds)
    {
        if (displays.Count == 0)
        {
            throw new ArgumentException("At least one display is required to resolve the focused screen.", nameof(displays));
        }

        var bestDisplayIndex = 0;
        var bestArea = -1L;
        for (var index = 0; index < displays.Count; index++)
        {
            var area = IntersectionArea(displays[index], windowBounds);
            if (area > bestArea)
            {
                bestDisplayIndex = index;
                bestArea = area;
            }
        }

        return bestDisplayIndex;
    }

    private static IReadOnlyList<CaptureDisplay> EnumerateDisplays()
    {
        var displays = new List<CaptureDisplay>();
        NativeMethods.MonitorEnumProc callback = (IntPtr monitor, IntPtr deviceContext, ref NativeMethods.Rect rect, IntPtr data) =>
        {
            displays.Add(new CaptureDisplay(displays.Count + 1, rect));
            return true;
        };

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero) || displays.Count == 0)
        {
            // Without monitor bounds the snapshot cannot name or prioritize the focused screen.
            throw new InvalidOperationException("Unable to enumerate display bounds for screenshot capture.");
        }

        return displays;
    }

    private static CaptureDisplay ResolveFocusedDisplay(IReadOnlyList<CaptureDisplay> displays, NativeMethods.Rect windowBounds)
    {
        var selectedIndex = SelectFocusedDisplayIndex(displays.Select(display => display.Bounds).ToArray(), windowBounds);
        return displays[selectedIndex];
    }

    private static ForegroundCaptureTarget CaptureForegroundTarget()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException("No active foreground window found.");
        }

        if (!NativeMethods.GetWindowRect(window, out var bounds))
        {
            throw new InvalidOperationException("Unable to read active window bounds.");
        }

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Invalid active window size.");
        }

        var processName = ReadProcessName(window);
        var title = ReadWindowTitle(window);
        var context = new ActivityContextProviderRegistry().Resolve(new ForegroundWindowInfo(processName, title));
        return new ForegroundCaptureTarget(window, bounds, context.Application, title);
    }

    private static string ReadProcessName(IntPtr window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return "System";
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.IsNullOrWhiteSpace(process.ProcessName) ? "System" : process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "System";
        }
        catch (InvalidOperationException)
        {
            return "System";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "System";
        }
    }

    private static string ReadWindowTitle(IntPtr window)
    {
        var builder = new StringBuilder(512);
        var length = NativeMethods.GetWindowText(window, builder, builder.Capacity);
        return length <= 0 ? string.Empty : builder.ToString().Trim();
    }

    private static ScreenshotFocusMetadata CreateFocusMetadata(
        CaptureDisplay display,
        ForegroundCaptureTarget foreground,
        string artifactStem)
        => new(
            display.Name,
            display.Index,
            display.Bounds.Left,
            display.Bounds.Top,
            display.Bounds.Right - display.Bounds.Left,
            display.Bounds.Bottom - display.Bounds.Top,
            foreground.ApplicationName,
            foreground.WindowTitle,
            artifactStem);

    private static long IntersectionArea(NativeMethods.Rect first, NativeMethods.Rect second)
    {
        var left = Math.Max(first.Left, second.Left);
        var top = Math.Max(first.Top, second.Top);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);
        return (long)width * height;
    }

    private (IReadOnlyList<string> Analysis, IReadOnlyList<string> Stored) CaptureRect(
        string directory,
        NativeMethods.Rect rect,
        string stem,
        string captureId,
        bool includeWatermark,
        string watermarkSuffix,
        string captureOrigin)
    {
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var validatedOrigin = ScreenshotCaptureOrigins.Validate(captureOrigin);
        var versionedStem = $"{captureId}_{_appVersion}_{validatedOrigin}_{stem}";
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
        // Load source bytes first so no file handle to sourcePng remains open.
        var sourceBytes = File.ReadAllBytes(sourcePng);

        using var sourceStream = new MemoryStream(sourceBytes, writable: false);
        using var source = new Bitmap(sourceStream);
        using var destination = new Bitmap(source);
        using var graphics = Graphics.FromImage(destination);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(225, 240, 240, 255));
        using var shadowBrush = new SolidBrush(Color.FromArgb(170, 5, 5, 5));
        var shadowOffset = new PointF(1f, 1f);
        var margin = 12;
        var textSize = graphics.MeasureString(watermarkText, font);
        var textWidth = textSize.Width;
        var textHeight = textSize.Height;
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
public sealed record ScreenshotCaptureResult(
    string CaptureId,
    IReadOnlyList<string> AnalysisScreenshotPaths,
    IReadOnlyList<string> StoredScreenshotPaths,
    string CaptureOrigin,
    IReadOnlyList<TrackMeUp.Application.ScreenshotTextSnapshot>? TextSnapshots = null,
    ScreenshotFocusMetadata? FocusMetadata = null)
{
    /// <summary>
    /// Returns all generated files, used for retention and cleanup.
    /// </summary>
    public IReadOnlyList<string> AllScreenshotPaths
        => AnalysisScreenshotPaths.Concat(StoredScreenshotPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>Describes the screen and foreground app associated with one screenshot pass.</summary>
/// <param name="ScreenName">Stable display label resolved during monitor enumeration.</param>
/// <param name="ScreenIndex">One-based display index resolved during monitor enumeration.</param>
/// <param name="ScreenLeft">Physical left edge of the focused display.</param>
/// <param name="ScreenTop">Physical top edge of the focused display.</param>
/// <param name="ScreenWidth">Physical focused display width.</param>
/// <param name="ScreenHeight">Physical focused display height.</param>
/// <param name="ApplicationName">Foreground application name resolved from process metadata.</param>
/// <param name="WindowTitle">Foreground window titlebar text captured at snapshot time.</param>
/// <param name="ArtifactStem">Artifact stem that should represent the focused target in the capture result.</param>
public sealed record ScreenshotFocusMetadata(
    string ScreenName,
    int ScreenIndex,
    int ScreenLeft,
    int ScreenTop,
    int ScreenWidth,
    int ScreenHeight,
    string ApplicationName,
    string WindowTitle,
    string ArtifactStem);

internal readonly record struct CaptureDisplay(int Index, NativeMethods.Rect Bounds)
{
    internal string Stem => $"monitor-{Index}";

    internal string Name => $"Monitor {Index}";
}

internal sealed record ForegroundCaptureTarget(
    IntPtr WindowHandle,
    NativeMethods.Rect WindowBounds,
    string ApplicationName,
    string WindowTitle);

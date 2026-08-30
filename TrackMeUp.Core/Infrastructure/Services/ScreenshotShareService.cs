// SPDX-License-Identifier: MIT

namespace TrackMeUp.Services;

/// <summary>Validates TrackMeUp screenshot ownership before invoking the shared Windows file-share service.</summary>
public sealed class ScreenshotShareService
{
    private readonly WindowsFileShareService _fileShare;

    /// <summary>Creates the screenshot-specific share adapter.</summary>
    public ScreenshotShareService(WindowsFileShareService? fileShare = null) =>
        _fileShare = fileShare ?? new WindowsFileShareService();

    /// <summary>
    /// Registers the selected screenshot as shareable content and opens the Windows Share UI.
    /// </summary>
    /// <param name="screenshotPath">Absolute path to a TrackMeUp-owned screenshot artifact.</param>
    /// <param name="windowHandle">HWND that owns the Share UI.</param>
    /// <returns>The validated screenshot path supplied to the Share UI.</returns>
    public string Share(string screenshotPath, IntPtr windowHandle)
    {
        var fullPath = ValidateScreenshotPath(screenshotPath);
        return _fileShare.Share(fullPath, windowHandle, Path.GetFileName(fullPath), "TrackMeUp screenshot");
    }

    private static string ValidateScreenshotPath(string screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath) || !Path.IsPathFullyQualified(screenshotPath))
        {
            throw new ArgumentException("The screenshot path must be an absolute path.", nameof(screenshotPath));
        }

        var fullPath = Path.GetFullPath(screenshotPath);
        if (!ScreenCaptureService.IsOwnedArtifact(fullPath))
        {
            throw new ArgumentException("The path is not a TrackMeUp-owned screenshot artifact.", nameof(screenshotPath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The TrackMeUp screenshot no longer exists.", fullPath);
        }

        return fullPath;
    }
}

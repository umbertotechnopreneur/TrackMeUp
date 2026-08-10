using System.Diagnostics;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>Validates, deletes, and relaunches the complete local TrackMeUp data set.</summary>
public sealed class AtomicResetService
{
    private readonly UtilityService _utilities = new();

    /// <summary>Builds the reset plan for the current installation without deleting any data.</summary>
    public AtomicResetPlan CreatePlan(string dataDirectory, string screenshotDirectory)
    {
        var dataRoot = NormalizeDirectory(dataDirectory, nameof(dataDirectory));
        var expectedRoot = NormalizeDirectory(_utilities.AppDataDirectory, nameof(dataDirectory));
        if (!string.Equals(dataRoot, expectedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Atomic reset can only target the current TrackMeUp data directory.");
        }

        var screenshots = NormalizeDirectory(screenshotDirectory, nameof(screenshotDirectory));
        var executable = Path.GetFullPath(Environment.ProcessPath
            ?? throw new InvalidOperationException("The current TrackMeUp executable path is unavailable."));
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("The current TrackMeUp executable is unavailable.", executable);
        }

        return new AtomicResetPlan(dataRoot, screenshots, executable);
    }

    /// <summary>Deletes the validated application data and launches a fresh TrackMeUp process.</summary>
    public void ExecuteAndRelaunch(AtomicResetPlan plan)
    {
        DeleteApplicationData(plan);
        var startInfo = new ProcessStartInfo
        {
            FileName = plan.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(plan.ExecutablePath)
                ?? throw new InvalidOperationException("The TrackMeUp executable directory is unavailable."),
            UseShellExecute = true
        };

        // Relaunch happens only after every owned target has been removed successfully.
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not relaunch TrackMeUp after the atomic reset.");
    }

    /// <summary>Deletes application-owned files from a previously validated reset plan.</summary>
    internal static void DeleteApplicationData(AtomicResetPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var dataRoot = NormalizeDirectory(plan.DataDirectory, nameof(plan));
        var screenshots = NormalizeDirectory(plan.ScreenshotDirectory, nameof(plan));
        ValidateDataRoot(dataRoot);

        if (!IsSameOrDescendant(screenshots, dataRoot) && Directory.Exists(screenshots))
        {
            foreach (var path in Directory.EnumerateFiles(screenshots, "*", SearchOption.TopDirectoryOnly)
                         .Where(ScreenCaptureService.IsOwnedArtifact))
            {
                File.Delete(path);
            }

            if (!Directory.EnumerateFileSystemEntries(screenshots).Any())
            {
                Directory.Delete(screenshots);
            }
        }

        if (Directory.Exists(dataRoot))
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    private static void ValidateDataRoot(string dataRoot)
    {
        var root = Path.GetPathRoot(dataRoot);
        if (string.IsNullOrWhiteSpace(root)
            || string.Equals(dataRoot, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(dataRoot), "TrackMeUp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Atomic reset refused an unsafe application-data target.");
        }
    }

    private static bool IsSameOrDescendant(string path, string parent)
    {
        if (string.Equals(path, parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A fully qualified directory is required.", parameterName);
        }

        return Path.GetFullPath(value)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

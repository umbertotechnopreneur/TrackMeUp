namespace TrackMeUp.Runtime;

/// <summary>Resolves whether the shared tracking runtime should start for one application launch.</summary>
public static class TrackingStartupPolicy
{
    /// <summary>Combines explicit launch switches with the persisted start-on-launch preference.</summary>
    /// <param name="options">Parsed bootstrap options for the current process.</param>
    /// <param name="settings">Persisted application settings.</param>
    /// <returns><see langword="true"/> when tracking should be started or kept running.</returns>
    public static bool ShouldStart(LaunchOptions options, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        // Explicit pause and safe mode suppress both persisted and command-line start requests.
        return !options.Paused
            && !options.SafeMode
            && (options.StartTracking || settings.StartTrackingOnLaunch);
    }
}

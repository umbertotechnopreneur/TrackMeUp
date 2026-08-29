using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrackMeUp.Application;
using TrackMeUp.Runtime;

namespace TrackMeUp.Presentation;

/// <summary>Contains the persisted settings, dashboard, and latest session resolved for the first main-window frame.</summary>
public sealed record MainWindowStartupState(
    AppSettings Settings,
    DashboardState Dashboard,
    LastSessionState? LastSession,
    bool StartedPaused);

/// <summary>Provides minimal observable state without depending on XAML or Spectre.Console.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Updates an observable backing field.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>Provides player state and actions to the WinUI player view.</summary>
public sealed class MainViewModel
{
    private readonly ITrackMeUpApplication _application;

    /// <summary>Initializes the view model with the shared application facade.</summary>
    public MainViewModel(ITrackMeUpApplication application) =>
        _application = application ?? throw new ArgumentNullException(nameof(application));

    /// <summary>Loads launch settings and applies the centralized automatic-tracking policy before the first dashboard render.</summary>
    public async Task<OperationResult<MainWindowStartupState>> InitializeAsync(
        LaunchOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settingsResult = await _application.GetSettingsAsync(cancellationToken);
        if (!settingsResult.Succeeded || settingsResult.Value is null)
        {
            return new OperationResult<MainWindowStartupState>(
                false,
                settingsResult.Code,
                settingsResult.MessageKey,
                null,
                settingsResult.Issues);
        }

        var effectiveSettings = settingsResult.Value with
        {
            UiLanguage = options.Language ?? settingsResult.Value.UiLanguage,
            Theme = options.Theme ?? settingsResult.Value.Theme,
            FlyoutPosition = options.Position ?? settingsResult.Value.FlyoutPosition
        };
        var shouldStart = TrackingStartupPolicy.ShouldStart(options, effectiveSettings);
        var dashboardResult = shouldStart
            ? await _application.StartTrackingAsync(
                new StartTrackingRequest(options.SafeMode, "winui.launch"),
                cancellationToken)
            : await _application.GetDashboardAsync(cancellationToken);
        if (!dashboardResult.Succeeded || dashboardResult.Value is null)
        {
            return new OperationResult<MainWindowStartupState>(
                false,
                dashboardResult.Code,
                dashboardResult.MessageKey,
                null,
                dashboardResult.Issues);
        }

        var lastSessionResult = await _application.GetLastSessionAsync(cancellationToken);
        if (!lastSessionResult.Succeeded)
        {
            return new OperationResult<MainWindowStartupState>(
                false,
                lastSessionResult.Code,
                lastSessionResult.MessageKey,
                null,
                lastSessionResult.Issues);
        }

        return OperationResult<MainWindowStartupState>.Success(
            "main.initialized",
            "MainWindowInitialized",
            new MainWindowStartupState(
                effectiveSettings,
                dashboardResult.Value,
                lastSessionResult.Value,
                StartedPaused: !shouldStart && !dashboardResult.Value.IsTracking));
    }

    /// <summary>Loads player data.</summary>
    public Task<OperationResult<DashboardState>> RefreshAsync(CancellationToken cancellationToken) =>
        _application.GetDashboardAsync(cancellationToken);

    /// <summary>Toggles tracking through the application facade.</summary>
    public Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken) =>
        _application.ToggleTrackingAsync(cancellationToken);

    /// <summary>Loads the latest session card.</summary>
    public Task<OperationResult<LastSessionState?>> RefreshLastSessionAsync(CancellationToken cancellationToken) =>
        _application.GetLastSessionAsync(cancellationToken);
}

/// <summary>Provides typed report queries to a presentation view.</summary>
public sealed class ReportViewModel
{
    private readonly ITrackMeUpApplication _application;

    /// <summary>Initializes the report view model.</summary>
    public ReportViewModel(ITrackMeUpApplication application) =>
        _application = application ?? throw new ArgumentNullException(nameof(application));

    /// <summary>Loads one aggregate report snapshot through the shared application facade.</summary>
    public async Task<OperationResult<ReportSnapshot>> LoadAsync(ReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var result = await _application.GetReportAsync(query, cancellationToken);
        if (result.Succeeded && result.Value is null)
        {
            return OperationResult<ReportSnapshot>.Failure("report.snapshot.missing", "ReportSnapshotMissing");
        }

        return result;
    }
}

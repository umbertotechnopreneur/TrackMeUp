using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrackMeUp.Application;
using TrackMeUp.Runtime;

namespace TrackMeUp.Presentation;

/// <summary>Contains the persisted presentation settings and dashboard resolved for the first main-window frame.</summary>
public sealed record MainWindowStartupState(AppSettings Settings, DashboardState Dashboard);

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
public sealed class MainViewModel : ViewModelBase
{
    private readonly ITrackMeUpApplication _application;
    private DashboardState? _dashboard;
    private LastSessionState? _lastSession;

    /// <summary>Initializes the view model with the shared application facade.</summary>
    public MainViewModel(ITrackMeUpApplication application)
    {
        _application = application;
        _application.RuntimeStateChanged += OnRuntimeStateChanged;
    }

    /// <summary>Gets the last dashboard state received from the application.</summary>
    public DashboardState? Dashboard { get => _dashboard; private set => Set(ref _dashboard, value); }

    /// <summary>Gets the latest recorded session.</summary>
    public LastSessionState? LastSession { get => _lastSession; private set => Set(ref _lastSession, value); }

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
        var dashboardResult = TrackingStartupPolicy.ShouldStart(options, effectiveSettings)
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

        Dashboard = dashboardResult.Value;
        return OperationResult<MainWindowStartupState>.Success(
            "main.initialized",
            "MainWindowInitialized",
            new MainWindowStartupState(effectiveSettings, dashboardResult.Value));
    }

    /// <summary>Loads player data.</summary>
    public async Task<OperationResult<DashboardState>> RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _application.GetDashboardAsync(cancellationToken);
        if (result.Succeeded) Dashboard = result.Value;
        return result;
    }

    /// <summary>Toggles tracking through the application facade.</summary>
    public async Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken)
    {
        var result = await _application.ToggleTrackingAsync(cancellationToken);
        if (result.Succeeded) Dashboard = result.Value;
        return result;
    }

    /// <summary>Loads the latest session card.</summary>
    public async Task<OperationResult<LastSessionState?>> RefreshLastSessionAsync(CancellationToken cancellationToken)
    {
        var result = await _application.GetLastSessionAsync(cancellationToken);
        if (result.Succeeded) LastSession = result.Value;
        return result;
    }

    private void OnRuntimeStateChanged(object? sender, RuntimeStateChangedEventArgs eventArgs) => Dashboard = eventArgs.Dashboard;
}

/// <summary>Provides typed editable settings to a view without persistence dependencies.</summary>
public sealed class OptionsViewModel : ViewModelBase
{
    private readonly ITrackMeUpApplication _application;
    private AppSettings? _settings;

    /// <summary>Initializes the options view model.</summary>
    public OptionsViewModel(ITrackMeUpApplication application) => _application = application;

    /// <summary>Gets the current editable settings snapshot.</summary>
    public AppSettings? Settings { get => _settings; private set => Set(ref _settings, value); }

    /// <summary>Loads settings into the presentation snapshot.</summary>
    public async Task<OperationResult<AppSettings>> LoadAsync(CancellationToken cancellationToken)
    {
        var result = await _application.GetSettingsAsync(cancellationToken);
        if (result.Succeeded) Settings = result.Value;
        return result;
    }

    /// <summary>Validates and saves an allowed patch.</summary>
    public async Task<OperationResult<AppSettings>> SaveAsync(SettingsPatch patch, CancellationToken cancellationToken)
    {
        var result = await _application.PatchSettingsAsync(patch, cancellationToken);
        if (result.Succeeded) Settings = result.Value;
        return result;
    }
}

/// <summary>Provides immutable product metadata to an About view.</summary>
public sealed class AboutViewModel
{
    private readonly ITrackMeUpApplication _application;

    /// <summary>Initializes the About view model.</summary>
    public AboutViewModel(ITrackMeUpApplication application) => _application = application;

    /// <summary>Loads product details and safe links.</summary>
    public Task<OperationResult<ProductInformation>> LoadAsync(CancellationToken cancellationToken) => _application.GetProductInformationAsync(cancellationToken);
}

/// <summary>Provides focus-session actions.</summary>
public sealed class FocusSessionViewModel
{
    private readonly ITrackMeUpApplication _application;

    /// <summary>Initializes the focus-session view model.</summary>
    public FocusSessionViewModel(ITrackMeUpApplication application) => _application = application;

    /// <summary>Starts a focus session.</summary>
    public Task<OperationResult<FocusSessionState>> StartAsync(string objective, CancellationToken cancellationToken) => _application.StartFocusSessionAsync(new StartFocusSessionRequest(objective), cancellationToken);

    /// <summary>Stops a focus session.</summary>
    public Task<OperationResult<FocusSessionSummary?>> StopAsync(bool summarize, CancellationToken cancellationToken) => _application.StopFocusSessionAsync(summarize, cancellationToken);
}

/// <summary>Provides privacy-rule operations.</summary>
public sealed class PrivacyViewModel
{
    private readonly ITrackMeUpApplication _application;

    /// <summary>Initializes the privacy view model.</summary>
    public PrivacyViewModel(ITrackMeUpApplication application) => _application = application;

    /// <summary>Loads privacy rules.</summary>
    public Task<OperationResult<IReadOnlyList<PrivacyRule>>> LoadAsync(CancellationToken cancellationToken) => _application.GetPrivacyRulesAsync(cancellationToken);
}

/// <summary>Provides typed report queries and progress ownership to a presentation view.</summary>
public sealed class ReportViewModel : ViewModelBase
{
    private readonly ITrackMeUpApplication _application;
    private ReportQuery? _query;
    private ReportSnapshot? _snapshot;
    private bool _isLoading;
    private string? _errorCode;

    /// <summary>Initializes the report view model.</summary>
    public ReportViewModel(ITrackMeUpApplication application) => _application = application;

    /// <summary>Gets the most recently requested report query.</summary>
    public ReportQuery? Query { get => _query; private set => Set(ref _query, value); }

    /// <summary>Gets the most recent complete report snapshot.</summary>
    public ReportSnapshot? Snapshot { get => _snapshot; private set => Set(ref _snapshot, value); }

    /// <summary>Gets whether a report query is in progress.</summary>
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }

    /// <summary>Gets the stable error code returned by the latest failed query.</summary>
    public string? ErrorCode { get => _errorCode; private set => Set(ref _errorCode, value); }

    /// <summary>Loads one aggregate report snapshot through the shared application facade.</summary>
    public async Task<OperationResult<ReportSnapshot>> LoadAsync(ReportQuery query, CancellationToken cancellationToken)
    {
        Query = query;
        ErrorCode = null;
        IsLoading = true;
        try
        {
            var result = await _application.GetReportAsync(query, cancellationToken);
            if (!result.Succeeded)
            {
                ErrorCode = result.Code;
                return result;
            }

            if (result.Value is null)
            {
                ErrorCode = "report.snapshot.missing";
                return OperationResult<ReportSnapshot>.Failure("report.snapshot.missing", "ReportSnapshotMissing");
            }

            Snapshot = result.Value;
            return result;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Generates today's report.</summary>
    public Task<OperationResult<string>> GenerateTodayAsync(string? outputDirectory, bool open, CancellationToken cancellationToken) => _application.GenerateTodayReportAsync(outputDirectory, open, cancellationToken);
}

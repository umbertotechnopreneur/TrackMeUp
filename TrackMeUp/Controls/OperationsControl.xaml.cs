using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects operational input and renders DTOs returned by the shared application facade.</summary>
public sealed partial class OperationsControl : UserControl
{
    private ITrackMeUpApplication? _application;
    private MicaDialogService? _dialogs;
    private Window? _ownerWindow;
    private LocalizationService _strings = new("system");
    private bool _operationInProgress;
    private bool _returnToOverviewOnBack;
    private Control? _lastLandingLink;
    private SnapshotAiOperationsControl? _snapshotAiSection;
    private ReportsOperationsControl? _reportsSection;
    private PrivacyOperationsControl? _privacySection;
    private RetentionOperationsControl? _retentionSection;
    private PluginOperationsControl? _pluginsSection;
    private InstallationTransferOperationsControl? _installationTransferSection;

    /// <summary>Creates the passive operational surface.</summary>
    public OperationsControl() => InitializeComponent();

    /// <summary>Occurs when back navigation is requested from the tools landing page.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Occurs after the visible operational page changes and the host may need to re-measure.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Occurs after the runtime has accepted both confirmations and prepared the reset plan.</summary>
    internal event EventHandler<AtomicResetPreparedEventArgs>? AtomicResetPrepared;

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        _snapshotAiSection?.ApplyLanguage(language);
        _reportsSection?.ApplyLanguage(language);
        _privacySection?.ApplyLanguage(language);
        _retentionSection?.ApplyLanguage(language);
        _pluginsSection?.ApplyLanguage(language);
        _installationTransferSection?.ApplyLanguage(language);
        ApplyNavigationAccessibility(OpenSnapshotAiLink, "Options.Navigation.SnapshotAi.Action", "Options.Navigation.SnapshotAi.Description");
        ApplyNavigationAccessibility(OpenReportsLink, "Options.Navigation.Reports.Action", "Options.Navigation.Reports.Description");
        ApplyNavigationAccessibility(OpenPrivacyLink, "Options.Navigation.Privacy.Action", "Options.Navigation.Privacy.Description");
        ApplyNavigationAccessibility(OpenRetentionLink, "Options.Navigation.Retention.Action", "Options.Navigation.Retention.Description");
        ApplyNavigationAccessibility(OpenPluginsLink, "Options.Navigation.Plugins.Action", "Options.Navigation.Plugins.Description");
        ApplyOptionalNavigationAccessibility(
            OpenInstallationTransferLink,
            "Operations.InstallationTransfer.Navigation.Action",
            "Manage installations and archives",
            "Operations.InstallationTransfer.Navigation.Description",
            "Name installations and transfer local data and screenshots between systems.");
        AutomationProperties.SetName(OperationProgress, _strings.Translate("Operations.Status.InProgress.Title"));
        AutomationProperties.SetName(AtomicNukeButton, _strings.Translate("Operations.AtomicNuke.Action"));
        AutomationProperties.SetHelpText(AtomicNukeButton, _strings.Translate("Operations.AtomicNuke.Description"));
    }

    /// <summary>Connects the surface to the facade owned by the composition root.</summary>
    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
    }

    /// <summary>Returns to the landing page for local tool navigation, or to the surface that opened a direct settings link.</summary>
    public void NavigateBack()
    {
        if (DetailScroll.Visibility == Visibility.Visible && _returnToOverviewOnBack)
        {
            ShowOverview(restoreFocus: true);
            return;
        }

        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a focused operational page without executing any operation.</summary>
    internal void NavigateTo(OperationsSection section, bool returnToOverview = true)
    {
        _returnToOverviewOnBack = returnToOverview;
        var selectedSection = EnsureSection(section);
        HideDetailSections();
        OperationsScroll.Visibility = Visibility.Collapsed;
        DetailScroll.Visibility = Visibility.Visible;
        selectedSection.Visibility = Visibility.Visible;
        DetailScroll.ChangeView(null, 0, null, disableAnimation: true);
        NotifyLayoutChanged();
        if (section == OperationsSection.Plugins)
        {
            _ = _pluginsSection!.LoadAsync();
        }
    }

    /// <summary>Shows the tools landing page without changing application state.</summary>
    internal void ShowOverview() => ShowOverview(restoreFocus: false);

    private ITrackMeUpApplication Application => _application ?? throw new InvalidOperationException("OperationsControl must be initialized before use.");

    private MicaDialogService Dialogs => _dialogs ?? throw new InvalidOperationException("OperationsControl must be initialized before use.");

    private Window OwnerWindow => _ownerWindow ?? throw new InvalidOperationException("OperationsControl must be initialized before use.");

    private async void RuntimeHealthButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.GetRuntimeHealthAsync(token));
        if (result is not { Succeeded: true, Value: { } health })
        {
            return;
        }

        RuntimeHealthEmptyText.Visibility = Visibility.Collapsed;
        RuntimeHealthSummary.Visibility = Visibility.Visible;
        RuntimeVersionValue.Text = health.ProductVersion;
        RuntimeProtocolValue.Text = health.ProtocolVersion.ToString(CultureInfo.InvariantCulture);
        RuntimeRoleValue.Text = _strings.Translate(health.IsRuntimeOwner
            ? "Operations.Runtime.Role.Owner"
            : "Operations.Runtime.Role.Client");
        RuntimeCapabilitiesList.ItemsSource = health.Capabilities.OrderBy(capability => capability, StringComparer.OrdinalIgnoreCase).ToArray();

        if (health.Observability is { } observability)
        {
            RuntimeConsoleValue.Text = EnabledDisabled(observability.ConsoleLoggingEnabled);
            RuntimeFileValue.Text = EnabledDisabled(observability.FileLoggingEnabled);
            RuntimeSentryValue.Text = observability.SentryStatus;
            RuntimePiiValue.Text = YesNo(observability.SendsDefaultPii);
            ObservabilityUnavailableText.Visibility = Visibility.Collapsed;
        }
        else
        {
            RuntimeConsoleValue.Text = RuntimeFileValue.Text = RuntimeSentryValue.Text = RuntimePiiValue.Text = "—";
            ObservabilityUnavailableText.Text = _strings.Translate("Operations.Runtime.ObservabilityUnavailable");
            ObservabilityUnavailableText.Visibility = Visibility.Visible;
        }
    }

    private async void SystemSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.CaptureSystemSnapshotAsync(token));
        if (result is not { Succeeded: true, Value: { } snapshot })
        {
            return;
        }

        SystemSnapshotEmptyText.Visibility = Visibility.Collapsed;
        SystemSnapshotSummary.Visibility = Visibility.Visible;
        SystemCpuValue.Text = $"{snapshot.CpuUsagePercent}%";
        SystemCpuDetail.Text = FormatTemperature(snapshot.CpuTemperatureCelsius);
        SystemGpuValue.Text = FormatPercent(snapshot.GpuUsagePercent);
        SystemGpuDetail.Text = FormatTemperature(snapshot.GpuTemperatureCelsius);
        SystemMemoryValue.Text = $"{FormatMemory(snapshot.MemoryUsedMb)} / {FormatMemory(snapshot.MemoryTotalMb)}";
        SystemNetworkValue.Text = $"↑ {FormatBytes(snapshot.Network.UploadBytesPerSecond)}/s\n↓ {FormatBytes(snapshot.Network.DownloadBytesPerSecond)}/s";
        SystemDisksList.ItemsSource = snapshot.Disks.Count == 0
            ? [_strings.Translate("Operations.System.NoStorage")]
            : snapshot.Disks.Select(disk => _strings.Format(
                "Operations.System.StorageRow",
                disk.Drive,
                FormatBytes(disk.FreeBytes),
                FormatBytes(disk.TotalBytes))).ToArray();
    }

    private void OpenSnapshotAiLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.SnapshotAi, sender);

    private void OpenReportsLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Reports, sender);

    private void OpenPrivacyLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Privacy, sender);

    private void OpenRetentionLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Retention, sender);

    private void OpenPluginsLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Plugins, sender);

    private void OpenInstallationTransferLink_Click(object sender, RoutedEventArgs e)
    {
        _lastLandingLink = sender as Control;
        _returnToOverviewOnBack = true;
        OperationsScroll.Visibility = Visibility.Collapsed;
        DetailScroll.Visibility = Visibility.Visible;
        HideDetailSections();
        var section = EnsureInstallationTransferSection();
        section.Visibility = Visibility.Visible;
        DetailScroll.ChangeView(null, 0, null, disableAnimation: true);
        NotifyLayoutChanged();
        _ = section.LoadAsync();
    }

    private async void AtomicNukeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress)
        {
            ShowStatus(
                _strings.Translate("Operations.Status.InProgress.Title"),
                _strings.Translate("Operations.Status.InProgress.Message"),
                InfoBarSeverity.Warning);
            return;
        }

        var firstConfirmation = await Dialogs.ConfirmAsync(
            Application,
            OwnerWindow,
            MicaDialogRequest.Confirmation(
                _strings.Translate("Operations.AtomicNuke.First.Title"),
                _strings.Translate("Operations.AtomicNuke.First.Message"),
                _strings.Translate("Operations.AtomicNuke.First.Continue"),
                _strings.Translate("Operations.AtomicNuke.Cancel"),
                Windows.UI.Color.FromArgb(255, 232, 118, 43)),
            ActualTheme);
        if (!firstConfirmation)
        {
            return;
        }

        var finalConfirmation = await Dialogs.ConfirmAsync(
            Application,
            OwnerWindow,
            MicaDialogRequest.Confirmation(
                _strings.Translate("Operations.AtomicNuke.Second.Title"),
                _strings.Translate("Operations.AtomicNuke.Second.Message"),
                _strings.Translate("Operations.AtomicNuke.Second.Confirm"),
                _strings.Translate("Operations.AtomicNuke.Cancel"),
                Windows.UI.Color.FromArgb(255, 200, 59, 49)),
            ActualTheme);
        if (!finalConfirmation)
        {
            return;
        }

        var result = await ExecuteAsync((application, token) => application.PrepareAtomicResetAsync(
            new AtomicResetRequest(firstConfirmation, finalConfirmation),
            token));
        if (result is { Succeeded: true, Value: { } plan })
        {
            AtomicResetPrepared?.Invoke(this, new AtomicResetPreparedEventArgs(plan));
        }
    }

    private void OpenSection(OperationsSection section, object sender)
    {
        _lastLandingLink = sender as Control;
        NavigateTo(section);
    }

    private void ShowOverview(bool restoreFocus)
    {
        _returnToOverviewOnBack = false;
        HideDetailSections();
        DetailScroll.Visibility = Visibility.Collapsed;
        OperationsScroll.Visibility = Visibility.Visible;
        OperationsScroll.ChangeView(null, 0, null, disableAnimation: true);
        NotifyLayoutChanged();
        if (restoreFocus)
        {
            _lastLandingLink?.Focus(FocusState.Programmatic);
        }
    }

    private FrameworkElement EnsureSection(OperationsSection section) => section switch
    {
        OperationsSection.SnapshotAi => EnsureSnapshotAiSection(),
        OperationsSection.Reports => EnsureReportsSection(),
        OperationsSection.Privacy => EnsurePrivacySection(),
        OperationsSection.Retention => EnsureRetentionSection(),
        OperationsSection.Plugins => EnsurePluginsSection(),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unsupported operations section.")
    };

    private SnapshotAiOperationsControl EnsureSnapshotAiSection()
    {
        if (_snapshotAiSection is not null)
        {
            return _snapshotAiSection;
        }

        _snapshotAiSection = new SnapshotAiOperationsControl();
        SnapshotAiHost.Content = _snapshotAiSection;
        _snapshotAiSection.Initialize(Application, Dialogs, OwnerWindow, OperationBanner);
        _snapshotAiSection.ApplyLanguage(_strings.Language);
        return _snapshotAiSection;
    }

    private ReportsOperationsControl EnsureReportsSection()
    {
        if (_reportsSection is not null)
        {
            return _reportsSection;
        }

        _reportsSection = new ReportsOperationsControl();
        ReportsHost.Content = _reportsSection;
        _reportsSection.Initialize(Application, Dialogs, OwnerWindow, OperationBanner);
        _reportsSection.ApplyLanguage(_strings.Language);
        return _reportsSection;
    }

    private PrivacyOperationsControl EnsurePrivacySection()
    {
        if (_privacySection is not null)
        {
            return _privacySection;
        }

        _privacySection = new PrivacyOperationsControl();
        PrivacyHost.Content = _privacySection;
        _privacySection.Initialize(Application, Dialogs, OwnerWindow, OperationBanner);
        _privacySection.ApplyLanguage(_strings.Language);
        return _privacySection;
    }

    private RetentionOperationsControl EnsureRetentionSection()
    {
        if (_retentionSection is not null)
        {
            return _retentionSection;
        }

        _retentionSection = new RetentionOperationsControl();
        RetentionHost.Content = _retentionSection;
        _retentionSection.Initialize(Application, Dialogs, OwnerWindow, OperationBanner);
        _retentionSection.ApplyLanguage(_strings.Language);
        return _retentionSection;
    }

    private PluginOperationsControl EnsurePluginsSection()
    {
        if (_pluginsSection is not null)
        {
            return _pluginsSection;
        }

        _pluginsSection = new PluginOperationsControl();
        PluginsHost.Content = _pluginsSection;
        _pluginsSection.Initialize(Application, Dialogs, OwnerWindow, OperationBanner);
        _pluginsSection.ApplyLanguage(_strings.Language);
        return _pluginsSection;
    }

    private InstallationTransferOperationsControl EnsureInstallationTransferSection()
    {
        if (_installationTransferSection is not null)
        {
            return _installationTransferSection;
        }

        _installationTransferSection = new InstallationTransferOperationsControl();
        InstallationTransferHost.Content = _installationTransferSection;
        _installationTransferSection.Initialize(Application, Dialogs, OwnerWindow, OperationBanner);
        _installationTransferSection.ApplyLanguage(_strings.Language);
        return _installationTransferSection;
    }

    private void HideDetailSections()
    {
        _snapshotAiSection?.Visibility = Visibility.Collapsed;
        _reportsSection?.Visibility = Visibility.Collapsed;
        _privacySection?.Visibility = Visibility.Collapsed;
        _retentionSection?.Visibility = Visibility.Collapsed;
        _pluginsSection?.Visibility = Visibility.Collapsed;
        _installationTransferSection?.Visibility = Visibility.Collapsed;
    }

    private void ApplyNavigationAccessibility(Control link, string actionKey, string descriptionKey)
    {
        AutomationProperties.SetName(link, _strings.Translate(actionKey));
        AutomationProperties.SetHelpText(link, _strings.Translate(descriptionKey));
    }

    private void ApplyOptionalNavigationAccessibility(
        Control link,
        string actionKey,
        string actionFallback,
        string descriptionKey,
        string descriptionFallback)
    {
        AutomationProperties.SetName(link, _strings.TryTranslate(actionKey, out var action) ? action : actionFallback);
        AutomationProperties.SetHelpText(link, _strings.TryTranslate(descriptionKey, out var description) ? description : descriptionFallback);
    }

    private void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private async Task<OperationResult<T>?> ExecuteAsync<T>(Func<ITrackMeUpApplication, CancellationToken, Task<OperationResult<T>>> operation)
    {
        if (_operationInProgress)
        {
            ShowStatus(
                _strings.Translate("Operations.Status.InProgress.Title"),
                _strings.Translate("Operations.Status.InProgress.Message"),
                InfoBarSeverity.Warning);
            return null;
        }

        _operationInProgress = true;
        OperationsScroll.IsEnabled = false;
        OperationProgress.IsActive = true;
        OperationProgress.Visibility = Visibility.Visible;
        try
        {
            var result = await operation(Application, CancellationToken.None);
            if (result.Succeeded)
            {
                ShowStatus(
                    _strings.Translate("Operations.Status.Completed.Title"),
                    ResultMessage(result.MessageKey, succeeded: true),
                    InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus(
                    _strings.Translate("Operations.Status.Failed.Title"),
                    ResultMessage(result.MessageKey, succeeded: false),
                    InfoBarSeverity.Error);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Presentation remains usable if the shared runtime cancels an operation.
            ShowStatus(
                _strings.Translate("Operations.Status.Cancelled.Title"),
                _strings.Translate("Operations.Status.Cancelled.Message"),
                InfoBarSeverity.Warning);
            return null;
        }
        catch (Exception)
        {
            // Runtime failures are rendered without leaking implementation or host details into the UI.
            ShowStatus(
                _strings.Translate("Operations.Status.RuntimeUnavailable.Title"),
                _strings.Translate("Operations.Status.RuntimeUnavailable.Message"),
                InfoBarSeverity.Error);
            return null;
        }
        finally
        {
            OperationProgress.IsActive = false;
            OperationProgress.Visibility = Visibility.Collapsed;
            OperationsScroll.IsEnabled = true;
            _operationInProgress = false;
        }
    }

    private void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        switch (severity)
        {
            case InfoBarSeverity.Success:
                Dialogs.ShowSuccessBanner(OperationBanner, title, message);
                break;
            case InfoBarSeverity.Error:
                Dialogs.ShowErrorBanner(OperationBanner, title, message);
                break;
            case InfoBarSeverity.Warning:
                Dialogs.ShowWarningBanner(OperationBanner, title, message);
                break;
            default:
                Dialogs.ShowInfoBanner(OperationBanner, title, message);
                break;
        }
    }

    private string ResultMessage(string messageKey, bool succeeded)
    {
        return _strings.TryTranslate(messageKey, out var localized)
            ? localized
            : _strings.Translate(succeeded ? "Operations.Result.Success" : "Operations.Result.Failure");
    }

    private string YesNo(bool value) => _strings.Translate(value ? "Common.Yes" : "Common.No");

    private string EnabledDisabled(bool value) => _strings.Translate(value ? "Common.Enabled" : "Common.Disabled");

    private string FormatPercent(int? value) => value is null
        ? _strings.Translate("Common.NotAvailable")
        : $"{value.Value.ToString("N0", _strings.Culture)}%";

    private string FormatTemperature(int? value) => value is null
        ? _strings.Translate("Common.NotAvailable")
        : $"{value.Value.ToString("N0", _strings.Culture)} °C";

    private string FormatMemory(long megabytes) => megabytes >= 1024
        ? $"{(megabytes / 1024d).ToString("0.0", _strings.Culture)} GB"
        : $"{Math.Max(0, megabytes).ToString("N0", _strings.Culture)} MB";

    private string FormatBytes(long bytes)
    {
        var size = Math.Max(0, bytes);
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;
        var value = (double)size;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return $"{value.ToString("0.#", _strings.Culture)} {units[unit]}";
    }
}

/// <summary>Contains the validated reset plan returned by the runtime owner.</summary>
internal sealed class AtomicResetPreparedEventArgs(AtomicResetPlan plan) : EventArgs
{
    internal AtomicResetPlan Plan { get; } = plan;
}

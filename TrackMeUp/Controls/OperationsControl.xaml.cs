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
    private LocalizationService _strings = new("system");
    private bool _operationInProgress;
    private bool _returnToOverviewOnBack;
    private Control? _lastLandingLink;

    /// <summary>Creates the passive operational surface.</summary>
    public OperationsControl() => InitializeComponent();

    /// <summary>Occurs when back navigation is requested from the tools landing page.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Occurs after the visible operational page changes and the host may need to re-measure.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        SnapshotAiSection.ApplyLanguage(language);
        ReportsSection.ApplyLanguage(language);
        PrivacySection.ApplyLanguage(language);
        RetentionSection.ApplyLanguage(language);
        PluginsSection.ApplyLanguage(language);
        ApplyNavigationAccessibility(OpenSnapshotAiLink, "Options.Navigation.SnapshotAi.Action", "Options.Navigation.SnapshotAi.Description");
        ApplyNavigationAccessibility(OpenReportsLink, "Options.Navigation.Reports.Action", "Options.Navigation.Reports.Description");
        ApplyNavigationAccessibility(OpenPrivacyLink, "Options.Navigation.Privacy.Action", "Options.Navigation.Privacy.Description");
        ApplyNavigationAccessibility(OpenRetentionLink, "Options.Navigation.Retention.Action", "Options.Navigation.Retention.Description");
        ApplyNavigationAccessibility(OpenPluginsLink, "Options.Navigation.Plugins.Action", "Options.Navigation.Plugins.Description");
    }

    /// <summary>Connects the surface to the facade owned by the composition root.</summary>
    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        ArgumentNullException.ThrowIfNull(ownerWindow);
        SnapshotAiSection.Initialize(application, dialogs, ownerWindow, OperationBanner);
        ReportsSection.Initialize(application, dialogs, ownerWindow, OperationBanner);
        PrivacySection.Initialize(application, dialogs, ownerWindow, OperationBanner);
        RetentionSection.Initialize(application, dialogs, ownerWindow, OperationBanner);
        PluginsSection.Initialize(application, dialogs, ownerWindow, OperationBanner);
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
        OperationsScroll.Visibility = Visibility.Collapsed;
        DetailScroll.Visibility = Visibility.Visible;
        SnapshotAiSection.Visibility = section == OperationsSection.SnapshotAi ? Visibility.Visible : Visibility.Collapsed;
        ReportsSection.Visibility = section == OperationsSection.Reports ? Visibility.Visible : Visibility.Collapsed;
        PrivacySection.Visibility = section == OperationsSection.Privacy ? Visibility.Visible : Visibility.Collapsed;
        RetentionSection.Visibility = section == OperationsSection.Retention ? Visibility.Visible : Visibility.Collapsed;
        PluginsSection.Visibility = section == OperationsSection.Plugins ? Visibility.Visible : Visibility.Collapsed;
        DetailScroll.ChangeView(null, 0, null, disableAnimation: true);
        NotifyLayoutChanged();
        if (section == OperationsSection.Plugins)
        {
            _ = PluginsSection.LoadAsync();
        }
    }

    /// <summary>Shows the tools landing page without changing application state.</summary>
    internal void ShowOverview() => ShowOverview(restoreFocus: false);

    private ITrackMeUpApplication Application => _application ?? throw new InvalidOperationException("OperationsControl must be initialized before use.");

    private MicaDialogService Dialogs => _dialogs ?? throw new InvalidOperationException("OperationsControl must be initialized before use.");

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
        RuntimeRoleValue.Text = health.IsRuntimeOwner ? L("Owner", "Proprietario") : L("Client", "Client");
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
            ObservabilityUnavailableText.Text = L("Logging and remote diagnostics are not exposed by the current runtime.", "Il runtime corrente non espone logging e diagnostica remota.");
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
            ? [L("No local storage volumes reported", "Nessun volume locale rilevato")]
            : snapshot.Disks.Select(disk => L(
                $"{disk.Drive,-4} {FormatBytes(disk.FreeBytes)} free / {FormatBytes(disk.TotalBytes)}",
                $"{disk.Drive,-4} {FormatBytes(disk.FreeBytes)} liberi / {FormatBytes(disk.TotalBytes)}")).ToArray();
    }

    private void OpenSnapshotAiLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.SnapshotAi, sender);

    private void OpenReportsLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Reports, sender);

    private void OpenPrivacyLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Privacy, sender);

    private void OpenRetentionLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Retention, sender);

    private void OpenPluginsLink_Click(object sender, RoutedEventArgs e) => OpenSection(OperationsSection.Plugins, sender);

    private void OpenSection(OperationsSection section, object sender)
    {
        _lastLandingLink = sender as Control;
        NavigateTo(section);
    }

    private void ShowOverview(bool restoreFocus)
    {
        _returnToOverviewOnBack = false;
        SnapshotAiSection.Visibility = Visibility.Collapsed;
        ReportsSection.Visibility = Visibility.Collapsed;
        PrivacySection.Visibility = Visibility.Collapsed;
        RetentionSection.Visibility = Visibility.Collapsed;
        PluginsSection.Visibility = Visibility.Collapsed;
        DetailScroll.Visibility = Visibility.Collapsed;
        OperationsScroll.Visibility = Visibility.Visible;
        OperationsScroll.ChangeView(null, 0, null, disableAnimation: true);
        NotifyLayoutChanged();
        if (restoreFocus)
        {
            _lastLandingLink?.Focus(FocusState.Programmatic);
        }
    }

    private void ApplyNavigationAccessibility(Control link, string actionKey, string descriptionKey)
    {
        AutomationProperties.SetName(link, _strings.Translate(actionKey));
        AutomationProperties.SetHelpText(link, _strings.Translate(descriptionKey));
    }

    private void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private async Task<OperationResult<T>?> ExecuteAsync<T>(Func<ITrackMeUpApplication, CancellationToken, Task<OperationResult<T>>> operation)
    {
        if (_operationInProgress)
        {
            ShowStatus(L("Operation in progress", "Operazione in corso"), L("Wait for the current operation to finish.", "Attendi il completamento dell'operazione corrente."), InfoBarSeverity.Warning);
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
                ShowStatus(L("Operation completed", "Operazione completata"), result.Code, InfoBarSeverity.Success);
            }
            else
            {
                var issues = result.Issues.Count == 0 ? string.Empty : $" · {string.Join(", ", result.Issues.Select(issue => $"{issue.Field}: {issue.Code}"))}";
                ShowStatus(L("Operation failed", "Operazione non completata"), $"{result.Code}{issues}", InfoBarSeverity.Error);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Presentation remains usable if the shared runtime cancels an operation.
            ShowStatus(L("Operation cancelled", "Operazione annullata"), L("The runtime cancelled the operation.", "Il runtime ha annullato l'operazione."), InfoBarSeverity.Warning);
            return null;
        }
        catch (Exception)
        {
            // Runtime failures are rendered without leaking implementation or host details into the UI.
            ShowStatus(L("Runtime unavailable", "Runtime non disponibile"), L("The operation returned no result.", "L'operazione non ha restituito un risultato."), InfoBarSeverity.Error);
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

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;

    private string YesNo(bool value) => value ? L("yes", "sì") : L("no", "no");

    private string EnabledDisabled(bool value) => value ? L("enabled", "abilitato") : L("disabled", "disabilitato");

    private static string FormatPercent(int? value) => value is null ? "n/d" : $"{value}%";

    private static string FormatTemperature(int? value) => value is null ? "n/d" : $"{value} °C";

    private static string FormatMemory(long megabytes) => megabytes >= 1024
        ? $"{megabytes / 1024d:0.0} GB"
        : $"{Math.Max(0, megabytes):N0} MB";

    private static string FormatBytes(long bytes)
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

        return $"{value:0.#} {units[unit]}";
    }
}

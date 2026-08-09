using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
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

    /// <summary>Creates the passive operational surface.</summary>
    public OperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        PrivacySection.ApplyLanguage(language);
        RetentionSection.ApplyLanguage(language);
        PluginsSection.ApplyLanguage(language);
    }

    /// <summary>Connects the surface to the facade owned by the composition root.</summary>
    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        ArgumentNullException.ThrowIfNull(ownerWindow);
        PrivacySection.Initialize(application, dialogs, ownerWindow);
        RetentionSection.Initialize(application, dialogs, ownerWindow);
        PluginsSection.Initialize(application, dialogs, ownerWindow);
    }

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

    private async void CaptureScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new CaptureScreenshotRequest(
            SelectedTag(ScreenshotModeBox, "all-screens"),
            KeepScreenshotBox.IsChecked == true,
            WatermarkScreenshotBox.IsChecked == true,
            ScreenshotCaptureOrigins.Manual);
        var result = await ExecuteAsync((application, token) => application.CaptureScreenshotAsync(request, token));
        if (result is { Succeeded: true, Value: { } capture })
        {
            ScreenshotResultText.Text = L(
                $"Snapshot {capture.CaptureId}: {capture.AnalysisScreenshotPaths.Count} analysis files, {capture.StoredScreenshotPaths.Count} retained.\n{string.Join("\n", capture.AllScreenshotPaths)}",
                $"Snapshot {capture.CaptureId}: {capture.AnalysisScreenshotPaths.Count} file per analisi, {capture.StoredScreenshotPaths.Count} conservati.\n{string.Join("\n", capture.AllScreenshotPaths)}");
        }
    }

    private async void LatestScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.GetLatestScreenshotAsync(token));
        if (result is { Succeeded: true })
        {
            ScreenshotResultText.Text = string.IsNullOrWhiteSpace(result.Value)
                ? L("No retained snapshot.", "Nessuno snapshot conservato.")
                : L($"Latest snapshot:\n{result.Value}", $"Ultimo snapshot:\n{result.Value}");
        }
    }

    private async void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.OpenScreenshotFolderAsync(token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ScreenshotResultText.Text = L($"Snapshot folder opened:\n{path}", $"Cartella snapshot aperta:\n{path}");
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new AnalyzeCurrentActivityRequest(AllowAiCaptureBox.IsChecked == true, "winui.operations");
        var result = await ExecuteAsync((application, token) => application.AnalyzeCurrentActivityAsync(request, token));
        if (result is { Succeeded: true, Value: { } analysis })
        {
            AiAnalysisText.Text = $"{analysis.Application} · {analysis.Context}\n{analysis.Summary}";
        }
    }

    private async void StartFocusButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new StartFocusSessionRequest(FocusObjectiveBox.Text);
        var result = await ExecuteAsync((application, token) => application.StartFocusSessionAsync(request, token));
        if (result is { Succeeded: true, Value: { } state })
        {
            RenderFocusState(state);
        }
    }

    private async void FocusStatusButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.GetFocusSessionAsync(token));
        if (result is { Succeeded: true, Value: { } state })
        {
            RenderFocusState(state);
        }
    }

    private async void StopFocusButton_Click(object sender, RoutedEventArgs e)
    {
        var summarize = SummarizeFocusBox.IsChecked == true;
        var result = await ExecuteAsync((application, token) => application.StopFocusSessionAsync(summarize, token));
        if (result is not { Succeeded: true })
        {
            return;
        }

        FocusStatusText.Text = result.Value is { } summary
            ? L(
                $"Finished: {summary.Objective}\n{summary.StartedAt:t}–{summary.EndedAt:t} · active {FormatDuration(summary.ActiveSeconds)} · idle {FormatDuration(summary.IdleSeconds)} · primary app {summary.PrimaryApplication ?? "n/a"}",
                $"Terminata: {summary.Objective}\n{summary.StartedAt:t}–{summary.EndedAt:t} · attivo {FormatDuration(summary.ActiveSeconds)} · inattivo {FormatDuration(summary.IdleSeconds)} · app principale {summary.PrimaryApplication ?? "n/d"}")
            : L("Focus session ended without a summary.", "Sessione focus terminata senza riepilogo.");
    }

    private async void TodayReportButton_Click(object sender, RoutedEventArgs e)
    {
        var open = OpenGeneratedReportBox.IsChecked == true;
        var result = await ExecuteAsync((application, token) => application.GenerateTodayReportAsync(null, open, token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = L($"Today's report:\n{path}", $"Report di oggi:\n{path}");
        }
    }

    private async void DigestButton_Click(object sender, RoutedEventArgs e)
    {
        if (DigestDatePicker.Date is not { } selectedDate)
        {
            ShowStatus(L("Date required", "Data richiesta"), L("Select the digest date.", "Seleziona la data del digest."), InfoBarSeverity.Warning);
            return;
        }

        var date = DateOnly.FromDateTime(selectedDate.DateTime);
        var open = OpenGeneratedReportBox.IsChecked == true;
        var result = await ExecuteAsync((application, token) => application.GenerateDailyDigestAsync(date, open, token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = $"Digest {date:yyyy-MM-dd}:\n{path}";
        }
    }

    private async void OpenReportsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.OpenReportsFolderAsync(token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = L($"Reports folder opened:\n{path}", $"Cartella report aperta:\n{path}");
        }
    }

    private void PrivacySectionButton_Click(object sender, RoutedEventArgs e) => ShowOperationsSection(OperationsSubsection.Privacy);

    private void RetentionSectionButton_Click(object sender, RoutedEventArgs e) => ShowOperationsSection(OperationsSubsection.Retention);

    private void PluginsSectionButton_Click(object sender, RoutedEventArgs e) => ShowOperationsSection(OperationsSubsection.Plugins);

    private void ShowOperationsSection(OperationsSubsection subsection)
    {
        PrivacySectionButton.IsChecked = subsection == OperationsSubsection.Privacy;
        RetentionSectionButton.IsChecked = subsection == OperationsSubsection.Retention;
        PluginsSectionButton.IsChecked = subsection == OperationsSubsection.Plugins;
        PrivacySection.Visibility = subsection == OperationsSubsection.Privacy ? Visibility.Visible : Visibility.Collapsed;
        RetentionSection.Visibility = subsection == OperationsSubsection.Retention ? Visibility.Visible : Visibility.Collapsed;
        PluginsSection.Visibility = subsection == OperationsSubsection.Plugins ? Visibility.Visible : Visibility.Collapsed;
    }

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

    private void RenderFocusState(FocusSessionState state)
    {
        FocusStatusText.Text = state.IsActive
            ? L(
                $"Active: {state.Objective}\nDuration {state.Elapsed:hh\\:mm\\:ss} · active {FormatDuration(state.ActiveSeconds)} · idle {FormatDuration(state.IdleSeconds)} · primary app {state.PrimaryApplication ?? "n/a"}",
                $"Attiva: {state.Objective}\nDurata {state.Elapsed:hh\\:mm\\:ss} · attivo {FormatDuration(state.ActiveSeconds)} · inattivo {FormatDuration(state.IdleSeconds)} · app principale {state.PrimaryApplication ?? "n/d"}")
            : L("No focus session is active.", "Nessuna sessione focus attiva.");
    }

    private void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        switch (severity)
        {
            case InfoBarSeverity.Success:
                Dialogs.ShowSuccessBanner(OperationInfoBar, title, message);
                break;
            case InfoBarSeverity.Error:
                Dialogs.ShowErrorBanner(OperationInfoBar, title, message);
                break;
            case InfoBarSeverity.Warning:
                Dialogs.ShowWarningBanner(OperationInfoBar, title, message);
                break;
            default:
                Dialogs.ShowInfoBanner(OperationInfoBar, title, message);
                break;
        }
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) => comboBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;

    private string YesNo(bool value) => value ? L("yes", "sì") : L("no", "no");

    private string EnabledDisabled(bool value) => value ? L("enabled", "abilitato") : L("disabled", "disabilitato");

    private static string FormatPercent(int? value) => value is null ? "n/d" : $"{value}%";

    private static string FormatTemperature(int? value) => value is null ? "n/d" : $"{value} °C";

    private static string FormatDuration(long seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

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

    private enum OperationsSubsection
    {
        Privacy,
        Retention,
        Plugins
    }
}

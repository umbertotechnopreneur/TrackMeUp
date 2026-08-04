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
    private bool _operationInProgress;
    private bool _retentionConfirmationOpen;

    /// <summary>Creates the passive operational surface.</summary>
    public OperationsControl() => InitializeComponent();

    /// <summary>Occurs when the user asks to return to the compact player.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Connects the surface to the facade owned by the composition root.</summary>
    public void Initialize(ITrackMeUpApplication application) => _application = application ?? throw new ArgumentNullException(nameof(application));

    private ITrackMeUpApplication Application => _application ?? throw new InvalidOperationException("OperationsControl must be initialized before use.");

    private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private async void RuntimeHealthButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.GetRuntimeHealthAsync(token));
        if (result is not { Succeeded: true, Value: { } health })
        {
            return;
        }

        RuntimeHealthText.Text = $"Versione {health.ProductVersion} · protocollo {health.ProtocolVersion} · owner: {YesNo(health.IsRuntimeOwner)}\nCapacità: {string.Join(", ", health.Capabilities)}";
        ObservabilityHealthText.Text = health.Observability is { } observability
            ? $"Logging console: {EnabledDisabled(observability.ConsoleLoggingEnabled)} · file: {EnabledDisabled(observability.FileLoggingEnabled)} · Sentry: {observability.SentryStatus} · PII predefiniti: {YesNo(observability.SendsDefaultPii)}"
            : "Diagnostica di logging e Sentry non esposta dal runtime corrente.";
    }

    private async void SystemSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.CaptureSystemSnapshotAsync(token));
        if (result is not { Succeeded: true, Value: { } snapshot })
        {
            return;
        }

        var disks = snapshot.Disks.Count == 0
            ? "nessun disco disponibile"
            : string.Join(" · ", snapshot.Disks.Select(disk => $"{disk.Drive} {FormatBytes(disk.FreeBytes)} liberi / {FormatBytes(disk.TotalBytes)}"));
        SystemSnapshotText.Text = $"CPU {snapshot.CpuUsagePercent}% ({FormatTemperature(snapshot.CpuTemperatureCelsius)}) · GPU {FormatPercent(snapshot.GpuUsagePercent)} ({FormatTemperature(snapshot.GpuTemperatureCelsius)})\nMemoria {snapshot.MemoryUsedMb:N0}/{snapshot.MemoryTotalMb:N0} MB · rete ↑ {FormatBytes(snapshot.Network.UploadBytesPerSecond)}/s ↓ {FormatBytes(snapshot.Network.DownloadBytesPerSecond)}/s\n{disks}";
    }

    private async void CaptureScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new CaptureScreenshotRequest(
            SelectedTag(ScreenshotModeBox, "all-screens"),
            KeepScreenshotBox.IsChecked == true,
            WatermarkScreenshotBox.IsChecked == true);
        var result = await ExecuteAsync((application, token) => application.CaptureScreenshotAsync(request, token));
        if (result is { Succeeded: true, Value: { } capture })
        {
            ScreenshotResultText.Text = $"Cattura {capture.CaptureId}: {capture.AnalysisScreenshotPaths.Count} file per analisi, {capture.StoredScreenshotPaths.Count} conservati.\n{string.Join("\n", capture.AllScreenshotPaths)}";
        }
    }

    private async void LatestScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.GetLatestScreenshotAsync(token));
        if (result is { Succeeded: true })
        {
            ScreenshotResultText.Text = string.IsNullOrWhiteSpace(result.Value) ? "Nessuno screenshot conservato." : $"Ultimo screenshot:\n{result.Value}";
        }
    }

    private async void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.OpenScreenshotFolderAsync(token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ScreenshotResultText.Text = $"Cartella screenshot aperta:\n{path}";
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new AnalyzeCurrentActivityRequest(AllowAiCaptureBox.IsChecked == true);
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
            ? $"Terminata: {summary.Objective}\n{summary.StartedAt:t}–{summary.EndedAt:t} · attivo {FormatDuration(summary.ActiveSeconds)} · inattivo {FormatDuration(summary.IdleSeconds)} · app principale {summary.PrimaryApplication ?? "n/d"}"
            : "Sessione focus terminata senza riepilogo.";
    }

    private async void TodayReportButton_Click(object sender, RoutedEventArgs e)
    {
        var open = OpenGeneratedReportBox.IsChecked == true;
        var result = await ExecuteAsync((application, token) => application.GenerateTodayReportAsync(null, open, token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = $"Report di oggi:\n{path}";
        }
    }

    private async void DigestButton_Click(object sender, RoutedEventArgs e)
    {
        if (DigestDatePicker.Date is not { } selectedDate)
        {
            ShowStatus("Data richiesta", "Seleziona la data del digest.", InfoBarSeverity.Warning);
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
            ReportResultText.Text = $"Cartella report aperta:\n{path}";
        }
    }

    private async void AddPrivacyRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var type = SelectedTag(PrivacyRuleTypeBox, "process");
        var result = await ExecuteAsync((application, token) => application.AddPrivacyRuleAsync(type, PrivacyRuleValueBox.Text, token));
        if (result is { Succeeded: true })
        {
            PrivacyRuleValueBox.Text = string.Empty;
            await RefreshPrivacyRulesAsync();
        }
    }

    private async void ListPrivacyRulesButton_Click(object sender, RoutedEventArgs e) => await RefreshPrivacyRulesAsync();

    private async Task RefreshPrivacyRulesAsync()
    {
        var result = await ExecuteAsync((application, token) => application.GetPrivacyRulesAsync(token));
        if (result is { Succeeded: true, Value: { } rules })
        {
            PrivacyRulesList.ItemsSource = rules.ToArray();
        }
    }

    private async void RemovePrivacyRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (PrivacyRulesList.SelectedItem is not PrivacyRule rule)
        {
            ShowStatus("Selezione richiesta", "Seleziona la regola privacy da rimuovere.", InfoBarSeverity.Warning);
            return;
        }

        var result = await ExecuteAsync((application, token) => application.RemovePrivacyRuleAsync(rule.Id, token));
        if (result is { Succeeded: true })
        {
            await RefreshPrivacyRulesAsync();
        }
    }

    private async void TestPrivacyButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.TestCurrentPrivacyAsync(token));
        if (result is { Succeeded: true, Value: { } blocked })
        {
            PrivacyTestText.Text = blocked ? "Il contesto corrente è bloccato dalle regole privacy." : "Il contesto corrente non è bloccato.";
        }
    }

    private async void RetentionStatusButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.GetRetentionStatusAsync(token));
        if (result is { Succeeded: true, Value: { } status })
        {
            RetentionStatusText.Text = $"Dati: {status.DataRetentionDays} giorni · screenshot: {status.ScreenshotRetentionDays} giorni\n{status.ScreenshotDirectory}";
        }
    }

    private async void RetentionPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ExecuteAsync((application, token) => application.PreviewRetentionAsync(token));
        if (result is { Succeeded: true, Value: { } preview })
        {
            RenderRetentionPreview(preview, executed: false);
        }
    }

    private async void RunRetentionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_retentionConfirmationOpen)
        {
            ShowStatus("Conferma già aperta", "Completa o annulla la conferma di pulizia corrente.", InfoBarSeverity.Warning);
            return;
        }

        _retentionConfirmationOpen = true;
        try
        {
            var previewResult = await ExecuteAsync((application, token) => application.PreviewRetentionAsync(token));
            if (previewResult is not { Succeeded: true, Value: { } preview })
            {
                return;
            }

            RenderRetentionPreview(preview, executed: false);
            var dialog = new ContentDialog
            {
                Title = "Conferma pulizia dati",
                Content = $"Eliminare definitivamente {preview.FileCount} elementi ({FormatBytes(preview.TotalBytes)}) elencati nell'anteprima?",
                PrimaryButtonText = "Elimina elementi",
                CloseButtonText = "Annulla",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                ShowStatus("Pulizia annullata", "Nessun elemento è stato eliminato.", InfoBarSeverity.Informational);
                return;
            }

            var runResult = await ExecuteAsync((application, token) => application.RunRetentionAsync(new RetentionRequest(Execute: true, Confirmed: true), token));
            if (runResult is { Succeeded: true, Value: { } deleted })
            {
                RenderRetentionPreview(deleted, executed: true);
            }
        }
        catch (Exception)
        {
            // A dialog-host failure leaves retention untouched and the rest of the surface available.
            ShowStatus("Conferma non disponibile", "La pulizia non è stata avviata.", InfoBarSeverity.Error);
        }
        finally
        {
            _retentionConfirmationOpen = false;
        }
    }

    private async void ListPluginsButton_Click(object sender, RoutedEventArgs e) => await RefreshPluginsAsync();

    private async Task RefreshPluginsAsync()
    {
        var result = await ExecuteAsync((application, token) => application.GetPluginsAsync(token));
        if (result is { Succeeded: true, Value: { } plugins })
        {
            PluginsList.ItemsSource = plugins.ToArray();
        }
    }

    private async void EnablePluginButton_Click(object sender, RoutedEventArgs e) => await SetSelectedPluginAsync(enabled: true);

    private async void DisablePluginButton_Click(object sender, RoutedEventArgs e) => await SetSelectedPluginAsync(enabled: false);

    private async Task SetSelectedPluginAsync(bool enabled)
    {
        if (PluginsList.SelectedItem is not PluginInfo plugin)
        {
            ShowStatus("Selezione richiesta", "Seleziona il plugin da modificare.", InfoBarSeverity.Warning);
            return;
        }

        var result = await ExecuteAsync((application, token) => application.SetPluginEnabledAsync(plugin.Id, enabled, token));
        if (result is { Succeeded: true })
        {
            await RefreshPluginsAsync();
        }
    }

    private async Task<OperationResult<T>?> ExecuteAsync<T>(Func<ITrackMeUpApplication, CancellationToken, Task<OperationResult<T>>> operation)
    {
        if (_operationInProgress)
        {
            ShowStatus("Operazione in corso", "Attendi il completamento dell'operazione corrente.", InfoBarSeverity.Warning);
            return null;
        }

        _operationInProgress = true;
        OperationsScroll.IsEnabled = false;
        OperationProgress.IsActive = true;
        try
        {
            var result = await operation(Application, CancellationToken.None);
            if (result.Succeeded)
            {
                ShowStatus("Operazione completata", result.Code, InfoBarSeverity.Success);
            }
            else
            {
                var issues = result.Issues.Count == 0 ? string.Empty : $" · {string.Join(", ", result.Issues.Select(issue => $"{issue.Field}: {issue.Code}"))}";
                ShowStatus("Operazione non completata", $"{result.Code}{issues}", InfoBarSeverity.Error);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Presentation remains usable if the shared runtime cancels an operation.
            ShowStatus("Operazione annullata", "Il runtime ha annullato l'operazione.", InfoBarSeverity.Warning);
            return null;
        }
        catch (Exception)
        {
            // Runtime failures are rendered without leaking implementation or host details into the UI.
            ShowStatus("Runtime non disponibile", "L'operazione non ha restituito un risultato.", InfoBarSeverity.Error);
            return null;
        }
        finally
        {
            OperationProgress.IsActive = false;
            OperationsScroll.IsEnabled = true;
            _operationInProgress = false;
        }
    }

    private void RenderFocusState(FocusSessionState state)
    {
        FocusStatusText.Text = state.IsActive
            ? $"Attiva: {state.Objective}\nDurata {state.Elapsed:hh\\:mm\\:ss} · attivo {FormatDuration(state.ActiveSeconds)} · inattivo {FormatDuration(state.IdleSeconds)} · app principale {state.PrimaryApplication ?? "n/d"}"
            : "Nessuna sessione focus attiva.";
    }

    private void RenderRetentionPreview(RetentionPreview preview, bool executed)
    {
        RetentionPreviewText.Text = executed
            ? $"Eliminati {preview.FileCount} elementi ({FormatBytes(preview.TotalBytes)})."
            : $"{preview.FileCount} elementi candidati ({FormatBytes(preview.TotalBytes)}).";
        RetentionPathsList.ItemsSource = preview.Paths.ToArray();
    }

    private void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        OperationInfoBar.Title = title;
        OperationInfoBar.Message = message;
        OperationInfoBar.Severity = severity;
        OperationInfoBar.IsOpen = true;
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) => comboBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private static string YesNo(bool value) => value ? "sì" : "no";

    private static string EnabledDisabled(bool value) => value ? "abilitato" : "disabilitato";

    private static string FormatPercent(int? value) => value is null ? "n/d" : $"{value}%";

    private static string FormatTemperature(int? value) => value is null ? "n/d" : $"{value} °C";

    private static string FormatDuration(long seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

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

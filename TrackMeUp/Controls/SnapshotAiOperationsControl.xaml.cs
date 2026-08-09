using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects screen-capture and AI-analysis input and renders facade results.</summary>
public sealed partial class SnapshotAiOperationsControl : UserControl
{
    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;

    /// <summary>Creates the independent screen-capture and AI operations surface.</summary>
    public SnapshotAiOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
    }

    /// <summary>Connects the passive surface to the application facade owned by the composition root.</summary>
    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow) =>
        _context = new OperationsSectionContext(application, dialogs, ownerWindow, StatusInfoBar, Progress, SectionBody, L);

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("SnapshotAiOperationsControl must be initialized before use.");

    private async void CaptureScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new CaptureScreenshotRequest(
            SelectedTag(ScreenshotModeBox, "all-screens"),
            KeepScreenshotBox.IsChecked == true,
            WatermarkScreenshotBox.IsChecked == true,
            ScreenshotCaptureOrigins.Manual);
        var result = await Context.ExecuteAsync((application, token) => application.CaptureScreenshotAsync(request, token));
        if (result is { Succeeded: true, Value: { } capture })
        {
            ScreenshotResultText.Text = L(
                $"Snapshot {capture.CaptureId}: {capture.AnalysisScreenshotPaths.Count} analysis files, {capture.StoredScreenshotPaths.Count} retained.\n{string.Join("\n", capture.AllScreenshotPaths)}",
                $"Snapshot {capture.CaptureId}: {capture.AnalysisScreenshotPaths.Count} file per analisi, {capture.StoredScreenshotPaths.Count} conservati.\n{string.Join("\n", capture.AllScreenshotPaths)}");
        }
    }

    private async void LatestScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.GetLatestScreenshotAsync(token));
        if (result is { Succeeded: true })
        {
            ScreenshotResultText.Text = string.IsNullOrWhiteSpace(result.Value)
                ? L("No retained screen capture.", "Nessuna cattura schermo conservata.")
                : L($"Latest screen capture:\n{result.Value}", $"Ultima cattura schermo:\n{result.Value}");
        }
    }

    private async void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.OpenScreenshotFolderAsync(token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ScreenshotResultText.Text = L($"Screen-capture folder opened:\n{path}", $"Cartella catture schermo aperta:\n{path}");
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new AnalyzeCurrentActivityRequest(AllowAiCaptureBox.IsChecked == true, "winui.operations");
        var result = await Context.ExecuteAsync((application, token) => application.AnalyzeCurrentActivityAsync(request, token));
        if (result is { Succeeded: true, Value: { } analysis })
        {
            AiAnalysisText.Text = $"{analysis.Application} · {analysis.Context}\n{analysis.Summary}";
        }
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) => comboBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;
}

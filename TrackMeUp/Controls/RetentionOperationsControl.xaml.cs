using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects and renders data-retention preview and cleanup operations.</summary>
public sealed partial class RetentionOperationsControl : UserControl
{
    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;
    private bool _confirmationOpen;

    /// <summary>Creates the independent retention operations surface.</summary>
    public RetentionOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
    }

    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow, TimedInfoBar banner) =>
        _context = new OperationsSectionContext(application, dialogs, ownerWindow, banner, Progress, SectionBody, L, key => _strings.Translate(key));

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("RetentionOperationsControl must be initialized before use.");

    private async void RetentionStatusButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.GetRetentionStatusAsync(token));
        if (result is { Succeeded: true, Value: { } status })
        {
            RetentionStatusText.Text = L(
                $"Activity data · {status.DataRetentionDays} days\nSnapshots · {status.ScreenshotRetentionDays} days\nLocation · {status.ScreenshotDirectory}",
                $"Dati attività · {status.DataRetentionDays} giorni\nSnapshot · {status.ScreenshotRetentionDays} giorni\nPosizione · {status.ScreenshotDirectory}");
        }
    }

    private async void RetentionPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.PreviewRetentionAsync(token));
        if (result is { Succeeded: true, Value: { } preview })
        {
            RenderRetentionPreview(preview, executed: false);
        }
    }

    private async void RunRetentionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_confirmationOpen)
        {
            Context.ShowStatus(L("Confirmation already open", "Conferma già aperta"), L("Complete or cancel the current cleanup confirmation.", "Completa o annulla la conferma di pulizia corrente."), InfoBarSeverity.Warning);
            return;
        }

        _confirmationOpen = true;
        try
        {
            var previewResult = await Context.ExecuteAsync((application, token) => application.PreviewRetentionAsync(token));
            if (previewResult is not { Succeeded: true, Value: { } preview })
            {
                return;
            }

            RenderRetentionPreview(preview, executed: false);
            var confirmed = await Context.Dialogs.ConfirmAsync(
                Context.Application,
                Context.OwnerWindow,
                MicaDialogRequest.Confirmation(
                    L("Confirm data cleanup", "Conferma pulizia dati"),
                    L(
                        $"Permanently delete the {preview.FileCount} items ({FormatBytes(preview.TotalBytes)}) listed in the preview?",
                        $"Eliminare definitivamente {preview.FileCount} elementi ({FormatBytes(preview.TotalBytes)}) elencati nell'anteprima?"),
                    L("Delete items", "Elimina elementi"),
                    L("Cancel", "Annulla")),
                RequestedTheme);
            if (!confirmed)
            {
                Context.ShowStatus(L("Cleanup cancelled", "Pulizia annullata"), L("No items were deleted.", "Nessun elemento è stato eliminato."), InfoBarSeverity.Informational);
                return;
            }

            var runResult = await Context.ExecuteAsync((application, token) => application.RunRetentionAsync(new RetentionRequest(Execute: true, Confirmed: true), token));
            if (runResult is { Succeeded: true, Value: { } deleted })
            {
                RenderRetentionPreview(deleted, executed: true);
            }
        }
        catch (Exception)
        {
            // A dialog-host failure leaves retention untouched and the subsection available.
            Context.ShowStatus(L("Confirmation unavailable", "Conferma non disponibile"), L("Cleanup was not started.", "La pulizia non è stata avviata."), InfoBarSeverity.Error);
        }
        finally
        {
            _confirmationOpen = false;
        }
    }

    private void RenderRetentionPreview(RetentionPreview preview, bool executed)
    {
        RetentionPreviewText.Text = executed
            ? L($"Deleted {preview.FileCount} items · {FormatBytes(preview.TotalBytes)}", $"Eliminati {preview.FileCount} elementi · {FormatBytes(preview.TotalBytes)}")
            : L($"{preview.FileCount} eligible items · {FormatBytes(preview.TotalBytes)}", $"{preview.FileCount} elementi idonei · {FormatBytes(preview.TotalBytes)}");
        RetentionPathsList.ItemsSource = preview.Paths.ToArray();
    }

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;

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

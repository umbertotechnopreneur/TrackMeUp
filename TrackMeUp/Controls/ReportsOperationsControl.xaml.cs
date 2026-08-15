using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects report parameters and renders report paths returned by the shared facade.</summary>
public sealed partial class ReportsOperationsControl : UserControl
{
    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;

    /// <summary>Creates the independent reports operations surface.</summary>
    public ReportsOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
    }

    /// <summary>Connects the passive surface to the application facade owned by the composition root.</summary>
    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow, TimedInfoBar banner) =>
        _context = new OperationsSectionContext(
            application,
            dialogs,
            ownerWindow,
            banner,
            Progress,
            SectionBody,
            key => _strings.TryTranslate(key, out var value) ? value : null);

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("ReportsOperationsControl must be initialized before use.");

    private async void TodayReportButton_Click(object sender, RoutedEventArgs e)
    {
        var open = OpenGeneratedReportBox.IsChecked == true;
        var result = await Context.ExecuteAsync((application, token) => application.GenerateTodayReportAsync(null, open, token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = _strings.Format("Operations.Reports.Today", path);
        }
    }

    private async void DigestButton_Click(object sender, RoutedEventArgs e)
    {
        if (DigestDatePicker.Date is not { } selectedDate)
        {
            Context.ShowStatus(
                _strings.Translate("Operations.Reports.DateRequired.Title"),
                _strings.Translate("Operations.Reports.DateRequired.Message"),
                InfoBarSeverity.Warning);
            return;
        }

        var date = DateOnly.FromDateTime(selectedDate.DateTime);
        var open = OpenGeneratedReportBox.IsChecked == true;
        var result = await Context.ExecuteAsync((application, token) => application.GenerateDailyDigestAsync(date, open, token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = _strings.Format("Operations.Reports.Digest", date, path);
        }
    }

    private async void OpenReportsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.OpenReportsFolderAsync(token));
        if (result is { Succeeded: true, Value: { } path })
        {
            ReportResultText.Text = _strings.Format("Operations.Reports.FolderOpened", path);
        }
    }
}

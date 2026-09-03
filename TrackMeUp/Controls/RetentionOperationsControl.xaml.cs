// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
        AutomationProperties.SetName(RetentionPathsList, _strings.Translate("Operations.Retention.Preview.Paths"));
    }

    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow, TimedInfoBar banner) =>
        _context = new OperationsSectionContext(
            application,
            dialogs,
            ownerWindow,
            banner,
            Progress,
            SectionBody,
            key => _strings.TryTranslate(key, out var value) ? value : null);

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("RetentionOperationsControl must be initialized before use.");

    private async void RetentionStatusButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.GetRetentionStatusAsync(token));
        if (result is { Succeeded: true, Value: { } status })
        {
            RetentionStatusText.Text = _strings.Format(
                "Operations.Retention.Status",
                status.DataRetentionDays,
                status.ScreenshotRetentionDays);
            RetentionDirectoryText.Text = status.ScreenshotDirectory;
            AutomationProperties.SetName(RetentionDirectoryText, status.ScreenshotDirectory);
            ToolTipService.SetToolTip(RetentionDirectoryText, status.ScreenshotDirectory);
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
            Context.ShowStatus(
                _strings.Translate("Operations.Retention.ConfirmationOpen.Title"),
                _strings.Translate("Operations.Retention.ConfirmationOpen.Message"),
                InfoBarSeverity.Warning);
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
                Context.OwnerWindow,
                DialogRequest.Confirmation(
                    _strings.Translate("Operations.Retention.Confirm.Title"),
                    _strings.Format("Operations.Retention.Confirm.Message", preview.FileCount, FormatBytes(preview.TotalBytes)),
                    _strings.Translate("Dialog.Ok"),
                    _strings.Translate("Dialog.Cancel")));
            if (!confirmed)
            {
                Context.ShowStatus(
                    _strings.Translate("Operations.Retention.Cancelled.Title"),
                    _strings.Translate("Operations.Retention.Cancelled.Message"),
                    InfoBarSeverity.Informational);
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
            Context.ShowStatus(
                _strings.Translate("Operations.Retention.ConfirmationUnavailable.Title"),
                _strings.Translate("Operations.Retention.ConfirmationUnavailable.Message"),
                InfoBarSeverity.Error);
        }
        finally
        {
            _confirmationOpen = false;
        }
    }

    private void RenderRetentionPreview(RetentionPreview preview, bool executed)
    {
        RetentionPreviewText.Text = executed
            ? _strings.Format("Operations.Retention.Deleted", preview.FileCount, FormatBytes(preview.TotalBytes))
            : _strings.Format("Operations.Retention.Eligible", preview.FileCount, FormatBytes(preview.TotalBytes));
        RetentionPathsList.ItemsSource = preview.Paths.ToArray();
    }

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

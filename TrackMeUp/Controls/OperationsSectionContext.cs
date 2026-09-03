// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;

namespace TrackMeUp.Controls;

/// <summary>Coordinates one passive operations subsection with the shared facade and banner host.</summary>
internal sealed class OperationsSectionContext
{
    private readonly TimedInfoBar _status;
    private readonly ProgressRing _progress;
    private readonly UIElement _interactionRoot;
    private readonly Func<string, string?> _tryTranslate;
    private bool _operationInProgress;

    internal OperationsSectionContext(
        ITrackMeUpApplication application,
        MicaDialogService dialogs,
        Window ownerWindow,
        TimedInfoBar status,
        ProgressRing progress,
        UIElement interactionRoot,
        Func<string, string?> tryTranslate)
    {
        Application = application ?? throw new ArgumentNullException(nameof(application));
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        OwnerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _interactionRoot = interactionRoot ?? throw new ArgumentNullException(nameof(interactionRoot));
        _tryTranslate = tryTranslate ?? throw new ArgumentNullException(nameof(tryTranslate));
    }

    internal ITrackMeUpApplication Application { get; }

    internal MicaDialogService Dialogs { get; }

    internal Window OwnerWindow { get; }

    internal async Task<OperationResult<T>?> ExecuteAsync<T>(
        Func<ITrackMeUpApplication, CancellationToken, Task<OperationResult<T>>> operation,
        bool showSuccess = true)
    {
        if (_operationInProgress)
        {
            ShowStatus(Translate("Operations.Status.InProgress.Title"), Translate("Operations.Status.InProgress.Message"), InfoBarSeverity.Warning);
            return null;
        }

        _operationInProgress = true;
        _interactionRoot.IsHitTestVisible = false;
        _interactionRoot.Opacity = 0.72;
        _progress.IsActive = true;
        _progress.Visibility = Visibility.Visible;
        try
        {
            var result = await operation(Application, CancellationToken.None);
            if (result.Succeeded)
            {
                if (showSuccess)
                {
                    ShowStatus(
                        Translate("Operations.Status.Completed.Title"),
                        ResultMessage(result.MessageKey, succeeded: true),
                        InfoBarSeverity.Success);
                }
            }
            else
            {
                ShowStatus(
                    Translate("Operations.Status.Failed.Title"),
                    ResultMessage(result.MessageKey, succeeded: false),
                    InfoBarSeverity.Error);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Cancellation keeps the subsection interactive and does not infer a successful result.
            ShowStatus(Translate("Operations.Status.Cancelled.Title"), Translate("Operations.Status.Cancelled.Message"), InfoBarSeverity.Warning);
            return null;
        }
        catch (Exception)
        {
            // Runtime failures are rendered without exposing implementation or host details.
            ShowStatus(Translate("Operations.Status.RuntimeUnavailable.Title"), Translate("Operations.Status.RuntimeUnavailable.Message"), InfoBarSeverity.Error);
            return null;
        }
        finally
        {
            _progress.IsActive = false;
            _progress.Visibility = Visibility.Collapsed;
            _interactionRoot.Opacity = 1;
            _interactionRoot.IsHitTestVisible = true;
            _operationInProgress = false;
        }
    }

    internal void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        switch (severity)
        {
            case InfoBarSeverity.Success:
                Dialogs.Notifications.ShowSuccess(_status, title, message);
                break;
            case InfoBarSeverity.Error:
                Dialogs.Notifications.ShowError(_status, title, message);
                break;
            case InfoBarSeverity.Warning:
                Dialogs.Notifications.ShowWarning(_status, title, message);
                break;
            default:
                Dialogs.Notifications.ShowInfo(_status, title, message);
                break;
        }
    }

    internal string ResultMessage(string messageKey, bool succeeded)
    {
        return _tryTranslate(messageKey)
            ?? Translate(succeeded ? "Operations.Result.Success" : "Operations.Result.Failure");
    }

    private string Translate(string key) =>
        _tryTranslate(key)
        ?? throw new KeyNotFoundException($"Missing required localization key '{key}'.");
}

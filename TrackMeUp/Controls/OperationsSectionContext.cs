using System;
using System.Linq;
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
    private readonly Func<string, string, string> _localize;
    private bool _operationInProgress;

    internal OperationsSectionContext(
        ITrackMeUpApplication application,
        MicaDialogService dialogs,
        Window ownerWindow,
        TimedInfoBar status,
        ProgressRing progress,
        UIElement interactionRoot,
        Func<string, string, string> localize)
    {
        Application = application ?? throw new ArgumentNullException(nameof(application));
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        OwnerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _interactionRoot = interactionRoot ?? throw new ArgumentNullException(nameof(interactionRoot));
        _localize = localize ?? throw new ArgumentNullException(nameof(localize));
    }

    internal ITrackMeUpApplication Application { get; }

    internal MicaDialogService Dialogs { get; }

    internal Window OwnerWindow { get; }

    internal async Task<OperationResult<T>?> ExecuteAsync<T>(Func<ITrackMeUpApplication, CancellationToken, Task<OperationResult<T>>> operation)
    {
        if (_operationInProgress)
        {
            ShowStatus(L("Operation in progress", "Operazione in corso"), L("Wait for the current operation to finish.", "Attendi il completamento dell'operazione corrente."), InfoBarSeverity.Warning);
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
            // Cancellation keeps the subsection interactive and does not infer a successful result.
            ShowStatus(L("Operation cancelled", "Operazione annullata"), L("The runtime cancelled the operation.", "Il runtime ha annullato l'operazione."), InfoBarSeverity.Warning);
            return null;
        }
        catch (Exception)
        {
            // Runtime failures are rendered without exposing implementation or host details.
            ShowStatus(L("Runtime unavailable", "Runtime non disponibile"), L("The operation returned no result.", "L'operazione non ha restituito un risultato."), InfoBarSeverity.Error);
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
                Dialogs.ShowSuccessBanner(_status, title, message);
                break;
            case InfoBarSeverity.Error:
                Dialogs.ShowErrorBanner(_status, title, message);
                break;
            case InfoBarSeverity.Warning:
                Dialogs.ShowWarningBanner(_status, title, message);
                break;
            default:
                Dialogs.ShowInfoBanner(_status, title, message);
                break;
        }
    }

    private string L(string english, string italian) => _localize(english, italian);
}

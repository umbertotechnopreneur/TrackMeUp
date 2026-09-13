// SPDX-License-Identifier: MIT

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using TrackMeUp.Runtime;

namespace TrackMeUp;

/// <summary>Starts WinUI only after enforcing one long-lived TrackMeUp process per user.</summary>
public static class Program
{
    private const string MainInstanceKey = "TrackMeUp.Main";
    private static readonly object ActivationGate = new();
    private static readonly Queue<AppActivationArguments> PendingActivations = new();
    private static App? _application;

    /// <summary>Redirects long-lived activations before XAML, services, or windows are initialized.</summary>
    [STAThread]
    public static async Task Main(string[] arguments)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RequiresSingleInstance(arguments))
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            var mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
            if (!mainInstance.IsCurrent)
            {
                // Redirection is terminal for this process: the registered instance receives the activation and this process creates no runtime or window.
                await mainInstance.RedirectActivationToAsync(activation);
                return;
            }

            mainInstance.Activated += MainInstance_Activated;
        }

        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            var application = new App();
            lock (ActivationGate)
            {
                // Publication and draining share the event handler's lock so no activation can be stranded after startup.
                _application = application;
                while (PendingActivations.TryDequeue(out var activation))
                {
                    application.HandleRedirectedActivation(activation);
                }
            }
        });
    }

    private static bool RequiresSingleInstance(IReadOnlyList<string> arguments)
    {
        // Invalid bootstrap arguments fail before redirection, including when another instance is already running.
        return LaunchOptions.Parse(arguments).Mode is not (LaunchMode.Cli or LaunchMode.Help or LaunchMode.Version);
    }

    private static void MainInstance_Activated(object? sender, AppActivationArguments activation)
    {
        lock (ActivationGate)
        {
            if (_application is null)
            {
                PendingActivations.Enqueue(activation);
                return;
            }

            _application.HandleRedirectedActivation(activation);
        }
    }
}

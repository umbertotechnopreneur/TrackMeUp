// SPDX-License-Identifier: MIT

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using TrackMeUp.Runtime;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Starts WinUI only after enforcing one long-lived TrackMeUp process per user.</summary>
public static class Program
{
    private const string MainInstanceKey = "TrackMeUp.Main";
    private static readonly object ActivationGate = new();
    private static readonly Queue<RedirectedActivationRequest> PendingActivations = new();
    private static App? _application;

    /// <summary>Redirects long-lived activations before XAML, services, or windows are initialized.</summary>
    [STAThread]
    public static void Main(string[] arguments)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RequiresSingleInstance(arguments))
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            var mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
            if (!mainInstance.IsCurrent)
            {
                // Redirection is terminal for this process: the registered instance receives the activation and this process creates no runtime or window.
                StartupThreadService.CompleteActivationRedirection(
                    () => mainInstance.RedirectActivationToAsync(activation).AsTask());
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
        // Consume the WinRT payload before returning from its callback; queues must retain only managed values.
        var request = CaptureRedirectedActivation(activation);
        lock (ActivationGate)
        {
            if (_application is null)
            {
                PendingActivations.Enqueue(request);
                return;
            }

            _application.HandleRedirectedActivation(request);
        }
    }

    private static RedirectedActivationRequest CaptureRedirectedActivation(AppActivationArguments activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        var kind = activation.Kind;
        var options = kind switch
        {
            ExtendedActivationKind.Launch when activation.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch =>
                WindowsLaunchArguments.Parse(launch.Arguments, "TrackMeUp.exe"),
            ExtendedActivationKind.StartupTask => StartupActivationPolicy.Apply(LaunchOptions.Parse([]), kind),
            // Invalid payloads must fail on the receiving callback instead of entering the UI queue with tracking defaults.
            _ => throw new ArgumentException("Unsupported redirected TrackMeUp activation.", nameof(activation))
        };

        if (options.Mode is not (LaunchMode.Ui or LaunchMode.Reports or LaunchMode.Background))
        {
            throw new ArgumentException("Unsupported redirected TrackMeUp launch mode.", nameof(activation));
        }

        // Copy the only collection so the immutable request owns every value needed after the sender has exited.
        return new RedirectedActivationRequest(
            options with { RemainingArguments = Array.AsReadOnly(options.RemainingArguments.ToArray()) },
            kind);
    }
}

/// <summary>Contains the managed snapshot consumed after a redirected WinRT activation callback returns.</summary>
internal sealed record RedirectedActivationRequest(LaunchOptions Options, ExtendedActivationKind Kind);

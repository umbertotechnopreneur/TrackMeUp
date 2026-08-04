using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Runtime;

/// <summary>Connects frontends to the single local runtime and starts its background host when absent.</summary>
public static class RuntimeConnector
{
    /// <summary>Returns a pipe-backed application facade after waiting for the shared runtime to become reachable.</summary>
    /// <param name="executablePath">Trusted TrackMeUp executable path supplied by the composition root.</param>
    /// <param name="timeoutSeconds">Per-request timeout in seconds.</param>
    /// <param name="cancellationToken">Cancellation for the connection attempt.</param>
    /// <param name="logger">Optional infrastructure logger.</param>
    public static async Task<ITrackMeUpApplication?> ConnectAsync(
        string executablePath,
        int timeoutSeconds,
        CancellationToken cancellationToken,
        ILogger<RuntimeClient>? logger = null)
    {
        var store = new LocalStore();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 300));
        var runtimeLogger = logger ?? NullLogger<RuntimeClient>.Instance;
        var installationId = store.TryLoadInstallationId();
        if (!string.IsNullOrWhiteSpace(installationId))
        {
            var existingClient = new RuntimeClient(installationId, timeout, runtimeLogger);
            var health = await existingClient.GetRuntimeHealthAsync(cancellationToken).ConfigureAwait(false);
            if (health.Succeeded)
            {
                return existingClient;
            }
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--background");
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            runtimeLogger.LogWarning("Background runtime launch failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return null;
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Min(Math.Clamp(timeoutSeconds, 1, 300), 5));
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            installationId = store.TryLoadInstallationId();
            if (!string.IsNullOrWhiteSpace(installationId))
            {
                var client = new RuntimeClient(installationId, timeout, runtimeLogger);
                var health = await client.GetRuntimeHealthAsync(cancellationToken).ConfigureAwait(false);
                if (health.Succeeded)
                {
                    return client;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}

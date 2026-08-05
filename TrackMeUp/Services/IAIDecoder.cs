using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TrackMeUp.Services;

/// <summary>
/// Contract for provider adapters that can enrich productivity analysis from context and screenshots.
/// </summary>
public interface IAIDecoder
{
    /// <summary>
    /// Provider identifier ("openai", "openrouter", "anthropic").
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Runs a provider request and returns text plus allowlisted usage and transport metadata.
    /// </summary>
    /// <param name="prompt">Fully rendered prompt to send.</param>
    /// <param name="screenshotPaths">Local image paths selected for the request.</param>
    /// <param name="settings">Current normalized AI settings.</param>
    /// <param name="apiKey">Resolved secret, used only for the outgoing request.</param>
    /// <param name="correlationId">Business correlation identifier for this snapshot.</param>
    /// <param name="cancellationToken">Cancels local file reads and the provider request.</param>
    /// <returns>Provider text and nullable usage metadata.</returns>
    Task<AiProviderResult> DecodeAsync(
        string prompt,
        IReadOnlyList<string> screenshotPaths,
        AppSettings settings,
        string apiKey,
        string correlationId,
        CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
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
    /// Runs provider request and returns a short text output.
    /// </summary>
    Task<string> DecodeAsync(string prompt, IReadOnlyList<string> screenshotPaths, AppSettings settings, string apiKey);
}

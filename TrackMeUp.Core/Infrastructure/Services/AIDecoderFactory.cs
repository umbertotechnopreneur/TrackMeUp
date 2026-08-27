using System;

namespace TrackMeUp.Services;

/// <summary>
/// Creates a decoder implementation for the selected AI provider.
/// </summary>
public static class AIDecoderFactory
{
    /// <summary>
    /// Builds decoder for configured provider.
    /// </summary>
    /// <param name="settings">Current app settings.</param>
    /// <returns>Decoder instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when provider is unknown.</exception>
    public static IAIDecoder Create(AppSettings settings)
    {
        var provider = (settings.AiProvider ?? "openai").ToLowerInvariant();
        return provider switch
        {
            "openai" or "open-ai" => new OpenAiDecoder(),
            "openrouter" => new OpenRouterDecoder(),
            "anthropic" => new AnthropicDecoder(),
            _ => throw new InvalidOperationException($"Provider AI '{provider}' non supportato. Valori validi: openai, openrouter, anthropic."),
        };
    }
}

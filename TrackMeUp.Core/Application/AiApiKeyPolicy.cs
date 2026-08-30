// SPDX-License-Identifier: MIT

namespace TrackMeUp.Application;

/// <summary>Classifies API-key values without authenticating them or exposing their contents.</summary>
public static class AiApiKeyPolicy
{
    private const int MinimumOpenAiKeyLength = 20;

    /// <summary>Returns whether a variable and secret are plausible for the selected provider.</summary>
    public static bool LooksPlausible(string? provider, string? keyVariable, string? secret) => provider?.ToLowerInvariant() switch
    {
        "openai" when IsVariable(keyVariable, "OPENAI_API_KEY", "TRACKMEUP_OPENAI_APIKEY") => LooksLikeOpenAiApiKey(secret),
        "openrouter" when IsVariable(keyVariable, "OPENROUTER_API_KEY") => HasOpaqueSecretShape(secret),
        "anthropic" when IsVariable(keyVariable, "ANTHROPIC_API_KEY") => HasOpaqueSecretShape(secret),
        _ => false
    };

    /// <summary>Returns whether a secret is plausible for one of the supported environment variables.</summary>
    public static bool LooksPlausibleForVariable(string? keyVariable, string? secret) => keyVariable?.ToUpperInvariant() switch
    {
        "OPENAI_API_KEY" or "TRACKMEUP_OPENAI_APIKEY" => LooksLikeOpenAiApiKey(secret),
        "OPENROUTER_API_KEY" or "ANTHROPIC_API_KEY" => HasOpaqueSecretShape(secret),
        _ => false
    };

    /// <summary>Returns whether a secret has the recognizable shape of an OpenAI API key.</summary>
    /// <remarks>This is a local plausibility check, not authentication with OpenAI.</remarks>
    public static bool LooksLikeOpenAiApiKey(string? secret) =>
        HasOpaqueSecretShape(secret)
        && secret!.Length >= MinimumOpenAiKeyLength
        && secret.StartsWith("sk-", StringComparison.Ordinal)
        && !secret.StartsWith("sk-or-", StringComparison.OrdinalIgnoreCase)
        && !secret.StartsWith("sk-ant-", StringComparison.OrdinalIgnoreCase);

    private static bool IsVariable(string? value, params string[] expected) =>
        value is not null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && expected.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool HasOpaqueSecretShape(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || !string.Equals(secret, secret.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in secret)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}

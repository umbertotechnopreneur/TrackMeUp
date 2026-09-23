// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkTrail.Application;

/// <summary>Remembers a successful connection only for its exact configuration and credential in this runtime.</summary>
internal sealed class AiConnectionVerification
{
    private string? _fingerprint;

    internal void Invalidate() => Volatile.Write(ref _fingerprint, null);

    internal void Record(AppSettings settings, string secret) =>
        Volatile.Write(ref _fingerprint, Fingerprint(settings, secret));

    internal bool Matches(AppSettings settings, string? secret) =>
        !string.IsNullOrEmpty(secret)
        && string.Equals(Volatile.Read(ref _fingerprint), Fingerprint(settings, secret), StringComparison.Ordinal);

    private static string Fingerprint(AppSettings settings, string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]
        {
            settings.AiProvider, settings.Model, settings.AiEndpoint, settings.AiApiKeyName, settings.AiReasoningEffort, secret
        }))));
}

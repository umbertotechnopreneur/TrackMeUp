// SPDX-License-Identifier: MIT

namespace TrackMeUp.Services;

/// <summary>Separates revoked collection permission from ordinary caller/shutdown cancellation.</summary>
internal static class AiPolicyCancellation
{
    private static readonly AsyncLocal<CancellationToken> Current = new();

    internal static async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken policyToken)
    {
        var previous = Current.Value;
        Current.Value = policyToken;
        try { return await operation().ConfigureAwait(false); }
        finally { Current.Value = previous; }
    }

    internal static void ThrowIfRevoked() => Current.Value.ThrowIfCancellationRequested();
}

// SPDX-License-Identifier: MIT

using WorkTrail.Application;
using Xunit;

namespace WorkTrail.Core.Tests;

public sealed class AiConnectionVerificationTests
{
    [Fact]
    public void CompletionRequiresASuccessForTheExactConfigurationAndCredential()
    {
        var verification = new AiConnectionVerification();
        var settings = new AppSettings();
        var syntheticCredential = new string('a', 32);

        Assert.False(verification.Matches(settings, syntheticCredential));
        verification.Record(settings, syntheticCredential);
        Assert.True(verification.Matches(settings with { OpenAiEnabled = true }, syntheticCredential));
        Assert.False(verification.Matches(settings, new string('b', 32)));
        Assert.False(verification.Matches(settings, null));
        Assert.False(verification.Matches(settings with { Model = "changed-model" }, syntheticCredential));
        Assert.False(verification.Matches(settings with { AiEndpoint = "https://example.invalid" }, syntheticCredential));
        Assert.False(verification.Matches(settings with { AiProvider = "anthropic" }, syntheticCredential));
        Assert.False(verification.Matches(settings with { AiApiKeyName = "OTHER_KEY" }, syntheticCredential));
        Assert.False(verification.Matches(settings with { AiReasoningEffort = "changed-effort" }, syntheticCredential));
        verification.Invalidate();
        Assert.False(verification.Matches(settings, syntheticCredential));
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class AiPromptAndPayloadTests
{
    [Theory]
    [InlineData("{\"error\":{\"code\":\"insufficient_quota\",\"type\":\"requests\"}}", "insufficient_quota")]
    [InlineData("{\"error\":{\"code\":\"rate_limit_exceeded\",\"type\":\"requests\"}}", "rate_limit_exceeded")]
    [InlineData("{\"error\":{\"code\":\"unsafe markup\",\"type\":\"rate_limit_error\"}}", "rate_limit_error")]
    [InlineData("{\"error\":{\"code\":\"<script>\",\"type\":\"also unsafe\"}}", null)]
    public void OpenAiErrorCode_OnlyReturnsAllowlistedMachineTokens(string response, string? expected)
    {
        Assert.Equal(expected, OpenAiDecoder.ReadApiErrorCode(response));
    }

    [Fact]
    public void Profiles_HaveDeterministicBudgetsAndDetailLevels()
    {
        var compact = AiAnalysisProfileCatalog.Resolve(" COMPACT ");
        var balanced = AiAnalysisProfileCatalog.Resolve("balanced");
        var detailed = AiAnalysisProfileCatalog.Resolve("Detailed");

        Assert.Equal(("compact", 512, "low", "low"),
            (compact.Name, compact.MaxOutputTokens, compact.ImageDetail, compact.TextVerbosity));
        Assert.Equal(("balanced", 1024, "auto", "medium"),
            (balanced.Name, balanced.MaxOutputTokens, balanced.ImageDetail, balanced.TextVerbosity));
        Assert.Equal(("detailed", 2048, "high", "high"),
            (detailed.Name, detailed.MaxOutputTokens, detailed.ImageDetail, detailed.TextVerbosity));
        Assert.Equal("balanced", AiAnalysisProfileCatalog.Resolve("unknown").Name);
    }

    [Fact]
    public void PromptRenderer_LoadsProfileAssetAndRendersContext()
    {
        var context = new AnalysisContextSnapshot(
            "Visual Studio",
            "TrackMeUp",
            "AiPromptCatalog.cs",
            "active",
            new Dictionary<string, string>
            {
                ["FocusedScreen"] = "Monitor 2",
                ["FocusedCapture"] = "monitor-2"
            });

        var prompt = AiPromptCatalog.RenderScreenshotAnalysis("balanced", context);

        Assert.Contains("## Visible data", prompt, StringComparison.Ordinal);
        Assert.Contains("application=Visual Studio", prompt, StringComparison.Ordinal);
        Assert.Contains("detail=TrackMeUp", prompt, StringComparison.Ordinal);
        Assert.Contains("focused_screen=Monitor 2", prompt, StringComparison.Ordinal);
        Assert.Contains("focused_capture=monitor-2", prompt, StringComparison.Ordinal);
        Assert.Contains("1024", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{LOCAL_CONTEXT}}", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptRenderer_FailsWhenRequiredAssetCannotBeLoaded()
    {
        Assert.Throws<InvalidOperationException>(() => AiPromptCatalog.RenderScreenshotAnalysis(
            "compact",
            new AnalysisContextSnapshot("App", "Task", "Window", "active", null),
            _ => throw new InvalidOperationException("simulated loader failure")));
    }

    [Fact]
    public void PromptRenderer_AppendsConfiguredCustomInstructionAfterBuiltInPrompt()
    {
        var prompt = AiPromptCatalog.RenderScreenshotAnalysis(
            "compact",
            new AnalysisContextSnapshot("App", "Task", "Window", "active", null, InformationalSchedule: "Monday: planned active hours 09:00-18:00."),
            customPrompt: "Prefer concise findings.");

        Assert.Contains("informational_schedule=Monday: planned active hours 09:00-18:00.", prompt, StringComparison.Ordinal);
        Assert.Contains("## Additional user instruction", prompt, StringComparison.Ordinal);
        Assert.EndsWith("Prefer concise findings.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderUsageReaders_PreserveNullableAndProviderSpecificFields()
    {
        using var openAi = JsonDocument.Parse("""
            {"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15,"input_tokens_details":{"cached_tokens":3,"cache_write_tokens":2},"output_tokens_details":{"reasoning_tokens":4}}}
            """);
        using var anthropic = JsonDocument.Parse("""
            {"usage":{"input_tokens":10,"output_tokens":5,"cache_read_input_tokens":3,"cache_creation_input_tokens":2,"output_tokens_details":{"thinking_tokens":4}}}
            """);
        using var openRouter = JsonDocument.Parse("""
            {"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15,"prompt_tokens_details":{"cached_tokens":3,"cache_write_tokens":2},"completion_tokens_details":{"reasoning_tokens":4},"cost":0.0125,"cost_details":{"upstream_inference_cost":0.01}}}
            """);

        var openAiUsage = OpenAiDecoder.ReadUsage(openAi.RootElement);
        var anthropicUsage = AnthropicDecoder.ReadUsage(anthropic.RootElement);
        var openRouterUsage = OpenRouterDecoder.ReadUsage(openRouter.RootElement);

        Assert.Equal(3, openAiUsage.CachedInputTokens);
        Assert.Equal(2, openAiUsage.CacheWriteTokens);
        Assert.Equal(4, openAiUsage.ReasoningTokens);
        Assert.Equal(15, anthropicUsage.InputTokens);
        Assert.Equal(20, anthropicUsage.TotalTokens);
        Assert.Equal(4, anthropicUsage.ThinkingTokens);
        Assert.Equal(0.0125m, openRouterUsage.ReportedCostUsd);
        Assert.Equal(0.01m, openRouterUsage.ReportedUpstreamCostUsd);
    }

    [Fact]
    public void AnthropicHeaders_UseProviderRequiredApiKeyHeader()
    {
        using var request = new System.Net.Http.HttpRequestMessage();

        AnthropicDecoder.ApplyRequiredHeaders(request, "test-key");

        Assert.Null(request.Headers.Authorization);
        Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Equal("2023-06-01", Assert.Single(request.Headers.GetValues("anthropic-version")));
    }

    [Fact]
    public void OpenAiPayload_AppliesDetailedProfileAndReasoningEffort()
    {
        var settings = new AppSettings(
            Model: "gpt-5.6",
            AiOutputDetail: "detailed",
            AiReasoningEffort: "high");

        using var payload = JsonDocument.Parse(OpenAiDecoder.SerializePayload(
            "analyze",
            ["data:image/webp;base64,AAAA"],
            settings));

        var root = payload.RootElement;
        Assert.Equal(2048, root.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal("high", root.GetProperty("text").GetProperty("verbosity").GetString());
        Assert.Equal("high", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("high", root.GetProperty("input")[0].GetProperty("content")[1].GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("unsupported")]
    public void OpenAiPayload_OmitsReasoningWhenAutomaticOrInvalid(string effort)
    {
        var settings = new AppSettings(AiReasoningEffort: effort);

        using var payload = JsonDocument.Parse(OpenAiDecoder.SerializePayload(
            "analyze",
            [],
            settings));

        Assert.False(payload.RootElement.TryGetProperty("reasoning", out _));
    }

    [Fact]
    public void ThirdPartyPayloads_ApplyOnlyCompatibleBudgetFields()
    {
        var openRouterSettings = new AppSettings(
            AiOutputDetail: "detailed",
            AiReasoningEffort: "max");
        using var openRouterPayload = JsonDocument.Parse(OpenRouterDecoder.SerializePayload(
            "analyze",
            ["data:image/webp;base64,AAAA"],
            openRouterSettings));

        Assert.Equal(2048, openRouterPayload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("high", openRouterPayload.RootElement.GetProperty("messages")[0]
            .GetProperty("content")[1].GetProperty("image_url").GetProperty("detail").GetString());
        Assert.False(openRouterPayload.RootElement.TryGetProperty("reasoning", out _));
        Assert.False(openRouterPayload.RootElement.TryGetProperty("text", out _));

        var anthropicSettings = new AppSettings(
            AiOutputDetail: "compact",
            AiReasoningEffort: "high");
        using var anthropicPayload = JsonDocument.Parse(AnthropicDecoder.SerializePayload(
            "analyze",
            ["AAAA"],
            anthropicSettings));

        Assert.Equal(512, anthropicPayload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.False(anthropicPayload.RootElement.TryGetProperty("reasoning", out _));
        Assert.False(anthropicPayload.RootElement.TryGetProperty("text", out _));
    }
}

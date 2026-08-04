using System;
using System.Text.Json;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class AiPromptAndPayloadTests
{
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
            null);

        var prompt = AiPromptCatalog.RenderScreenshotAnalysis("balanced", context);

        Assert.Contains("## Visible data", prompt, StringComparison.Ordinal);
        Assert.Contains("application=Visual Studio", prompt, StringComparison.Ordinal);
        Assert.Contains("detail=TrackMeUp", prompt, StringComparison.Ordinal);
        Assert.Contains("1024", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{LOCAL_CONTEXT}}", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptRenderer_UsesCompiledFallbackWhenLoaderFails()
    {
        var prompt = AiPromptCatalog.RenderScreenshotAnalysis(
            "compact",
            new AnalysisContextSnapshot("App {{SYSTEM_TELEMETRY}}", "Task", "Window", "active", null),
            _ => throw new InvalidOperationException("simulated loader failure"));

        Assert.Contains("Return four short Markdown bullets", prompt, StringComparison.Ordinal);
        Assert.Contains("App { {SYSTEM_TELEMETRY} }", prompt, StringComparison.Ordinal);
        Assert.Contains("512", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{MAX_OUTPUT_TOKENS}}", prompt, StringComparison.Ordinal);
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
            new[] { "data:image/webp;base64,AAAA" },
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
            Array.Empty<string>(),
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
            new[] { "data:image/webp;base64,AAAA" },
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
            new[] { "AAAA" },
            anthropicSettings));

        Assert.Equal(512, anthropicPayload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.False(anthropicPayload.RootElement.TryGetProperty("reasoning", out _));
        Assert.False(anthropicPayload.RootElement.TryGetProperty("text", out _));
    }
}

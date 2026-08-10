using System;
using System.Globalization;
using TrackMeUp.Application;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class ScreenshotDetailsProjectionTests
{
    [Fact]
    public void ParseMarkdown_ProjectsHeadingsListsAndParagraphsWithoutInteractiveTargets()
    {
        var blocks = ScreenshotDetailsProjection.ParseMarkdown("""
            ## Focus
            Worked on **TrackMeUp** and [reviewed the change](https://example.test/private).

            - Implemented the pane
            1. Verified <script>alert('no')</script> layout
            """);

        Assert.Collection(
            blocks,
            block => Assert.Equal(new SafeMarkdownBlock(SafeMarkdownBlockKind.Heading, "Focus"), block),
            block => Assert.Equal(new SafeMarkdownBlock(SafeMarkdownBlockKind.Paragraph, "Worked on TrackMeUp and reviewed the change."), block),
            block => Assert.Equal(new SafeMarkdownBlock(SafeMarkdownBlockKind.Bullet, "Implemented the pane", "•"), block),
            block => Assert.Equal(new SafeMarkdownBlock(SafeMarkdownBlockKind.Numbered, "Verified alert('no') layout", "1."), block));
        Assert.DoesNotContain(blocks, block => block.Text.Contains("https://", StringComparison.Ordinal));
        Assert.DoesNotContain(blocks, block => block.Text.Contains("<script>", StringComparison.Ordinal));
    }

    [Fact]
    public void ToPlainTextPreview_RemovesMarkdownHeadingsFormattingAndTargets()
    {
        var preview = ScreenshotDetailsProjection.ToPlainTextPreview("""
            ## Activity

            The user is listening to **Spotify** while reviewing [liked songs](https://example.test/private).
            """);

        Assert.Equal("The user is listening to Spotify while reviewing liked songs.", preview);
        Assert.DoesNotContain("#", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("**", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_FormatsOptionalSnapshotDetailsWithoutInventingMissingAiContent()
    {
        var capturedAt = new DateTimeOffset(2026, 8, 9, 2, 21, 0, TimeSpan.Zero);
        var item = new ScreenshotGalleryItem(
            capturedAt,
            "C:\\captures\\frame.webp",
            "ChatGPT",
            "monitor",
            "scheduled",
            [new ActivityLabelSample(capturedAt, "Review")],
            AiDescriptionMarkdown: null,
            AiAnalyzedAt: null,
            ActivityIndex: 72);

        var state = ScreenshotDetailsProjection.Create(
            item,
            CultureInfo.GetCultureInfo("it-IT"),
            "Schermo",
            "Pianificata",
            "--");

        Assert.Equal("ChatGPT", state.Application);
        Assert.Equal("Schermo", state.CaptureKind);
        Assert.Equal("Pianificata", state.Origin);
        Assert.Equal("72", state.ActivityIndex);
        Assert.Equal("Review", state.ActivityLabels);
        Assert.Empty(state.AiDescription);
        Assert.Null(state.AnalysisTime);
    }
}

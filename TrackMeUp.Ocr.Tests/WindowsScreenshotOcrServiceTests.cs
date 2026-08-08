using TrackMeUp.Ocr;
using Xunit;

namespace TrackMeUp.Ocr.Tests;

public sealed class WindowsScreenshotOcrServiceTests
{
    [Fact]
    public void Constructor_WithNullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsScreenshotOcrService(null!));
    }

    [Fact]
    public async Task ExtractAsync_WhenDisabled_ReturnsBeforePathValidationOrCancellation()
    {
        var service = new WindowsScreenshotOcrService(new OcrOptions { Enabled = false });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ScreenshotOcrResult result = await service.ExtractAsync("invalid\0path", cancellation.Token);

        Assert.False(service.IsEnabled);
        Assert.Equal(OcrExtractionStatus.Disabled, result.Status);
        Assert.Equal(string.Empty, result.RawText);
        Assert.Null(result.EffectiveLanguageTag);
        Assert.Null(result.TextAngleDegrees);
        Assert.Null(result.PixelWidth);
        Assert.Null(result.PixelHeight);
        Assert.Equal(WindowsScreenshotOcrService.EngineName, result.EngineName);
        Assert.Equal(TimeSpan.Zero, result.CompletedAtUtc.Offset);
        Assert.Empty(result.Lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExtractAsync_WhenEnabledAndPathIsBlank_Throws(string imagePath)
    {
        var service = new WindowsScreenshotOcrService(new OcrOptions { Enabled = true });

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExtractAsync(imagePath));
    }

    [Fact]
    public async Task ExtractAsync_WhenEnabledAndPathIsMissing_ThrowsFileNotFoundBeforeOcrInterop()
    {
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"trackmeup-ocr-missing-{Guid.NewGuid():N}.png");
        var service = new WindowsScreenshotOcrService(new OcrOptions { Enabled = true });

        FileNotFoundException exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.ExtractAsync(missingPath));

        Assert.Equal(Path.GetFullPath(missingPath), exception.FileName);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(" it-IT")]
    [InlineData("it-IT ")]
    public async Task ExtractAsync_WhenPreferredLanguageTagIsInvalid_ThrowsBeforePathAccess(
        string preferredLanguageTag)
    {
        var service = new WindowsScreenshotOcrService(new OcrOptions
        {
            Enabled = true,
            PreferredLanguageTag = preferredLanguageTag,
        });

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExtractAsync("invalid\0path"));
    }

    [Fact]
    public async Task ExtractAsync_WhenEnabledAndCancelled_ThrowsBeforePathAccess()
    {
        var service = new WindowsScreenshotOcrService(new OcrOptions { Enabled = true });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExtractAsync("invalid\0path", cancellation.Token));
    }
}

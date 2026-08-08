using System.Collections.Immutable;
using TrackMeUp.Ocr;
using Xunit;

namespace TrackMeUp.Ocr.Tests;

public sealed class OcrContractTests
{
    [Fact]
    public void Options_DefaultToDisabledWithAutomaticProfileLanguageSelection()
    {
        var options = new OcrOptions();

        Assert.False(options.Enabled);
        Assert.Null(options.PreferredLanguageTag);
    }

    [Fact]
    public void ExtractionStatus_ContainsOnlyTheSupportedOutcomes()
    {
        Assert.Equal(
            ["Disabled", "Succeeded", "NoText"],
            Enum.GetNames<OcrExtractionStatus>());
    }

    [Fact]
    public void TextLine_TakesAnImmutableCopyOfWords()
    {
        var source = new List<OcrTextWord>
        {
            new("raw", new OcrTextRectangle(1, 2, 30, 10)),
        };

        var line = new OcrTextLine("raw", source);
        source.Clear();

        OcrTextWord word = Assert.Single(line.Words);
        Assert.Equal("raw", word.Text);
        Assert.Equal(new OcrTextRectangle(1, 2, 30, 10), word.BoundingRectangle);
    }

    [Fact]
    public void TextRectangle_RejectsNonFiniteOrNegativeGeometry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OcrTextRectangle(double.NaN, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OcrTextRectangle(0, 0, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OcrTextRectangle(0, 0, 1, double.PositiveInfinity));
    }

    [Fact]
    public void RecognizedFactory_PreservesRawTextAndSelectsSucceededStatus()
    {
        ImmutableArray<OcrTextLine> lines =
        [new OcrTextLine("First line", [new OcrTextWord("First", new OcrTextRectangle(0, 0, 20, 10))])];

        ScreenshotOcrResult result = ScreenshotOcrResult.CreateRecognized(
            "First line\r\nSecond line",
            "it-IT",
            1.5,
            DateTimeOffset.UnixEpoch,
            WindowsScreenshotOcrService.EngineName,
            1920,
            1080,
            lines);

        Assert.Equal(OcrExtractionStatus.Succeeded, result.Status);
        Assert.Equal("First line\r\nSecond line", result.RawText);
        Assert.Equal("it-IT", result.EffectiveLanguageTag);
        Assert.Equal(1.5, result.TextAngleDegrees);
        Assert.Equal((uint)1920, result.PixelWidth);
        Assert.Equal((uint)1080, result.PixelHeight);
        Assert.Equal(lines, result.Lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n")]
    public void RecognizedFactory_SelectsNoTextWithoutChangingRawText(string rawText)
    {
        ScreenshotOcrResult result = ScreenshotOcrResult.CreateRecognized(
            rawText,
            "en-US",
            null,
            DateTimeOffset.UnixEpoch,
            WindowsScreenshotOcrService.EngineName,
            100,
            50,
            ImmutableArray<OcrTextLine>.Empty);

        Assert.Equal(OcrExtractionStatus.NoText, result.Status);
        Assert.Equal(rawText, result.RawText);
    }

    [Fact]
    public void ResultAndNestedContractsExposeNoPublicSetters()
    {
        Type[] immutableContracts =
        [
            typeof(ScreenshotOcrResult),
            typeof(OcrTextLine),
            typeof(OcrTextWord),
            typeof(OcrTextRectangle),
        ];

        foreach (Type contract in immutableContracts)
        {
            Assert.All(
                contract.GetProperties(),
                property => Assert.Null(property.SetMethod));
        }
    }
}

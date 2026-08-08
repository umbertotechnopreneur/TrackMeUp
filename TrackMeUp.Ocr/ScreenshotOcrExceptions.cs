using System.Collections.Immutable;

namespace TrackMeUp.Ocr;

/// <summary>
/// Identifies the Windows operation that failed during OCR interop.
/// </summary>
public enum OcrInteropStage
{
    /// <summary>
    /// Windows failed while selecting or creating the OCR engine.
    /// </summary>
    InitializeEngine = 0,

    /// <summary>
    /// Windows failed while opening the source image.
    /// </summary>
    OpenImage = 1,

    /// <summary>
    /// Windows failed while reading image metadata.
    /// </summary>
    DecodeImage = 2,

    /// <summary>
    /// Windows failed while converting the image to a supported software bitmap.
    /// </summary>
    ConvertImage = 3,

    /// <summary>
    /// Windows failed while recognizing or projecting text results.
    /// </summary>
    RecognizeText = 4,
}

/// <summary>
/// Provides a common base for explicit local OCR failures.
/// </summary>
public abstract class ScreenshotOcrException : Exception
{
    /// <summary>
    /// Initializes an OCR failure with a message.
    /// </summary>
    /// <param name="message">The failure description.</param>
    protected ScreenshotOcrException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes an OCR failure with a message and its underlying cause.
    /// </summary>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying failure.</param>
    protected ScreenshotOcrException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Indicates that Windows has no recognizer for the requested language selection.
/// </summary>
public sealed class ScreenshotOcrLanguageUnavailableException : ScreenshotOcrException
{
    internal ScreenshotOcrLanguageUnavailableException(
        string? requestedLanguageTag,
        ImmutableArray<string> availableLanguageTags)
        : base(CreateMessage(requestedLanguageTag, availableLanguageTags))
    {
        RequestedLanguageTag = requestedLanguageTag;
        AvailableLanguageTags = availableLanguageTags;
    }

    /// <summary>
    /// Gets the requested language tag, or <see langword="null"/> when user-profile selection failed.
    /// </summary>
    public string? RequestedLanguageTag { get; }

    /// <summary>
    /// Gets the immutable language tags reported by Windows when the failure occurred.
    /// </summary>
    public ImmutableArray<string> AvailableLanguageTags { get; }

    private static string CreateMessage(
        string? requestedLanguageTag,
        ImmutableArray<string> availableLanguageTags)
    {
        string requested = requestedLanguageTag is null
            ? "the configured user-profile languages"
            : $"'{requestedLanguageTag}'";
        string available = availableLanguageTags.IsEmpty
            ? "none"
            : string.Join(", ", availableLanguageTags);

        return $"Windows OCR cannot resolve {requested}. Available recognizer languages: {available}.";
    }
}

/// <summary>
/// Indicates that an image exceeds the maximum dimensions accepted by Windows OCR.
/// </summary>
public sealed class ScreenshotOcrImageTooLargeException : ScreenshotOcrException
{
    internal ScreenshotOcrImageTooLargeException(uint pixelWidth, uint pixelHeight, uint maximumDimension)
        : base(
            $"Image dimensions {pixelWidth}x{pixelHeight} exceed the Windows OCR maximum " +
            $"dimension of {maximumDimension} pixels.")
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        MaximumDimension = maximumDimension;
    }

    /// <summary>
    /// Gets the decoded source-image width.
    /// </summary>
    public uint PixelWidth { get; }

    /// <summary>
    /// Gets the decoded source-image height.
    /// </summary>
    public uint PixelHeight { get; }

    /// <summary>
    /// Gets the maximum supported width or height.
    /// </summary>
    public uint MaximumDimension { get; }
}

/// <summary>
/// Indicates that a Windows file, imaging, or OCR interop operation failed.
/// </summary>
public sealed class ScreenshotOcrInteropException : ScreenshotOcrException
{
    internal ScreenshotOcrInteropException(OcrInteropStage stage, string message, Exception innerException)
        : base(message, innerException)
    {
        Stage = stage;
    }

    /// <summary>
    /// Gets the Windows operation that failed.
    /// </summary>
    public OcrInteropStage Stage { get; }
}

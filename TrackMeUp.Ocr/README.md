# TrackMeUp.Ocr

`TrackMeUp.Ocr` is an optional, on-device screenshot OCR adapter over
`Windows.Media.Ocr`. It does not call cloud services or AI providers.

## Usage

```csharp
IScreenshotOcrService ocr = new WindowsScreenshotOcrService(new OcrOptions
{
    Enabled = true,
    PreferredLanguageTag = "it-IT",
});

ScreenshotOcrResult result = await ocr.ExtractAsync(screenshotPath, cancellationToken);
```

When `PreferredLanguageTag` is `null`, Windows selects the first installed OCR
language that matches the user's profile languages. There is no fallback from an
explicit unsupported language to another language.

The Windows OCR API requires package identity for desktop applications. TrackMeUp's
MSIX deployment supplies that identity. An unsupported unpackaged runtime is reported
as `ScreenshotOcrInteropException`; the module does not switch engines silently.

## Verification checklist

- [x] Unit: disabled extraction returns `Disabled` before cancellation, path validation, or image I/O.
- [x] Unit: enabled extraction rejects invalid options and paths explicitly.
- [x] Unit: result states and nested line/word collections are immutable.
- [ ] Integration: run OCR against a retained screenshot from the packaged TrackMeUp app on a machine with the selected Windows OCR language installed.

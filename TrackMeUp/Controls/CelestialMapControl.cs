// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Storage.Streams;

namespace TrackMeUp.Controls;

/// <summary>Displays application-rendered Earth pixels without owning geographic calculations or asset I/O.</summary>
public sealed class CelestialMapControl : UserControl
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private int _renderVersion;

    /// <summary>Creates the passive Earth image surface.</summary>
    public CelestialMapControl()
    {
        Content = _image;
    }

    /// <summary>Decodes the supplied in-memory PNG and ignores superseded asynchronous render completions.</summary>
    internal async Task ApplyAsync(CelestialMapImage image, LocalizationService strings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(strings);
        cancellationToken.ThrowIfCancellationRequested();
        var version = ++_renderVersion;
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(image.PngBytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        cancellationToken.ThrowIfCancellationRequested();
        if (version == _renderVersion)
        {
            _image.Source = bitmap;
            AutomationProperties.SetName(this, strings.Translate("Celestial.Map.Title"));
        }
    }
}

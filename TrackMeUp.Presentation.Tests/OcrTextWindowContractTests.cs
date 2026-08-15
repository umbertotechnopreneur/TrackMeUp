using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class OcrTextWindowContractTests
{
    [Fact]
    public void OcrTextWindow_IsASelectableMicaSurfaceWithDebouncedHighlighting()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "OcrTextWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "OcrTextWindow.xaml.cs"));
        var search = window.Descendants().Single(element => HasName(element, "SearchBox"));
        var text = window.Descendants().Single(element => HasName(element, "OcrTextBlock"));

        Assert.Contains(window.Descendants(), element =>
            element.Name.LocalName == "MicaBackdrop" && element.Attribute("Kind")?.Value == "BaseAlt");
        Assert.DoesNotContain(window.Descendants(), element => element.Name.LocalName == "Border");
        Assert.Equal("Right", search.Attribute("HorizontalAlignment")?.Value);
        Assert.Contains(search.Descendants(), element =>
            element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE721");
        Assert.Equal("True", text.Attribute("IsTextSelectionEnabled")?.Value);
        Assert.Contains("MinimumQueryLength = 2", source, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(400)", source, StringComparison.Ordinal);
        Assert.Contains("_searchTimer.IsRepeating = false", source, StringComparison.Ordinal);
        Assert.Contains("OcrTextSearch.FindMatches", source, StringComparison.Ordinal);
        Assert.Contains("OcrTextBlock.TextHighlighters", source, StringComparison.Ordinal);
        Assert.Contains("new SolidColorBrush(Colors.Yellow)", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.OcrText", source, StringComparison.Ordinal);
        Assert.Contains("public void UpdateContent(string ocrText, ElementTheme theme, string language)", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged", source, StringComparison.Ordinal);
        Assert.Contains("ApplyThemeChrome", source, StringComparison.Ordinal);
        Assert.Contains("_xamlRoot.Changed += XamlRoot_Changed", source, StringComparison.Ordinal);
        Assert.Contains("_xamlRoot.Changed -= XamlRoot_Changed", source, StringComparison.Ordinal);
        Assert.Contains("_placement.KeepCurrentBoundsInWorkArea(RootGrid)", source, StringComparison.Ordinal);
        Assert.Contains("RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token)", source, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Cancel();", source, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)", source, StringComparison.Ordinal);
        Assert.Contains("catch (InvalidOperationException) when (_lifetimeCancellation.IsCancellationRequested)", source, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Dispose();", source, StringComparison.Ordinal);
        Assert.Contains("sender.Text.Trim().Length < MinimumQueryLength", source, StringComparison.Ordinal);
        Assert.Contains("ApplyHighlights(SearchBox.Text.Trim())", source, StringComparison.Ordinal);

        var textChangedStart = source.IndexOf("private void SearchBox_TextChanged", StringComparison.Ordinal);
        var clearHighlights = source.IndexOf("ClearHighlights();", textChangedStart, StringComparison.Ordinal);
        var restartTimer = source.IndexOf("_searchTimer.Start();", textChangedStart, StringComparison.Ordinal);
        Assert.True(textChangedStart >= 0);
        Assert.True(clearHighlights > textChangedStart);
        Assert.True(restartTimer > clearHighlights);
    }

    private static string RepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TrackMeUp.slnx")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            }
        }

        throw new DirectoryNotFoundException("Could not locate the TrackMeUp repository root.");
    }

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);
}

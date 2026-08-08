using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class AiStateSurfaceContractTests
{
    [Fact]
    public void MenuAndOptions_BindToOneSharedAiState()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var menuToggle = main.Descendants().Single(element => HasName(element, "OpenAiMenuToggle"));
        var optionsToggle = options.Descendants().Single(element => HasName(element, "OpenAiEnabledSwitch"));
        var statusText = options.Descendants().Single(element => HasName(element, "ApiKeyStatusText"));
        var statusIcon = options.Descendants().Single(element => HasName(element, "ApiKeyStatusIcon"));

        Assert.Equal("{x:Bind AiState.Enabled, Mode=OneWay}", menuToggle.Attribute("IsOn")?.Value);
        Assert.Equal("{x:Bind AiState.CanToggle, Mode=OneWay}", menuToggle.Attribute("IsEnabled")?.Value);
        Assert.Equal("{Binding Enabled, Mode=OneWay}", optionsToggle.Attribute("IsOn")?.Value);
        Assert.Equal("{Binding CanToggle, Mode=OneWay}", optionsToggle.Attribute("IsEnabled")?.Value);
        Assert.Equal("Polite", statusText.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.LiveSetting").Value);
        Assert.Equal("Raw", statusIcon.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.AccessibilityView").Value);
        Assert.Contains("AiState = new AiApplicationState(application);", mainSource, StringComparison.Ordinal);
        Assert.Contains("OptionsControl.Initialize(application, AiState);", mainSource, StringComparison.Ordinal);
        Assert.Contains("DataContext = aiState;", optionsSource, StringComparison.Ordinal);
        Assert.Contains("UpdateApiKeyPresentation();", optionsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"ai.enabled\"]", optionsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", optionsSource, StringComparison.Ordinal);
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

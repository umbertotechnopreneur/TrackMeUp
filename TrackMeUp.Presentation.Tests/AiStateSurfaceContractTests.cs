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

        Assert.Equal("{x:Bind AiState.Enabled, Mode=OneWay}", menuToggle.Attribute("IsChecked")?.Value);
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

    [Fact]
    public void AiOptions_ShowAccessibleDailyDescriptionQuotaFromFacadeDtos()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var stateSource = File.ReadAllText(RepositoryFile("TrackMeUp.Presentation", "AiApplicationState.cs"));
        var quotaPanel = options.Descendants().Single(element => HasName(element, "AiQuotaPanel"));
        var quotaTitle = options.Descendants().Single(element => HasName(element, "AiQuotaTitleText"));
        var quotaUsage = options.Descendants().Single(element => HasName(element, "AiQuotaUsageText"));
        var quotaProgress = options.Descendants().Single(element => HasName(element, "AiQuotaProgressBar"));
        var quotaDescription = options.Descendants().Single(element => HasName(element, "AiQuotaDescriptionText"));
        var quotaExpander = options.Descendants().Single(element => HasName(element, "AiDailyLimitExpander"));
        var quotaLimit = options.Descendants().Single(element => HasName(element, "AiDailyLimitBox"));
        var quotaSave = options.Descendants().Single(element => HasName(element, "SaveAiDailyLimitButton"));

        Assert.Equal("Border", quotaPanel.Name.LocalName);
        Assert.Equal("Polite", quotaPanel.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.LiveSetting").Value);
        Assert.Equal("Options.AiQuota.Title", quotaTitle.Attribute("Tag")?.Value);
        Assert.Equal("Polite", quotaUsage.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.LiveSetting").Value);
        Assert.Equal("ProgressBar", quotaProgress.Name.LocalName);
        Assert.Equal("Options.AiQuota.Description", quotaDescription.Attribute("Tag")?.Value);
        Assert.Equal("Expander", quotaExpander.Name.LocalName);
        Assert.Equal("Options.AiQuota.Configure", quotaExpander.Descendants().Single(element => HasName(element, "AiDailyLimitActionText")).Attribute("Tag")?.Value);
        Assert.Equal("NumberBox", quotaLimit.Name.LocalName);
        Assert.Equal("0", quotaLimit.Attribute("Minimum")?.Value);
        Assert.Equal("400", quotaLimit.Attribute("Maximum")?.Value);
        Assert.Equal("20", quotaLimit.Attribute("Value")?.Value);
        Assert.Equal("SaveAiDailyLimitButton_Click", quotaSave.Attribute("Click")?.Value);
        Assert.Contains("_openAiDailyLimit = settings.OpenAiDailyLimit;", optionsSource, StringComparison.Ordinal);
        Assert.Contains("[\"ai.daily_limit\"]", optionsSource, StringComparison.Ordinal);
        Assert.Contains("SettingsCatalog.MaximumAiDailyLimit", optionsSource, StringComparison.Ordinal);
        Assert.Contains("costGate.DailyAnalysisCount", optionsSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(AiQuotaProgressBar", optionsSource, StringComparison.Ordinal);
        Assert.Contains("nameof(AiApplicationState.CostGate)", optionsSource, StringComparison.Ordinal);
        Assert.Contains("_application.GetAiStatusAsync(cancellationToken)", stateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqliteActivityStore", optionsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CountAiAnalysisResults", optionsSource, StringComparison.Ordinal);
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

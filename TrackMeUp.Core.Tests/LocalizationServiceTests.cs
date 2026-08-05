using System.Globalization;
using System.Collections.Generic;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void ExplicitLanguage_OverridesWindowsUiCulture()
    {
        Assert.Equal("en", LocalizationService.ResolveLanguage("en", CultureInfo.GetCultureInfo("it-IT")));
        Assert.Equal("vi", LocalizationService.ResolveLanguage("vi", CultureInfo.GetCultureInfo("it-IT")));
    }

    [Fact]
    public void SystemLanguage_FollowsSupportedWindowsUiCulture()
    {
        Assert.Equal("it", LocalizationService.ResolveLanguage("system", CultureInfo.GetCultureInfo("it-IT")));
        Assert.Equal("en", LocalizationService.ResolveLanguage("system", CultureInfo.GetCultureInfo("ja-JP")));
    }

    [Fact]
    public void SettingsCatalog_AcceptsSystemLanguageAsTheGreenfieldDefault()
    {
        var settings = new AppSettings();
        var result = SettingsCatalog.Apply(settings, new SettingsPatch(new Dictionary<string, string?> { ["language"] = "system" }));

        Assert.Equal("system", settings.UiLanguage);
        Assert.True(result.Succeeded);
        Assert.Equal("system", result.Value?.UiLanguage);
    }
}

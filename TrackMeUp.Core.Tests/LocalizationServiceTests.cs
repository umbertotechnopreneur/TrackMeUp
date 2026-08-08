using System.Globalization;
using System.Collections.Generic;
using System;
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

    [Fact]
    public void ApiKeyStatus_IsExplicitInEnglishAndItalian()
    {
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");

        Assert.Equal("API key set and ready.", english.Translate("Options.ApiKeyStatus.Set"));
        Assert.Equal("API key not set.", english.Translate("Options.ApiKeyStatus.Missing"));
        Assert.Equal("API key status is unavailable.", english.Translate("Options.ApiKeyStatus.Unavailable"));
        Assert.Equal("Chiave API impostata e pronta.", italian.Translate("Options.ApiKeyStatus.Set"));
        Assert.Equal("Chiave API non impostata.", italian.Translate("Options.ApiKeyStatus.Missing"));
        Assert.Equal("Lo stato della chiave API non è disponibile.", italian.Translate("Options.ApiKeyStatus.Unavailable"));
    }

    [Fact]
    public void DialogMessages_AreLocalizedWithoutEmbeddingSecretValues()
    {
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");

        Assert.Contains("environment variable {0}", english.Translate("Dialog.AiKeyMissing.Message"), StringComparison.Ordinal);
        Assert.Contains("variabile di ambiente {0}", italian.Translate("Dialog.AiKeyMissing.Message"), StringComparison.Ordinal);
        Assert.Equal("Frame analysis unavailable", english.Translate("Dialog.AiAnalysisFailed.Title"));
        Assert.Equal("Analisi del frame non disponibile", italian.Translate("Dialog.AiAnalysisFailed.Title"));
        Assert.Equal("Change key", english.Translate("Options.ApiKeyAction.Change"));
        Assert.Equal("Cambia chiave", italian.Translate("Options.ApiKeyAction.Change"));
    }
}

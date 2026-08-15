using System.Globalization;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void ExplicitLanguage_OverridesWindowsUiCulture()
    {
        Assert.Equal("en-US", LocalizationService.ResolveLanguage("en-US", CultureInfo.GetCultureInfo("it-IT")));
        Assert.Equal("vi-VN", LocalizationService.ResolveLanguage("vi-VN", CultureInfo.GetCultureInfo("it-IT")));
    }

    [Fact]
    public void SystemLanguage_FollowsSupportedWindowsUiCulture()
    {
        Assert.Equal("it-IT", LocalizationService.ResolveLanguage("system", CultureInfo.GetCultureInfo("it-IT")));
        Assert.Equal("en-US", LocalizationService.ResolveLanguage("system", CultureInfo.GetCultureInfo("ja-JP")));
    }

    [Theory]
    [InlineData("en-GB", "en-US")]
    [InlineData("it-CH", "it-IT")]
    [InlineData("fr-CA", "fr-FR")]
    [InlineData("de-CH", "de-DE")]
    [InlineData("es-MX", "es-ES")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh-TW", "en-US")]
    [InlineData("vi-VN", "vi-VN")]
    [InlineData("ko-KR", "ko-KR")]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("pt-BR", "pt-BR")]
    public void SystemLanguage_ResolvesSupportedRegionAndScriptRules(string systemLocale, string expected)
    {
        Assert.Equal(expected, LocalizationService.ResolveLanguage("system", CultureInfo.GetCultureInfo(systemLocale)));
    }

    [Fact]
    public void ExplicitLanguage_AcceptsOnlyCanonicalProductLocales()
    {
        string[] expected =
        [
            "en-US",
            "it-IT",
            "fr-FR",
            "de-DE",
            "es-ES",
            "zh-Hans",
            "vi-VN",
            "ko-KR",
            "pt-PT",
            "pt-BR"
        ];

        Assert.Equal(expected, LocalizationService.SupportedLanguages);
        Assert.All(
            expected,
            locale => Assert.Equal(locale, LocalizationService.ResolveLanguage(locale, CultureInfo.GetCultureInfo("ja-JP"))));
        Assert.All(
            new[] { "en", "it", "fr", "vi", "pt", "zh", "zh-Hant" },
            locale => Assert.Throws<ArgumentException>(
                () => LocalizationService.ResolveLanguage(locale, CultureInfo.GetCultureInfo("en-US"))));
    }

    [Fact]
    public void LanguageContracts_KeepUiSearchAndWindowsOcrCapabilitiesDistinct()
    {
        Assert.Equal(
            new[] { "system", "en-US", "it-IT", "fr-FR", "de-DE", "es-ES", "zh-Hans", "vi-VN", "ko-KR", "pt-PT", "pt-BR" },
            ProductLanguageCatalog.UiChoices);
        Assert.Equal(ProductLanguageCatalog.UiChoices, ProductLanguageCatalog.SearchChoices);
        Assert.Equal(
            new[] { "system", "en-US", "it-IT", "fr-FR", "de-DE", "es-ES", "zh-CN", "ko-KR", "pt-PT", "pt-BR" },
            ProductLanguageCatalog.OcrChoices);
        Assert.Null(ProductLanguageCatalog.ResolveOcrLanguage("system"));
        Assert.Equal("zh-CN", ProductLanguageCatalog.ResolveOcrLanguage("zh-CN"));
        Assert.Throws<ArgumentException>(() => ProductLanguageCatalog.ResolveOcrLanguage("vi-VN"));
        Assert.Throws<ArgumentException>(() => ProductLanguageCatalog.ResolveOcrLanguage("zh-Hans"));

        Assert.Equal(
            ProductLanguageCatalog.UiChoices,
            SettingsCatalog.Definitions.Single(definition => definition.Key == "language").AllowedValues);
        Assert.Equal(
            ProductLanguageCatalog.SearchChoices,
            SettingsCatalog.Definitions.Single(definition => definition.Key == "search.language").AllowedValues);
        Assert.Equal(
            ProductLanguageCatalog.OcrChoices,
            SettingsCatalog.Definitions.Single(definition => definition.Key == "ocr.language").AllowedValues);
    }

    [Theory]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("fr-CA", "fr-FR")]
    [InlineData("nl-NL", "nl-NL")]
    public void SystemSearchLanguage_UsesCanonicalSupportedLocalesWithoutCollapsingUnsupportedCultures(
        string systemCulture,
        string expected)
    {
        Assert.Equal(
            expected,
            ProductLanguageCatalog.ResolveSearchLanguage("system", CultureInfo.GetCultureInfo(systemCulture)));
    }

    [Fact]
    public void SettingsCatalog_AcceptsEachDomainLocaleAndRejectsLegacyOrUnsupportedChoices()
    {
        Assert.All(ProductLanguageCatalog.UiChoices, locale =>
            Assert.True(SettingsCatalog.Apply(
                new AppSettings(),
                new SettingsPatch(new Dictionary<string, string?> { ["language"] = locale })).Succeeded));
        Assert.All(ProductLanguageCatalog.SearchChoices, locale =>
            Assert.True(SettingsCatalog.Apply(
                new AppSettings(),
                new SettingsPatch(new Dictionary<string, string?> { ["search.language"] = locale })).Succeeded));
        Assert.All(ProductLanguageCatalog.OcrChoices, locale =>
            Assert.True(SettingsCatalog.Apply(
                new AppSettings(),
                new SettingsPatch(new Dictionary<string, string?> { ["ocr.language"] = locale })).Succeeded));

        Assert.False(SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?> { ["language"] = "en" })).Succeeded);
        Assert.False(SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?> { ["ocr.language"] = "vi-VN" })).Succeeded);

        Assert.Throws<InvalidDataException>(() => SettingsCatalog.NormalizePersisted(
            new AppSettings { UiLanguage = "it" },
            Path.GetTempPath()));
        var unsupportedLegacyOcr = SettingsCatalog.MigrateLegacyPersistedLocaleIds(
            new AppSettings { OcrLanguage = "vi" },
            out var migrated);
        Assert.True(migrated);
        Assert.Equal("system", SettingsCatalog.NormalizePersisted(
            unsupportedLegacyOcr,
            Path.GetTempPath()).OcrLanguage);
    }

    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("it", "it-IT")]
    [InlineData("fr", "fr-FR")]
    [InlineData("de", "de-DE")]
    [InlineData("es", "es-ES")]
    [InlineData("vi", "vi-VN")]
    public void LoadingLegacyLocaleIds_RewritesCanonicalSettingsExactlyOnce(string legacyLocale, string canonicalLocale)
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(dataDirectory, "appsettings.json");
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var installationId = Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(dataDirectory);
            var legacy = new AppSettings(
                InstallationId: installationId,
                UiLanguage: legacyLocale,
                OcrLanguage: legacyLocale,
                SearchLanguage: legacyLocale);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(legacy, serializerOptions));

            var loaded = new LocalStore(dataDirectory).LoadSettings();
            var persisted = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), serializerOptions);

            var expectedOcrLocale = legacyLocale == "vi" ? "system" : canonicalLocale;
            Assert.Equal(canonicalLocale, loaded.UiLanguage);
            Assert.Equal(expectedOcrLocale, loaded.OcrLanguage);
            Assert.Equal(canonicalLocale, loaded.SearchLanguage);
            Assert.Equal(installationId, loaded.InstallationId);
            Assert.Equal(canonicalLocale, persisted?.UiLanguage);
            Assert.Equal(expectedOcrLocale, persisted?.OcrLanguage);
            Assert.Equal(canonicalLocale, persisted?.SearchLanguage);

            var sentinelWriteTime = DateTime.UtcNow.AddDays(-2);
            File.SetLastWriteTimeUtc(settingsPath, sentinelWriteTime);
            var persistedWriteTime = File.GetLastWriteTimeUtc(settingsPath);
            _ = new LocalStore(dataDirectory).LoadSettings();
            Assert.Equal(persistedWriteTime, File.GetLastWriteTimeUtc(settingsPath));
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void EverySupportedCatalog_LoadsWithAConcreteCultureCompleteKeysAndValidFormats()
    {
        Assert.All(LocalizationService.SupportedLanguages, locale =>
        {
            var strings = new LocalizationService(locale);

            Assert.Equal(locale, strings.Language);
            Assert.NotEqual("StateRunning", strings.Translate("StateRunning"));
            Assert.False(string.IsNullOrWhiteSpace(strings.Culture.Name));
            var format = strings.Translate("AiReprocess.QuotaValue");
            var formatted = strings.Format("AiReprocess.QuotaValue", 1, 10, 9, 2, 3);
            Assert.NotEqual(format, formatted);
            Assert.DoesNotContain("{0", formatted, StringComparison.Ordinal);
        });
        Assert.Equal("zh-CN", new LocalizationService("zh-Hans").Culture.Name);
        Assert.Equal("pt-PT", new LocalizationService("pt-PT").Culture.Name);
        Assert.Equal("pt-BR", new LocalizationService("pt-BR").Culture.Name);
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
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

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
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Contains("environment variable {0}", english.Translate("Dialog.AiKeyMissing.Message"), StringComparison.Ordinal);
        Assert.Contains("variabile di ambiente {0}", italian.Translate("Dialog.AiKeyMissing.Message"), StringComparison.Ordinal);
        Assert.Equal("Frame analysis unavailable", english.Translate("Notification.AiAnalysisFailed.Title"));
        Assert.Equal("Analisi del frame non disponibile", italian.Translate("Notification.AiAnalysisFailed.Title"));
        Assert.Equal("Change key", english.Translate("Options.ApiKeyAction.Change"));
        Assert.Equal("Cambia chiave", italian.Translate("Options.ApiKeyAction.Change"));
    }

    [Fact]
    public void AiConnectionTest_IsLocalizedInEnglishAndItalian()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Equal("AI provider connected", english.Translate("AiConnectionTest.Connected.Title"));
        Assert.Equal("Provider AI connesso", italian.Translate("AiConnectionTest.Connected.Title"));
        Assert.Equal("response", english.Translate("AiConnectionTest.Terminal.Response"));
        Assert.Equal("risposta", italian.Translate("AiConnectionTest.Terminal.Response"));
        Assert.Equal("Close", english.Translate("AiConnectionTest.Close"));
        Assert.Equal("Chiudi", italian.Translate("AiConnectionTest.Close"));
    }

    [Fact]
    public void DailyAiLimitNotification_IsLocalizedInEverySupportedLanguage()
    {
        Assert.All(LocalizationService.SupportedLanguages, language =>
        {
            var strings = new LocalizationService(language);
            Assert.NotEqual("Notification.AiDailyLimitReached.Title", strings.Translate("Notification.AiDailyLimitReached.Title"));
            Assert.NotEqual("Notification.AiDailyLimitReached.Message", strings.Translate("Notification.AiDailyLimitReached.Message"));
            Assert.Contains("OCR", strings.Translate("Notification.AiDailyLimitReached.Message"), StringComparison.OrdinalIgnoreCase);
        });

        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");
        Assert.Equal("Daily AI provider request limit reached", english.Translate("Notification.AiDailyLimitReached.Title"));
        Assert.Equal("Limite giornaliero di richieste al provider AI raggiunto", italian.Translate("Notification.AiDailyLimitReached.Title"));
        Assert.Equal(
            LocalizationService.SupportedLanguages.Count,
            LocalizationService.SupportedLanguages
                .Select(language => new LocalizationService(language).Translate("Notification.AiDailyLimitReached.Message"))
                .Distinct(StringComparer.Ordinal)
                .Count());

        Assert.Contains("visual AI provider requests", english.Translate("Notification.AiDailyLimitReached.Message"), StringComparison.Ordinal);
        Assert.Contains("le nuove descrizioni", italian.Translate("Notification.AiDailyLimitReached.Message"), StringComparison.Ordinal);
        Assert.Contains("mezzanotte locale", italian.Translate("Notification.AiDailyLimitReached.Message"), StringComparison.Ordinal);
        Assert.Contains("perfezionamento OCR tramite AI", italian.Translate("Notification.AiDailyLimitReached.Message"), StringComparison.Ordinal);
    }

    [Fact]
    public void AiQuotaPanel_IsLocalizedInEverySupportedLanguage()
    {
        Assert.All(LocalizationService.SupportedLanguages, language =>
        {
            var strings = new LocalizationService(language);
            Assert.NotEqual("Options.AiQuota.Title", strings.Translate("Options.AiQuota.Title"));
            Assert.NotEqual("Options.AiQuota.Available", strings.Translate("Options.AiQuota.Available"));
            Assert.NotEqual("Options.AiQuota.Reached", strings.Translate("Options.AiQuota.Reached"));
            Assert.NotEqual("Options.AiQuota.Unavailable", strings.Translate("Options.AiQuota.Unavailable"));
            Assert.NotEqual("Options.AiQuota.Description", strings.Translate("Options.AiQuota.Description"));
            Assert.Contains("OCR", strings.Translate("Options.AiQuota.Description"), StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual("Options.AiQuota.ProgressAccessible", strings.Translate("Options.AiQuota.ProgressAccessible"));
            Assert.NotEqual("Options.AiQuota.UnavailableAccessible", strings.Translate("Options.AiQuota.UnavailableAccessible"));
            Assert.NotEqual("Options.AiQuota.Configure", strings.Translate("Options.AiQuota.Configure"));
            Assert.NotEqual("Options.AiQuota.Limit", strings.Translate("Options.AiQuota.Limit"));
            Assert.NotEqual("Options.AiQuota.LimitHint", strings.Translate("Options.AiQuota.LimitHint"));
            Assert.NotEqual("Options.AiQuota.Save", strings.Translate("Options.AiQuota.Save"));
            Assert.NotEqual("Options.AiQuota.Saved", strings.Translate("Options.AiQuota.Saved"));
            Assert.NotEqual("Options.AiQuota.Invalid", strings.Translate("Options.AiQuota.Invalid"));
            Assert.NotEqual("Options.AiQuota.SaveError", strings.Translate("Options.AiQuota.SaveError"));
        });

        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");
        Assert.Equal("Daily AI provider request quota", english.Translate("Options.AiQuota.Title"));
        Assert.Equal("Quota giornaliera di richieste al provider AI", italian.Translate("Options.AiQuota.Title"));
        Assert.Contains("Every visual request", english.Translate("Options.AiQuota.Description"), StringComparison.Ordinal);
        Assert.Contains("local midnight", english.Translate("Options.AiQuota.Description"), StringComparison.Ordinal);
        Assert.Contains("failed attempts", english.Translate("Options.AiQuota.Description"), StringComparison.Ordinal);
        Assert.Contains("ogni richiesta visiva", italian.Translate("Options.AiQuota.Description"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mezzanotte locale", italian.Translate("Options.AiQuota.Description"), StringComparison.Ordinal);
        Assert.Contains("tentativi non riusciti", italian.Translate("Options.AiQuota.Description"), StringComparison.Ordinal);
        Assert.Equal("Funzionalità AI", italian.Translate("Main.Menu.AiProvider"));
        Assert.Equal("Descrizioni AI automatiche", italian.Translate("MenuToggleOpenAi"));
        Assert.Equal("AI features", english.Translate("Options.OpenAi.Header"));
    }

    [Fact]
    public void SnapshotDeleteCommand_IsLocalizedInEverySupportedLanguage()
    {
        Dictionary<string, string> expected = new()
        {
            ["en-US"] = "Delete snapshot",
            ["it-IT"] = "Elimina snapshot",
            ["fr-FR"] = "Supprimer l'instantané",
            ["de-DE"] = "Snapshot löschen",
            ["es-ES"] = "Eliminar instantánea",
            ["vi-VN"] = "Xóa bản chụp"
        };

        Assert.All(expected, item =>
        {
            var strings = new LocalizationService(item.Key);
            Assert.Equal(item.Value, strings.Translate("Snapshot.Delete"));
        });
    }

    [Fact]
    public void ScreenshotDetails_DistinguishHistoricalActivityIndexFromLiveScore()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Equal("Activity index", english.Translate("Screenshots.ActivityIndex"));
        Assert.Equal("Indice attività", italian.Translate("Screenshots.ActivityIndex"));
        Assert.Equal("Show snapshot details", english.Translate("Screenshots.Details.Show"));
        Assert.Equal("Nascondi dettagli snapshot", italian.Translate("Screenshots.Details.Hide"));
        Assert.Equal("Active window", english.Translate("Screenshots.CaptureKind.ActiveWindow"));
        Assert.Equal("Monitor", english.Translate("Screenshots.CaptureKind.Monitor"));
        Assert.Equal("Finestra attiva", italian.Translate("Screenshots.CaptureKind.ActiveWindow"));
        Assert.Equal("Schermo", italian.Translate("Screenshots.CaptureKind.Monitor"));
    }

    [Fact]
    public void AboutDiagnostics_AreLocalizedForOpenAndRedactedShareActions()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Equal("View logs", english.Translate("About.ShowLogs"));
        Assert.Equal("Report a problem", english.Translate("About.ShareLog"));
        Assert.Equal("Visualizza i log", italian.Translate("About.ShowLogs"));
        Assert.Equal("Segnala problema", italian.Translate("About.ShareLog"));
        Assert.Contains("segreti", italian.Translate("About.Diagnostics.Description"), StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIndexingProgress_IsLocalizedInEnglishAndItalian()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Equal("Search history", english.Translate("Search.Title"));
        Assert.Equal("No matching snapshots", english.Translate("Search.NoResults"));
        Assert.Equal("Search indexing", english.Translate("SearchIndex.Title"));
        Assert.Equal("Search results", english.Translate("SearchIndex.Results.Title"));
        Assert.Equal("Search suggestions", english.Translate("SearchIndex.Suggestions.Title"));
        Assert.Equal("Searching the local index", english.Translate("Search.Working"));
        Assert.Equal("Cancel", english.Translate("SearchIndex.Cancel"));
        Assert.Equal("Cerca nella cronologia", italian.Translate("Search.Title"));
        Assert.Equal("Nessuno snapshot corrispondente", italian.Translate("Search.NoResults"));
        Assert.Equal("Indicizzazione ricerca", italian.Translate("SearchIndex.Title"));
        Assert.Equal("Risultati di ricerca", italian.Translate("SearchIndex.Results.Title"));
        Assert.Equal("Suggerimenti di ricerca", italian.Translate("SearchIndex.Suggestions.Title"));
        Assert.Equal("Ricerca nell'indice locale", italian.Translate("Search.Working"));
        Assert.Equal("Annulla", italian.Translate("SearchIndex.Cancel"));
    }

    [Fact]
    public void ActivityCalendar_IsLocalizedInEnglishAndItalian()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Equal("Activity calendar", english.Translate("ActivityCalendar.MenuTitle"));
        Assert.Equal("Calendario attività", italian.Translate("ActivityCalendar.MenuTitle"));
        Assert.Equal("DAY SCORE", english.Translate("ActivityCalendar.Score"));
        Assert.Equal("PUNTEGGIO GIORNALIERO", italian.Translate("ActivityCalendar.Score"));
        Assert.Contains("out of 100", english.Translate("ActivityCalendar.Day.ScoreAccessible"), StringComparison.Ordinal);
        Assert.Contains("su 100", italian.Translate("ActivityCalendar.Day.ScoreAccessible"), StringComparison.Ordinal);
        Assert.Contains("last 12 months", english.Translate("ActivityCalendar.Empty"), StringComparison.Ordinal);
        Assert.Contains("ultimi 12 mesi", italian.Translate("ActivityCalendar.Empty"), StringComparison.Ordinal);
        Assert.Contains("not productivity", english.Translate("ActivityCalendar.Subtitle"), StringComparison.Ordinal);
        Assert.Contains("non la produttività", italian.Translate("ActivityCalendar.Subtitle"), StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalAiReprocessing_UsesExplicitScreenshotAndAcquisitionWordingInEnglishAndItalian()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");

        Assert.Equal("Reprocess missing AI descriptions", english.Translate("ActivityCalendar.Reprocess"));
        Assert.Equal("Riprocessa descrizioni AI mancanti", italian.Translate("ActivityCalendar.Reprocess"));
        Assert.Equal("SCREENSHOTS", english.Translate("AiReprocess.Screenshots"));
        Assert.Equal("SCHERMATE", italian.Translate("AiReprocess.Screenshots"));
        Assert.Equal("ACQUISITIONS", english.Translate("AiReprocess.Captures"));
        Assert.Equal("ACQUISIZIONI", italian.Translate("AiReprocess.Captures"));
        Assert.Contains("AI requests", english.Translate("AiReprocess.ScopeSummary"), StringComparison.Ordinal);
        Assert.Contains("richieste AI", italian.Translate("AiReprocess.ScopeSummary"), StringComparison.Ordinal);
        Assert.Contains("{0:N0}", english.Translate("AiReprocess.Start"), StringComparison.Ordinal);
        Assert.Contains("{0:N0}", italian.Translate("AiReprocess.Start"), StringComparison.Ordinal);
        Assert.Contains("{4:N0}", english.Translate("AiReprocess.QuotaValue"), StringComparison.Ordinal);
        Assert.Contains("{4:N0}", italian.Translate("AiReprocess.QuotaValue"), StringComparison.Ordinal);
        Assert.Contains("{1:N0}", english.Translate("AiReprocess.CompletedCount"), StringComparison.Ordinal);
        Assert.Contains("{1:N0}", italian.Translate("AiReprocess.CompletedCount"), StringComparison.Ordinal);
        Assert.Contains("continues", english.Translate("AiReprocess.CloseKeepsRunning"), StringComparison.Ordinal);
        Assert.Contains("continua", italian.Translate("AiReprocess.CloseKeepsRunning"), StringComparison.Ordinal);
    }

    [Fact]
    public void OperationsDescriptions_AreDetailedInEnglishAndItalian()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");
        (string Key, string English, string Italian)[] descriptions =
        [
            ("Operations.Runtime.Description", "Review the TrackMeUp runtime, protocol and capabilities, logging status, and a current snapshot of CPU, GPU, memory, network, and local storage. These diagnostics are read from this PC.", "Controlla il runtime di TrackMeUp, il protocollo e le funzionalità, lo stato dei log e una fotografia attuale di CPU, GPU, memoria, rete e archiviazione locale. Questi dati diagnostici vengono letti da questo PC."),
            ("Operations.SnapshotAi.Description", "Inspect the latest retained capture, open the screen-capture folder, or ask the configured AI provider to describe the current context. A new capture is created for analysis only when you allow it.", "Controlla l'ultima cattura conservata, apri la cartella delle catture oppure chiedi al provider AI configurato di descrivere il contesto corrente. Una nuova cattura per l'analisi viene creata solo quando lo consenti."),
            ("Operations.Reports.Description", "Create today's report or a digest for a selected date from activity already stored on this PC. Open the generated file automatically or browse the reports folder.", "Crea il report di oggi o il digest di una data scelta usando l'attività già salvata su questo PC. Apri automaticamente il file generato oppure consulta la cartella dei report."),
            ("Operations.Privacy.Description", "Create local rules that exclude matching app names, window titles, or context details before TrackMeUp stores the context or shares it with an AI provider. Review existing rules and test whether the current context would be skipped.", "Crea regole locali che escludono nomi di app, titoli di finestre o dettagli di contesto corrispondenti prima che TrackMeUp salvi il contesto o lo condivida con un provider AI. Controlla le regole esistenti e verifica se il contesto corrente verrebbe ignorato."),
            ("Operations.Retention.Description", "Review how long activity data and screen captures remain on this PC. Preview exactly which records and files are eligible for cleanup; deletion always requires your explicit confirmation.", "Controlla per quanto tempo dati di attività e catture schermo restano su questo PC. Visualizza in anteprima quali record e file possono essere rimossi: l'eliminazione richiede sempre una conferma esplicita."),
            ("Operations.Plugins.Description", "Enable or disable local, app-specific context enrichers. Each plugin can add details from a supported app; disabling it stops that enrichment while core activity tracking continues.", "Abilita o disabilita gli arricchimenti locali del contesto specifici per app. Ogni plugin può aggiungere dettagli da un'app supportata; disabilitarlo interrompe quell'arricchimento senza fermare il monitoraggio attività di base.")
        ];

        Assert.All(descriptions, description =>
        {
            Assert.Equal(description.English, english.Translate(description.Key));
            Assert.Equal(description.Italian, italian.Translate(description.Key));
        });

        Assert.Equal("Data-retention status loaded.", english.Translate("RetentionStatusLoaded"));
        Assert.Equal("Stato della conservazione dati caricato.", italian.Translate("RetentionStatusLoaded"));
        Assert.Equal("Azzeramento totale", italian.Translate("Operations.AtomicNuke.Title"));
        Assert.Equal("The operation could not be completed.", english.Translate("Operations.Result.Failure"));
        Assert.Equal("Non è stato possibile completare l'operazione.", italian.Translate("Operations.Result.Failure"));
    }

    [Fact]
    public void OperationsNavigation_IsLocalizedAndVendorAgnostic()
    {
        var english = new LocalizationService("en-US");
        var italian = new LocalizationService("it-IT");
        (string Key, string English, string Italian)[] navigation =
        [
            ("Options.Operations.Section", "Tools and data controls", "Strumenti e controllo dei dati"),
            ("Options.Operations.Description", "Open dedicated pages for screen captures and AI features, reports, privacy rules, data retention, and context plugins.", "Apri pagine dedicate alle catture schermo e alle funzionalità AI, ai report, alle regole di privacy, alla conservazione dei dati e ai plugin di contesto."),
            ("Options.Navigation.SnapshotAi.Title", "Screen captures and AI features", "Catture schermo e funzionalità AI"),
            ("Options.Navigation.SnapshotAi.Description", "Capture the current screen, inspect saved images, or request a description from the configured AI provider.", "Cattura lo schermo corrente, controlla le immagini salvate o richiedi una descrizione al provider AI configurato."),
            ("Options.Navigation.SnapshotAi.Action", "Open capture and analysis tools", "Apri gli strumenti di cattura e analisi"),
            ("Options.Navigation.Reports.Title", "Reports and digests", "Report e digest"),
            ("Options.Navigation.Reports.Description", "Create local reports from activity already stored on this PC.", "Crea report locali usando l'attività già salvata su questo PC."),
            ("Options.Navigation.Reports.Action", "Open report tools", "Apri gli strumenti per i report"),
            ("Options.Navigation.Privacy.Title", "Privacy rules", "Regole di privacy"),
            ("Options.Navigation.Privacy.Description", "Choose which apps, window titles, and context details TrackMeUp must ignore before storing or sharing context.", "Scegli quali app, titoli di finestra e dettagli di contesto TrackMeUp deve ignorare prima di salvare o condividere il contesto."),
            ("Options.Navigation.Privacy.Action", "Manage privacy rules", "Gestisci le regole di privacy"),
            ("Options.Navigation.Retention.Title", "Data retention", "Conservazione dei dati"),
            ("Options.Navigation.Retention.Description", "Review how long local activity and screen captures are kept and preview cleanup before confirming it.", "Controlla per quanto tempo attività e catture schermo restano sul PC e visualizza in anteprima la pulizia prima di confermarla."),
            ("Options.Navigation.Retention.Action", "Manage data retention", "Gestisci la conservazione dei dati"),
            ("Options.Navigation.Plugins.Title", "Context plugins", "Plugin di contesto"),
            ("Options.Navigation.Plugins.Description", "Manage local, app-specific details without changing core activity tracking.", "Gestisci dettagli locali specifici per app senza modificare il monitoraggio attività di base."),
            ("Options.Navigation.Plugins.Action", "Manage context plugins", "Gestisci i plugin di contesto")
        ];

        Assert.All(navigation, item =>
        {
            Assert.Equal(item.English, english.Translate(item.Key));
            Assert.Equal(item.Italian, italian.Translate(item.Key));
        });

        string[] featureCopyKeys =
        [
            "Options.Operations.Description",
            "Options.Navigation.SnapshotAi.Title"
        ];
        string[] providerCopyKeys =
        [
            "Options.Navigation.SnapshotAi.Description",
            "Operations.SnapshotAi.Description",
            "Operations.Privacy.Description"
        ];
        string[] vendorNames = ["OpenAI", "OpenRouter", "Anthropic"];

        Assert.All(featureCopyKeys, key =>
        {
            Assert.Contains("AI features", english.Translate(key), StringComparison.Ordinal);
            Assert.Contains("funzionalità AI", italian.Translate(key), StringComparison.Ordinal);
        });

        Assert.All(providerCopyKeys, key =>
        {
            var englishCopy = english.Translate(key);
            var italianCopy = italian.Translate(key);
            Assert.Contains("AI provider", englishCopy, StringComparison.Ordinal);
            Assert.Contains("provider AI", italianCopy, StringComparison.Ordinal);
            Assert.All(vendorNames, vendor =>
            {
                Assert.DoesNotContain(vendor, englishCopy, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(vendor, italianCopy, StringComparison.OrdinalIgnoreCase);
            });
        });
        Assert.All(LocalizationService.SupportedLanguages, language =>
        {
            var strings = new LocalizationService(language);
            Assert.All(providerCopyKeys, key =>
                Assert.All(vendorNames, vendor =>
                    Assert.DoesNotContain(vendor, strings.Translate(key), StringComparison.OrdinalIgnoreCase)));
        });

        string[] obsoleteSectionKeys =
        [
            "Operations.Section.Privacy",
            "Operations.Section.Retention",
            "Operations.Section.Plugins"
        ];

        Assert.All(obsoleteSectionKeys, key =>
        {
            Assert.Throws<KeyNotFoundException>(() => english.Translate(key));
            Assert.Throws<KeyNotFoundException>(() => italian.Translate(key));
        });
    }
}

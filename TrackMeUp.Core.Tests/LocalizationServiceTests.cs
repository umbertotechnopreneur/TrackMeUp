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

    [Fact]
    public void ScreenshotDetails_DistinguishHistoricalActivityIndexFromLiveScore()
    {
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");

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
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");

        Assert.Equal("Show log", english.Translate("About.ShowLog"));
        Assert.Equal("Share log", english.Translate("About.ShareLog"));
        Assert.Equal("Mostra log", italian.Translate("About.ShowLog"));
        Assert.Equal("Condividi log", italian.Translate("About.ShareLog"));
        Assert.Contains("segreti", italian.Translate("About.Diagnostics.Description"), StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIndexingProgress_IsLocalizedInEnglishAndItalian()
    {
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");

        Assert.Equal("Search snapshots", english.Translate("Search.Title"));
        Assert.Equal("No matching snapshots", english.Translate("Search.NoResults"));
        Assert.Equal("Search indexing", english.Translate("SearchIndex.Title"));
        Assert.Equal("Search results", english.Translate("SearchIndex.Results.Title"));
        Assert.Equal("Search suggestions", english.Translate("SearchIndex.Suggestions.Title"));
        Assert.Equal("Cancel", english.Translate("SearchIndex.Cancel"));
        Assert.Equal("Cerca negli snapshot", italian.Translate("Search.Title"));
        Assert.Equal("Nessuno snapshot corrispondente", italian.Translate("Search.NoResults"));
        Assert.Equal("Indicizzazione ricerca", italian.Translate("SearchIndex.Title"));
        Assert.Equal("Risultati di ricerca", italian.Translate("SearchIndex.Results.Title"));
        Assert.Equal("Suggerimenti di ricerca", italian.Translate("SearchIndex.Suggestions.Title"));
        Assert.Equal("Annulla", italian.Translate("SearchIndex.Cancel"));
    }

    [Fact]
    public void OperationsDescriptions_AreDetailedInEnglishAndItalian()
    {
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");
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
    }

    [Fact]
    public void OperationsNavigation_IsLocalizedAndVendorAgnostic()
    {
        var english = new LocalizationService("en");
        var italian = new LocalizationService("it");
        (string Key, string English, string Italian)[] navigation =
        [
            ("Options.Operations.Section", "Tools and data controls", "Strumenti e controllo dei dati"),
            ("Options.Operations.Description", "Open dedicated pages for screen captures and AI provider actions, reports, privacy rules, data retention, and context plugins.", "Apri pagine dedicate alle catture schermo e alle azioni del provider AI, ai report, alle regole di privacy, alla conservazione dei dati e ai plugin di contesto."),
            ("Options.Navigation.SnapshotAi.Title", "Screen captures and AI provider", "Catture schermo e provider AI"),
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

        string[] providerCopyKeys =
        [
            "Options.Operations.Description",
            "Options.Navigation.SnapshotAi.Title",
            "Options.Navigation.SnapshotAi.Description",
            "Operations.SnapshotAi.Description",
            "Operations.Privacy.Description"
        ];
        string[] vendorNames = ["OpenAI", "OpenRouter", "Anthropic"];

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

        string[] obsoleteSectionKeys =
        [
            "Operations.Section.Privacy",
            "Operations.Section.Retention",
            "Operations.Section.Plugins"
        ];

        Assert.All(obsoleteSectionKeys, key =>
        {
            Assert.Equal(key, english.Translate(key));
            Assert.Equal(key, italian.Translate(key));
        });
    }
}

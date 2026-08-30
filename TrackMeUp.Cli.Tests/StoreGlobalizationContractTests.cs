// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Cli.Tests;

public sealed class StoreGlobalizationContractTests
{
    private static readonly string[] RequiredLocales =
    [
        "en-US", "it-IT", "fr-FR", "de-DE", "es-ES", "zh-Hans", "vi-VN", "ko-KR", "pt-PT", "pt-BR"
    ];

    [Fact]
    public void Listing_ContainsCompleteCanonicalVendorAgnosticLocaleSet()
    {
        Assert.Equal(RequiredLocales, ProductLanguageCatalog.UiLocales);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "store", "listing.json")));
        var locales = document.RootElement.GetProperty("locales");
        var localeNames = locales.EnumerateObject().Select(locale => locale.Name).ToArray();

        Assert.Equal(RequiredLocales, localeNames);
        foreach (var locale in locales.EnumerateObject())
        {
            var copy = locale.Value;
            Assert.False(string.IsNullOrWhiteSpace(copy.GetProperty("displayName").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(copy.GetProperty("subtitle").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(copy.GetProperty("shortDescription").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(copy.GetProperty("description").GetString()));
            Assert.NotEmpty(copy.GetProperty("features").EnumerateArray());

            var serialized = copy.GetRawText();
            Assert.DoesNotContain("OpenAI", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OpenRouter", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Anthropic", serialized, StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotEqual(
            locales.GetProperty("pt-PT").GetProperty("shortDescription").GetString(),
            locales.GetProperty("pt-BR").GetProperty("shortDescription").GetString());

        foreach (var screenshot in document.RootElement.GetProperty("screenshots").GetProperty("items").EnumerateArray())
        {
            Assert.Contains(screenshot.GetProperty("locale").GetString(), RequiredLocales);
        }
    }

    [Fact]
    public void PackageManifest_DeclaresTheSameExplicitCanonicalLocales()
    {
        var manifest = XDocument.Load(Path.Combine(RepositoryRoot(), "TrackMeUp", "Package.appxmanifest"));
        var ns = manifest.Root!.Name.Namespace;
        var locales = manifest.Root
            .Element(ns + "Resources")!
            .Elements(ns + "Resource")
            .Select(resource => resource.Attribute("Language")?.Value)
            .ToArray();

        Assert.Equal(RequiredLocales, locales);
        Assert.DoesNotContain("x-generate", locales);
    }

    [Fact]
    public void StoreValidator_RejectsDuplicateJsonPropertiesBeforePowerShellDeserialization()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "TrackMeUp.StoreValidator.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var listingPath = Path.Combine(temporaryDirectory, "listing.json");
        File.WriteAllText(
            listingPath,
            """
            {
              "schemaVersion": 1,
              "locales": {
                "en-US": {},
                "en-US": {}
              }
            }
            """);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(RepositoryRoot(), "scripts", "TrackMeUp.ps1"));
            startInfo.ArgumentList.Add("-Action");
            startInfo.ArgumentList.Add("ValidateStoreListing");
            startInfo.ArgumentList.Add("-ListingPath");
            startInfo.ArgumentList.Add(listingPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("PowerShell 7 could not be started for Store validation.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(
                "Duplicate JSON property '$.locales.en-US'.",
                standardOutput + standardError,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "store", "listing.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("TrackMeUp repository root was not found.");
    }
}

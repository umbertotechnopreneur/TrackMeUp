// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class StorePublishingWorkflowContractTests
{
    [Fact]
    public void StoreWorkflow_IsValidationOnlyAndCannotPublish()
    {
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "store-listing.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\TrackMeUp.ps1 -Action ValidateStoreListing", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("environment:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("msstore", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("submission publish", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("STORE_AUTOPUBLISH", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("AZURE_AD_APPLICATION_SECRET", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--clientSecret", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreListing_HasNoAutomaticPublicationSwitch()
    {
        using var listing = JsonDocument.Parse(File.ReadAllText(RepositoryFile("store", "listing.json")));
        var publishingProperties = listing.RootElement
            .GetProperty("publishing")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(
            new[] { "partnerCenterProductId", "partnerCenterMetadataPath" },
            publishingProperties);
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
}

// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TrackMeUp.Runtime;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class RuntimeOperationCatalogTests
{
    /// <summary>Verifies that each typed runtime operation has one unique stable wire name.</summary>
    [Fact]
    public void Catalog_MapsEveryTypedOperationToOneUniqueWireName()
    {
        var definitions = RuntimeOperationCatalog.All;
        var operations = Enum.GetValues<RuntimeOperation>();

        Assert.Equal(operations.Length, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(static item => item.Operation).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(static item => item.WireName).Distinct(StringComparer.Ordinal).Count());
        foreach (var definition in definitions)
        {
            Assert.Matches("^[a-z0-9._]+$", definition.WireName);
            Assert.Equal(definition.WireName, RuntimeOperationCatalog.GetWireName(definition.Operation));
            Assert.True(RuntimeOperationCatalog.TryResolve(definition.WireName, out var resolved));
            Assert.Equal(definition.Operation, resolved);
        }

        Assert.Equal(
            "screenshot.analysis.delete.v1",
            RuntimeOperationCatalog.GetWireName(RuntimeOperation.ScreenshotAnalysisDeleteV1));
    }

    /// <summary>Verifies that duplicate runtime wire names fail catalog construction.</summary>
    [Fact]
    public void Catalog_FailsFastWhenWireNamesAreDuplicated()
    {
        RuntimeOperationDefinition[] duplicates =
        [
            new(RuntimeOperation.RuntimeHealth, "duplicate.operation"),
            new(RuntimeOperation.TrackingStart, "duplicate.operation")
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RuntimeOperationCatalog.BuildWireLookup(duplicates));

        Assert.Equal("Duplicate runtime operation wire name 'duplicate.operation'.", exception.Message);
    }

    /// <summary>Verifies that the host and client use the complete shared typed operation catalog.</summary>
    [Fact]
    public void HostAndClient_ReferenceTheCompleteSharedTypedCatalog()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Runtime", "RuntimeHost.cs"));
        var dispatchStart = source.IndexOf("return operation switch", StringComparison.Ordinal);
        var dispatchEnd = source.IndexOf("catch (OperationCanceledException)", dispatchStart, StringComparison.Ordinal);
        var clientStart = source.IndexOf("public sealed class RuntimeClient", StringComparison.Ordinal);
        Assert.True(dispatchStart >= 0);
        Assert.True(dispatchEnd > dispatchStart);
        Assert.True(clientStart > dispatchEnd);
        var dispatchSource = source[dispatchStart..dispatchEnd];
        var clientSource = source[clientStart..];
        var expected = RuntimeOperationCatalog.All
            .Select(static definition => definition.Operation.ToString())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var hostOperations = Regex.Matches(
                dispatchSource,
                @"RuntimeOperation\.(?<name>[A-Za-z0-9]+)\s*=>")
            .Select(static match => match.Groups["name"].Value)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var clientOperations = Regex.Matches(
                clientSource,
                @"RuntimeOperation\.(?<name>[A-Za-z0-9]+)")
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, hostOperations);
        Assert.Equal(expected, clientOperations);
        Assert.Contains("RuntimeOperation operation", clientSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string operation", clientSource, StringComparison.Ordinal);
        Assert.All(RuntimeOperationCatalog.All, definition =>
            Assert.DoesNotContain($"\"{definition.WireName}\"", source, StringComparison.Ordinal));
    }

    private static string RepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrackMeUp.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, .. segments]);
    }
}

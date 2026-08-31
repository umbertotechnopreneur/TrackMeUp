// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TrackMeUp.Application;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards the dependency direction and passive frontend boundaries of the application.</summary>
public sealed class ArchitectureBoundaryContractTests
{
    private static readonly string[] ForbiddenFrontendInfrastructureTokens =
    [
        "using Microsoft.Win32;",
        "using System.IO;",
        "using System.Net.Http;",
        "using TrackMeUp.Infrastructure;",
        "Directory.CreateDirectory(",
        "Directory.Delete(",
        "Environment.GetEnvironmentVariable(",
        "File.Delete(",
        "File.ReadAll",
        "File.WriteAll",
        "new HttpClient(",
        "new LocalStore(",
        "new ScreenCaptureService(",
        "new SqliteActivityStore(",
        "new TrackingDomainService(",
        "Process.Start("
    ];

    /// <summary>Ensures Core cannot acquire a dependency on a frontend or presentation framework.</summary>
    [Fact]
    public void CoreAssembly_DoesNotReferenceFrontendAssemblies()
    {
        var forbiddenPrefixes = new[]
        {
            "Microsoft.UI",
            "Microsoft.WindowsAppSDK",
            "Spectre",
            "TrackMeUp.Cli",
            "TrackMeUp.Presentation",
            "TrackMeUp.Taskbar"
        };
        var references = typeof(ITrackMeUpApplication).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(references);
    }

    /// <summary>Ensures project references continue to point from frontends toward shared application layers.</summary>
    [Fact]
    public void ProjectReferences_FollowTheApprovedDependencyGraph()
    {
        var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["TrackMeUp"] = ["TrackMeUp.Cli", "TrackMeUp.Core", "TrackMeUp.Presentation", "TrackMeUp.Taskbar"],
            ["TrackMeUp.Cli"] = ["TrackMeUp.Core"],
            ["TrackMeUp.Core"] = ["TrackMeUp.Ocr", "TrackMeUp.Search"],
            ["TrackMeUp.Ocr"] = [],
            ["TrackMeUp.Presentation"] = ["TrackMeUp.Core"],
            ["TrackMeUp.Search"] = [],
            ["TrackMeUp.Taskbar"] = ["TrackMeUp.Core"]
        };

        foreach (var (projectName, expected) in expectedReferences)
        {
            var projectFile = RepositoryFile(projectName, $"{projectName}.csproj");
            var actual = XDocument.Load(projectFile)
                .Descendants("ProjectReference")
                .Select(reference => ProjectNameFromReference((string?)reference.Attribute("Include")))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
        }
    }

    /// <summary>Ensures WinUI presentation sources delegate I/O and environment work to the application facade.</summary>
    [Fact]
    public void WinUiCodeBehind_DoesNotOwnInfrastructureOperations()
    {
        var frontendDirectory = RepositoryFile("TrackMeUp");
        var codeBehindFiles = Directory
            .EnumerateFiles(frontendDirectory, "*.xaml.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}App.xaml.cs", StringComparison.OrdinalIgnoreCase))
            .Concat(Directory.EnumerateFiles(
                Path.Combine(frontendDirectory, "Controls"),
                "*.cs",
                SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(codeBehindFiles);
        Assert.All(codeBehindFiles, AssertPassiveFrontendSource);
    }

    /// <summary>Ensures Spectre command routing and rendering cannot bypass the shared application facade.</summary>
    [Fact]
    public void CliCommandsAndRenderers_DoNotOwnInfrastructureOperations()
    {
        var cliDirectory = RepositoryFile("TrackMeUp.Cli");
        var cliSources = Directory
            .EnumerateFiles(cliDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}CliBootstrap.cs", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(cliSources);
        Assert.All(cliSources, AssertPassiveFrontendSource);
    }

    private static void AssertPassiveFrontendSource(string path)
    {
        var source = File.ReadAllText(path);

        foreach (var forbiddenToken in ForbiddenFrontendInfrastructureTokens)
        {
            Assert.DoesNotContain(forbiddenToken, source, StringComparison.Ordinal);
        }
    }

    private static string ProjectNameFromReference(string? include)
    {
        Assert.False(string.IsNullOrWhiteSpace(include));
        var normalized = include!.Replace('\\', '/');
        return Path.GetFileNameWithoutExtension(normalized);
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

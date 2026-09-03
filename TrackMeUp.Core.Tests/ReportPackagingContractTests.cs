// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Core.Tests;

/// <summary>Guards the production web-report payload from being omitted or stale in desktop packages.</summary>
public sealed partial class ReportPackagingContractTests
{
    /// <summary>Verifies that the tracked report entry point and all of its direct bundles exist.</summary>
    [Fact]
    public void TrackedReportDistribution_IsCompleteAndSelfContained()
    {
        var distributionDirectory = RepositoryFile("TrackMeUp.Reports.Web", "dist");
        var indexPath = Path.Combine(distributionDirectory, "index.html");
        var noticesPath = Path.Combine(distributionDirectory, "THIRD_PARTY_NOTICES.md");

        Assert.True(File.Exists(indexPath), $"Missing production report entry point: {indexPath}");
        Assert.True(File.Exists(noticesPath), $"Missing production report notices: {noticesPath}");

        var indexHtml = File.ReadAllText(indexPath);
        var assetPaths = ProductionAssetReference()
            .Matches(indexHtml)
            .Select(match => match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar))
            .ToArray();

        Assert.NotEmpty(assetPaths);
        Assert.All(assetPaths, relativePath =>
            Assert.True(
                File.Exists(Path.Combine(distributionDirectory, relativePath)),
                $"The report entry point references a missing production bundle: {relativePath}"));
    }

    /// <summary>Verifies that normal builds fail fast instead of silently emitting an incomplete report payload.</summary>
    [Fact]
    public void DesktopProject_ValidatesReportDistributionBeforeBuild()
    {
        var project = XDocument.Load(RepositoryFile("TrackMeUp", "TrackMeUp.csproj"));
        var target = project.Descendants("Target").Single(element =>
            element.Attribute("Name")?.Value == "ValidateTrackMeUpReportsWebAssets");
        var errorConditions = target.Elements("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .ToArray();

        Assert.Contains(errorConditions, condition => condition.Contains("index.html", StringComparison.Ordinal));
        Assert.Contains(errorConditions, condition => condition.Contains("THIRD_PARTY_NOTICES.md", StringComparison.Ordinal));
        Assert.Equal("PrepareForBuild", target.Attribute("BeforeTargets")?.Value);
    }

    /// <summary>Verifies that release automation rebuilds and inspects report assets inside the signed package.</summary>
    [Fact]
    public void PackagingAutomation_RebuildsAndValidatesReportPayload()
    {
        var script = File.ReadAllText(RepositoryFile("scripts", "TrackMeUp.ps1"));
        var workflow = File.ReadAllText(RepositoryFile(".github", "workflows", "build.yml"));
        var packageFunction = script.IndexOf("function Invoke-TrackMeUpMsixPackage", StringComparison.Ordinal);
        var buildCall = script.IndexOf("Invoke-TrackMeUpBuildReports", packageFunction, StringComparison.Ordinal);
        var msbuildCall = script.IndexOf("'msbuild'", packageFunction, StringComparison.Ordinal);

        Assert.True(packageFunction >= 0);
        Assert.InRange(buildCall, packageFunction, msbuildCall - 1);
        Assert.Contains("ReportsWeb/index.html", script, StringComparison.Ordinal);
        Assert.Contains("ReportsWeb/THIRD_PARTY_NOTICES.md", script, StringComparison.Ordinal);
        Assert.Contains("Assert-TrackMeUpPackageIntegrity -PackageFile $packageFile", script, StringComparison.Ordinal);
        Assert.Contains("git status --porcelain --untracked-files=all -- TrackMeUp.Reports.Web/dist", workflow, StringComparison.Ordinal);
    }

    [GeneratedRegex("(?:src|href)=[\"']\\./(?<path>assets/[^\"']+)[\"']", RegexOptions.CultureInvariant)]
    private static partial Regex ProductionAssetReference();

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

// SPDX-License-Identifier: MIT

using System;
using System.IO;
using WorkTrail.Application;
using WorkTrail.Services;
using Xunit;

namespace WorkTrail.Core.Tests;

public sealed class AtomicResetServiceTests
{
    [Fact]
    public void DeleteApplicationData_RemovesDataRootAndOnlyOwnedFilesFromCustomScreenshotDirectory()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"WorkTrail-atomic-reset-{Guid.NewGuid():N}");
        var dataRoot = Path.Combine(testRoot, "WorkTrail");
        var customScreenshots = Path.Combine(testRoot, "captures");
        Directory.CreateDirectory(Path.Combine(dataRoot, "search"));
        Directory.CreateDirectory(customScreenshots);
        File.WriteAllText(Path.Combine(dataRoot, "activity.sqlite3"), "database");
        File.WriteAllText(Path.Combine(dataRoot, "search", "index"), "index");
        var ownedScreenshot = Path.Combine(customScreenshots, $"{new string('a', 32)}_1.0.0_manual_monitor-1.webp");
        var unrelatedFile = Path.Combine(customScreenshots, "keep-me.txt");
        File.WriteAllText(ownedScreenshot, "image");
        File.WriteAllText(unrelatedFile, "unrelated");

        try
        {
            AtomicResetService.DeleteApplicationData(new AtomicResetPlan(
                dataRoot,
                customScreenshots,
                Path.Combine(testRoot, "WorkTrail.exe")));

            Assert.False(Directory.Exists(dataRoot));
            Assert.False(File.Exists(ownedScreenshot));
            Assert.True(File.Exists(unrelatedFile));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void DeleteApplicationData_RejectsDirectoryThatIsNotWorkTrailRoot()
    {
        var unsafeRoot = Path.Combine(Path.GetTempPath(), $"not-the-app-{Guid.NewGuid():N}");
        Directory.CreateDirectory(unsafeRoot);
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AtomicResetService.DeleteApplicationData(new AtomicResetPlan(unsafeRoot, unsafeRoot, "WorkTrail.exe")));

            Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(unsafeRoot));
        }
        finally
        {
            Directory.Delete(unsafeRoot, recursive: true);
        }
    }
}

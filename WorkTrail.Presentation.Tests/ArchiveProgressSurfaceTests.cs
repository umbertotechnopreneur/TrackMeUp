// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using WorkTrail.Application;
using Xunit;

namespace WorkTrail.Presentation.Tests;

public sealed class ArchiveProgressSurfaceTests
{
    /// <summary>Archive phases have localized captions and the modal surface exposes phase, count and elapsed time.</summary>
    [Fact]
    public void ArchiveDialog_ProvidesLocalizedProgressAndStopsPollingOnClose()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "WorkTrail.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var xaml = XDocument.Load(Path.Combine(root.FullName, "WorkTrail", "OperationProgressDialogWindow.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var name in new[] { "PhaseText", "CountText", "ElapsedText", "OperationProgress" })
            Assert.Contains(xaml.Descendants(), element => element.Attribute(ns + "Name")?.Value == name);
        var source = File.ReadAllText(Path.Combine(root.FullName, "WorkTrail", "OperationProgressDialogWindow.xaml.cs"));
        Assert.Contains("GetDataArchiveProgressAsync", source, StringComparison.Ordinal);
        Assert.Contains("_progressTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("_progressTimer.Tick -= ProgressTimer_Tick;", source, StringComparison.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "WorkTrail.Core", "Localization"), "*.json"))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var key in Enum.GetNames<DataArchivePhase>().Concat(new[] { "Waiting", "Elapsed", "Items", "Processed", "Unavailable" }))
                Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("Archive.Progress." + key).GetString()));
        }
    }
}

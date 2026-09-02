// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TrackMeUp.Services;

/// <summary>Durable intent makes filesystem/SQLite/index deletion retryable across errors and restarts.</summary>
internal sealed class ScreenshotDeletionJournal(LocalStore store)
{
    private string JournalDirectory => Path.Combine(store.DataDirectory, "pending-screenshot-deletions");

    internal sealed record Plan(string ScreenshotPath, string[] Artifacts, bool DeleteAnalysis);

    internal Plan? Begin(string path, bool deleteAnalysis)
    {
        if (!ScreenCaptureService.IsOwnedArtifact(path) || !Path.IsPathFullyQualified(path)) return null;
        var journal = GetPath(path);
        if (File.Exists(journal))
        {
            var pending = Read(journal);
            if (deleteAnalysis && !pending.DeleteAnalysis)
            {
                pending = pending with { DeleteAnalysis = true };
                Save(journal, pending);
            }
            return pending;
        }
        var artifacts = store.FindScreenshotArtifacts(path);
        if (artifacts.Count == 0) return null;
        var plan = new Plan(Path.GetFullPath(path), artifacts.ToArray(), deleteAnalysis);
        Validate(plan);
        Save(journal, plan);
        return plan;
    }

    internal IReadOnlyList<Plan> Pending() => Directory.Exists(JournalDirectory)
        ? Directory.EnumerateFiles(JournalDirectory, "*.json").Select(Read).ToArray() : [];

    internal void Execute(Plan plan)
    {
        Validate(plan);
        // Intent is already durable. Any failure leaves it available even when the image is now absent.
        foreach (var path in plan.Artifacts) File.Delete(path);
        if (plan.DeleteAnalysis)
            foreach (var path in plan.Artifacts.Append(plan.ScreenshotPath).Distinct(StringComparer.OrdinalIgnoreCase))
                store.DeleteAiAnalysesReferencingScreenshot(path);
        store.DeleteScreenshotTextSnapshot(plan.ScreenshotPath);
        store.DeleteScreenshotIntervalTelemetry(plan.ScreenshotPath);
        store.DeleteScreenshotCaptureIfOrphaned(plan.ScreenshotPath);
    }

    internal void Complete(Plan plan) => File.Delete(GetPath(plan.ScreenshotPath));

    private Plan Read(string path)
    {
        RejectLinks(path);
        var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Screenshot deletion intent is invalid.");
        Validate(plan);
        if (!string.Equals(Path.GetFullPath(path), GetPath(plan.ScreenshotPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Screenshot deletion identity does not match its journal.");
        return plan;
    }

    private void Validate(Plan plan)
    {
        var settings = store.LoadSettings();
        var root = ScreenshotStorageLayout.NormalizeRoot(settings.ScreenshotDirectory);
        var identity = LocalStore.ScreenshotIdentity(Path.GetFileName(plan.ScreenshotPath));
        if (plan.Artifacts is null || plan.Artifacts.Length == 0) throw new InvalidDataException("Screenshot deletion has no artifacts.");
        foreach (var path in plan.Artifacts.Append(plan.ScreenshotPath))
        {
            if (!Path.IsPathFullyQualified(path) || !ScreenCaptureService.IsOwnedArtifact(path)
                || !ScreenshotStorageLayout.IsSameOrDescendant(Path.GetDirectoryName(Path.GetFullPath(path))!, root)
                || !string.Equals(identity, LocalStore.ScreenshotIdentity(Path.GetFileName(path)), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Screenshot deletion escaped the configured capture identity or root.");
            RejectLinks(path);
        }
    }

    private static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Screenshot deletion does not follow filesystem links.");
    }

    private string GetPath(string path)
    {
        var identityPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, LocalStore.ScreenshotIdentity(Path.GetFileName(path)));
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityPath.ToUpperInvariant())));
        return Path.GetFullPath(Path.Combine(JournalDirectory, key + ".json"));
    }

    private void Save(string path, Plan plan)
    {
        RejectLinks(JournalDirectory);
        Directory.CreateDirectory(JournalDirectory);
        var temporary = path + ".tmp";
        RejectLinks(temporary);
        // Flush intent before removing any artifact. Atomic replacement keeps crash recovery deterministic.
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, plan);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }
}

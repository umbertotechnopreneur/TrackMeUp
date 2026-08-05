using System.Text.Json;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>Loads the immutable build provenance generated during compilation.</summary>
public sealed class BuildInformationService
{
    private const string FileName = "BuildInfo.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Loads and validates the distributed build manifest.</summary>
    public BuildInformation Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required build manifest is missing: {FileName}");
        }

        using var stream = File.OpenRead(path);
        var info = JsonSerializer.Deserialize<BuildInformation>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The build manifest is empty or invalid.");

        if (info.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(info.SemVer) ||
            string.IsNullOrWhiteSpace(info.GitCommit) ||
            string.IsNullOrWhiteSpace(info.MachineName))
        {
            throw new InvalidOperationException("The build manifest is incomplete or unsupported.");
        }

        return info;
    }
}

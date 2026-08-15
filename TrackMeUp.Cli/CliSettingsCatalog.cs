using TrackMeUp.Application;

namespace TrackMeUp.Cli;

/// <summary>Represents one safe setting value in plain, rich, and JSON output.</summary>
internal sealed record CliSettingValue(
    string Key,
    string ValueType,
    object? Value,
    string Description,
    bool RequiresRestart,
    IReadOnlyList<string> AllowedValues);

/// <summary>Projects the shared Core settings catalog without reflecting over internal settings fields.</summary>
internal static class CliSettingsCatalog
{
    /// <summary>Gets descriptors in stable display order.</summary>
    internal static IReadOnlyList<SettingDescriptor> Settings => SettingsCatalog.Definitions;

    /// <summary>Gets a compact help summary containing only public writable keys.</summary>
    internal static string HelpValueSummary => string.Join(", ", SettingsCatalog.Definitions.Select(setting =>
        setting.AllowedValues.Count == 0
            ? $"{setting.Key} <{setting.ValueType}>"
            : $"{setting.Key} <{string.Join('|', setting.AllowedValues)}>"));

    /// <summary>Gets the English help sentence retained for contract-level tests.</summary>
    internal static string HelpSummary => string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        CliStrings.Get("en-US", "detail.configWritable"),
        HelpValueSummary);

    /// <summary>Finds a descriptor by stable public key.</summary>
    internal static bool TryGet(string key, out SettingDescriptor? descriptor) =>
        (descriptor = SettingsCatalog.Definitions.FirstOrDefault(setting => setting.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) is not null;

    /// <summary>Projects a typed settings snapshot to its non-secret CLI surface.</summary>
    internal static IReadOnlyList<CliSettingValue> ReadAll(AppSettings settings) =>
        [.. SettingsCatalog.Definitions.Select(descriptor => Read(descriptor, settings))];

    /// <summary>Projects one public setting from a typed snapshot.</summary>
    internal static CliSettingValue Read(SettingDescriptor descriptor, AppSettings settings)
    {
        _ = SettingsCatalog.TryGetValue(settings, descriptor.Key, out var value);
        return new CliSettingValue(descriptor.Key, descriptor.ValueType, value, descriptor.Description, descriptor.RequiresRestart, descriptor.AllowedValues);
    }
}

// SPDX-License-Identifier: MIT

namespace TrackMeUp.Application;

/// <summary>Product access tiers, independent of the purchase channel.</summary>
public enum ProductTier { Free, Premium }

/// <summary>Stable feature identities used by the catalog, presentation and application guards.</summary>
public enum ProductFeature { Tracking, Screenshots, Search, DataTransfer, WorldClocks, Astronomy, HardwareSensors, ActivityLabels, Cli }

/// <summary>Declares one feature and the settings operations that require its entitlement.</summary>
public sealed record FeatureDefinition(ProductFeature Id, string TitleKey, ProductTier RequiredTier, IReadOnlyList<string> SettingKeys);

/// <summary>The runtime's current access state. This DTO is never persisted as a license.</summary>
public sealed record FeatureAccessSnapshot(ProductTier Tier, bool IsDebugSimulation, bool CanSimulate);

/// <summary>Supplies a verified license tier from a trusted runtime-owned licensing integration.</summary>
public interface IFeatureLicenseSource
{
    /// <summary>Gets the validated tier; implementations must report verification failures explicitly.</summary>
    ProductTier Tier { get; }
}

/// <summary>The explicit unlicensed state until a commercial license provider is configured.</summary>
internal sealed class UnlicensedFeatureSource : IFeatureLicenseSource
{
    /// <inheritdoc />
    public ProductTier Tier => ProductTier.Free;
}

/// <summary>Single source of feature classification; adding a premium setting requires no UI license conditionals.</summary>
public static class FeatureCatalog
{
    /// <summary>Gets all declared features and their access requirements.</summary>
    public static IReadOnlyList<FeatureDefinition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new FeatureDefinition(ProductFeature.Tracking, "Main.TrackingStatus", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.Screenshots, "Screenshots.Title", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.Search, "Search.Title", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.DataTransfer, "Operations.InstallationTransfer.Title", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.WorldClocks, "WorldClock.OpenWindow", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.Astronomy, "Celestial.Agenda.Title", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.HardwareSensors, "Sensors.Open", ProductTier.Free, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.Cli, "Cli.Title", ProductTier.Premium, Array.Empty<string>()),
        new FeatureDefinition(ProductFeature.ActivityLabels, "Labels.Title", ProductTier.Premium,
            Array.AsReadOnly(new[] { "activity.label.save", "activity.label.delete", "activity.label.select" }))
    });

    /// <summary>Resolves a feature and rejects undeclared identities.</summary>
    public static FeatureDefinition Get(ProductFeature feature) => Definitions.Single(item => item.Id == feature);

    /// <summary>Evaluates the same tier rule for application guards and passive presentation.</summary>
    public static bool IsAllowed(ProductFeature feature, FeatureAccessSnapshot access) => access.Tier >= Get(feature).RequiredTier;
}

/// <summary>Owns entitlement decisions. Debug overrides live only in memory in the shared runtime.</summary>
public sealed class FeatureAccessPolicy
{
    private readonly IFeatureLicenseSource _license;
#if DEBUG
    private int _debugTier = -1;
#endif

    /// <summary>Creates a policy using the runtime's trusted license source; no configured source means Free.</summary>
    public FeatureAccessPolicy(IFeatureLicenseSource? license = null) => _license = license ?? new UnlicensedFeatureSource();

    /// <summary>Gets an immutable snapshot without exposing a writable persisted entitlement.</summary>
    public FeatureAccessSnapshot Snapshot
    {
        get
        {
#if DEBUG
            var simulated = Volatile.Read(ref _debugTier);
            if (simulated >= 0) return new FeatureAccessSnapshot((ProductTier)simulated, true, true);
#endif
            var tier = _license.Tier;
            if (!Enum.IsDefined(tier)) throw new InvalidOperationException("The license source returned an unsupported tier.");
            return new FeatureAccessSnapshot(tier, false,
#if DEBUG
                true
#else
                false
#endif
            );
        }
    }

    /// <summary>Rejects protected settings before validation or persistence, regardless of frontend.</summary>
    public ValidationIssue? DeniedSetting(SettingsPatch patch, AppSettings settings)
    {
        var access = Snapshot;
        foreach (var (rawKey, rawValue) in patch.Values)
        {
            var key = rawKey?.Trim().ToLowerInvariant();
            var value = rawValue?.Trim();
            // Clearing the active label must remain possible after losing entitlement.
            if (key == "activity.label.select" && value == "") continue;
            var feature = FeatureCatalog.Definitions.FirstOrDefault(item => item.SettingKeys.Contains(key));
            // The existing taskbar text route must not bypass selection of a saved Premium label.
            if (key == "activity.span_label" && !string.IsNullOrEmpty(value)
                && (settings.ActivityLabels ?? []).Any(label => string.Equals(label.Name, value, StringComparison.OrdinalIgnoreCase)))
                feature = FeatureCatalog.Get(ProductFeature.ActivityLabels);
            if (feature is not null && !FeatureCatalog.IsAllowed(feature.Id, access))
                return new ValidationIssue(rawKey!, "premium_required", "Premium.Required");
        }
        return null;
    }

    /// <summary>Removes only a currently selected protected label when that feature is unavailable.</summary>
    public static AppSettings WithoutUnavailableSelection(AppSettings settings, FeatureAccessSnapshot access) =>
        !FeatureCatalog.IsAllowed(ProductFeature.ActivityLabels, access)
        && (settings.ActivityLabels ?? []).Any(label => label.Name == settings.SpanLabel)
            ? settings with { SpanLabel = "" } : settings;

#if DEBUG
    /// <summary>Sets a process-local simulation; this method does not exist in Release builds.</summary>
    public void Simulate(ProductTier tier)
    {
        if (!Enum.IsDefined(tier)) throw new ArgumentOutOfRangeException(nameof(tier));
        Volatile.Write(ref _debugTier, (int)tier);
    }
#endif
}

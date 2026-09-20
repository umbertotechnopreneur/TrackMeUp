// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

/// <summary>Checks tier enforcement independently of a frontend and persisted settings.</summary>
public sealed class FeatureAccessPolicyTests
{
    private static readonly ActivityLabelDefinition Work = new("123456781234123412341234567890ab", "Work", "work", "#FF6268");
    private static SettingsPatch Patch(string key, string value) => new(new Dictionary<string, string?> { [key] = value });

    /// <summary>Every registered protected label key is denied to the unlicensed profile.</summary>
    [Theory]
    [InlineData("activity.label.save")]
    [InlineData("activity.label.delete")]
    [InlineData("activity.label.select")]
    [InlineData(" ACTIVITY.LABEL.SELECT ")]
    public void Free_RejectsEveryProtectedSettingRegardlessOfFrontend(string key)
    {
        var policy = new FeatureAccessPolicy();
        var denied = policy.DeniedSetting(Patch(key, Work.Id), new AppSettings(ActivityLabels: [Work]));
        Assert.Equal("premium_required", denied?.Code);
    }

    /// <summary>Saved-label selection cannot bypass the guard through the original text setting.</summary>
    [Fact]
    public void Free_CannotSelectSavedLabelThroughTheTaskbarTextRoute()
    {
        var policy = new FeatureAccessPolicy();
        var settings = new AppSettings(ActivityLabels: [Work]);
        Assert.NotNull(policy.DeniedSetting(Patch("activity.span_label", " work "), settings));
        Assert.Null(policy.DeniedSetting(Patch("activity.span_label", "One-off"), settings));
        Assert.Null(policy.DeniedSetting(Patch("activity.label.select", ""), settings));
        Assert.Null(policy.DeniedSetting(Patch("theme", "dark"), settings));
    }

    /// <summary>Entitlement grants use without changing the feature's Premium classification.</summary>
    [Fact]
    public void Premium_AllowsProtectedWritesWhileTheCatalogStillMarksTheFeaturePremium()
    {
        var policy = new FeatureAccessPolicy(new License(ProductTier.Premium));
        Assert.Null(policy.DeniedSetting(Patch("activity.label.save", "{}"), new AppSettings()));
        Assert.Equal(ProductTier.Premium, FeatureCatalog.Get(ProductFeature.ActivityLabels).RequiredTier);
        Assert.False(policy.Snapshot.IsDebugSimulation);
    }

    /// <summary>Loss of access clears selection but does not remove user definitions.</summary>
    [Fact]
    public void Downgrade_ClearsOnlySelectedSavedLabelAndPreservesDefinitions()
    {
        var settings = new AppSettings(SpanLabel: Work.Name, ActivityLabels: [Work]);
        var cleared = FeatureAccessPolicy.WithoutUnavailableSelection(settings, new(ProductTier.Free, true, true));
        Assert.Empty(cleared.SpanLabel);
        Assert.Same(settings.ActivityLabels, cleared.ActivityLabels);
        Assert.Equal(settings, FeatureAccessPolicy.WithoutUnavailableSelection(settings, new(ProductTier.Premium, false, false)));
    }

    /// <summary>An invalid verified-source result fails closed.</summary>
    [Fact]
    public void UnknownLicenseTier_IsRejectedRatherThanGrantedAccess()
    {
        var policy = new FeatureAccessPolicy(new License((ProductTier)42));
        Assert.Throws<InvalidOperationException>(() => policy.Snapshot);
    }

#if DEBUG
    /// <summary>Simulation never survives creation of a fresh runtime policy.</summary>
    [Fact]
    public void DebugOverride_IsRuntimeLocalAndCanSwitchBackToFree()
    {
        var policy = new FeatureAccessPolicy();
        policy.Simulate(ProductTier.Premium);
        Assert.True(policy.Snapshot.IsDebugSimulation);
        Assert.Equal(ProductTier.Premium, policy.Snapshot.Tier);
        Assert.Equal(ProductTier.Free, new FeatureAccessPolicy().Snapshot.Tier);
        policy.Simulate(ProductTier.Free);
        Assert.NotNull(policy.DeniedSetting(Patch("activity.label.delete", Work.Id), new AppSettings(ActivityLabels: [Work])));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Simulate((ProductTier)42));
    }
#else
    /// <summary>Non-Debug assemblies contain no callable simulation method.</summary>
    [Fact]
    public void NonDebug_HasNoSimulationEntryPoint()
    {
        Assert.False(new FeatureAccessPolicy().Snapshot.CanSimulate);
        Assert.Null(typeof(ITrackMeUpApplication).GetMethod("SimulateFeatureAccessAsync"));
        Assert.Null(typeof(FeatureAccessPolicy).GetMethod("Simulate"));
    }
#endif

    private sealed class License(ProductTier tier) : IFeatureLicenseSource
    {
        /// <inheritdoc />
        public ProductTier Tier => tier;
    }
}

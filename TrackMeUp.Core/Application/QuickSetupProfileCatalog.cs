namespace TrackMeUp.Application;

/// <summary>Maps the supported Quick Setup profiles to deterministic, whitelist-bound settings patches.</summary>
public static class QuickSetupProfileCatalog
{
    /// <summary>Creates the complete settings patch for one supported profile.</summary>
    public static OperationResult<SettingsPatch> CreatePatch(QuickSetupProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profileId = request.ProfileId?.Trim().ToLowerInvariant();
        var profile = profileId switch
        {
            QuickSetupProfileIds.Complete => (AiEnabled: true, ScreenshotsEnabled: true),
            QuickSetupProfileIds.Assisted => (AiEnabled: true, ScreenshotsEnabled: false),
            QuickSetupProfileIds.LocalRecord => (AiEnabled: false, ScreenshotsEnabled: true),
            QuickSetupProfileIds.EssentialOffline => (AiEnabled: false, ScreenshotsEnabled: false),
            _ => ((bool AiEnabled, bool ScreenshotsEnabled)?)null
        };
        if (profile is null)
        {
            return OperationResult<SettingsPatch>.Failure(
                "quick_setup.profile.invalid",
                "QuickSetupProfileInvalid",
                new ValidationIssue("profileId", "unsupported", "QuickSetupProfileInvalid"));
        }

        var patch = new SettingsPatch(new Dictionary<string, string?>
        {
            ["ai.enabled"] = profile.Value.AiEnabled.ToString(),
            ["screenshots.enabled"] = profile.Value.ScreenshotsEnabled.ToString(),
            ["screenshots.keep"] = profile.Value.ScreenshotsEnabled.ToString(),
            ["startup.enabled"] = request.StartWithWindows.ToString(),
            ["quick_setup.completed"] = bool.TrueString
        });
        return OperationResult<SettingsPatch>.Success(
            "quick_setup.profile.validated",
            "QuickSetupProfileValidated",
            patch);
    }
}

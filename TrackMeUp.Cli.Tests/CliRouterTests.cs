using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Cli;
using TrackMeUp.Search;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Cli.Tests;

public sealed class CliRouterTests
{
    [Fact]
    public async Task SlashStatus_RoutesToSharedApplicationFacade()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/status"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, application.DashboardReads);
    }

    [Fact]
    public async Task RuntimeHealth_RoutesToSharedApplicationFacade()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/runtime", "health"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, application.RuntimeHealthReads);
    }

    [Fact]
    public async Task Diagnostics_RunsReadOnlyFacadeChecks()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/diagnostics"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, application.RuntimeHealthReads);
        Assert.Equal(1, application.DashboardReads);
        Assert.Equal(1, application.AiStatusReads);
        Assert.Equal(1, application.RetentionStatusReads);
        Assert.Equal(1, application.StartupStatusReads);
        Assert.Equal(1, application.PluginReads);
    }

    [Fact]
    public async Task ConfigGet_UsesPublicKeyAndReadsThroughFacade()
    {
        var application = new RecordingApplication { Settings = new AppSettings(Theme: "dark") };
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/config", "get", "theme"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, application.SettingsReads);
    }

    [Fact]
    public async Task ConfigGet_RejectsInternalPropertyNameBeforeFacadeCall()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/config", "get", nameof(AppSettings.InstallationId)], CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, application.SettingsReads);
    }

    [Fact]
    public async Task ConfigSet_ForwardsCanonicalPublicKeyToFacade()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/settings", "set", "theme", "dark"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(application.LastPatch);
        Assert.Equal("dark", application.LastPatch!.Values["theme"]);
    }

    [Fact]
    public async Task ConfigSet_ForwardsAiReasoningSettingFromCoreCatalog()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/config", "set", "ai.reasoning_effort", "high"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(application.LastPatch);
        Assert.Equal("high", application.LastPatch!.Values["ai.reasoning_effort"]);
    }

    [Fact]
    public async Task AiConfigure_ForwardsOutputAndReasoningProfiles()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/ai", "configure", "--output-detail", "compact", "--reasoning-effort", "low"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(application.LastPatch);
        Assert.Equal("compact", application.LastPatch!.Values["ai.output_detail"]);
        Assert.Equal("low", application.LastPatch.Values["ai.reasoning_effort"]);
    }

    [Fact]
    public async Task OpenUi_RoutesThroughSharedApplicationFacade()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/open", "ui"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, application.UiOpenCalls);
    }

    [Fact]
    public async Task ScreenshotCaptureWithoutMode_ForwardsPersistedDefaultSentinel()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/screenshot", "capture", "--keep"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(application.LastCaptureRequest);
        Assert.Null(application.LastCaptureRequest!.Mode);
    }

    [Fact]
    public async Task ScreenshotCaptureWithInvalidMode_PropagatesApplicationValidationFailure()
    {
        var application = new RecordingApplication
        {
            ScreenshotCaptureResponse = OperationResult<ScreenshotCaptureResult>.Failure(
                "screenshot.mode.invalid",
                "ScreenshotModeUnsupported",
                new ValidationIssue("mode", "unsupported", "ScreenshotModeUnsupported"))
        };
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/screenshot", "capture", "--mode", "unsupported-mode"], CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.NotNull(application.LastCaptureRequest);
        Assert.Equal("unsupported-mode", application.LastCaptureRequest!.Mode);
    }

    [Fact]
    public async Task ScreenshotCaptureWithMissingModeValue_FailsBeforeCallingApplication()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/screenshot", "capture", "--mode"], CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Null(application.LastCaptureRequest);
        Assert.Equal(0, application.TotalCalls);
    }

    [Fact]
    public async Task SlashCommandHelp_DoesNotCallApplicationFacade()
    {
        var application = new RecordingApplication();
        var router = CreateRouter(application);

        var exitCode = await router.RunAsync(["/help", "/config"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, application.TotalCalls);
    }

    private static CliRouter CreateRouter(RecordingApplication application)
    {
        var options = new CliOptions(CliFormat.Plain, "en", true, true, true, true, false, 5, false, []);
        return new CliRouter(application, new CliOutput(options), options);
    }

    private sealed class RecordingApplication : ITrackMeUpApplication
    {
        public event EventHandler<RuntimeStateChangedEventArgs>? RuntimeStateChanged
        {
            add { }
            remove { }
        }

        internal AppSettings Settings { get; set; } = new();
        internal SettingsPatch? LastPatch { get; private set; }
        internal int DashboardReads { get; private set; }
        internal int RuntimeHealthReads { get; private set; }
        internal int AiStatusReads { get; private set; }
        internal int RetentionStatusReads { get; private set; }
        internal int StartupStatusReads { get; private set; }
        internal int PluginReads { get; private set; }
        internal int SettingsReads { get; private set; }
        internal int UiOpenCalls { get; private set; }
        internal int TotalCalls { get; private set; }
        internal CaptureScreenshotRequest? LastCaptureRequest { get; private set; }
        internal OperationResult<ScreenshotCaptureResult>? ScreenshotCaptureResponse { get; init; }

        public Task<OperationResult<DashboardState>> GetDashboardAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            DashboardReads++;
            var utcNow = new DateTimeOffset(2026, 8, 5, 15, 0, 0, TimeSpan.Zero);
            return Success(new DashboardState("PAUSED", "Ready", 0, 0, 0, 0, false, null, utcNow.ToLocalTime(), utcNow), "dashboard.loaded");
        }

        public Task<OperationResult<AppSettings>> GetSettingsAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            SettingsReads++;
            return Success(Settings, "settings.loaded");
        }

        public Task<OperationResult<AppSettings>> ApplyQuickSetupProfileAsync(QuickSetupProfileRequest request, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Success(Settings, "quick_setup.applied");
        }

        public Task<OperationResult<AppSettings>> PatchSettingsAsync(SettingsPatch patch, CancellationToken cancellationToken)
        {
            TotalCalls++;
            LastPatch = patch;
            if (patch.Values.TryGetValue("theme", out var theme) && theme is not null)
            {
                Settings = Settings with { Theme = theme };
            }
            return Success(Settings, "settings.saved");
        }

        public Task<OperationResult<WindowState?>> RestoreWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken) => Unsupported<WindowState?>();
        public Task<OperationResult<WindowState>> SaveWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken) => Unsupported<WindowState>();

        public Task<OperationResult<RuntimeHealth>> GetRuntimeHealthAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            RuntimeHealthReads++;
            return Success(new RuntimeHealth("1.0.0", 1, "test-installation", true, ["settings.get"]), "runtime.healthy");
        }
        public Task<OperationResult<DashboardState>> StartTrackingAsync(StartTrackingRequest request, CancellationToken cancellationToken) => Unsupported<DashboardState>();
        public Task<OperationResult<DashboardState>> PauseTrackingAsync(CancellationToken cancellationToken) => Unsupported<DashboardState>();
        public Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken) => Unsupported<DashboardState>();
        public Task<OperationResult<LastSessionState?>> GetLastSessionAsync(CancellationToken cancellationToken) => Unsupported<LastSessionState?>();
        public Task<OperationResult<DailySummary>> GetTodaySummaryAsync(CancellationToken cancellationToken) => Unsupported<DailySummary>();
        public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken) => Unsupported<SearchResponse>();
        public Task<OperationResult<IReadOnlyList<SearchSuggestion>>> GetSearchSuggestionsAsync(SearchSuggestionRequest request, CancellationToken cancellationToken) => Unsupported<IReadOnlyList<SearchSuggestion>>();
        public Task<OperationResult<int>> RebuildSearchIndexAsync(CancellationToken cancellationToken) => Unsupported<int>();
        public Task<OperationResult<ReportSnapshot>> GetReportAsync(ReportQuery query, CancellationToken cancellationToken) => Unsupported<ReportSnapshot>();
        public Task<OperationResult<SystemSnapshot>> CaptureSystemSnapshotAsync(CancellationToken cancellationToken) => Unsupported<SystemSnapshot>();
        public Task<OperationResult<ScreenshotCaptureResult>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken)
        {
            TotalCalls++;
            LastCaptureRequest = request;
            return Task.FromResult(ScreenshotCaptureResponse ?? OperationResult<ScreenshotCaptureResult>.Success(
                "screenshot.captured",
                "ScreenshotCaptured",
                new ScreenshotCaptureResult("test-capture", ["test.webp"], ["test.webp"], ScreenshotCaptureOrigins.Manual)));
        }
        /// <inheritdoc />
        public Task<OperationResult<PendingManualScreenshotState>> CaptureManualScreenshotAsync(CancellationToken cancellationToken) => Unsupported<PendingManualScreenshotState>();
        /// <inheritdoc />
        public Task<OperationResult<bool>> DeletePendingManualScreenshotAsync(CancellationToken cancellationToken) => Unsupported<bool>();
        public Task<OperationResult<AiAnalysis>> AnalyzeCapturedScreenshotAsync(AnalyzeCapturedScreenshotRequest request, CancellationToken cancellationToken) => Unsupported<AiAnalysis>();
        public Task<OperationResult<string>> DeleteScreenshotAsync(string screenshotPath, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string>> DeleteSnapshotAsync(string screenshotPath, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string?>> GetLatestScreenshotAsync(CancellationToken cancellationToken) => Unsupported<string?>();
        public Task<OperationResult<ScreenshotGallery>> GetScreenshotGalleryAsync(DateOnly date, CancellationToken cancellationToken) => Unsupported<ScreenshotGallery>();
        public Task<OperationResult<ScreenshotGallery>> GetLatestScreenshotGalleryAsync(CancellationToken cancellationToken) => Unsupported<ScreenshotGallery>();
        public Task<OperationResult<string>> SaveScreenshotAsync(string screenshotPath, string destinationPath, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string>> ShareScreenshotAsync(string screenshotPath, long windowHandle, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<bool>> OpenApplicationLogAsync(CancellationToken cancellationToken) => Unsupported<bool>();
        public Task<OperationResult<bool>> ShareApplicationLogAsync(long windowHandle, CancellationToken cancellationToken) => Unsupported<bool>();
        public Task<OperationResult<string>> OpenScreenshotFolderAsync(CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string>> OpenScreenshotFolderAsync(string directory, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<IReadOnlyList<ApplicationNotification>>> DrainApplicationNotificationsAsync(CancellationToken cancellationToken) => Unsupported<IReadOnlyList<ApplicationNotification>>();
        public Task<OperationResult<AiStatus>> GetAiStatusAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            AiStatusReads++;
            return Success(new AiStatus(false, "openai", "gpt-5.6", "https://api.openai.com/v1/responses", "OPENAI_API_KEY", false, false, new AnalysisCostGate(true, null, 0m, 0, 0m)), "ai.status.loaded");
        }
        public Task<OperationResult<AiPricingOverview>> GetAiPricingOverviewAsync(CancellationToken cancellationToken) => Unsupported<AiPricingOverview>();
        public Task<OperationResult<AiConnectionTestResult>> TestAiConnectionAsync(CancellationToken cancellationToken) => Unsupported<AiConnectionTestResult>();
        public Task<OperationResult<AiModelCatalogSnapshot>> GetAiModelCatalogAsync(CancellationToken cancellationToken) => Unsupported<AiModelCatalogSnapshot>();
        public Task<OperationResult<AiStatus>> SetAiEnabledAsync(bool enabled, CancellationToken cancellationToken) => Unsupported<AiStatus>();
        public Task<OperationResult<AppSettings>> ConfigureAiAsync(SettingsPatch patch, CancellationToken cancellationToken)
        {
            TotalCalls++;
            LastPatch = patch;
            return Success(Settings, "ai.configured");
        }
        public Task<OperationResult<string>> SetAiKeyAsync(string keyVariable, string secret, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<AiAnalysis>> AnalyzeCurrentActivityAsync(AnalyzeCurrentActivityRequest request, CancellationToken cancellationToken) => Unsupported<AiAnalysis>();
        public Task<OperationResult<string>> GenerateTodayReportAsync(string? outputDirectory, bool open, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string>> GenerateDailyDigestAsync(DateOnly date, bool open, CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string>> OpenReportsFolderAsync(CancellationToken cancellationToken) => Unsupported<string>();
        public Task<OperationResult<string>> OpenUserInterfaceAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            UiOpenCalls++;
            return Success("TrackMeUp UI", "ui.opened");
        }
        public Task<OperationResult<IReadOnlyList<PrivacyRule>>> GetPrivacyRulesAsync(CancellationToken cancellationToken) => Unsupported<IReadOnlyList<PrivacyRule>>();
        public Task<OperationResult<PrivacyRule>> AddPrivacyRuleAsync(string type, string value, CancellationToken cancellationToken) => Unsupported<PrivacyRule>();
        public Task<OperationResult<bool>> RemovePrivacyRuleAsync(string id, CancellationToken cancellationToken) => Unsupported<bool>();
        public Task<OperationResult<bool>> TestCurrentPrivacyAsync(CancellationToken cancellationToken) => Unsupported<bool>();
        public Task<OperationResult<RetentionStatus>> GetRetentionStatusAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            RetentionStatusReads++;
            return Success(new RetentionStatus(30, 30, "private-test-path"), "retention.status.loaded");
        }
        public Task<OperationResult<RetentionPreview>> PreviewRetentionAsync(CancellationToken cancellationToken) => Unsupported<RetentionPreview>();
        public Task<OperationResult<RetentionPreview>> RunRetentionAsync(RetentionRequest request, CancellationToken cancellationToken) => Unsupported<RetentionPreview>();

        public Task<OperationResult<AtomicResetPlan>> PrepareAtomicResetAsync(AtomicResetRequest request, CancellationToken cancellationToken) => Unsupported<AtomicResetPlan>();
        public Task<OperationResult<IReadOnlyList<PluginInfo>>> GetPluginsAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            PluginReads++;
            return Success<IReadOnlyList<PluginInfo>>([new PluginInfo("word", "Word", true, "test")], "plugins.loaded");
        }
        public Task<OperationResult<PluginInfo>> GetPluginAsync(string id, CancellationToken cancellationToken) => Unsupported<PluginInfo>();
        public Task<OperationResult<PluginInfo>> SetPluginEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => Unsupported<PluginInfo>();
        public Task<OperationResult<bool>> GetStartupStatusAsync(CancellationToken cancellationToken)
        {
            TotalCalls++;
            StartupStatusReads++;
            return Success(false, "startup.status.loaded");
        }
        public Task<OperationResult<bool>> SetStartupEnabledAsync(bool enabled, CancellationToken cancellationToken) => Unsupported<bool>();
        public Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken) => Unsupported<ProductInformation>();
        public Task<OperationResult<bool>> OpenProductLinkAsync(string linkKey, CancellationToken cancellationToken) => Unsupported<bool>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static Task<OperationResult<T>> Success<T>(T value, string code) => Task.FromResult(OperationResult<T>.Success(code, code, value));
        private Task<OperationResult<T>> Unsupported<T>()
        {
            TotalCalls++;
            return Task.FromResult(OperationResult<T>.Failure("test.unsupported", "TestUnsupported"));
        }
    }
}

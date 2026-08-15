using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Hosts the native report controls and renders aggregate DTOs in the packaged Vue surface.</summary>
public sealed partial class ReportsWindow : Window
{
    private const string ReportsHost = "reports.trackmeup.local";
    private const string ReportsOrigin = "https://reports.trackmeup.local";
    private static readonly TimeSpan SnapshotCacheTtl = TimeSpan.FromSeconds(15);
    private const int LogicalWindowWidth = 1180;
    private const int LogicalWindowHeight = 800;
    private const int LogicalScreenMargin = 24;
    private static readonly Uri ReportsUri = new($"{ReportsOrigin}/index.html");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ITrackMeUpApplication _application;
    private readonly ReportViewModel _viewModel;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly string? _launchTheme;
    private CancellationTokenSource? _refreshCancellation;
    private TaskCompletionSource<bool>? _frontendReadyCompletion;
    private bool _webReady;
    private bool _initializing;
    private XamlRoot? _xamlRoot;
    private LocalizationService _strings = new("system");
    private string _reportTheme = "system";
    private string _reportLanguage = "en-US";
    private ReportRangeKey? _cachedRange;
    private ReportSnapshot? _cachedSnapshot;
    private DateTimeOffset _cachedAtUtc;

    /// <summary>Creates a reports window backed by the shared application facade.</summary>
    public ReportsWindow(ITrackMeUpApplication application, string? launchTheme = null)
    {
        _application = application;
        _viewModel = new ReportViewModel(application);
        _launchTheme = launchTheme;
        InitializeComponent();
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(_application, this, _appWindow, WindowStateKeys.Reports, LogicalWindowWidth, LogicalWindowHeight, LogicalScreenMargin);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        ApplyReportTheme(_reportTheme);
        ApplyLocalization();

        var today = DateTimeOffset.Now.Date;
        CustomFromPicker.Date = new DateTimeOffset(today);
        CustomToPicker.Date = new DateTimeOffset(today);
        RangeComboBox.SelectedIndex = 0;
        ViewComboBox.SelectedIndex = 0;
        _placement.ApplyDefaultBounds(RootGrid);
        Closed += ReportsWindow_Closed;
    }

    /// <summary>Reselects the reports surface to the current day so repeated menu opens stay anchored to today.</summary>
    public void SelectToday()
    {
        RangeComboBox.SelectedIndex = 0;
        ViewComboBox.SelectedIndex = 0;
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        _placement.ApplyDefaultBounds(RootGrid);
        await _placement.RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token);

        if (_initializing || _webReady)
        {
            return;
        }

        _initializing = true;
        ShowLoading(T("Reports.Loading.Settings"));
        if (!await InitializeThemeAsync())
        {
            return;
        }

        await InitializeWebViewAsync();
    }

    private async Task<bool> InitializeThemeAsync()
    {
        try
        {
            var result = _launchTheme is null
                ? await _application.GetSettingsAsync(_lifetimeCancellation.Token)
                : await _application.PatchSettingsAsync(
                    new SettingsPatch(new Dictionary<string, string?> { ["theme"] = _launchTheme }),
                    _lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                ShowError(_strings.Format("Reports.Error.SettingsUnavailable", result.Code));
                return false;
            }

            _strings = new LocalizationService(result.Value.UiLanguage);
            ApplyLocalization();
            if (!IsReportTheme(result.Value.Theme))
            {
                ShowError(T("Reports.Error.ThemeInvalid"));
                return false;
            }

            ApplyReportTheme(result.Value.Theme);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            ShowError(T("Reports.Error.SettingsLoad"));
            return false;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        ShowLoading(T("Reports.Loading.Engine"));
        try
        {
            await ReportsWebView.EnsureCoreWebView2Async();
            var core = ReportsWebView.CoreWebView2 ?? throw new InvalidOperationException("WebView2 initialization returned no runtime instance.");
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
#if !DEBUG
            core.Settings.AreDevToolsEnabled = false;
#endif
            core.NavigationStarting += Core_NavigationStarting;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.ProcessFailed += Core_ProcessFailed;
            core.WebMessageReceived += Core_WebMessageReceived;
            core.SetVirtualHostNameToFolderMapping(
                ReportsHost,
                Path.Combine(AppContext.BaseDirectory, "ReportsWeb"),
                CoreWebView2HostResourceAccessKind.DenyCors);
            _frontendReadyCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ReportsWebView.NavigationCompleted += ReportsWebView_NavigationCompleted;
            ReportsWebView.Source = ReportsUri;
        }
        catch (Exception)
        {
            ShowError(T("Reports.Error.WebViewUnavailable"));
        }
    }

    private async void ReportsWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            ShowError(T("Reports.Error.AssetsUnavailable"));
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await (_frontendReadyCompletion?.Task ?? Task.FromException<bool>(new InvalidOperationException())).WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            // A loaded document without the Vue readiness handshake is invalid; no legacy renderer is substituted.
            ShowError(T("Reports.Error.FrontendTimeout"));
            return;
        }
        catch (Exception)
        {
            ShowError(T("Reports.Error.FrontendInvalid"));
            return;
        }

        if (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        _webReady = true;
        await RefreshReportAsync();
    }

    private async void Core_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var source) ||
            !string.Equals(source.GetLeftPart(UriPartial.Authority), ReportsOrigin, StringComparison.OrdinalIgnoreCase))
        {
            ShowError(T("Reports.Error.WebMessageOrigin"));
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String)
            {
                ShowError(T("Reports.Error.WebMessageType"));
                return;
            }

            if (type.GetString() == "report.ready")
            {
                PostLanguageState();
                PostThemeState();
                _frontendReadyCompletion?.TrySetResult(true);
                return;
            }

            if (type.GetString() == "report.theme.set" &&
                TryReadReportTheme(document.RootElement, out var selectedTheme))
            {
                await PersistReportThemeAsync(selectedTheme);
                return;
            }
        }
        catch (JsonException)
        {
            // Invalid frontend messages fail closed and never reach the application facade.
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            ShowError(T("Reports.Error.WebCommunication"));
            return;
        }

        ShowError(T("Reports.Error.WebMessageInvalid"));
    }

    private async Task PersistReportThemeAsync(string theme)
    {
        var result = await _application.PatchSettingsAsync(
            new SettingsPatch(new Dictionary<string, string?> { ["theme"] = theme }),
            _lifetimeCancellation.Token);
        if (!result.Succeeded || result.Value is null || !IsReportTheme(result.Value.Theme))
        {
            PostThemeError(result.Code);
            return;
        }

        ApplyReportTheme(result.Value.Theme);
        PostThemeState();
    }

    private static bool TryReadReportTheme(JsonElement message, out string theme)
    {
        if (!message.TryGetProperty("theme", out var value) || value.ValueKind != JsonValueKind.String)
        {
            theme = string.Empty;
            return false;
        }

        theme = value.GetString() ?? string.Empty;
        return IsReportTheme(theme);
    }

    private static bool IsReportTheme(string theme) => theme is "system" or "light" or "dark";

    private void PostThemeState() => PostWebEnvelope(new
    {
        type = "report.theme.state",
        theme = _reportTheme
    });

    private void PostLanguageState() => PostWebEnvelope(new
    {
        type = "report.language.state",
        language = _reportLanguage
    });

    private void PostThemeError(string code) => PostWebEnvelope(new
    {
        type = "report.theme.error",
        theme = _reportTheme,
        code
    });

    private void PostWebEnvelope(object envelope)
    {
        var core = ReportsWebView.CoreWebView2 ?? throw new InvalidOperationException("WebView2 is not ready for a host message.");
        core.PostWebMessageAsJson(JsonSerializer.Serialize(envelope, SerializerOptions));
    }

    private void ApplyReportTheme(string theme)
    {
        _reportTheme = theme;
        RootGrid.RequestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        var effectiveTheme = RootGrid.RequestedTheme == ElementTheme.Default
            ? RootGrid.ActualTheme
            : RootGrid.RequestedTheme;
        ApplyThemeChrome(effectiveTheme);
    }

    private void ApplyThemeChrome(ElementTheme effectiveTheme)
    {
        ReportsWebView.DefaultBackgroundColor = effectiveTheme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 20, 24, 29)
            : Windows.UI.Color.FromArgb(255, 245, 247, 250);

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var dark = effectiveTheme == ElementTheme.Dark;
        var titleBar = _appWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = dark
            ? Windows.UI.Color.FromArgb(160, 255, 255, 255)
            : Windows.UI.Color.FromArgb(160, 0, 0, 0);
        titleBar.ButtonHoverBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(32, 255, 255, 255)
            : Windows.UI.Color.FromArgb(24, 0, 0, 0);
        titleBar.ButtonPressedBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(48, 255, 255, 255)
            : Windows.UI.Color.FromArgb(40, 0, 0, 0);
    }

    private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_reportTheme == "system")
        {
            ApplyThemeChrome(sender.ActualTheme);
        }
    }

    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var target) &&
            string.Equals(target.GetLeftPart(UriPartial.Authority), ReportsOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        args.Cancel = true;
        ShowError(T("Reports.Error.ExternalNavigation"));
    }

    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        ShowError(T("Reports.Error.ExternalWindow"));
    }

    private void Core_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => ShowError(T("Reports.Error.WebViewProcess")));

    private async void ReportSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        var custom = SelectedTag(RangeComboBox) == "custom";
        CustomFromPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        CustomToPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        if (_webReady)
        {
            await RefreshReportAsync();
        }
    }

    private async void CustomDatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
    {
        if (_webReady && SelectedTag(RangeComboBox) == "custom")
        {
            await RefreshReportAsync();
        }
    }

    private async void RefreshReportButton_Click(object sender, RoutedEventArgs e)
    {
        InvalidateReportCache();
        await RefreshReportAsync();
    }

    private async Task RefreshReportAsync()
    {
        if (!_webReady || ReportsWebView.CoreWebView2 is null)
        {
            ShowError(T("Reports.Error.WebViewNotReady"));
            return;
        }

        if (!TryCreateQuery(out var query, out var validationError))
        {
            ShowError(validationError);
            return;
        }

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
        var rangeKey = new ReportRangeKey(query.From, query.ToInclusive, query.TimeZoneId);
        var cacheAge = DateTimeOffset.UtcNow - _cachedAtUtc;
        if (_cachedRange == rangeKey &&
            _cachedSnapshot is not null &&
            cacheAge >= TimeSpan.Zero &&
            cacheAge <= SnapshotCacheTtl)
        {
            PostSnapshot(query.View, _cachedSnapshot);
            ShowReport();
            return;
        }

        _refreshCancellation = new CancellationTokenSource();
        var cancellationToken = _refreshCancellation.Token;
        ShowLoading(T("Reports.Loading.Calculating"));
        try
        {
            var result = await _viewModel.LoadAsync(query, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.Succeeded || result.Value is null)
            {
                ShowError(_strings.Format("Reports.Error.ReportUnavailable", result.Code));
                return;
            }

            _cachedRange = rangeKey;
            _cachedSnapshot = result.Value;
            _cachedAtUtc = DateTimeOffset.UtcNow;
            PostSnapshot(query.View, result.Value);
            ShowReport();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer selector value owns the next query; the superseded result is intentionally ignored.
        }
        catch (Exception)
        {
            ShowError(T("Reports.Error.ReportDispatch"));
        }
    }

    private void PostSnapshot(ReportView view, ReportSnapshot snapshot)
    {
        var envelope = new
        {
            type = "report.snapshot",
            view = WebViewName(view),
            snapshot
        };
        PostWebEnvelope(envelope);
    }

    private void InvalidateReportCache()
    {
        _cachedRange = null;
        _cachedSnapshot = null;
        _cachedAtUtc = default;
    }

    private bool TryCreateQuery(out ReportQuery query, out string validationError)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var preset = SelectedTag(RangeComboBox);
        var to = today;
        var from = preset switch
        {
            "today" => today,
            "30" => today.AddDays(-29),
            "90" => today.AddDays(-89),
            "thisMonth" => new DateOnly(today.Year, today.Month, 1),
            "lastMonth" => new DateOnly(today.Year, today.Month, 1).AddMonths(-1),
            "custom" => DateOnly.FromDateTime(CustomFromPicker.Date.DateTime),
            _ => today.AddDays(-6)
        };

        if (preset == "lastMonth")
        {
            to = new DateOnly(today.Year, today.Month, 1).AddDays(-1);
        }
        else if (preset == "custom")
        {
            to = DateOnly.FromDateTime(CustomToPicker.Date.DateTime);
        }

        if (from > to)
        {
            query = default!;
            validationError = T("Reports.Error.DateRange");
            return false;
        }

        var view = SelectedTag(ViewComboBox) switch
        {
            "hourOfWeek" => ReportView.HourOfWeek,
            "trend" => ReportView.Trend,
            "applications" => ReportView.Applications,
            _ => ReportView.Calendar
        };
        query = new ReportQuery(from, to, string.Empty, view);
        validationError = string.Empty;
        return true;
    }

    private static string SelectedTag(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

    private static string WebViewName(ReportView view) => view switch
    {
        ReportView.HourOfWeek => "hourOfWeek",
        ReportView.Trend => "trend",
        ReportView.Applications => "applications",
        _ => "calendar"
    };

    private void ApplyLocalization()
    {
        UiLocalization.Apply(RootGrid, _strings);
        _reportLanguage = _strings.Language;
        Title = T("Reports.WindowTitle");
        TitleBarCaptionText.Text = Title;
        var refreshLabel = T("Reports.Refresh");
        AutomationProperties.SetName(RefreshReportButton, refreshLabel);
        ToolTipService.SetToolTip(RefreshReportButton, refreshLabel);
        AutomationProperties.SetName(ReportsWebView, T("Reports.WebView.Accessible"));
        AutomationProperties.SetName(ReportProgressRing, T("Reports.Loading.Accessible"));
    }

    private string T(string key) => _strings.Translate(key);

    private void ShowLoading(string message)
    {
        RefreshReportButton.IsEnabled = false;
        ReportStatusText.Text = message;
        ReportProgressRing.IsActive = true;
        ReportProgressRing.Visibility = Visibility.Visible;
        ReportErrorIcon.Visibility = Visibility.Collapsed;
        ReportStatusPanel.Visibility = Visibility.Visible;
    }

    private void ShowError(string message)
    {
        RefreshReportButton.IsEnabled = _webReady;
        ReportStatusText.Text = message;
        ReportProgressRing.IsActive = false;
        ReportProgressRing.Visibility = Visibility.Collapsed;
        ReportErrorIcon.Visibility = Visibility.Visible;
        ReportStatusPanel.Visibility = Visibility.Visible;
        ReportsWebView.Visibility = Visibility.Collapsed;
    }

    private void ShowReport()
    {
        RefreshReportButton.IsEnabled = true;
        ReportProgressRing.IsActive = false;
        ReportStatusPanel.Visibility = Visibility.Collapsed;
        ReportsWebView.Visibility = Visibility.Visible;
    }

    private async void ReportsWindow_Closed(object sender, WindowEventArgs args)
    {
        await _placement.SaveAsync(CancellationToken.None);
        _placement.Dispose();

        _lifetimeCancellation.Cancel();
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged;
        ReportsWebView.NavigationCompleted -= ReportsWebView_NavigationCompleted;

        if (ReportsWebView.CoreWebView2 is { } core)
        {
            core.NavigationStarting -= Core_NavigationStarting;
            core.NewWindowRequested -= Core_NewWindowRequested;
            core.ProcessFailed -= Core_ProcessFailed;
            core.WebMessageReceived -= Core_WebMessageReceived;
        }

        _lifetimeCancellation.Dispose();
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        }
    }

    private readonly record struct ReportRangeKey(DateOnly From, DateOnly ToInclusive, string TimeZoneId);
}

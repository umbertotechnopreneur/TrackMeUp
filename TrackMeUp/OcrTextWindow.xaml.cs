using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Displays selectable OCR text with debounced, local highlighting in a reusable Mica window.</summary>
internal sealed partial class OcrTextWindow : Window
{
    private const int LogicalWindowWidth = 900;
    private const int LogicalWindowHeight = 680;
    private const int LogicalScreenMargin = 24;
    private const int MinimumQueryLength = 2;
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(400);
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly DispatcherQueueTimer _searchTimer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private LocalizationService _strings;
    private string _ocrText = string.Empty;
    private ElementTheme _theme;
    private XamlRoot? _xamlRoot;

    /// <summary>Creates an OCR text window anchored to the screenshot gallery display.</summary>
    internal OcrTextWindow(
        ITrackMeUpApplication application,
        AppWindow ownerAppWindow,
        string ocrText,
        ElementTheme theme,
        string language)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(ownerAppWindow);
        ArgumentNullException.ThrowIfNull(ocrText);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _strings = new LocalizationService(language);
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.OcrText,
            LogicalWindowWidth,
            LogicalWindowHeight,
            LogicalScreenMargin,
            ownerAppWindow.Id);
        ConfigureWindowChrome();
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;

        _searchTimer = DispatcherQueue.CreateTimer();
        _searchTimer.Interval = SearchDebounce;
        _searchTimer.IsRepeating = false;
        _searchTimer.Tick += SearchTimer_Tick;

        UpdateContent(ocrText, theme, language);
        Closed += OcrTextWindow_Closed;
    }

    /// <summary>Replaces the OCR text and resets the themed, localized search surface for a new snapshot.</summary>
    public void UpdateContent(string ocrText, ElementTheme theme, string language)
    {
        ArgumentNullException.ThrowIfNull(ocrText);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        _searchTimer.Stop();
        _ocrText = ocrText;
        _strings = new LocalizationService(language);
        _theme = theme;
        RootGrid.RequestedTheme = theme;
        ApplyThemeChrome(theme == ElementTheme.Default ? RootGrid.ActualTheme : theme);
        UiLocalization.Apply(RootGrid, _strings);

        var windowTitle = T("Screenshots.OcrText.WindowTitle");
        Title = windowTitle;
        TitleBarText.Text = windowTitle.ToUpper(_strings.Culture);
        SearchBox.PlaceholderText = T("Screenshots.OcrText.Search.Placeholder");
        AutomationProperties.SetName(SearchBox, T("Screenshots.OcrText.Search.AccessibleName"));
        ToolTipService.SetToolTip(SearchBox, T("Screenshots.OcrText.Search.Tooltip"));

        SearchBox.Text = string.Empty;
        OcrTextBlock.Text = _ocrText;
        ClearHighlights();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        _placement.ApplyDefaultBounds(RootGrid);
        try
        {
            await _placement.RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (InvalidOperationException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        UpdateTitleBarInsets();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchTimer.Stop();
        ClearHighlights();
        if (sender.Text.Trim().Length < MinimumQueryLength)
        {
            return;
        }

        _searchTimer.Start();
    }

    private void SearchTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        ApplyHighlights(SearchBox.Text.Trim());
    }

    private void ApplyHighlights(string query)
    {
        ClearHighlights();
        if (query.Length < MinimumQueryLength)
        {
            return;
        }

        var matches = OcrTextSearch.FindMatches(_ocrText, query);
        if (matches.Count == 0)
        {
            return;
        }

        var highlighter = new TextHighlighter
        {
            Background = new SolidColorBrush(Colors.Yellow),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        foreach (var match in matches)
        {
            highlighter.Ranges.Add(new TextRange
            {
                StartIndex = match.StartIndex,
                Length = match.Length
            });
        }

        OcrTextBlock.TextHighlighters.Add(highlighter);
    }

    private void ClearHighlights() => OcrTextBlock.TextHighlighters.Clear();

    private async void OcrTextWindow_Closed(object sender, WindowEventArgs args)
    {
        _lifetimeCancellation.Cancel();
        _searchTimer.Stop();
        _searchTimer.Tick -= SearchTimer_Tick;
        RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged;
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        _ = await _placement.TrySaveForCloseAsync(CancellationToken.None);
        _placement.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private void TitleBarDragRegion_Loaded(object sender, RoutedEventArgs e) => UpdateTitleBarInsets();

    private void TitleBarDragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarInsets();

    private void ConfigureWindowChrome()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        _appWindow.TitleBar.BackgroundColor = Colors.Transparent;
        _appWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
        _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    private void ApplyThemeChrome(ElementTheme effectiveTheme)
    {
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
        if (_theme == ElementTheme.Default)
        {
            ApplyThemeChrome(sender.ActualTheme);
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
            UpdateTitleBarInsets();
        }
    }

    private void UpdateTitleBarInsets()
    {
        if (!ExtendsContentIntoTitleBar || TitleBarDragRegion.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var scale = Math.Max(0.1d, xamlRoot.RasterizationScale);
        TitleBarLeftInsetColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scale);
        TitleBarRightInsetColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scale);
    }

    private string T(string key) => _strings.Translate(key);
}

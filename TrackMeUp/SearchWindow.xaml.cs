using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Displays a bounded, always-on-top Mica surface for local screenshot search.</summary>
public sealed partial class SearchWindow : Window
{
    private const int LogicalWindowWidth = 620;
    private const int LogicalWindowHeight = 620;
    private const int LogicalScreenMargin = 22;
    private readonly SearchViewModel _viewModel;
    private readonly LocalizationService _strings;
    private readonly CultureInfo _culture;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _queryCancellation;
    private XamlRoot? _xamlRoot;

    /// <summary>Creates the floating local-search window with an explicit theme and language.</summary>
    public SearchWindow(ITrackMeUpApplication application, string theme, string language)
    {
        ArgumentNullException.ThrowIfNull(application);
        _viewModel = new SearchViewModel(application);
        _strings = new LocalizationService(language);
        _culture = CultureInfo.GetCultureInfo(_strings.Language);
        InitializeComponent();
        RootGrid.DataContext = _viewModel;
        RootGrid.RequestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        UiLocalization.Apply(RootGrid, _strings);
        Title = _strings.Translate("Search.Title");
        QueryBox.PlaceholderText = _strings.Translate("Search.Placeholder");
        AutomationProperties.SetName(QueryBox, _strings.Translate("Search.Placeholder"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(application, this, _appWindow, WindowStateKeys.Search, LogicalWindowWidth, LogicalWindowHeight, LogicalScreenMargin, centerDefault: true);
        ConfigureWindowBehavior();
        _placement.ApplyDefaultBounds(RootGrid);
        Closed += SearchWindow_Closed;
    }

    /// <summary>Occurs when the user selects a screenshot result for the existing inspector.</summary>
    public event EventHandler<ScreenshotPreviewRequestedEventArgs>? ScreenshotRequested;

    /// <summary>Moves keyboard focus to the query field when an existing window is reactivated.</summary>
    public void FocusQuery() => QueryBox.Focus(FocusState.Programmatic);

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        _placement.ApplyDefaultBounds(RootGrid);
        await _placement.RestoreOrKeepCurrentAsync(RootGrid, _lifetimeCancellation.Token);
        UpdateTitleBarInsets();
        FocusQuery();
    }

    private void QueryBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        _ = ExecuteSearchAsync(args.QueryText);

    private void QueryBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput
            && string.IsNullOrWhiteSpace(sender.Text))
        {
            _queryCancellation?.Cancel();
            _viewModel.Clear();
            UpdateResultState(hasExecutedQuery: false);
        }
    }

    private async Task ExecuteSearchAsync(string query)
    {
        _queryCancellation?.Cancel();
        _queryCancellation?.Dispose();
        _queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        var cancellationToken = _queryCancellation.Token;
        SearchInfoBar.IsOpen = false;
        SearchProgressRing.IsActive = true;
        QueryBox.IsEnabled = false;
        try
        {
            var result = await _viewModel.SearchAsync(query, _culture, cancellationToken);
            if (!result.Succeeded)
            {
                SearchInfoBar.Title = _strings.Translate("Search.Error");
                SearchInfoBar.Message = string.Empty;
                SearchInfoBar.IsOpen = true;
            }

            UpdateResultState(hasExecutedQuery: !string.IsNullOrWhiteSpace(query));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A superseding query or window close owns the next presentation state.
        }
        catch (Exception)
        {
            SearchInfoBar.Title = _strings.Translate("Search.Error");
            SearchInfoBar.Message = string.Empty;
            SearchInfoBar.IsOpen = true;
            UpdateResultState(hasExecutedQuery: true);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SearchProgressRing.IsActive = false;
                QueryBox.IsEnabled = true;
                FocusQuery();
            }
        }
    }

    private void UpdateResultState(bool hasExecutedQuery)
    {
        var hasResults = _viewModel.Results.Count > 0;
        SearchResultsList.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasExecutedQuery && !hasResults ? Visibility.Visible : Visibility.Collapsed;
        ResultCountText.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        ResultCountText.Text = string.Format(
            _culture,
            _strings.Translate("Search.ResultCount"),
            _viewModel.Results.Count,
            _viewModel.TotalCount);
    }

    private void SearchResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScreenshotSearchResult result)
        {
            ScreenshotRequested?.Invoke(
                this,
                new ScreenshotPreviewRequestedEventArgs(result.ScreenshotPath, result.CapturedAt));
        }
    }

    private void ConfigureWindowBehavior()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _appWindow.TitleBar.BackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void TitleBarDragRegion_Loaded(object sender, RoutedEventArgs e) => UpdateTitleBarInsets();

    private void TitleBarDragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarInsets();

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

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        }
    }

    private async void SearchWindow_Closed(object sender, WindowEventArgs args)
    {
        await _placement.SaveAsync(CancellationToken.None);
        _placement.Dispose();
        _lifetimeCancellation.Cancel();
        _queryCancellation?.Cancel();
        _queryCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }
    }
}

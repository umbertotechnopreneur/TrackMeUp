// SPDX-License-Identifier: MIT

using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Displays a light Acrylic surface for local screenshot search.</summary>
public sealed partial class SearchWindow : Window
{
    private const int LogicalWindowWidth = 960;
    private const int MaximumLogicalWidth = 960;
    private const int CompactLogicalHeight = 168;
    private const int EmptyLogicalHeight = 204;
    private const int ErrorLogicalHeight = 250;
    private const int ResultsChromeLogicalHeight = 162;
    private const int ResultLogicalHeight = 180;
    private const int LogicalScreenMargin = 22;
    private const double CursorDisplayWidthRatio = 0.64d;
    private const double MaximumCursorDisplayHeightRatio = 0.78d;
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);
    private readonly SearchViewModel _viewModel;
    private readonly LocalizationService _strings;
    private readonly CultureInfo _culture;
    private readonly AppWindow _appWindow;
    private readonly CustomTitleBarController _titleBar;
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _queryCancellation;
    private CancellationTokenSource? _debounceCancellation;
    private XamlRoot? _xamlRoot;
    private bool _hasExecutedQuery;
    private bool _closing;
    private int _activeSearchOperationCount;

    /// <summary>Creates the fixed-light floating local-search window in the requested language.</summary>
    public SearchWindow(ITrackMeUpApplication application, string language, SearchAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(availability);
        if (availability.TotalSnapshotCount <= 0 ||
            availability.TodaySnapshotCount < 0 ||
            availability.TodaySnapshotCount > availability.TotalSnapshotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(availability), "Search availability must describe at least one retained snapshot.");
        }

        _strings = new LocalizationService(language);
        _culture = _strings.Culture;
        _viewModel = new SearchViewModel(
            application,
            _strings.Translate("Search.Result.Match"),
            FormatClickCount);
        InitializeComponent();
        RootGrid.DataContext = _viewModel;
        RootGrid.RequestedTheme = ElementTheme.Light;
        UiLocalization.Apply(RootGrid, _strings);
        Title = _strings.Translate("Search.Title");
        QueryBox.PlaceholderText = _strings.Translate("Search.Placeholder");
        AutomationProperties.SetName(QueryBox, _strings.Translate("Search.Placeholder"));
        AutomationProperties.SetName(SearchActivityProgressRing, _strings.Translate("Search.Working"));
        SearchAvailabilityText.Text = string.Format(
            _culture,
            _strings.Translate("Search.Availability"),
            availability.TotalSnapshotCount,
            availability.TodaySnapshotCount,
            _strings.Translate(availability.TextReadingEnabled
                ? "Search.TextReading.Enabled"
                : "Search.TextReading.Disabled"));
        AutomationProperties.SetName(SearchAvailabilityText, SearchAvailabilityText.Text);
        _appWindow = AppWindow.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _titleBar = new CustomTitleBarController(
            this,
            _appWindow,
            RootGrid,
            TitleBarDragRegion,
            TitleBarLeftInsetColumn,
            TitleBarRightInsetColumn,
            static () => Array.Empty<FrameworkElement>(),
            useTallTitleBar: false);
        _titleBar.ApplyTheme(ElementTheme.Light);
        _placement = new WindowPlacementService(application, this, _appWindow, WindowStateKeys.Search, LogicalWindowWidth, CompactLogicalHeight, LogicalScreenMargin);
        ConfigureWindowBehavior();
        _placement.ApplyDefaultBounds(RootGrid);
        Closed += SearchWindow_Closed;
    }

    private string FormatClickCount(long? clickCount, CultureInfo culture)
    {
        if (clickCount is null)
        {
            return _strings.Translate("Search.Result.Clicks.None");
        }

        var key = clickCount == 1
            ? "Search.Result.Clicks.One"
            : "Search.Result.Clicks.Many";
        return string.Format(culture, _strings.Translate(key), clickCount.Value);
    }

    /// <summary>Occurs when the user selects a screenshot result for the existing inspector.</summary>
    public event EventHandler<ScreenshotPreviewRequestedEventArgs>? ScreenshotRequested;

    /// <summary>Moves keyboard focus to the query field when an existing window is reactivated.</summary>
    public void FocusQuery()
    {
        QueryBox.Focus(FocusState.Programmatic);
        SelectAllQueryText();
    }

    /// <summary>Activates the existing search window centered on the monitor containing the pointer.</summary>
    public void ActivateAtCursor()
    {
        ResizeForCurrentState();
        Activate();
        FocusQuery();
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
            await _placement.RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token, centerOnCursorDisplay: true);
        }
        catch (OperationCanceledException) when (_closing)
        {
            return;
        }

        ResizeForCurrentState();
        CenterQueryText();
        FocusQuery();
    }

    private void QueryBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.QueryText.Trim().Length < SearchViewModel.MinimumQueryLength)
        {
            return;
        }

        CancelDebounce();
        _ = ExecuteSearchAsync(args.QueryText);
    }

    private void QueryBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        CancelDebounce();
        _queryCancellation?.Cancel();
        var query = sender.Text.Trim();
        if (query.Length < SearchViewModel.MinimumQueryLength)
        {
            _queryCancellation?.Cancel();
            _viewModel.Clear();
            SearchInfoBar.IsOpen = false;
            UpdateResultState(hasExecutedQuery: false);
            return;
        }

        _debounceCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        var cancellationToken = _debounceCancellation.Token;
        _ = DebounceSearchAsync(query, cancellationToken);
    }

    private async Task DebounceSearchAsync(string query, CancellationToken cancellationToken)
    {
        BeginSearchActivity();
        try
        {
            await Task.Delay(SearchDebounce, cancellationToken);
            await ExecuteSearchAsync(query);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer keystroke owns the next debounce window.
        }
        finally
        {
            EndSearchActivity();
        }
    }

    private async Task ExecuteSearchAsync(string query)
    {
        _queryCancellation?.Cancel();
        _queryCancellation?.Dispose();
        _queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        var cancellationToken = _queryCancellation.Token;
        SearchInfoBar.IsOpen = false;
        BeginSearchActivity();
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
            EndSearchActivity();
        }
    }

    private void BeginSearchActivity()
    {
        var wasIdle = _activeSearchOperationCount == 0;
        _activeSearchOperationCount++;
        if (wasIdle)
        {
            SearchActivityGlow.Visibility = Visibility.Visible;
            UpdateResultState(_hasExecutedQuery);
        }
    }

    private void EndSearchActivity()
    {
        if (_activeSearchOperationCount <= 0)
        {
            throw new InvalidOperationException("Search activity tracking became unbalanced.");
        }

        _activeSearchOperationCount--;
        if (_activeSearchOperationCount == 0)
        {
            SearchActivityGlow.Visibility = Visibility.Collapsed;
            UpdateResultState(_hasExecutedQuery);
        }
    }

    private void UpdateResultState(bool hasExecutedQuery)
    {
        _hasExecutedQuery = hasExecutedQuery;
        var hasResults = _viewModel.Results.Count > 0;
        var isSearchingWithoutResults = _activeSearchOperationCount > 0 && !hasResults;
        SearchResultsList.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasExecutedQuery && !hasResults && !isSearchingWithoutResults
            ? Visibility.Visible
            : Visibility.Collapsed;
        SearchActivityStatus.Visibility = isSearchingWithoutResults ? Visibility.Visible : Visibility.Collapsed;
        ResizeForCurrentState();
    }

    private void ResizeForCurrentState()
    {
        var resultCount = _viewModel.Results.Count;
        var logicalHeight = resultCount > 0
            ? checked(ResultsChromeLogicalHeight + (resultCount * ResultLogicalHeight))
            : SearchInfoBar.IsOpen
                ? ErrorLogicalHeight
                : _hasExecutedQuery
                    ? EmptyLogicalHeight
                    : CompactLogicalHeight;
        _placement.ResizeAndCenterOnCursorDisplay(
            RootGrid,
            CursorDisplayWidthRatio,
            MaximumLogicalWidth,
            logicalHeight,
            MaximumCursorDisplayHeightRatio);
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

    private void QueryBox_GotFocus(object sender, RoutedEventArgs e)
    {
        CenterQueryText();
        SelectAllQueryText();
    }

    private void CenterQueryText()
    {
        if (FindDescendant<TextBox>(QueryBox) is { } textBox)
        {
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            textBox.Padding = new Thickness(textBox.Padding.Left, 0, textBox.Padding.Right, 0);
        }
    }

    private void SelectAllQueryText()
    {
        if (FindDescendant<TextBox>(QueryBox) is { } textBox)
        {
            textBox.SelectAll();
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private void CancelDebounce()
    {
        _debounceCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _debounceCancellation = null;
    }

    private void ConfigureWindowBehavior()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            ResizeForCurrentState();
        }
    }

    private async void SearchWindow_Closed(object sender, WindowEventArgs args)
    {
        _closing = true;
        SearchActivityGlow.Visibility = Visibility.Collapsed;
        SearchActivityStatus.Visibility = Visibility.Collapsed;
        _lifetimeCancellation.Cancel();
        CancelDebounce();
        _queryCancellation?.Cancel();
        _ = await _placement.TrySaveForCloseAsync(CancellationToken.None);
        _titleBar.Dispose();
        _placement.Dispose();
        _queryCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }
    }
}

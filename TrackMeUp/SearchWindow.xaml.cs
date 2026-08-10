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

/// <summary>Displays a light, always-on-top Acrylic surface for local screenshot search.</summary>
public sealed partial class SearchWindow : Window
{
    private const int LogicalWindowWidth = 960;
    private const int MaximumLogicalWidth = 960;
    private const int CompactLogicalHeight = 140;
    private const int EmptyLogicalHeight = 176;
    private const int ErrorLogicalHeight = 222;
    private const int ResultsChromeLogicalHeight = 158;
    private const int ResultLogicalHeight = 180;
    private const int LogicalScreenMargin = 22;
    private const double CursorDisplayWidthRatio = 0.64d;
    private const double MaximumCursorDisplayHeightRatio = 0.78d;
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(700);
    private readonly SearchViewModel _viewModel;
    private readonly LocalizationService _strings;
    private readonly CultureInfo _culture;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _queryCancellation;
    private CancellationTokenSource? _debounceCancellation;
    private XamlRoot? _xamlRoot;
    private bool _hasExecutedQuery;
    private bool _hasBeenActivated;
    private bool _isActive;
    private bool _closeOnDeactivationQueued;
    private bool _closing;
    private int _activeSearchOperationCount;

    /// <summary>Creates the fixed-light floating local-search window in the requested language.</summary>
    public SearchWindow(ITrackMeUpApplication application, string language)
    {
        ArgumentNullException.ThrowIfNull(application);
        _viewModel = new SearchViewModel(application);
        _strings = new LocalizationService(language);
        _culture = CultureInfo.GetCultureInfo(_strings.Language);
        InitializeComponent();
        RootGrid.DataContext = _viewModel;
        RootGrid.RequestedTheme = ElementTheme.Light;
        UiLocalization.Apply(RootGrid, _strings);
        Title = _strings.Translate("Search.Title");
        QueryBox.PlaceholderText = _strings.Translate("Search.Placeholder");
        AutomationProperties.SetName(QueryBox, _strings.Translate("Search.Placeholder"));
        AutomationProperties.SetName(SearchActivityBar, _strings.Translate("Search.Working"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(application, this, _appWindow, WindowStateKeys.Search, LogicalWindowWidth, CompactLogicalHeight, LogicalScreenMargin);
        ConfigureWindowBehavior();
        _placement.ApplyDefaultBounds(RootGrid);
        Activated += SearchWindow_Activated;
        Closed += SearchWindow_Closed;
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
        UpdateTitleBarInsets();
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
        QueryBox.ItemsSource = null;
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
        _ = UpdateSuggestionsAsync(query, cancellationToken);
    }

    private async Task DebounceSearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounce, cancellationToken);
            await ExecuteSearchAsync(query);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer keystroke owns the next debounce window.
        }
    }

    private async Task UpdateSuggestionsAsync(string query, CancellationToken cancellationToken)
    {
        BeginSearchActivity();
        try
        {
            var result = await _viewModel.SuggestAsync(query, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && result.Succeeded)
            {
                QueryBox.ItemsSource = result.Value?
                    .Select(suggestion => new SearchSuggestionDisplayItem(
                        suggestion.Text,
                        suggestion.ConfidenceDisplay,
                        string.Format(
                            _culture,
                            _strings.Translate("Search.Suggestion.Confidence"),
                            suggestion.ConfidencePercent)))
                    .ToArray()
                    ?? Array.Empty<SearchSuggestionDisplayItem>();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer keystroke owns the next suggestion request.
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
        _activeSearchOperationCount++;
        SearchActivityGlow.Visibility = Visibility.Visible;
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
        }
    }

    private void UpdateResultState(bool hasExecutedQuery)
    {
        _hasExecutedQuery = hasExecutedQuery;
        var hasResults = _viewModel.Results.Count > 0;
        SearchResultsList.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasExecutedQuery && !hasResults ? Visibility.Visible : Visibility.Collapsed;
        ResultCountText.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        ResultCountText.Text = string.Format(
            _culture,
            _strings.Translate("Search.ResultCount"),
            _viewModel.Results.Count,
            _viewModel.TotalCount);
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
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _appWindow.TitleBar.BackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
            _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(150, 0, 0, 0);
            _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(22, 0, 0, 0);
            _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(36, 0, 0, 0);
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
            ResizeForCurrentState();
            UpdateTitleBarInsets();
        }
    }

    private void SearchWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _isActive = args.WindowActivationState != WindowActivationState.Deactivated;
        if (_isActive)
        {
            _hasBeenActivated = true;
            return;
        }

        if (!_hasBeenActivated || _closing || _closeOnDeactivationQueued)
        {
            return;
        }

        _closeOnDeactivationQueued = true;
        void CloseIfStillInactive()
        {
            _closeOnDeactivationQueued = false;
            if (!_isActive && !_closing)
            {
                _closing = true;
                Close();
            }
        }

        if (!DispatcherQueue.TryEnqueue(CloseIfStillInactive))
        {
            CloseIfStillInactive();
        }
    }

    private async void SearchWindow_Closed(object sender, WindowEventArgs args)
    {
        _closing = true;
        SearchActivityGlow.Visibility = Visibility.Collapsed;
        Activated -= SearchWindow_Activated;
        _lifetimeCancellation.Cancel();
        CancelDebounce();
        _queryCancellation?.Cancel();
        await _placement.SaveAsync(CancellationToken.None);
        _placement.Dispose();
        _queryCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }
    }
}

/// <summary>Contains localized values rendered by one AutoSuggestBox popup row.</summary>
internal sealed record SearchSuggestionDisplayItem(
    string Text,
    string ConfidenceDisplay,
    string ConfidenceLabel);

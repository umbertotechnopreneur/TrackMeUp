using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Globalization;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Storage.Pickers;
using Windows.System;

namespace TrackMeUp;

/// <summary>Renders the retained screenshot gallery and forwards date/navigation intent to the shared facade.</summary>
public sealed partial class ScreenshotWindow : Window
{
    private const int LogicalWindowWidth = 1180;
    private const int LogicalWindowHeight = 820;
    private const int LogicalScreenMargin = 24;
    private const double DefaultDetailsPaneWidth = 360d;
    private const double MinimumDetailsPaneWidth = 300d;
    private const double MaximumDetailsPaneWidthRatio = 0.5d;
    private const double DetailsPaneKeyboardResizeStep = 16d;
    private const string DefaultAiDescriptionEmptyMessageKey = "Screenshots.AiDescription.Empty";
    private const string DailyAiLimitEmptyMessageKey = "Notification.AiDailyLimitReached.Message";

    private readonly ITrackMeUpApplication _application;
    private readonly AppWindow _appWindow;
    private readonly MicaDialogService _dialogs = new();
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly string? _launchTheme;
    private CancellationTokenSource? _galleryCancellation;
    private IReadOnlyList<ScreenshotGalleryItem> _items = Array.Empty<ScreenshotGalleryItem>();
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private LocalizationService _strings = new("system");
    private XamlRoot? _xamlRoot;
    private string _theme = "system";
    private string _aiDescriptionEmptyMessageKey = DefaultAiDescriptionEmptyMessageKey;
    private ScreenshotDetailsViewState? _selectedDetailsState;
    private bool _initialized;
    private bool _settingSelectedDate;
    private uint? _detailsResizePointerId;
    private double _detailsResizeStartPointerX;
    private double _detailsResizeStartWidth;
    private string? _requestedScreenshotPath;

    private CalendarDatePicker SelectedDatePicker => HeaderSection.DatePicker;

    private TextBlock GalleryCountText => HeaderSection.CountText;

    private TextBlock ExtendedDateText => HeaderSection.DisplayDateText;

    private Controls.ScreenshotImageViewerControl ScreenshotViewer => GallerySection.Viewer;

    private Grid MetadataPanel => GallerySection.MetadataContainer;

    private TextBlock MetadataDateValueText => GallerySection.MetadataDateText;

    private TextBlock MetadataTimeValueText => GallerySection.MetadataTimeText;

    private TextBlock MetadataAppValueText => GallerySection.MetadataApplicationText;

    private Grid EmptyGalleryPanel => GallerySection.EmptyPanel;

    private TextBlock EmptyGalleryText => GallerySection.EmptyText;

    private ProgressRing GalleryProgressRing => GallerySection.LoadingRing;

    private StackPanel FilmstripStrip => TimelineSection.TimelineRoot;

    private ListView FilmstripList => TimelineSection.ItemsView;

    private Button FilmstripToggleButton => TimelineSection.ToggleButton;

    private FontIcon FilmstripChevronIcon => TimelineSection.ToggleChevronIcon;

    /// <summary>Creates the translucent screenshot inspector backed by the shared application facade.</summary>
    public ScreenshotWindow(
        ITrackMeUpApplication application,
        string? launchTheme = null,
        string? requestedScreenshotPath = null,
        DateTimeOffset? requestedCapturedAt = null)
    {
        if ((requestedScreenshotPath is null) != (requestedCapturedAt is null))
        {
            throw new ArgumentException("A targeted screenshot requires both its path and capture timestamp.");
        }

        if (requestedScreenshotPath is not null && string.IsNullOrWhiteSpace(requestedScreenshotPath))
        {
            throw new ArgumentException("The targeted screenshot path cannot be empty.", nameof(requestedScreenshotPath));
        }

        _application = application;
        _launchTheme = launchTheme;
        _requestedScreenshotPath = requestedScreenshotPath;
        if (requestedCapturedAt is { } capturedAt)
        {
            _selectedDate = DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime);
        }

        InitializeComponent();
        WireViewEvents();
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(_application, this, _appWindow, WindowStateKeys.Screenshots, LogicalWindowWidth, LogicalWindowHeight, LogicalScreenMargin);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        SetSelectedDate(_selectedDate);
        ApplyTheme(_theme);
        ApplyLocalization();
        _placement.ApplyDefaultBounds(RootGrid);
        Closed += ScreenshotWindow_Closed;
    }

    private void WireViewEvents()
    {
        SelectedDatePicker.DateChanged += SelectedDatePicker_DateChanged;
        ScreenshotViewer.SaveRequested += ScreenshotViewer_SaveRequested;
        ScreenshotViewer.ShareRequested += ScreenshotViewer_ShareRequested;
        ScreenshotViewer.OpenFolderRequested += ScreenshotViewer_OpenFolderRequested;
        ScreenshotViewer.DeleteScreenshotRequested += ScreenshotViewer_DeleteScreenshotRequested;
        ScreenshotViewer.DeleteSnapshotRequested += ScreenshotViewer_DeleteSnapshotRequested;
        ScreenshotViewer.DetailsVisibilityRequested += ScreenshotViewer_DetailsVisibilityRequested;
        TimelineSection.SelectedIndexChanged += TimelineSection_SelectedIndexChanged;
        FilmstripToggleButton.Click += FilmstripToggleButton_Click;
    }

    /// <summary>Selects a retained capture when an already-open inspector is reused.</summary>
    public async Task FocusScreenshotAsync(string screenshotPath, DateTimeOffset capturedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);
        _requestedScreenshotPath = screenshotPath;
        var selectedDate = DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime);
        SetSelectedDate(selectedDate);
        if (_initialized)
        {
            await LoadGalleryAsync(selectedDate);
        }
    }

    /// <summary>Reselects the gallery to the most recent retained capture when the menu reopens an existing window.</summary>
    public async Task FocusLatestAsync()
    {
        _requestedScreenshotPath = null;
        if (_initialized)
        {
            await LoadGalleryAsync(null);
        }
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

        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var result = _launchTheme is null
                ? await _application.GetSettingsAsync(_lifetimeCancellation.Token)
                : await _application.PatchSettingsAsync(
                    new SettingsPatch(new Dictionary<string, string?> { ["theme"] = _launchTheme }),
                    _lifetimeCancellation.Token);
            if (result.Succeeded && result.Value is not null)
            {
                _theme = result.Value.Theme;
                _strings = new LocalizationService(result.Value.UiLanguage);
                ApplyTheme(_theme);
                ApplyLocalization();
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            // The gallery remains usable with the system theme and default strings if optional settings cannot be read.
            _strings = new LocalizationService("system");
            ApplyLocalization();
        }

        await LoadGalleryAsync(_requestedScreenshotPath is null ? null : _selectedDate);
    }

    private async void SelectedDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_settingSelectedDate || !_initialized || args.NewDate is not { } newDate)
        {
            return;
        }

        _selectedDate = DateOnly.FromDateTime(newDate.DateTime);
        await LoadGalleryAsync(_selectedDate);
    }

    private async Task LoadGalleryAsync(DateOnly? date)
    {
        _galleryCancellation?.Cancel();
        _galleryCancellation?.Dispose();
        _galleryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        var cancellationToken = _galleryCancellation.Token;
        SetLoading(true);

        try
        {
            var result = date is { } selectedDate
                ? await _application.GetScreenshotGalleryAsync(selectedDate, cancellationToken)
                : await _application.GetLatestScreenshotGalleryAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.Succeeded || result.Value is null)
            {
                _items = Array.Empty<ScreenshotGalleryItem>();
                RenderGallery(_strings.Format("Screenshots.Error.UnavailableWithCode", result.Code));
                return;
            }

            await RefreshAiDescriptionEmptyMessageAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SetSelectedDate(result.Value.Date);
            _items = result.Value.Items;
            SelectRequestedScreenshot();
            RenderGallery(null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer date selection owns the next gallery request; the superseded result is ignored.
        }
        catch (Exception)
        {
            _items = Array.Empty<ScreenshotGalleryItem>();
            RenderGallery(T("Screenshots.Error.Unavailable"));
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetLoading(false);
            }
        }
    }

    private async Task RefreshAiDescriptionEmptyMessageAsync(CancellationToken cancellationToken)
    {
        _aiDescriptionEmptyMessageKey = DefaultAiDescriptionEmptyMessageKey;
        try
        {
            var result = await _application.GetAiStatusAsync(cancellationToken);
            if (result.Succeeded &&
                result.Value is { Enabled: true, CostGate: { Allowed: false, Reason: "daily_limit" } })
            {
                // Historical rows do not retain an empty-description reason; report only the truthful current gate state.
                _aiDescriptionEmptyMessageKey = DailyAiLimitEmptyMessageKey;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Contextual gate status is optional; the existing neutral empty-state copy remains the safe fallback.
            _aiDescriptionEmptyMessageKey = DefaultAiDescriptionEmptyMessageKey;
        }
    }

    private void SetSelectedDate(DateOnly date)
    {
        _selectedDate = date;
        UpdateDisplayedDate();
        _settingSelectedDate = true;
        try
        {
            SelectedDatePicker.Date = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
        }
        finally
        {
            _settingSelectedDate = false;
        }
    }

    private void ApplyHeaderLocalization()
    {
        SelectedDatePicker.PlaceholderText = T("Screenshots.Date.Placeholder");
        AutomationProperties.SetName(SelectedDatePicker, T("Screenshots.Date"));
        UpdateDisplayedDate();
        UpdateDetailsToggleAccessibility();
        UpdateFilmstripToggleAccessibility();
    }

    private void ApplyLocalization()
    {
        UiLocalization.Apply(RootGrid, _strings);
        Title = T("Screenshots.Title");
        var resizeLabel = T("Screenshots.Details.Resize");
        AutomationProperties.SetName(DetailsResizeGrip, resizeLabel);
        ToolTipService.SetToolTip(DetailsResizeGrip, resizeLabel);
        ApplyHeaderLocalization();
    }

    private void UpdateDisplayedDate()
    {
        ExtendedDateText.Text = _selectedDate.ToDateTime(TimeOnly.MinValue).ToString("D", _strings.Culture);
    }

    private void SelectRequestedScreenshot()
    {
        if (_requestedScreenshotPath is not { } requestedPath)
        {
            return;
        }

        var requestedFullPath = Path.GetFullPath(requestedPath);
        for (var index = 0; index < _items.Count; index++)
        {
            if (string.Equals(Path.GetFullPath(_items[index].Path), requestedFullPath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedIndex = index;
                _requestedScreenshotPath = null;
                return;
            }
        }
    }

    private void RenderGallery(string? error)
    {
        var hasItems = _items.Count > 0;
        GalleryCountText.Text = _items.Count switch
        {
            0 => T("Screenshots.Count.Zero"),
            1 => T("Screenshots.Count.One"),
            _ => _strings.Format("Screenshots.Count.Many", _items.Count)
        };
        EmptyGalleryPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ScreenshotViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        FilmstripStrip.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        if (!hasItems)
        {
            SetDetailsPaneVisibility(isVisible: false);
        }
        EmptyGalleryText.Text = error ?? (_items.Count == 0 ? T("Screenshots.Empty") : string.Empty);

        _selectedIndex = hasItems ? Math.Clamp(_selectedIndex, 0, _items.Count - 1) : 0;
        TimelineSection.SetItems(_items, hasItems ? _selectedIndex : -1, _strings.Language);
        RenderSelectedScreenshot();
        UpdateDetailsToggleAccessibility();
    }

    private int _selectedIndex;

    private void TimelineSection_SelectedIndexChanged(int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= _items.Count)
        {
            return;
        }

        _selectedIndex = selectedIndex;
        RenderSelectedScreenshot();
    }

    private void RenderSelectedScreenshot()
    {
        if (_items.Count == 0)
        {
            ScreenshotViewer.SetItem(null, -1, 0, _strings.Language);
            RenderMetadata(null);
            return;
        }

        var selected = _items[_selectedIndex];
        ScreenshotViewer.SetItem(selected, _selectedIndex, _items.Count, _strings.Language);
        RenderMetadata(selected);
    }

    private void RenderMetadata(ScreenshotGalleryItem? item)
    {
        if (item is null)
        {
            MetadataDateValueText.Text = "--";
            MetadataTimeValueText.Text = "--";
            MetadataAppValueText.Text = "--";
            _selectedDetailsState = null;
            RenderDetails();
            MetadataPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var culture = _strings.Culture;
        var localTime = item.CapturedAt.ToLocalTime();
        MetadataDateValueText.Text = FormatMetadataDate(localTime, culture);
        MetadataTimeValueText.Text = localTime.ToString("t", culture);
        MetadataAppValueText.Text = string.IsNullOrWhiteSpace(item.ForegroundApplication)
            ? T("Screenshots.Application.Desktop")
            : item.ForegroundApplication;
        var captureOrigin = FormatCaptureOrigin(item.CaptureOrigin);
        _selectedDetailsState = ScreenshotDetailsProjection.Create(
            item,
            culture,
            FormatCaptureKind(item.CaptureKind),
            captureOrigin,
            "--");
        RenderDetails();
        MetadataPanel.Visibility = Visibility.Visible;
    }

    private void RenderDetails()
    {
        // The pane starts collapsed, so explicitly localize its declared subtree whenever it is bound or revealed.
        UiLocalization.Apply(DetailsSection, _strings);
        DetailsSection.Render(
            _selectedDetailsState,
            _strings.Translate(_aiDescriptionEmptyMessageKey));
    }

    private void ScreenshotViewer_DetailsVisibilityRequested(bool isVisible) =>
        SetDetailsPaneVisibility(isVisible);

    private void SetDetailsPaneVisibility(bool isVisible)
    {
        DetailsPane.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        if (isVisible)
        {
            SetDetailsPaneWidth(double.IsNaN(DetailsPane.Width) ? DefaultDetailsPaneWidth : DetailsPane.Width);
            RenderDetails();
        }

        UpdateDetailsToggleAccessibility();
    }

    private void UpdateDetailsToggleAccessibility()
    {
        var key = DetailsPane.Visibility == Visibility.Visible
            ? "Screenshots.Details.Hide"
            : "Screenshots.Details.Show";
        var label = _strings.Translate(key);
        ScreenshotViewer.SetDetailsState(_items.Count > 0, DetailsPane.Visibility == Visibility.Visible, label);
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DetailsPane.Visibility == Visibility.Visible)
        {
            SetDetailsPaneWidth(DetailsPane.Width);
        }
    }

    private void DetailsResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid);
        if (sender is not Controls.HorizontalResizeGrip grip
            || (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            || !grip.CapturePointer(e.Pointer))
        {
            return;
        }

        _detailsResizePointerId = e.Pointer.PointerId;
        _detailsResizeStartPointerX = point.Position.X;
        _detailsResizeStartWidth = DetailsPane.Width;
        e.Handled = true;
    }

    private void DetailsResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_detailsResizePointerId != e.Pointer.PointerId)
        {
            return;
        }

        var currentPointerX = e.GetCurrentPoint(RootGrid).Position.X;
        SetDetailsPaneWidth(_detailsResizeStartWidth + (_detailsResizeStartPointerX - currentPointerX));
        e.Handled = true;
    }

    private void DetailsResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_detailsResizePointerId != e.Pointer.PointerId || sender is not Controls.HorizontalResizeGrip grip)
        {
            return;
        }

        _detailsResizePointerId = null;
        grip.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void DetailsResizeGrip_PointerCanceled(object sender, PointerRoutedEventArgs e) =>
        _detailsResizePointerId = null;

    private void DetailsResizeGrip_PointerCaptureLost(object sender, PointerRoutedEventArgs e) =>
        _detailsResizePointerId = null;

    private void DetailsResizeGrip_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var targetWidth = e.Key switch
        {
            VirtualKey.Left => DetailsPane.Width + DetailsPaneKeyboardResizeStep,
            VirtualKey.Right => DetailsPane.Width - DetailsPaneKeyboardResizeStep,
            VirtualKey.Home => MinimumDetailsPaneWidth,
            VirtualKey.End => RootGrid.ActualWidth * MaximumDetailsPaneWidthRatio,
            _ => double.NaN
        };
        if (double.IsNaN(targetWidth))
        {
            return;
        }

        SetDetailsPaneWidth(targetWidth);
        e.Handled = true;
    }

    private void SetDetailsPaneWidth(double requestedWidth)
    {
        var maximumWidth = Math.Max(1d, RootGrid.ActualWidth * MaximumDetailsPaneWidthRatio);
        var minimumWidth = Math.Min(MinimumDetailsPaneWidth, maximumWidth);
        DetailsPane.Width = Math.Clamp(requestedWidth, minimumWidth, maximumWidth);
    }

    private static string FormatMetadataDate(DateTimeOffset capturedAt, CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.LongDatePattern
            .Replace("dddd,", string.Empty, StringComparison.Ordinal)
            .Replace("dddd", string.Empty, StringComparison.Ordinal)
            .TrimStart(' ', ',');
        return capturedAt.ToString(pattern, culture);
    }

    private string FormatCaptureOrigin(string captureOrigin)
    {
        var normalizedOrigin = captureOrigin.Trim().ToLowerInvariant();
        return normalizedOrigin switch
        {
            ScreenshotCaptureOrigins.Manual => _strings.Translate("Screenshots.Origin.Manual"),
            ScreenshotCaptureOrigins.Scheduled => _strings.Translate("Screenshots.Origin.Scheduled"),
            _ => captureOrigin
        };
    }

    private string FormatCaptureKind(string captureKind)
    {
        var normalizedKind = captureKind.Trim().ToLowerInvariant();
        return normalizedKind switch
        {
            "active-window" => _strings.Translate("Screenshots.CaptureKind.ActiveWindow"),
            "monitor" => _strings.Translate("Screenshots.CaptureKind.Monitor"),
            _ => throw new InvalidDataException($"Unsupported screenshot capture kind: {captureKind}")
        };
    }

    private async void ScreenshotViewer_DeleteScreenshotRequested(object? sender, EventArgs e)
    {
        var selected = GetSelectedItem();
        var result = await _application.DeleteScreenshotAsync(selected.Path, _lifetimeCancellation.Token);
        if (result.Succeeded)
        {
            await LoadGalleryAsync(_selectedDate);
        }

        ShowActionResult(result, "Screenshots.Action.ScreenshotDeleted");
    }

    private async void ScreenshotViewer_DeleteSnapshotRequested(object? sender, EventArgs e)
    {
        var selected = GetSelectedItem();
        var result = await _application.DeleteSnapshotAsync(selected.Path, _lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.SnapshotDeleted");
    }

    private async void ScreenshotViewer_SaveRequested(object? sender, EventArgs e) =>
        await SaveSelectedScreenshotAsync();

    private async Task SaveSelectedScreenshotAsync()
    {
        var selected = GetSelectedItem();
        var extension = Path.GetExtension(selected.Path) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new InvalidDataException($"Screenshot has no file extension: {selected.Path}");
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = Path.GetFileName(selected.Path)
        };
        picker.FileTypeChoices.Add(T("Screenshots.FileType"), new[] { extension });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var destination = await picker.PickSaveFileAsync();
        if (destination is null)
        {
            return;
        }

        var result = await _application.SaveScreenshotAsync(selected.Path, destination.Path, _lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.Saved");
    }

    private async void ScreenshotViewer_OpenFolderRequested(object? sender, EventArgs e)
    {
        var result = await _application.OpenScreenshotFolderAsync(_lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.FolderOpened");
    }

    private async void ScreenshotViewer_ShareRequested(object? sender, EventArgs e)
    {
        var selected = GetSelectedItem();
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64();
        var result = await _application.ShareScreenshotAsync(selected.Path, windowHandle, _lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.ShareOpened");
    }

    private void ShowActionResult<T>(OperationResult<T> result, string successKey)
    {
        var title = _strings.Translate("Screenshots.Caption");
        var message = _strings.Translate(result.Succeeded ? successKey : "Screenshots.Action.Failed");
        if (result.Succeeded)
        {
            _dialogs.ShowSuccessBanner(ScreenshotActionBanner, title, message);
        }
        else
        {
            _dialogs.ShowErrorBanner(ScreenshotActionBanner, title, message);
        }
    }

    private ScreenshotGalleryItem GetSelectedItem()
        => _items.Count == 0
            ? throw new InvalidOperationException("Screenshot action requested without a selected capture.")
            : _items[_selectedIndex];

    private void FilmstripToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var isExpanded = FilmstripList.Visibility != Visibility.Visible;
        FilmstripList.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        FilmstripChevronIcon.Glyph = isExpanded ? "\uE70E" : "\uE70D";
        UpdateFilmstripToggleAccessibility();
    }

    private void UpdateFilmstripToggleAccessibility()
    {
        var key = FilmstripList.Visibility == Visibility.Visible
            ? "Screenshots.Timeline.Hide"
            : "Screenshots.Timeline.Show";
        var label = _strings.Translate(key);
        FilmstripToggleButton.Tag = key;
        AutomationProperties.SetName(FilmstripToggleButton, label);
        ToolTipService.SetToolTip(FilmstripToggleButton, label);
    }

    private void SetLoading(bool isLoading)
    {
        GalleryProgressRing.IsActive = isLoading;
        GalleryProgressRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectedDatePicker.IsEnabled = !isLoading;
    }

    private string T(string key) => _strings.Translate(key);

    private void ApplyTheme(string theme)
    {
        _theme = theme;
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
        if (_theme == "system")
        {
            ApplyThemeChrome(sender.ActualTheme);
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        }

    }

    private async void ScreenshotWindow_Closed(object sender, WindowEventArgs args)
    {
        await _placement.SaveAsync(CancellationToken.None);
        _placement.Dispose();

        _lifetimeCancellation.Cancel();
        _galleryCancellation?.Cancel();
        _galleryCancellation?.Dispose();
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged;
        _lifetimeCancellation.Dispose();
    }
}

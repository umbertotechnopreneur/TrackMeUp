using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Renders the retained screenshot gallery and forwards date/navigation intent to the shared facade.</summary>
public sealed partial class ScreenshotWindow : Window
{
    private const int LogicalWindowWidth = 1180;
    private const int LogicalWindowHeight = 820;
    private const int LogicalScreenMargin = 24;

    private readonly ITrackMeUpApplication _application;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly string? _launchTheme;
    private CancellationTokenSource? _galleryCancellation;
    private IReadOnlyList<ScreenshotGalleryItem> _items = Array.Empty<ScreenshotGalleryItem>();
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private LocalizationService _strings = new("system");
    private XamlRoot? _xamlRoot;
    private string _theme = "system";
    private bool _initialized;
    private bool _settingSelectedDate;
    private string? _requestedScreenshotPath;

    private CalendarDatePicker SelectedDatePicker => HeaderSection.DatePicker;

    private TextBlock GalleryCountText => HeaderSection.CountText;

    private Controls.ScreenshotImageViewerControl ScreenshotViewer => GallerySection.Viewer;

    private Border MetadataPanel => GallerySection.MetadataContainer;

    private TextBlock MetadataDateValueText => GallerySection.MetadataDateText;

    private TextBlock MetadataTimeValueText => GallerySection.MetadataTimeText;

    private TextBlock MetadataAppValueText => GallerySection.MetadataApplicationText;

    private TextBlock MetadataOriginValueText => GallerySection.MetadataOriginText;

    private TextBlock MetadataSpanLabelsValueText => GallerySection.MetadataSpanLabelsText;

    private TextBlock MetadataActivityIndexValueText => GallerySection.MetadataActivityIndexText;

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
        _placement = new WindowPlacementService(_application, this, _appWindow, WindowStateKeys.Screenshots, LogicalWindowWidth, LogicalWindowHeight, LogicalScreenMargin, centerDefault: true);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        SetSelectedDate(_selectedDate);
        ApplyTheme(_theme);
        _placement.ApplyDefaultBounds(RootGrid);
        UpdateTitleBarLayout();
        Closed += ScreenshotWindow_Closed;
    }

    private void WireViewEvents()
    {
        SelectedDatePicker.DateChanged += SelectedDatePicker_DateChanged;
        ScreenshotViewer.SaveRequested += ScreenshotViewer_SaveRequested;
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
        UpdateTitleBarLayout();
        await _placement.RestoreOrKeepCurrentAsync(RootGrid, _lifetimeCancellation.Token);

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
                UiLocalization.Apply(RootGrid, _strings);
                ApplyHeaderLocalization();
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
            UiLocalization.Apply(RootGrid, _strings);
            ApplyHeaderLocalization();
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
                RenderGallery($"Screenshot gallery unavailable ({result.Code}).");
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
            RenderGallery("Screenshot gallery unavailable.");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetLoading(false);
            }
        }
    }

    private void SetSelectedDate(DateOnly date)
    {
        _selectedDate = date;
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
        SelectedDatePicker.PlaceholderText = _strings.Translate("Screenshots.Date.Placeholder");
        AutomationProperties.SetName(SelectedDatePicker, _strings.Translate("Screenshots.Date"));
        UpdateDetailsToggleAccessibility();
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
        GalleryCountText.Text = hasItems ? $"{_items.Count} captures" : "0 captures";
        EmptyGalleryPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ScreenshotViewer.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        FilmstripStrip.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        DetailsToggleButton.IsEnabled = hasItems;
        if (!hasItems)
        {
            DetailsToggleButton.IsChecked = false;
            SetDetailsPaneVisibility(isVisible: false);
        }
        EmptyGalleryText.Text = error ?? (_items.Count == 0 ? "No screenshots for this day." : string.Empty);

        _selectedIndex = hasItems ? Math.Clamp(_selectedIndex, 0, _items.Count - 1) : 0;
        TimelineSection.SetItems(_items, hasItems ? _selectedIndex : -1, _strings.Language);
        RenderSelectedScreenshot();
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
            MetadataOriginValueText.Text = "--";
            MetadataSpanLabelsValueText.Text = "--";
            MetadataActivityIndexValueText.Text = "--";
            DetailsSection.Render(null);
            MetadataPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var culture = CultureInfo.GetCultureInfo(_strings.Language);
        var localTime = item.CapturedAt.ToLocalTime();
        MetadataDateValueText.Text = FormatMetadataDate(localTime, culture);
        MetadataTimeValueText.Text = localTime.ToString("t", culture);
        MetadataAppValueText.Text = string.IsNullOrWhiteSpace(item.ForegroundApplication) ? "Desktop" : item.ForegroundApplication;
        MetadataOriginValueText.Text = FormatCaptureOrigin(item.CaptureOrigin);
        MetadataSpanLabelsValueText.Text = FormatSpanLabels(item.SpanLabels, culture);
        MetadataActivityIndexValueText.Text = item.ActivityIndex?.ToString(culture) ?? "--";
        DetailsSection.Render(ScreenshotDetailsProjection.Create(
            item,
            culture,
            FormatCaptureKind(item.CaptureKind),
            MetadataOriginValueText.Text,
            "--"));
        MetadataPanel.Visibility = Visibility.Visible;
    }

    private void DetailsToggleButton_Click(object sender, RoutedEventArgs e) =>
        SetDetailsPaneVisibility(DetailsToggleButton.IsChecked == true);

    private void SetDetailsPaneVisibility(bool isVisible)
    {
        DetailsPane.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateDetailsToggleAccessibility();
    }

    private void UpdateDetailsToggleAccessibility()
    {
        var key = DetailsPane.Visibility == Visibility.Visible
            ? "Screenshots.Details.Hide"
            : "Screenshots.Details.Show";
        var label = _strings.Translate(key);
        AutomationProperties.SetName(DetailsToggleButton, label);
        ToolTipService.SetToolTip(DetailsToggleButton, label);
    }

    private static string FormatSpanLabels(IReadOnlyList<ActivityLabelSample>? labels, CultureInfo culture) =>
        labels is not { Count: > 0 }
            ? "--"
            : string.Join("  ·  ", labels.Select(label => $"{label.SampledAt.ToLocalTime().ToString("t", culture)} {label.Label}"));

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

    private void MoreMenu_Opened(object sender, object e)
    {
        if (TitleBarMoreButton.Flyout is Flyout flyout && flyout.Content is DependencyObject content)
        {
            UiLocalization.Apply(content, _strings);
        }

        ApplyMenuCommandLabel(SaveScreenshotMenuItem, "Screenshots.Menu.Save");
        ApplyMenuCommandLabel(ShareScreenshotMenuItem, "Screenshots.Menu.Share");
        ApplyMenuCommandLabel(OpenScreenshotFolderMenuItem, "Screenshots.Menu.OpenFolder");
        ApplyMenuCommandLabel(DeleteScreenshotMenuItem, "Screenshots.Menu.DeleteScreenshot");
        ApplyMenuCommandLabel(DeleteSnapshotMenuItem, "Screenshots.Menu.DeleteSnapshot");

        var hasSelection = _items.Count > 0;
        SaveScreenshotMenuItem.IsEnabled = hasSelection;
        ShareScreenshotMenuItem.IsEnabled = hasSelection;
        DeleteScreenshotMenuItem.IsEnabled = hasSelection;
        DeleteSnapshotMenuItem.IsEnabled = hasSelection;
    }

    private void TitleBarMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (TitleBarMoreButton.Flyout is Flyout flyout)
        {
            flyout.ShowAt(TitleBarMoreButton);
        }
    }

    private void ApplyMenuCommandLabel(Button button, string key) =>
        AutomationProperties.SetName(button, _strings.Translate(key));

    private void TitleBarDragRegion_Loaded(object sender, RoutedEventArgs e) => UpdateTitleBarLayout();

    private void TitleBarDragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarLayout();

    private void UpdateTitleBarLayout()
    {
        if (!ExtendsContentIntoTitleBar || TitleBarDragRegion.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var scale = xamlRoot.RasterizationScale;
        InputNonClientPointerSource
            .GetForWindowId(_appWindow.Id)
            .SetRegionRects(
                NonClientRegionKind.Passthrough,
                [ElementRect(DetailsToggleButton, scale), ElementRect(TitleBarMoreButton, scale)]);
    }

    private static RectInt32 ElementRect(FrameworkElement element, double scale)
    {
        var transform = element.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new RectInt32(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale),
            (int)Math.Round(bounds.Width * scale),
            (int)Math.Round(bounds.Height * scale));
    }

    private async void DeleteScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItem();
        var result = await _application.DeleteScreenshotAsync(selected.Path, _lifetimeCancellation.Token);
        if (result.Succeeded)
        {
            await LoadGalleryAsync(_selectedDate);
        }

        ShowActionResult(result, "Screenshots.Action.ScreenshotDeleted");
    }

    private async void DeleteSnapshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItem();
        var result = await _application.DeleteSnapshotAsync(selected.Path, _lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.SnapshotDeleted");
    }

    private async void SaveScreenshotMenuItem_Click(object sender, RoutedEventArgs e) =>
        await SaveSelectedScreenshotAsync();

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
        picker.FileTypeChoices.Add("Screenshot", new[] { extension });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var destination = await picker.PickSaveFileAsync();
        if (destination is null)
        {
            return;
        }

        var result = await _application.SaveScreenshotAsync(selected.Path, destination.Path, _lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.Saved");
    }

    private async void OpenScreenshotFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var result = await _application.OpenScreenshotFolderAsync(_lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.FolderOpened");
    }

    private async void ShareScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItem();
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64();
        var result = await _application.ShareScreenshotAsync(selected.Path, windowHandle, _lifetimeCancellation.Token);
        ShowActionResult(result, "Screenshots.Action.ShareOpened");
    }

    private void ShowActionResult<T>(OperationResult<T> result, string successKey)
    {
        ScreenshotActionInfoBar.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ScreenshotActionInfoBar.Message = _strings.Translate(result.Succeeded ? successKey : "Screenshots.Action.Failed");
        ScreenshotActionInfoBar.IsOpen = true;
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
        AutomationProperties.SetName(FilmstripToggleButton, isExpanded ? "Hide screenshot strip" : "Show screenshot strip");
    }

    private void SetLoading(bool isLoading)
    {
        GalleryProgressRing.IsActive = isLoading;
        GalleryProgressRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectedDatePicker.IsEnabled = !isLoading;
    }

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

        UpdateTitleBarLayout();
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

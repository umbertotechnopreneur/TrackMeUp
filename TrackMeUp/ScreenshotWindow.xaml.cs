using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using TrackMeUp.Application;
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
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly string? _launchTheme;
    private CancellationTokenSource? _galleryCancellation;
    private IReadOnlyList<ScreenshotGalleryItem> _items = Array.Empty<ScreenshotGalleryItem>();
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private LocalizationService _strings = new("system");
    private XamlRoot? _xamlRoot;
    private double _rasterizationScale = 1d;
    private string _theme = "system";
    private bool _initialized;
    private bool _windowStateRestored;
    private bool _settingSelectedDate;
    private string? _requestedScreenshotPath;

    private CalendarDatePicker SelectedDatePicker => HeaderSection.DatePicker;

    private TextBlock GalleryCountText => HeaderSection.CountText;

    private Grid GallerySurface => GallerySection.Surface;

    private Grid CoverFlowPanel => GallerySection.CoverFlow;

    private Border GalleryImageFrame => GallerySection.CurrentFrame;

    private Border PreviousPreviewFrame => GallerySection.PreviousFrame;

    private Border NextPreviewFrame => GallerySection.NextFrame;

    private Image CurrentImage => GallerySection.CurrentGalleryImage;

    private Image PreviousImage => GallerySection.PreviousGalleryImage;

    private Image NextImage => GallerySection.NextGalleryImage;

    private Button PreviousButton => GallerySection.PreviousNavigationButton;

    private Button NextButton => GallerySection.NextNavigationButton;

    private Border MetadataPanel => GallerySection.MetadataContainer;

    private TextBlock MetadataDateValueText => GallerySection.MetadataDateText;

    private TextBlock MetadataTimeValueText => GallerySection.MetadataTimeText;

    private TextBlock MetadataAppValueText => GallerySection.MetadataApplicationText;

    private TextBlock MetadataOriginValueText => GallerySection.MetadataOriginText;

    private Grid EmptyGalleryPanel => GallerySection.EmptyPanel;

    private TextBlock EmptyGalleryText => GallerySection.EmptyText;

    private ProgressRing GalleryProgressRing => GallerySection.LoadingRing;

    private StackPanel FilmstripStrip => TimelineSection.TimelineRoot;

    private ScrollViewer FilmstripPanelHost => TimelineSection.FilmstripHost;

    private StackPanel FilmstripPanel => TimelineSection.ItemsHost;

    private Button FilmstripToggleButton => TimelineSection.ToggleButton;

    private FontIcon FilmstripChevronIcon => TimelineSection.ToggleChevronIcon;

    /// <summary>Creates the Mica screenshot inspector backed by the shared application facade.</summary>
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
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        SetSelectedDate(_selectedDate);
        ApplyTheme(_theme);
        ResizeForLogicalContent();
        UpdateTitleBarLayout();
        Closed += ScreenshotWindow_Closed;
    }

    private void WireViewEvents()
    {
        SelectedDatePicker.DateChanged += SelectedDatePicker_DateChanged;
        PreviousButton.Click += PreviousButton_Click;
        NextButton.Click += NextButton_Click;
        FilmstripToggleButton.Click += FilmstripToggleButton_Click;
        GallerySurface.PointerEntered += GallerySurface_PointerEntered;
        GallerySurface.PointerExited += GallerySurface_PointerExited;
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

    /// <summary>Reselects the gallery to the current day when the menu reopens an existing window.</summary>
    public async Task FocusTodayAsync()
    {
        _requestedScreenshotPath = null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        SetSelectedDate(today);
        if (_initialized)
        {
            await LoadGalleryAsync(today);
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForLogicalContent();
        UpdateTitleBarLayout();
        if (!_windowStateRestored)
        {
            _windowStateRestored = true;
            var windowState = await _application.RestoreWindowStateAsync(WindowStateKeys.Screenshots, WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64(), _lifetimeCancellation.Token);
            if (!windowState.Succeeded)
            {
                throw new InvalidOperationException($"Window state could not be restored ({windowState.Code}).");
            }
        }

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

        await LoadGalleryAsync(_selectedDate);
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

    private async Task LoadGalleryAsync(DateOnly date)
    {
        _galleryCancellation?.Cancel();
        _galleryCancellation?.Dispose();
        _galleryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        var cancellationToken = _galleryCancellation.Token;
        SetLoading(true);

        try
        {
            var result = await _application.GetScreenshotGalleryAsync(date, cancellationToken);
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
        CoverFlowPanel.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        FilmstripStrip.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyGalleryText.Text = error ?? (_items.Count == 0 ? "No screenshots for this day." : string.Empty);

        _selectedIndex = hasItems ? Math.Clamp(_selectedIndex, 0, _items.Count - 1) : 0;
        RenderSelection();
    }

    private int _selectedIndex;

    private void RenderSelection()
    {
        FilmstripPanel.Children.Clear();
        for (var index = 0; index < _items.Count; index++)
        {
            FilmstripPanel.Children.Add(CreateFilmstripButton(_items[index], index));
        }

        var current = _items.Count == 0 ? null : _items[_selectedIndex];
        var previous = _items.Count > 1 ? _items[(_selectedIndex - 1 + _items.Count) % _items.Count] : null;
        var next = _items.Count > 1 ? _items[(_selectedIndex + 1) % _items.Count] : null;
        SetImage(CurrentImage, current);
        SetImage(PreviousImage, previous);
        SetImage(NextImage, next);
        GalleryImageFrame.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
        PreviousPreviewFrame.Visibility = previous is null ? Visibility.Collapsed : Visibility.Visible;
        NextPreviewFrame.Visibility = next is null ? Visibility.Collapsed : Visibility.Visible;

        PreviousButton.IsEnabled = _items.Count > 1;
        NextButton.IsEnabled = _items.Count > 1;
        SetNavigationOpacity(1);
        RenderMetadata(current);
    }

    private void RenderMetadata(ScreenshotGalleryItem? item)
    {
        if (item is null)
        {
            MetadataDateValueText.Text = "--";
            MetadataTimeValueText.Text = "--";
            MetadataAppValueText.Text = "--";
            MetadataOriginValueText.Text = "--";
            MetadataPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var culture = CultureInfo.GetCultureInfo(_strings.Language);
        var localTime = item.CapturedAt.ToLocalTime();
        MetadataDateValueText.Text = localTime.ToString("D", culture);
        MetadataTimeValueText.Text = localTime.ToString("T", culture);
        MetadataAppValueText.Text = string.IsNullOrWhiteSpace(item.ForegroundApplication) ? "Desktop" : item.ForegroundApplication;
        MetadataOriginValueText.Text = FormatCaptureOrigin(item.CaptureOrigin);
        MetadataPanel.Visibility = Visibility.Visible;
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

    private void MoreMenu_Opened(object sender, object e)
    {
        if (TitleBarMoreButton.Flyout is Flyout flyout && flyout.Content is DependencyObject content)
        {
            UiLocalization.Apply(content, _strings);
        }

        var hasSelection = _items.Count > 0;
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
            .SetRegionRects(NonClientRegionKind.Passthrough, [ElementRect(TitleBarMoreButton, scale)]);
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
    }

    private async void DeleteSnapshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItem();
        await _application.DeleteSnapshotAsync(selected.Path, _lifetimeCancellation.Token);
    }

    private async void SaveScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
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

        await _application.SaveScreenshotAsync(selected.Path, destination.Path, _lifetimeCancellation.Token);
    }

    private async void OpenScreenshotFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = GetSelectedItem();
        await _application.OpenScreenshotFolderAsync(_lifetimeCancellation.Token);
    }

    private async void ShareScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItem();
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64();
        await _application.ShareScreenshotAsync(selected.Path, windowHandle, _lifetimeCancellation.Token);
    }

    private ScreenshotGalleryItem GetSelectedItem()
        => _items.Count == 0
            ? throw new InvalidOperationException("Screenshot action requested without a selected capture.")
            : _items[_selectedIndex];

    private Button CreateFilmstripButton(ScreenshotGalleryItem item, int index)
    {
        var localTime = item.CapturedAt.ToLocalTime();
        var culture = CultureInfo.GetCultureInfo(_strings.Language);
        var thumbnail = new Image
        {
            Width = 124,
            Height = 76,
            Stretch = Stretch.UniformToFill
        };
        SetImage(thumbnail, item);

        var preview = new Border
        {
            Width = 128,
            Height = 80,
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(index == _selectedIndex ? 2 : 1),
            BorderBrush = new SolidColorBrush(index == _selectedIndex ? Colors.DodgerBlue : Colors.Transparent),
            Child = thumbnail
        };
        var metadata = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        metadata.Children.Add(new TextBlock
        {
            Text = localTime.ToString("d MMM", culture),
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.Gray)
        });
        metadata.Children.Add(new TextBlock
        {
            Text = localTime.ToString("HH:mm", culture),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(preview);
        content.Children.Add(metadata);
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Tag = index
        };
        AutomationProperties.SetName(button, $"Screenshot {index + 1}");
        button.Click += FilmstripButton_Click;
        return button;
    }

    private void FilmstripButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index } && index >= 0 && index < _items.Count)
        {
            _selectedIndex = index;
            RenderSelection();
        }
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count > 1)
        {
            _selectedIndex = (_selectedIndex - 1 + _items.Count) % _items.Count;
            RenderSelection();
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count > 1)
        {
            _selectedIndex = (_selectedIndex + 1) % _items.Count;
            RenderSelection();
        }
    }

    private void FilmstripToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var isExpanded = FilmstripPanelHost.Visibility != Visibility.Visible;
        FilmstripPanelHost.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        FilmstripChevronIcon.Glyph = isExpanded ? "\uE70E" : "\uE70D";
        AutomationProperties.SetName(FilmstripToggleButton, isExpanded ? "Hide screenshot strip" : "Show screenshot strip");
    }

    private void GallerySurface_PointerEntered(object sender, PointerRoutedEventArgs e) => SetNavigationOpacity(1);

    private void GallerySurface_PointerExited(object sender, PointerRoutedEventArgs e) => SetNavigationOpacity(1);

    private void SetNavigationOpacity(double opacity)
    {
        var effectiveOpacity = _items.Count > 1 ? opacity : 0;
        PreviousButton.Opacity = effectiveOpacity;
        NextButton.Opacity = effectiveOpacity;
    }

    private static void SetImage(Image target, ScreenshotGalleryItem? item)
    {
        target.Source = null;
        if (item is null)
        {
            target.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            target.Source = new BitmapImage(new Uri(item.Path, UriKind.Absolute));
            target.Visibility = Visibility.Visible;
        }
        catch (UriFormatException)
        {
            target.Visibility = Visibility.Collapsed;
        }
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
        if (Math.Abs(sender.RasterizationScale - _rasterizationScale) >= 0.001d)
        {
            ResizeForLogicalContent();
        }

        UpdateTitleBarLayout();
    }

    private void ResizeForLogicalContent()
    {
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? _rasterizationScale);
        _rasterizationScale = scale;
        var workArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var physicalMargin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var physicalWidth = Math.Min(Math.Max(1, workArea.Width - (physicalMargin * 2)), (int)Math.Ceiling(LogicalWindowWidth * scale));
        var physicalHeight = Math.Min(Math.Max(1, workArea.Height - (physicalMargin * 2)), (int)Math.Ceiling(LogicalWindowHeight * scale));
        _appWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
    }

    private async void ScreenshotWindow_Closed(object sender, WindowEventArgs args)
    {
        var windowState = await _application.SaveWindowStateAsync(WindowStateKeys.Screenshots, WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64(), CancellationToken.None);
        if (!windowState.Succeeded)
        {
            throw new InvalidOperationException($"Window state could not be saved ({windowState.Code}).");
        }

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

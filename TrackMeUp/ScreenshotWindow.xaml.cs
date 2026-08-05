using Microsoft.UI;
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
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        SetSelectedDate(_selectedDate);
        ApplyTheme(_theme);
        ResizeForLogicalContent();
        Closed += ScreenshotWindow_Closed;
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

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForLogicalContent();
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
        FilmstripCountText.Text = _items.Count.ToString(CultureInfo.InvariantCulture);
        EmptyGalleryPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
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

        if (current is null)
        {
            CaptureDateText.Text = "—";
            CaptureTimeText.Text = "—";
            CaptureApplicationText.Text = "—";
            CaptureOriginText.Text = "—";
        }
        else
        {
            var localTime = current.CapturedAt.ToLocalTime();
            var culture = CultureInfo.GetCultureInfo(_strings.Language);
            CaptureDateText.Text = localTime.ToString("d MMM yyyy", culture);
            CaptureTimeText.Text = localTime.ToString("HH:mm:ss", culture);
            CaptureApplicationText.Text = string.IsNullOrWhiteSpace(current.ForegroundApplication) ? "Desktop" : current.ForegroundApplication;
            CaptureOriginText.Text = FormatCaptureOrigin(current.CaptureOrigin);
        }

        PreviousButton.IsEnabled = _items.Count > 1;
        NextButton.IsEnabled = _items.Count > 1;
        SetNavigationOpacity(1);
    }

    private void ScreenshotMenu_Opening(object sender, object e)
    {
        SaveScreenshotMenuItem.Text = _strings.Translate("Screenshots.Menu.Save");
        OpenScreenshotFolderMenuItem.Text = _strings.Translate("Screenshots.Menu.OpenFolder");
        ShareScreenshotMenuItem.Text = _strings.Translate("Screenshots.Menu.Share");
        var hasSelection = _items.Count > 0;
        SaveScreenshotMenuItem.IsEnabled = hasSelection;
        OpenScreenshotFolderMenuItem.IsEnabled = hasSelection;
        ShareScreenshotMenuItem.IsEnabled = hasSelection;
    }

    private async void SaveScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItem();
        var extension = Path.GetExtension(selected.Path);
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
        var thumbnail = new Image
        {
            Width = 116,
            Height = 72,
            Stretch = Stretch.UniformToFill
        };
        SetImage(thumbnail, item);

        var border = new Border
        {
            Width = 122,
            Height = 78,
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(index == _selectedIndex ? 2 : 1),
            BorderBrush = new SolidColorBrush(index == _selectedIndex ? Colors.DodgerBlue : Colors.Transparent),
            Child = thumbnail
        };
        var button = new Button
        {
            Content = border,
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

    private string FormatCaptureOrigin(string origin) => origin switch
    {
        ScreenshotCaptureOrigins.Manual => _strings.Language == "it" ? "Manuale" : "Manual",
        ScreenshotCaptureOrigins.Scheduled => _strings.Language == "it" ? "Pianificato" : "Scheduled",
        _ => throw new InvalidDataException($"Unsupported screenshot capture origin: {origin}")
    };

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

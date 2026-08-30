// SPDX-License-Identifier: MIT

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace TrackMeUp;

/// <summary>Owns the native/XAML contract shared by TrackMeUp custom title bars.</summary>
internal sealed class CustomTitleBarController : IDisposable
{
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly FrameworkElement _root;
    private readonly FrameworkElement _dragRegion;
    private readonly ColumnDefinition _leftInsetColumn;
    private readonly ColumnDefinition _rightInsetColumn;
    private readonly Func<IEnumerable<FrameworkElement>> _interactiveElements;
    private XamlRoot? _xamlRoot;
    private bool _layoutUpdateQueued;
    private bool _disposed;

    internal CustomTitleBarController(
        Window window,
        AppWindow appWindow,
        FrameworkElement root,
        FrameworkElement dragRegion,
        ColumnDefinition leftInsetColumn,
        ColumnDefinition rightInsetColumn,
        Func<IEnumerable<FrameworkElement>> interactiveElements,
        bool useTallTitleBar = true)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _appWindow = appWindow ?? throw new ArgumentNullException(nameof(appWindow));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _dragRegion = dragRegion ?? throw new ArgumentNullException(nameof(dragRegion));
        _leftInsetColumn = leftInsetColumn ?? throw new ArgumentNullException(nameof(leftInsetColumn));
        _rightInsetColumn = rightInsetColumn ?? throw new ArgumentNullException(nameof(rightInsetColumn));
        _interactiveElements = interactiveElements ?? throw new ArgumentNullException(nameof(interactiveElements));

        _window.ExtendsContentIntoTitleBar = true;
        _window.SetTitleBar(_dragRegion);
        if (useTallTitleBar && AppWindowTitleBar.IsCustomizationSupported())
        {
            // Tall is the native 48-DIP caption height used by every TrackMeUp title-bar grid.
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }

        _root.ActualThemeChanged += Root_ActualThemeChanged;
        _dragRegion.Loaded += DragRegion_Loaded;
        _dragRegion.SizeChanged += DragRegion_SizeChanged;
        ApplyTheme(_root.ActualTheme);
    }

    /// <summary>Occurs after native caption colors have followed a XAML theme change.</summary>
    internal event Action<ElementTheme>? ThemeChanged;

    /// <summary>Applies the shared light, dark, or high-contrast native caption palette.</summary>
    internal void ApplyTheme(ElementTheme effectiveTheme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var palette = ResolvePalette(effectiveTheme);
        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.InactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = palette.Foreground;
        titleBar.ButtonInactiveForegroundColor = palette.InactiveForeground;
        titleBar.ButtonHoverForegroundColor = palette.HoverForeground;
        titleBar.ButtonPressedForegroundColor = palette.PressedForeground;
        titleBar.ButtonHoverBackgroundColor = palette.HoverBackground;
        titleBar.ButtonPressedBackgroundColor = palette.PressedBackground;
    }

    /// <summary>Queues one post-layout refresh of caption insets and interactive regions.</summary>
    internal void QueueLayoutUpdate()
    {
        if (_disposed || _layoutUpdateQueued)
        {
            return;
        }

        _layoutUpdateQueued = true;
        if (!_root.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                _layoutUpdateQueued = false;
                if (_disposed || !_root.IsLoaded)
                {
                    return;
                }

                // Flush pending XAML layout before deriving window-relative physical rectangles.
                _root.UpdateLayout();
                UpdateLayout();
            }))
        {
            _layoutUpdateQueued = false;
        }
    }

    private void DragRegion_Loaded(object sender, RoutedEventArgs e)
    {
        AttachXamlRoot(_dragRegion.XamlRoot);
        UpdateLayout();
    }

    private void DragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateLayout();

    private void Root_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyTheme(sender.ActualTheme);
        ThemeChanged?.Invoke(sender.ActualTheme);
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) => QueueLayoutUpdate();

    private void AttachXamlRoot(XamlRoot? xamlRoot)
    {
        if (ReferenceEquals(_xamlRoot, xamlRoot))
        {
            return;
        }

        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        _xamlRoot = xamlRoot;
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed += XamlRoot_Changed;
        }
    }

    private void UpdateLayout()
    {
        if (_disposed || !_window.ExtendsContentIntoTitleBar || _dragRegion.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        AttachXamlRoot(xamlRoot);
        var scale = Math.Max(0.1d, xamlRoot.RasterizationScale);
        _leftInsetColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scale);
        _rightInsetColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scale);

        var passthroughRects = _interactiveElements()
            .Where(static element =>
                element.Visibility == Visibility.Visible
                && element.IsHitTestVisible
                && element.ActualWidth > 0d
                && element.ActualHeight > 0d)
            .Distinct()
            .Select(element => ElementRect(element, scale))
            .ToArray();

        // The OS retains dragging and caption buttons; only explicit XAML commands pass through.
        InputNonClientPointerSource
            .GetForWindowId(_appWindow.Id)
            .SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects);
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

    private static TitleBarPalette ResolvePalette(ElementTheme effectiveTheme)
    {
        if (new AccessibilitySettings().HighContrast)
        {
            var settings = new UISettings();
            var foreground = settings.GetColorValue(UIColorType.Foreground);
            var background = settings.GetColorValue(UIColorType.Background);
            var accent = settings.GetColorValue(UIColorType.Accent);
            return new TitleBarPalette(foreground, foreground, background, background, accent, accent);
        }

        if (effectiveTheme == ElementTheme.Dark)
        {
            return new TitleBarPalette(
                Colors.White,
                Color.FromArgb(160, 255, 255, 255),
                Colors.White,
                Colors.White,
                Color.FromArgb(32, 255, 255, 255),
                Color.FromArgb(48, 255, 255, 255));
        }

        return new TitleBarPalette(
            Colors.Black,
            Color.FromArgb(160, 0, 0, 0),
            Colors.Black,
            Colors.Black,
            Color.FromArgb(24, 0, 0, 0),
            Color.FromArgb(40, 0, 0, 0));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _root.ActualThemeChanged -= Root_ActualThemeChanged;
        _dragRegion.Loaded -= DragRegion_Loaded;
        _dragRegion.SizeChanged -= DragRegion_SizeChanged;
        AttachXamlRoot(null);
        ThemeChanged = null;
    }

    private sealed record TitleBarPalette(
        Color Foreground,
        Color InactiveForeground,
        Color HoverForeground,
        Color PressedForeground,
        Color HoverBackground,
        Color PressedBackground);
}

// SPDX-License-Identifier: MIT

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using TrackMeUp.Application;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Applies default bounds and delegates persisted placement to the shared application facade.</summary>
internal sealed class WindowPlacementService : IDisposable
{
    private const uint WmGetMinMaxInfo = 0x0024;
    private static int s_nextSubclassId;
    private readonly ITrackMeUpApplication _application;
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly IntPtr _windowHandle;
    private readonly string _windowKey;
    private readonly int _logicalDefaultWidth;
    private readonly int _logicalDefaultHeight;
    private readonly int _logicalScreenMargin;
    private readonly WindowMinimumSize _logicalMinimumSize;
    private readonly WindowId _displayAnchorId;
    private readonly NativeWindowSubclassProc _subclassProc;
    private readonly nuint _subclassId;
    private bool _restoreAttempted;
    private bool _subclassInstalled;
    private bool _disposed;
    private SizeInt32 _minimumPhysicalSize = new(1, 1);
    private double _rasterizationScale = 1d;

    internal WindowPlacementService(
        ITrackMeUpApplication application,
        Window window,
        AppWindow appWindow,
        string windowKey,
        int logicalDefaultWidth,
        int logicalDefaultHeight,
        int logicalScreenMargin,
        WindowId? displayAnchorId = null)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _appWindow = appWindow ?? throw new ArgumentNullException(nameof(appWindow));
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _windowKey = string.IsNullOrWhiteSpace(windowKey) ? throw new ArgumentException("A window key is required.", nameof(windowKey)) : windowKey;
        _logicalDefaultWidth = logicalDefaultWidth > 0 ? logicalDefaultWidth : throw new ArgumentOutOfRangeException(nameof(logicalDefaultWidth));
        _logicalDefaultHeight = logicalDefaultHeight > 0 ? logicalDefaultHeight : throw new ArgumentOutOfRangeException(nameof(logicalDefaultHeight));
        _logicalScreenMargin = logicalScreenMargin >= 0 ? logicalScreenMargin : throw new ArgumentOutOfRangeException(nameof(logicalScreenMargin));
        _logicalMinimumSize = WindowStateService.GetMinimumSize(_windowKey);
        _displayAnchorId = displayAnchorId ?? _appWindow.Id;
        _subclassProc = WindowSubclassProc;
        _subclassId = (nuint)Interlocked.Increment(ref s_nextSubclassId);
        InstallMinimumSizeSubclass();
    }

    internal double RasterizationScale => _rasterizationScale;

    /// <summary>Applies the requested default size without choosing a screen position.</summary>
    internal void ApplyDefaultSize(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var scale = ResolveScale(root);
        var area = OpeningWorkArea();
        var margin = (int)Math.Ceiling(_logicalScreenMargin * scale);
        var availableWidth = Math.Max(1, area.Width - (margin * 2));
        var availableHeight = Math.Max(1, area.Height - (margin * 2));
        var minimumSize = UpdateMinimumSize(scale, area);
        var width = Math.Min(availableWidth, Math.Max(minimumSize.Width, (int)Math.Ceiling(_logicalDefaultWidth * scale)));
        var height = Math.Min(availableHeight, Math.Max(minimumSize.Height, (int)Math.Ceiling(_logicalDefaultHeight * scale)));
        _appWindow.Resize(new SizeInt32(width, height));
    }

    internal void ApplyDefaultBounds(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var scale = ResolveScale(root);
        var area = OpeningWorkArea();
        var margin = (int)Math.Ceiling(_logicalScreenMargin * scale);
        var availableWidth = Math.Max(1, area.Width - (margin * 2));
        var availableHeight = Math.Max(1, area.Height - (margin * 2));
        var minimumSize = UpdateMinimumSize(scale, area);
        var width = Math.Min(availableWidth, Math.Max(minimumSize.Width, (int)Math.Ceiling(_logicalDefaultWidth * scale)));
        var height = Math.Min(availableHeight, Math.Max(minimumSize.Height, (int)Math.Ceiling(_logicalDefaultHeight * scale)));
        _appWindow.Resize(new SizeInt32(width, height));
        CenterInWorkArea(area);
    }

    /// <summary>Restores saved bounds without replacing the user's position with an application-selected anchor.</summary>
    internal async Task<bool> RestoreAsync(FrameworkElement root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_restoreAttempted)
        {
            return false;
        }

        _restoreAttempted = true;
        var result = await _application.RestoreWindowStateAsync(
            _windowKey,
            _windowHandle.ToInt64(),
            cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Window state could not be restored ({result.Code}).");
        }

        // Restored coordinates are authoritative; only clamp bounds that no longer fit the active display topology.
        KeepCurrentBoundsInWorkArea(root);
        return result.Value is not null;
    }

    internal async Task RestoreAndCenterAsync(
        FrameworkElement root,
        CancellationToken cancellationToken,
        bool centerOnCursorDisplay = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_restoreAttempted)
        {
            return;
        }

        _restoreAttempted = true;
        var result = await _application.RestoreWindowStateAsync(
            _windowKey,
            _windowHandle.ToInt64(),
            cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Window state could not be restored ({result.Code}).");
        }

        var area = centerOnCursorDisplay ? CursorWorkArea() : OpeningWorkArea();
        KeepCurrentBoundsInWorkArea(root, area);
        CenterInWorkArea(area);
    }

    internal void ResizeAndCenterOnCursorDisplay(FrameworkElement root, double widthRatio, double heightRatio)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (widthRatio is <= 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(widthRatio));
        }

        if (heightRatio is <= 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(heightRatio));
        }

        // Re-resolve the pointer display for every activation so monitor choice and DPI follow the user's current context.
        var area = CursorWorkArea();
        var scale = ResolveScale(root);
        var margin = (int)Math.Ceiling(_logicalScreenMargin * scale);
        var availableWidth = Math.Max(1, area.Width - (margin * 2));
        var availableHeight = Math.Max(1, area.Height - (margin * 2));
        var minimumSize = UpdateMinimumSize(scale, area);
        var width = Math.Clamp((int)Math.Round(area.Width * widthRatio), minimumSize.Width, availableWidth);
        var height = Math.Clamp((int)Math.Round(area.Height * heightRatio), minimumSize.Height, availableHeight);
        _appWindow.Resize(new SizeInt32(width, height));
        CenterInWorkArea(area);
    }

    internal void ResizeAndCenterOnCursorDisplay(
        FrameworkElement root,
        double widthRatio,
        int maximumLogicalWidth,
        int logicalHeight,
        double maximumHeightRatio)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (widthRatio is <= 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(widthRatio));
        }

        if (logicalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalHeight));
        }

        if (maximumLogicalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLogicalWidth));
        }

        if (maximumHeightRatio is <= 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeightRatio));
        }

        // Content chooses the desired logical height; the cursor display supplies DPI and a hard work-area ceiling.
        var area = CursorWorkArea();
        var scale = ResolveScale(root);
        var margin = (int)Math.Ceiling(_logicalScreenMargin * scale);
        var availableWidth = Math.Max(1, area.Width - (margin * 2));
        var availableHeight = Math.Max(1, area.Height - (margin * 2));
        var minimumSize = UpdateMinimumSize(scale, area);
        var maximumWidth = Math.Clamp(
            (int)Math.Ceiling(maximumLogicalWidth * scale),
            minimumSize.Width,
            availableWidth);
        var width = Math.Clamp(
            Math.Min((int)Math.Round(area.Width * widthRatio), maximumWidth),
            minimumSize.Width,
            availableWidth);
        var maximumHeight = Math.Clamp(
            (int)Math.Round(area.Height * maximumHeightRatio),
            minimumSize.Height,
            availableHeight);
        var height = Math.Clamp(
            (int)Math.Ceiling(logicalHeight * scale),
            minimumSize.Height,
            maximumHeight);
        _appWindow.Resize(new SizeInt32(width, height));
        CenterInWorkArea(area);
    }

    internal async Task SaveAsync(CancellationToken cancellationToken)
    {
        var result = await _application.SaveWindowStateAsync(
            _windowKey,
            _windowHandle.ToInt64(),
            cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Window state could not be saved ({result.Code}).");
        }
    }

    /// <summary>Saves placement during close without allowing a persistence failure to escape an event callback.</summary>
    internal async Task<bool> TrySaveForCloseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            // A window that is already closing cannot present a reliable error surface. Trace the failure and
            // let its deterministic cleanup continue; the next open uses the last valid persisted placement.
            Trace.TraceError(
                "Window placement save failed during close. WindowKey={0} ExceptionType={1} Message={2}",
                _windowKey,
                exception.GetType().Name,
                exception.Message);
            return false;
        }
    }

    internal void KeepCurrentBoundsInWorkArea(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        KeepCurrentBoundsInWorkArea(root, area);
    }

    private void KeepCurrentBoundsInWorkArea(FrameworkElement root, RectInt32 area)
    {
        var scale = ResolveScale(root);
        var margin = (int)Math.Ceiling(_logicalScreenMargin * scale);
        var availableWidth = Math.Max(1, area.Width - (margin * 2));
        var availableHeight = Math.Max(1, area.Height - (margin * 2));
        var minimumSize = UpdateMinimumSize(scale, area);
        var width = Math.Clamp(Math.Max(1, _appWindow.Size.Width), minimumSize.Width, availableWidth);
        var height = Math.Clamp(Math.Max(1, _appWindow.Size.Height), minimumSize.Height, availableHeight);
        if (width != _appWindow.Size.Width || height != _appWindow.Size.Height)
        {
            _appWindow.Resize(new SizeInt32(width, height));
        }

        var left = area.X + margin;
        var top = area.Y + margin;
        var right = Math.Max(left, area.X + area.Width - margin - width);
        var bottom = Math.Max(top, area.Y + area.Height - margin - height);
        var x = Math.Clamp(_appWindow.Position.X, left, right);
        var y = Math.Clamp(_appWindow.Position.Y, top, bottom);
        if (x != _appWindow.Position.X || y != _appWindow.Position.Y)
        {
            _appWindow.Move(new PointInt32(x, y));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_subclassInstalled)
        {
            _ = RemoveWindowSubclass(_windowHandle, _subclassProc, _subclassId);
            _subclassInstalled = false;
        }
    }

    private double ResolveScale(FrameworkElement root)
    {
        var scale = Math.Max(0.1d, root.XamlRoot?.RasterizationScale ?? _rasterizationScale);
        _rasterizationScale = scale;
        return scale;
    }

    private SizeInt32 UpdateMinimumSize(double scale, RectInt32 workArea)
    {
        var margin = (int)Math.Ceiling(_logicalScreenMargin * scale);
        var availableWidth = Math.Max(1, workArea.Width - (margin * 2));
        var availableHeight = Math.Max(1, workArea.Height - (margin * 2));
        var width = Math.Min(availableWidth, Math.Max(1, (int)Math.Ceiling(_logicalMinimumSize.Width * scale)));
        var height = Math.Min(availableHeight, Math.Max(1, (int)Math.Ceiling(_logicalMinimumSize.Height * scale)));
        _minimumPhysicalSize = new SizeInt32(width, height);
        return _minimumPhysicalSize;
    }

    private void CenterInWorkArea(RectInt32 workArea)
    {
        _appWindow.Move(new PointInt32(
            workArea.X + Math.Max(0, (workArea.Width - _appWindow.Size.Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - _appWindow.Size.Height) / 2)));
    }

    private RectInt32 OpeningWorkArea() =>
        DisplayArea.GetFromWindowId(_displayAnchorId, DisplayAreaFallback.Primary).WorkArea;

    private static RectInt32 CursorWorkArea()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not locate the pointer display.");
        }

        // The cursor point is resolved before the window is activated, so multi-monitor launches follow the user's current display.
        return DisplayArea.GetFromPoint(
            new PointInt32(cursorPosition.X, cursorPosition.Y),
            DisplayAreaFallback.Primary).WorkArea;
    }

    private void InstallMinimumSizeSubclass()
    {
        if (_subclassInstalled)
        {
            return;
        }

        if (!SetWindowSubclass(_windowHandle, _subclassProc, _subclassId, 0))
        {
            throw new InvalidOperationException($"Unable to install minimum window size constraint (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        _subclassInstalled = true;
    }

    private IntPtr WindowSubclassProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam, nuint subclassId, nuint referenceData)
    {
        if (message == WmGetMinMaxInfo)
        {
            var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            minMaxInfo.MinTrackSize.X = Math.Max(minMaxInfo.MinTrackSize.X, _minimumPhysicalSize.Width);
            minMaxInfo.MinTrackSize.Y = Math.Max(minMaxInfo.MinTrackSize.Y, _minimumPhysicalSize.Height);
            Marshal.StructureToPtr(minMaxInfo, lParam, false);
            return IntPtr.Zero;
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private delegate IntPtr NativeWindowSubclassProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam, nuint subclassId, nuint referenceData);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [DllImport("Comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, NativeWindowSubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("Comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, NativeWindowSubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
}

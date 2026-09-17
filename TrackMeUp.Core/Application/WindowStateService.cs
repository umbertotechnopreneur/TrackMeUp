// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using TrackMeUp.Services;

namespace TrackMeUp.Application;

/// <summary>Stable identifiers for the user-facing top-level windows.</summary>
public static class WindowStateKeys
{
    /// <summary>Identifies the main player window.</summary>
    public const string Main = "main";

    /// <summary>Identifies the world-clock city picker dialog.</summary>
    public const string WorldClockCityPicker = "world-clock-city-picker";

    /// <summary>Identifies the independent world-clock comparison window.</summary>
    public const string WorldClocks = "world-clocks";

    /// <summary>Identifies the independent geographical day-and-night map window.</summary>
    public const string WorldMap = "world-map";

    /// <summary>Identifies the independent lunar-phase window.</summary>
    public const string LunarPhase = "lunar-phase";

    /// <summary>Identifies the reports window.</summary>
    public const string Reports = "reports";

    /// <summary>Identifies the native activity-calendar dialog window.</summary>
    public const string ActivityCalendar = "activity-calendar";

    /// <summary>Identifies the historical screenshot AI-reprocessing dialog window.</summary>
    public const string AiScreenshotReprocessing = "ai-screenshot-reprocessing";

    /// <summary>Identifies the screenshot gallery window.</summary>
    public const string Screenshots = "screenshots";

    /// <summary>Identifies the selectable OCR text window.</summary>
    public const string OcrText = "ocr-text";

    /// <summary>Identifies the about window.</summary>
    public const string About = "about";

    /// <summary>Identifies the third-party licenses window.</summary>
    public const string Licenses = "licenses";

    /// <summary>Identifies the local search window.</summary>
    public const string Search = "search";

    /// <summary>Identifies the local search-index progress window.</summary>
    public const string SearchIndexing = "search-indexing";

    /// <summary>Identifies the screenshot schedule window.</summary>
    public const string Schedule = "schedule";

    /// <summary>Identifies the first-run and reusable Quick Setup window.</summary>
    public const string QuickSetup = "quick-setup";

    /// <summary>Identifies the reusable message dialog window.</summary>
    public const string Dialog = "dialog";

    /// <summary>Identifies the simplified AI pricing dialog window.</summary>
    public const string AiPricing = "ai-pricing";

    /// <summary>Identifies the AI provider connection test dialog window.</summary>
    public const string AiConnectionTest = "ai-connection-test";
}

/// <summary>Persists and restores native top-level window placement.</summary>
public sealed class WindowStateService
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint ShowNormal = 1;
    private const int ExtendedWindowStyle = -20;
    private const int ToolWindowStyle = 0x00000080;
    private readonly LocalStore _store;

    /// <summary>Creates the window-state service over the shared settings store.</summary>
    public WindowStateService(LocalStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Gets the minimum usable logical size for the supplied persisted window key.</summary>
    public static WindowMinimumSize GetMinimumSize(string windowKey)
    {
        if (string.IsNullOrWhiteSpace(windowKey))
        {
            throw new ArgumentException("A window key is required.", nameof(windowKey));
        }

        return windowKey switch
        {
            WindowStateKeys.Main => new(470, 240),
            WindowStateKeys.Reports => new(720, 520),
            WindowStateKeys.ActivityCalendar => new(760, 560),
            WindowStateKeys.AiScreenshotReprocessing => new(640, 560),
            WindowStateKeys.Screenshots => new(760, 540),
            WindowStateKeys.OcrText => new(560, 360),
            WindowStateKeys.About => new(900, 700),
            WindowStateKeys.Licenses => new(720, 520),
            WindowStateKeys.Search => new(780, 420),
            WindowStateKeys.SearchIndexing => new(560, 420),
            WindowStateKeys.Schedule => new(620, 480),
            WindowStateKeys.QuickSetup => new(760, 560),
            WindowStateKeys.Dialog => new(320, 196),
            WindowStateKeys.WorldClocks => new(480, 240),
            WindowStateKeys.WorldMap => new(192, 160),
            WindowStateKeys.LunarPhase => new(192, 192),
            WindowStateKeys.WorldClockCityPicker => new(500, 560),
            WindowStateKeys.AiPricing => new(620, 430),
            WindowStateKeys.AiConnectionTest => new(480, 480),
            _ => throw new ArgumentException("The window key is not supported.", nameof(windowKey))
        };
    }

    /// <summary>Persists one window's open or closed state without changing its saved placement.</summary>
    public AppSettings SetOpenState(string windowKey, bool isOpen)
    {
        _ = GetMinimumSize(windowKey);
        var settings = _store.LoadSettings();
        var openStates = settings.WindowOpenStates is null
            ? new Dictionary<string, bool>(StringComparer.Ordinal)
            : new Dictionary<string, bool>(settings.WindowOpenStates, StringComparer.Ordinal);
        openStates[windowKey] = isOpen;
        var updated = settings with { WindowOpenStates = openStates };
        // LocalStore writes atomically; persistence failures propagate and the facade retains its previous snapshot.
        _store.SaveSettings(updated);
        return updated;
    }

    internal static void ValidateOpenStates(IReadOnlyDictionary<string, bool>? openStates)
    {
        if (openStates is null) return;
        foreach (var key in openStates.Keys)
        {
            // Unsupported persisted keys are a contract error; never silently drop a saved window.
            _ = GetMinimumSize(key);
        }
    }

    /// <summary>Stores the screenshot identity for restoring the OCR text view from retained metadata.</summary>
    public AppSettings SetOcrTextSource(string screenshotPath, DateTimeOffset capturedAt)
    {
        var source = new OcrTextWindowSource(screenshotPath, capturedAt);
        ValidateOcrTextSource(source);
        var settings = _store.LoadSettings() with { OcrTextWindowSource = source };
        // Store only the source identity. Missing or deleted screenshots are resolved explicitly when the view reopens.
        _store.SaveSettings(settings);
        return settings;
    }

    internal static void ValidateOcrTextSource(OcrTextWindowSource? source)
    {
        if (source is null) return;
        if (string.IsNullOrWhiteSpace(source.ScreenshotPath) || !Path.IsPathFullyQualified(source.ScreenshotPath)
            || source.CapturedAt == default)
        {
            // Invalid context cannot be replaced with a different screenshot without changing what the user sees.
            throw new ArgumentException("The OCR text window requires an absolute screenshot path and its capture timestamp.");
        }
    }

    /// <summary>Persists native placement and returns its committed settings for the runtime snapshot.</summary>
    public (WindowState State, AppSettings Settings) Save(string windowKey, long windowHandle)
    {
        ValidateRequest(windowKey, windowHandle);
        var handle = new IntPtr(windowHandle);
        var monitor = GetMonitorForWindow(handle);
        var rect = GetNormalWindowRect(handle, monitor);
        var state = new WindowState(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, monitor.DeviceName);
        if (state.Width <= 0 || state.Height <= 0)
        {
            throw new InvalidOperationException("The native window returned invalid bounds.");
        }

        var settings = _store.LoadSettings();
        var placements = settings.WindowStates is null
            ? new Dictionary<string, WindowState>(StringComparer.Ordinal)
            : new Dictionary<string, WindowState>(settings.WindowStates, StringComparer.Ordinal);
        placements[windowKey] = state;
        var updated = settings with { WindowStates = placements };
        // A failed write propagates before the facade can publish these settings; no reread is needed after success.
        _store.SaveSettings(updated);
        return (state, updated);
    }

    /// <summary>Restores the saved placement and keeps it inside the selected monitor work area.</summary>
    public WindowState? Restore(string windowKey, long windowHandle)
    {
        ValidateRequest(windowKey, windowHandle);
        var settings = _store.LoadSettings();
        if (settings.WindowStates is null || !settings.WindowStates.TryGetValue(windowKey, out var savedState))
        {
            return null;
        }

        ValidatePersistedState(savedState);
        var handle = new IntPtr(windowHandle);
        var currentMonitor = GetMonitorForWindow(handle);
        var monitors = EnumerateMonitors();
        var targetMonitor = monitors.FirstOrDefault(candidate => candidate.DeviceName.Equals(savedState.MonitorDeviceName, StringComparison.OrdinalIgnoreCase)) ?? currentMonitor;
        // A removed or renamed monitor is an expected topology change; the documented fallback is the current monitor.
        var minimumSize = GetMinimumSize(windowKey);
        var safeState = WindowStateCalculator.ClampToWorkArea(
            savedState,
            targetMonitor.WorkArea,
            targetMonitor.DeviceName,
            minimumSize.Width,
            minimumSize.Height);
        // Geometry restoration must retain visibility, including a main window intentionally hidden in the notification area.
        if (!SetWindowPos(handle, IntPtr.Zero, safeState.X, safeState.Y, safeState.Width, safeState.Height, SetWindowPosNoActivate | SetWindowPosNoZOrder))
        {
            throw new InvalidOperationException($"Unable to restore window bounds (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return safeState;
    }

    private static void ValidateRequest(string windowKey, long windowHandle)
    {
        _ = GetMinimumSize(windowKey);

        if (windowHandle == 0)
        {
            throw new ArgumentException("A native window handle is required.", nameof(windowHandle));
        }
    }

    private static void ValidatePersistedState(WindowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Width <= 0 || state.Height <= 0 || string.IsNullOrWhiteSpace(state.MonitorDeviceName))
        {
            throw new InvalidOperationException("Persisted window state is invalid.");
        }
    }

    private static NativeMonitor GetMonitorForWindow(IntPtr handle)
    {
        var monitorHandle = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitorHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to identify the window monitor (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return ReadMonitor(monitorHandle);
    }

    private static IReadOnlyList<NativeMonitor> EnumerateMonitors()
    {
        var monitors = new List<NativeMonitor>();
        MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            monitors.Add(ReadMonitor(monitor));
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new InvalidOperationException($"Unable to enumerate display monitors (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return monitors;
    }

    private static NativeMonitor ReadMonitor(IntPtr monitorHandle)
    {
        var monitorInfo = new MonitorInfoEx { CbSize = (uint)Marshal.SizeOf<MonitorInfoEx>() };
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo) || string.IsNullOrWhiteSpace(monitorInfo.DeviceName))
        {
            throw new InvalidOperationException($"Unable to read monitor information (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        var work = monitorInfo.WorkArea;
        return new NativeMonitor(
            monitorInfo.DeviceName,
            new WindowWorkArea(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top),
            monitorInfo.MonitorArea);
    }

    private static NativeRect GetNormalWindowRect(IntPtr handle, NativeMonitor monitor)
    {
        var placement = new NativeWindowPlacement { Length = (uint)Marshal.SizeOf<NativeWindowPlacement>() };
        if (!GetWindowPlacement(handle, ref placement))
        {
            // A failed native read must not overwrite the previous valid placement with synthetic bounds.
            throw new InvalidOperationException($"Unable to read window placement (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        if (placement.ShowCommand == ShowNormal) return GetWindowRect(handle);

        Marshal.SetLastPInvokeError(0);
        var extendedStyle = GetWindowLong(handle, ExtendedWindowStyle);
        var error = Marshal.GetLastWin32Error();
        if (extendedStyle == 0 && error != 0)
        {
            throw new InvalidOperationException($"Unable to read window style (Win32 error {error}).");
        }

        var normal = placement.NormalPosition;
        if ((extendedStyle & ToolWindowStyle) == 0)
        {
            // WINDOWPLACEMENT uses workspace coordinates for ordinary top-level windows. Our stored bounds
            // and SetWindowPos use screen coordinates, so account for taskbars on the monitor's top or left.
            var offsetX = monitor.WorkArea.X - monitor.MonitorArea.Left;
            var offsetY = monitor.WorkArea.Y - monitor.MonitorArea.Top;
            normal.Left += offsetX;
            normal.Right += offsetX;
            normal.Top += offsetY;
            normal.Bottom += offsetY;
        }

        return normal;
    }

    private static NativeRect GetWindowRect(IntPtr handle)
    {
        if (!GetWindowRectNative(handle, out var rect))
        {
            throw new InvalidOperationException($"Unable to read window bounds (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return rect;
    }

    private sealed record NativeMonitor(string DeviceName, WindowWorkArea WorkArea, NativeRect MonitorArea);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPlacement
    {
        public uint Length;
        public uint Flags;
        public uint ShowCommand;
        public NativePoint MinimumPosition;
        public NativePoint MaximumPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint CbSize;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr monitorRect, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayMonitors", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll", EntryPoint = "MonitorFromWindow", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRectNative(IntPtr handle, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr handle, ref NativeWindowPlacement placement);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

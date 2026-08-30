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
    public const string AiConnectionTest = "ai-connection-test";
}

/// <summary>Persists and restores native top-level window placement.</summary>
public sealed class WindowStateService
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosShowWindow = 0x0040;
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
            WindowStateKeys.About => new(360, 420),
            WindowStateKeys.Licenses => new(720, 520),
            WindowStateKeys.Search => new(780, 140),
            WindowStateKeys.SearchIndexing => new(560, 420),
            WindowStateKeys.Schedule => new(620, 480),
            WindowStateKeys.QuickSetup => new(760, 560),
            WindowStateKeys.Dialog => new(320, 196),
            WindowStateKeys.WorldClockCityPicker => new(500, 560),
            WindowStateKeys.AiPricing => new(620, 430),
            WindowStateKeys.AiConnectionTest => new(480, 480),
            _ => new(320, 240)
        };
    }

    /// <summary>Reads the native window placement and persists it under the supplied key.</summary>
    public WindowState Save(string windowKey, long windowHandle)
    {
        ValidateRequest(windowKey, windowHandle);
        var handle = new IntPtr(windowHandle);
        var monitor = GetMonitorForWindow(handle);
        var rect = GetWindowRect(handle);
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
        _store.SaveSettings(settings with { WindowStates = placements });
        return state;
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
        if (!SetWindowPos(handle, IntPtr.Zero, safeState.X, safeState.Y, safeState.Width, safeState.Height, SetWindowPosNoActivate | SetWindowPosNoZOrder | SetWindowPosShowWindow))
        {
            throw new InvalidOperationException($"Unable to restore window bounds (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return safeState;
    }

    private static void ValidateRequest(string windowKey, long windowHandle)
    {
        if (string.IsNullOrWhiteSpace(windowKey))
        {
            throw new ArgumentException("A window key is required.", nameof(windowKey));
        }

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
        return new NativeMonitor(monitorInfo.DeviceName, new WindowWorkArea(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top));
    }

    private static NativeRect GetWindowRect(IntPtr handle)
    {
        if (!GetWindowRectNative(handle, out var rect))
        {
            throw new InvalidOperationException($"Unable to read window bounds (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        return rect;
    }

    private sealed record NativeMonitor(string DeviceName, WindowWorkArea WorkArea);

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

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

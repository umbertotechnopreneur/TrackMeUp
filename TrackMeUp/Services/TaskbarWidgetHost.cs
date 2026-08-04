using System.Runtime.InteropServices;

namespace TrackMeUp.Services;

/// <summary>Hosts the compact TrackMeUp control inside the Windows taskbar without owning tracking state.</summary>
public sealed class TaskbarWidgetHost : IDisposable
{
    /// <summary>Gets the widget width in device-independent pixels.</summary>
    public const int LogicalWidth = 144;

    /// <summary>Gets the widget height in device-independent pixels.</summary>
    public const int LogicalHeight = 40;

    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const int SwHide = 0;

    private readonly object _gate = new();
    private Timer? _explorerRecoveryTimer;
    private IntPtr _widgetHandle;
    private string _position = TaskbarWidgetPositions.Left;
    private bool _disposed;

    /// <summary>Embeds a WinUI window in the primary Windows taskbar and begins recovery checks.</summary>
    public bool Attach(IntPtr widgetHandle, string position)
    {
        if (widgetHandle == IntPtr.Zero)
        {
            return false;
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            _widgetHandle = widgetHandle;
            _position = NormalizePosition(position);
            var attached = TryAttachAndPosition();
            _explorerRecoveryTimer ??= new Timer(static state => ((TaskbarWidgetHost)state!).RecoverFromExplorerChanges(), this, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            return attached;
        }
    }

    /// <summary>Changes the placement of the embedded control without changing any tracking setting.</summary>
    public void Configure(string position)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _position = NormalizePosition(position);
            TryAttachAndPosition();
        }
    }

    /// <summary>Hides a top-level flyout after the taskbar control has been attached successfully.</summary>
    public void HideTopLevelWindow(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero && IsWindow(windowHandle))
        {
            ShowWindow(windowHandle, SwHide);
        }
    }

    /// <summary>Stops recovery checks and removes taskbar parenting before the widget window is closed.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _explorerRecoveryTimer?.Dispose();
            _explorerRecoveryTimer = null;
            if (_widgetHandle != IntPtr.Zero && IsWindow(_widgetHandle))
            {
                ShowWindow(_widgetHandle, SwHide);
                SetParent(_widgetHandle, IntPtr.Zero);
            }

            _widgetHandle = IntPtr.Zero;
        }
    }

    private void RecoverFromExplorerChanges()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // Explorer may recreate the taskbar after a restart; an unavailable taskbar simply leaves the widget hidden until the next probe.
            TryAttachAndPosition();
        }
    }

    private bool TryAttachAndPosition()
    {
        if (_widgetHandle == IntPtr.Zero || !IsWindow(_widgetHandle))
        {
            return false;
        }

        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        if (taskbarHandle == IntPtr.Zero || !IsWindow(taskbarHandle) || !GetClientRect(taskbarHandle, out var taskbarBounds))
        {
            return false;
        }

        if (GetParent(_widgetHandle) != taskbarHandle)
        {
            var style = GetWindowStyle(_widgetHandle);
            var childStyle = (style | WsChild) & ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
            SetWindowStyle(_widgetHandle, childStyle);
            SetParent(_widgetHandle, taskbarHandle);
            if (GetParent(_widgetHandle) != taskbarHandle)
            {
                return false;
            }
        }

        var scale = Math.Max(1d, GetDpiForWindow(taskbarHandle) / 96d);
        var taskbarWidth = taskbarBounds.Right - taskbarBounds.Left;
        var taskbarHeight = taskbarBounds.Bottom - taskbarBounds.Top;
        var maxWidgetWidth = (int)Math.Ceiling(LogicalWidth * scale);
        var maxWidgetHeight = (int)Math.Ceiling(LogicalHeight * scale);
        var widgetScale = Math.Min(1d, Math.Min(taskbarWidth / (double)maxWidgetWidth, taskbarHeight / (double)maxWidgetHeight));
        var widgetWidth = Math.Max(1, (int)Math.Round(maxWidgetWidth * widgetScale));
        var widgetHeight = Math.Max(1, (int)Math.Round(maxWidgetHeight * widgetScale));
        if (taskbarWidth <= 0 || taskbarHeight <= 0)
        {
            return false;
        }

        var isHorizontalTaskbar = taskbarWidth >= taskbarHeight;
        var x = isHorizontalTaskbar ? _position switch
        {
            TaskbarWidgetPositions.Center => Math.Max(0, (taskbarWidth - widgetWidth) / 2),
            TaskbarWidgetPositions.Right => Math.Max(0, taskbarWidth - widgetWidth - (int)Math.Ceiling(320 * scale)),
            _ => (int)Math.Ceiling(12 * scale)
        } : Math.Max(0, (taskbarWidth - widgetWidth) / 2);
        var y = isHorizontalTaskbar ? Math.Max(0, (taskbarHeight - widgetHeight) / 2) : _position switch
        {
            TaskbarWidgetPositions.Center => Math.Max(0, (taskbarHeight - widgetHeight) / 2),
            TaskbarWidgetPositions.Right => Math.Max(0, taskbarHeight - widgetHeight - (int)Math.Ceiling(320 * scale)),
            _ => (int)Math.Ceiling(12 * scale)
        };

        // SetWindowPos is intentionally best-effort: a third-party shell can refuse child placement without affecting the local tracking runtime.
        return SetWindowPos(_widgetHandle, IntPtr.Zero, x, y, widgetWidth, widgetHeight, SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    private static string NormalizePosition(string? position) => position is TaskbarWidgetPositions.Center or TaskbarWidgetPositions.Right ? position : TaskbarWidgetPositions.Left;

    private static long GetWindowStyle(IntPtr windowHandle) => IntPtr.Size == 8 ? GetWindowLongPtr64(windowHandle, GwlStyle).ToInt64() : GetWindowLong32(windowHandle, GwlStyle);

    private static void SetWindowStyle(IntPtr windowHandle, long style)
    {
        if (IntPtr.Size == 8)
        {
            _ = SetWindowLongPtr64(windowHandle, GwlStyle, new IntPtr(style));
        }
        else
        {
            _ = SetWindowLong32(windowHandle, GwlStyle, unchecked((int)style));
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TaskbarWidgetHost));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr childWindow, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr windowHandle, int index, int newValue);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);
}

// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace TrackMeUp.Services;

/// <summary>Provides localized labels for the two commands in the notification-area context menu.</summary>
public sealed record TrayIconMenuLabels(string ShowMainWindow, string HideMainWindow, string CloseApplication);

/// <summary>Owns the notification-area icon that can hide and restore one top-level TrackMeUp window.</summary>
public sealed class TrayIconService : IDisposable
{
    private const uint NotificationIconAdd = 0x00000000;
    private const uint NotificationIconDelete = 0x00000002;
    private const uint NotificationIconSetVersion = 0x00000004;
    private const uint NotificationIconMessage = 0x00000001;
    private const uint NotificationIconIcon = 0x00000002;
    private const uint NotificationIconTip = 0x00000004;
    private const uint NotificationIconVersion4 = 4;
    private const uint TrayCallbackMessage = 0x8000 + 0x350;
    private const uint LeftButtonUpMessage = 0x0202;
    private const uint RightButtonUpMessage = 0x0205;
    private const uint ImageIcon = 1;
    private const uint LoadImageFromFile = 0x0010;
    private const uint MenuString = 0x0000;
    private const uint TrackPopupMenuReturnCommand = 0x0100;
    private const uint TrackPopupMenuNoNotify = 0x0080;
    private const uint TrackPopupMenuRightButton = 0x0002;
    private const uint TrackPopupMenuWorkArea = 0x10000;
    private const uint MenuCommandToggleWindow = 1;
    private const uint MenuCommandCloseApplication = 2;
    private const uint NullWindowMessage = 0;
    private const int ShowWindowHide = 0;
    private const int ShowWindowNormal = 1;
    private static readonly UIntPtr SubclassId = new(1);
    private static readonly SubclassProcDelegate SubclassProcedure = WindowSubclassProcedure;
    private readonly ILogger _logger;
    private GCHandle _selfHandle;
    private IntPtr _windowHandle;
    private IntPtr _iconHandle;
    private TrayIconMenuLabels? _menuLabels;
    private bool _iconRegistered;
    private bool _disposed;

    /// <summary>Creates the native notification-area owner with process-local diagnostics.</summary>
    public TrayIconService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Occurs after the user explicitly selects Close app in the notification-area context menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Registers the notification-area icon if necessary, then hides the main window from the taskbar.</summary>
    public void HideToNotificationArea(IntPtr windowHandle, string iconPath, string toolTip, TrayIconMenuLabels menuLabels)
    {
        ThrowIfDisposed();
        EnsureAttached(windowHandle, iconPath, toolTip, menuLabels);

        // Hiding the real top-level window removes its taskbar button while leaving its message queue available for the tray callback.
        _ = ShowWindow(_windowHandle, ShowWindowHide);
    }

    /// <summary>Removes the icon and releases the native subclass before the owning window is destroyed.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_iconRegistered)
        {
            var notification = CreateNotificationData();
            if (!ShellNotifyIcon(NotificationIconDelete, ref notification))
            {
                _logger.LogWarning("The notification-area icon could not be removed during window shutdown. Win32Error={Win32Error}", Marshal.GetLastWin32Error());
            }

            _iconRegistered = false;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            _ = RemoveWindowSubclass(_windowHandle, SubclassProcedure, SubclassId);
        }

        if (_iconHandle != IntPtr.Zero)
        {
            _ = DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        _windowHandle = IntPtr.Zero;
        _menuLabels = null;
    }

    private void EnsureAttached(IntPtr windowHandle, string iconPath, string toolTip, TrayIconMenuLabels menuLabels)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("A valid top-level window handle is required for the notification-area icon.", nameof(windowHandle));
        }

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            throw new ArgumentException("The notification-area icon path is required.", nameof(iconPath));
        }

        if (string.IsNullOrWhiteSpace(toolTip))
        {
            throw new ArgumentException("The notification-area tooltip is required.", nameof(toolTip));
        }

        ArgumentNullException.ThrowIfNull(menuLabels);
        if (string.IsNullOrWhiteSpace(menuLabels.ShowMainWindow)
            || string.IsNullOrWhiteSpace(menuLabels.HideMainWindow)
            || string.IsNullOrWhiteSpace(menuLabels.CloseApplication))
        {
            throw new ArgumentException("Every notification-area context-menu label is required.", nameof(menuLabels));
        }

        if (_iconRegistered)
        {
            if (_windowHandle != windowHandle)
            {
                throw new InvalidOperationException("The notification-area icon is already attached to a different window.");
            }

            _menuLabels = menuLabels;
            return;
        }

        if (!File.Exists(iconPath))
        {
            throw new FileNotFoundException("The notification-area icon file is unavailable.", iconPath);
        }

        _windowHandle = windowHandle;
        _menuLabels = menuLabels;
        _selfHandle = GCHandle.Alloc(this);
        try
        {
            if (!SetWindowSubclass(_windowHandle, SubclassProcedure, SubclassId, GCHandle.ToIntPtr(_selfHandle)))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not receive notification-area icon activation messages.");
            }

            _iconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LoadImageFromFile);
            if (_iconHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not load its notification-area icon.");
            }

            var notification = CreateNotificationData(toolTip);
            if (!ShellNotifyIcon(NotificationIconAdd, ref notification))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not add its notification-area icon.");
            }

            _iconRegistered = true;
            if (!ShellNotifyIcon(NotificationIconSetVersion, ref notification))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not configure notification-area icon activation.");
            }

            _logger.LogInformation("Notification-area icon attached to the TrackMeUp main window.");
        }
        catch (Exception exception)
        {
            // A failed registration leaves the player visible and removes every partially registered native resource.
            CleanupFailedAttach();
            _logger.LogError(exception, "Notification-area icon initialization failed.");
            throw;
        }
    }

    private void CleanupFailedAttach()
    {
        if (_iconRegistered)
        {
            var notification = CreateNotificationData();
            _ = ShellNotifyIcon(NotificationIconDelete, ref notification);
            _iconRegistered = false;
        }

        if (_iconHandle != IntPtr.Zero)
        {
            _ = DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            _ = RemoveWindowSubclass(_windowHandle, SubclassProcedure, SubclassId);
        }

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        _windowHandle = IntPtr.Zero;
        _menuLabels = null;
    }

    private void ToggleMainWindowVisibility()
    {
        if (_disposed || _windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (IsWindowVisible(_windowHandle))
        {
            _ = ShowWindow(_windowHandle, ShowWindowHide);
            return;
        }

        // Restoring the same native window preserves the active WinUI surface and places it in the foreground after a tray click.
        _ = ShowWindow(_windowHandle, ShowWindowNormal);
        _ = SetForegroundWindow(_windowHandle);
    }

    private void ShowContextMenu()
    {
        if (_disposed || _windowHandle == IntPtr.Zero || _menuLabels is null)
        {
            return;
        }

        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not create its notification-area context menu.");
        }

        try
        {
            var toggleText = IsWindowVisible(_windowHandle)
                ? _menuLabels.HideMainWindow
                : _menuLabels.ShowMainWindow;
            if (!AppendMenu(menu, MenuString, new UIntPtr(MenuCommandToggleWindow), toggleText)
                || !AppendMenu(menu, MenuString, new UIntPtr(MenuCommandCloseApplication), _menuLabels.CloseApplication))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not populate its notification-area context menu.");
            }

            if (!GetCursorPos(out var cursorPosition))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "TrackMeUp could not position its notification-area context menu.");
            }

            // Windows requires the owner to be foreground before a tray popup so outside clicks dismiss it correctly.
            _ = SetForegroundWindow(_windowHandle);
            var command = TrackPopupMenuEx(
                menu,
                TrackPopupMenuReturnCommand | TrackPopupMenuNoNotify | TrackPopupMenuRightButton | TrackPopupMenuWorkArea,
                cursorPosition.X,
                cursorPosition.Y,
                _windowHandle,
                IntPtr.Zero);
            if (command == MenuCommandToggleWindow)
            {
                ToggleMainWindowVisibility();
            }
            else if (command == MenuCommandCloseApplication)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _ = DestroyMenu(menu);
            _ = PostMessage(_windowHandle, NullWindowMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private NotifyIconData CreateNotificationData(string toolTip = "") => new()
    {
        Size = Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _windowHandle,
        Id = 1,
        Flags = _iconHandle == IntPtr.Zero
            ? NotificationIconMessage
            : NotificationIconMessage | NotificationIconIcon | NotificationIconTip,
        CallbackMessage = TrayCallbackMessage,
        IconHandle = _iconHandle,
        ToolTip = toolTip,
        Info = string.Empty,
        InfoTitle = string.Empty,
        Version = NotificationIconVersion4
    };

    private static IntPtr WindowSubclassProcedure(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr referenceData)
    {
        if (message == TrayCallbackMessage && referenceData != IntPtr.Zero)
        {
            try
            {
                var activationMessage = (uint)lParam.ToInt64() & 0xFFFF;
                if (GCHandle.FromIntPtr(referenceData).Target is TrayIconService service)
                {
                    if (activationMessage == LeftButtonUpMessage)
                    {
                        service.ToggleMainWindowVisibility();
                    }
                    else if (activationMessage == RightButtonUpMessage)
                    {
                        service.ShowContextMenu();
                    }
                }
            }
            catch (Exception exception)
            {
                // Native window callbacks must always return control to Windows, even if the tray state is no longer usable.
                if (GCHandle.FromIntPtr(referenceData).Target is TrayIconService service)
                {
                    service._logger.LogError(exception, "Notification-area icon activation failed.");
                }
            }
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ToolTip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIconHandle;

        public uint Version
        {
            readonly get => TimeoutOrVersion;
            set => TimeoutOrVersion = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SubclassProcDelegate(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr referenceData);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData notificationData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int desiredWidth, int desiredHeight, uint loadFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menuHandle, uint flags, UIntPtr itemId, string text);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(IntPtr menuHandle, uint flags, int x, int y, IntPtr ownerWindowHandle, IntPtr parameters);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menuHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr windowHandle, SubclassProcDelegate procedure, UIntPtr subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr windowHandle, SubclassProcDelegate procedure, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr windowHandle, uint message, UIntPtr wParam, IntPtr lParam);
}

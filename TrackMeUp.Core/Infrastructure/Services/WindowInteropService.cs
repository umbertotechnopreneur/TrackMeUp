using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrackMeUp.Services;

/// <summary>Owns the native window operations shared by WinUI presentation surfaces.</summary>
public static class WindowInteropService
{
    private const int GwlHwndParent = -8;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint MbOk = 0x00000000;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbSetForeground = 0x00010000;
    private static readonly IntPtr HwndTopMost = new(-1);

    /// <summary>Assigns one native window as the owner of another window.</summary>
    public static void SetOwner(IntPtr windowHandle, IntPtr ownerHandle)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, IntPtr.Zero);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerHandle, IntPtr.Zero);

        // SetWindowLongPtr can legitimately return zero, so only a nonzero native error means failure.
        Marshal.SetLastPInvokeError(0);
        var previousOwner = IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, GwlHwndParent, ownerHandle)
            : new IntPtr(SetWindowLongPtr32(windowHandle, GwlHwndParent, ownerHandle.ToInt32()));
        var error = Marshal.GetLastPInvokeError();
        if (previousOwner == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error, "Unable to assign the native owner window.");
        }
    }

    /// <summary>Places a window in the topmost band without moving, resizing, or activating it.</summary>
    public static void MakeTopmostWithoutActivation(IntPtr windowHandle)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, IntPtr.Zero);
        if (!SetWindowPos(windowHandle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to place the dialog in the topmost window band.");
        }
    }

    /// <summary>Disables the other enabled native windows owned by the current UI thread.</summary>
    public static IReadOnlyList<IntPtr> DisableCurrentThreadPeerWindows(IntPtr dialogHandle)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dialogHandle, IntPtr.Zero);
        var disabled = new List<IntPtr>();
        Marshal.SetLastPInvokeError(0);
        var enumerated = EnumThreadWindows(GetCurrentThreadId(), (windowHandle, parameter) =>
        {
            if (windowHandle == dialogHandle || !IsWindowEnabled(windowHandle))
            {
                return true;
            }

            _ = EnableWindow(windowHandle, false);
            disabled.Add(windowHandle);
            return true;
        }, IntPtr.Zero);

        var error = Marshal.GetLastPInvokeError();
        if (!enumerated && error != 0)
        {
            // Roll back the partial modal state before exposing the interop failure to the caller.
            RestoreWindows(disabled);
            throw new Win32Exception(error, "Unable to enumerate peer windows for the modal dialog.");
        }

        return disabled;
    }

    /// <summary>Re-enables native windows previously disabled for a modal dialog.</summary>
    public static void RestoreWindows(IEnumerable<IntPtr> windowHandles)
    {
        ArgumentNullException.ThrowIfNull(windowHandles);
        foreach (var windowHandle in windowHandles)
        {
            if (windowHandle != IntPtr.Zero && IsWindow(windowHandle))
            {
                _ = EnableWindow(windowHandle, true);
            }
        }
    }

    /// <summary>Shows an owned Windows warning message with the standard acknowledgement action.</summary>
    public static void ShowWarningMessage(IntPtr ownerHandle, string title, string message)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(ownerHandle, IntPtr.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // This synchronous Win32 call owns its modal loop and returns only after the standard OK action.
        if (MessageBoxW(ownerHandle, message, title, MbOk | MbIconWarning | MbSetForeground) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to show the Windows warning message.");
        }
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLongPtr32(IntPtr windowHandle, int index, int newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr windowHandle, bool enable);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(uint threadId, EnumThreadDelegate callback, IntPtr parameter);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr ownerHandle, string text, string caption, uint type);

    private delegate bool EnumThreadDelegate(IntPtr windowHandle, IntPtr parameter);
}

// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrackMeUp.Services;

/// <summary>Defines the native icon shown by a Windows system message box.</summary>
public enum SystemMessageBoxSeverity
{
    /// <summary>Shows the standard information icon.</summary>
    Information,

    /// <summary>Shows the standard warning icon.</summary>
    Warning,

    /// <summary>Shows the standard error icon.</summary>
    Error
}

/// <summary>Describes the localized title and message rendered by one Windows system message box.</summary>
public sealed record SystemMessageBoxRequest(
    string Title,
    string Message,
    SystemMessageBoxSeverity Severity)
{
    /// <summary>Creates a one-button informational request.</summary>
    public static SystemMessageBoxRequest Informative(
        string title,
        string message,
        SystemMessageBoxSeverity severity) =>
        new(title, message, severity);

    /// <summary>Creates a warning confirmation whose native Cancel button is the safe default.</summary>
    public static SystemMessageBoxRequest Confirmation(string title, string message) =>
        new(title, message, SystemMessageBoxSeverity.Warning);
}

/// <summary>Owns the native window operations shared by WinUI presentation surfaces.</summary>
public static class WindowInteropService
{
    private const int GwlHwndParent = -8;
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowAttributeBorderColor = 34;
    private const uint DwmWindowCornerPreferenceRound = 2;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint MbOk = 0x00000000;
    private const uint MbOkCancel = 0x00000001;
    private const uint MbIconError = 0x00000010;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbIconInformation = 0x00000040;
    private const uint MbDefaultButton2 = 0x00000100;
    private const uint MbSetForeground = 0x00010000;
    private const uint MbTopMost = 0x00040000;
    private const int IdOk = 1;
    private const int IdCancel = 2;
    private static readonly IntPtr HwndTopMost = new(-1);

    /// <summary>Applies the optional native chrome used by the compact player window.</summary>
    public static void ApplyPlayerWindowChrome(IntPtr windowHandle)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, IntPtr.Zero);

        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowAttributeBorderColor,
            ref borderColor,
            Marshal.SizeOf<uint>());

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // Rounded-corner preference is a Windows 11 visual enhancement; older supported
            // systems intentionally retain their native default window shape.
            return;
        }

        var cornerPreference = DwmWindowCornerPreferenceRound;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowAttributeCornerPreference,
            ref cornerPreference,
            Marshal.SizeOf<uint>());
    }

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

    /// <summary>Shows an owned Windows message with the standard localized OK action.</summary>
    public static void ShowInformativeMessage(IntPtr ownerHandle, SystemMessageBoxRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(ownerHandle, IntPtr.Zero);
        ValidateSystemMessageRequest(request);

        // This synchronous Win32 call owns its modal loop and returns only after the standard OK action.
        var result = MessageBoxW(
            ownerHandle,
            request.Message,
            request.Title,
            MbOk | SeverityFlag(request.Severity) | MbSetForeground | MbTopMost);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to show the Windows system message.");
        }

        if (result != IdOk)
        {
            throw new InvalidOperationException($"Windows returned unsupported informative message result {result}.");
        }
    }

    /// <summary>Shows an owned Windows OK/Cancel confirmation with Cancel selected by default.</summary>
    /// <returns><see langword="true"/> only when the user explicitly chooses the native OK action.</returns>
    public static bool ShowConfirmationMessage(IntPtr ownerHandle, SystemMessageBoxRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(ownerHandle, IntPtr.Zero);
        ValidateSystemMessageRequest(request);

        var result = MessageBoxW(
            ownerHandle,
            request.Message,
            request.Title,
            MbOkCancel | MbDefaultButton2 | SeverityFlag(request.Severity) | MbSetForeground | MbTopMost);
        if (result == 0)
        {
            // A P/Invoke failure is surfaced instead of being confused with user cancellation; either path
            // prevents the caller from proceeding with a destructive action.
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to show the Windows confirmation message.");
        }

        // Closing the box and every response other than the explicit OK action are safe cancellation.
        return result switch
        {
            IdOk => true,
            IdCancel => false,
            _ => false
        };
    }

    private static uint SeverityFlag(SystemMessageBoxSeverity severity) => severity switch
    {
        SystemMessageBoxSeverity.Information => MbIconInformation,
        SystemMessageBoxSeverity.Warning => MbIconWarning,
        SystemMessageBoxSeverity.Error => MbIconError,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported system message severity.")
    };

    private static void ValidateSystemMessageRequest(SystemMessageBoxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);
        _ = SeverityFlag(request.Severity);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLongPtr32(IntPtr windowHandle, int index, int newValue);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref uint attributeValue,
        int attributeSize);

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

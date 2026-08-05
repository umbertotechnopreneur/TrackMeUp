using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace TrackMeUp.Services;

/// <summary>
/// Opens the Windows Share UI for a TrackMeUp-owned screenshot.
/// </summary>
public sealed class ScreenshotShareService
{
    private static readonly Guid DataTransferManagerIid = new(
        0xa5caee9b,
        0x8708,
        0x49d1,
        0x8d,
        0x36,
        0x67,
        0xd2,
        0x5a,
        0x8d,
        0xa0,
        0x0c);

    /// <summary>
    /// Registers the selected screenshot as shareable content and opens the Windows Share UI.
    /// </summary>
    /// <param name="screenshotPath">Absolute path to a TrackMeUp-owned screenshot artifact.</param>
    /// <param name="windowHandle">HWND that owns the Share UI.</param>
    /// <returns>The validated screenshot path supplied to the Share UI.</returns>
    /// <exception cref="ArgumentException">Thrown when a path or window handle is invalid.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the screenshot no longer exists.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when Windows sharing is unavailable.</exception>
    public string Share(string screenshotPath, IntPtr windowHandle)
    {
        var fullPath = ValidateScreenshotPath(screenshotPath);
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("A valid owner window handle is required.", nameof(windowHandle));
        }

        var storageFile = LoadStorageFile(fullPath);
        var (interop, dataTransferManager) = GetDataTransferManager(windowHandle);

        TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? handler = null;
        handler = (sender, args) =>
        {
            try
            {
                args.Request.Data.Properties.Title = Path.GetFileName(fullPath);
                args.Request.Data.Properties.Description = "TrackMeUp screenshot";
                args.Request.Data.RequestedOperation = DataPackageOperation.Copy;
                args.Request.Data.SetStorageItems(new[] { storageFile });
            }
            catch (Exception exception)
            {
                // The Share contract cannot return an exception to this synchronous caller; surface the failure in the Share UI.
                args.Request.FailWithDisplayText($"TrackMeUp could not share the screenshot: {exception.Message}");
            }
            finally
            {
                sender.DataRequested -= handler;
            }
        };

        dataTransferManager.DataRequested += handler;
        try
        {
            // WinUI 3 desktop apps must use the per-HWND interop entry point; the current-view API is unsupported here.
            interop.ShowShareUIForWindow(windowHandle);
        }
        catch (COMException exception) when (IsUnsupported(exception))
        {
            dataTransferManager.DataRequested -= handler;
            throw CreateUnsupportedException(exception);
        }
        catch
        {
            dataTransferManager.DataRequested -= handler;
            throw;
        }

        return fullPath;
    }

    private static string ValidateScreenshotPath(string screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath) || !Path.IsPathFullyQualified(screenshotPath))
        {
            throw new ArgumentException("The screenshot path must be an absolute path.", nameof(screenshotPath));
        }

        var fullPath = Path.GetFullPath(screenshotPath);
        if (!ScreenCaptureService.IsOwnedArtifact(fullPath))
        {
            throw new ArgumentException("The path is not a TrackMeUp-owned screenshot artifact.", nameof(screenshotPath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The TrackMeUp screenshot no longer exists.", fullPath);
        }

        return fullPath;
    }

    private static StorageFile LoadStorageFile(string screenshotPath)
    {
        try
        {
            return StorageFile.GetFileFromPathAsync(screenshotPath).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or FileNotFoundException)
        {
            throw new IOException($"Windows could not prepare the screenshot for sharing: '{screenshotPath}'.", exception);
        }
    }

    private static (IDataTransferManagerInterop Interop, DataTransferManager Manager) GetDataTransferManager(IntPtr windowHandle)
    {
        try
        {
            IDataTransferManagerInterop interop = DataTransferManager.As<IDataTransferManagerInterop>();
            var abi = interop.GetForWindow(windowHandle, DataTransferManagerIid);
            if (abi == IntPtr.Zero)
            {
                throw CreateUnsupportedException();
            }

            try
            {
                var manager = WinRT.MarshalInterface<DataTransferManager>.FromAbi(abi)
                    ?? throw CreateUnsupportedException();
                return (interop, manager);
            }
            finally
            {
                Marshal.Release(abi);
            }
        }
        catch (PlatformNotSupportedException)
        {
            throw;
        }
        catch (COMException exception) when (IsUnsupported(exception))
        {
            throw CreateUnsupportedException(exception);
        }
        catch (InvalidCastException exception)
        {
            throw CreateUnsupportedException(exception);
        }
    }

    private static bool IsUnsupported(COMException exception)
        => exception.HResult is unchecked((int)0x80004001) or unchecked((int)0x80004002) or unchecked((int)0x80070032);

    private static PlatformNotSupportedException CreateUnsupportedException(Exception? innerException = null)
        => new("Windows Share UI is not supported by this Windows installation or app deployment.", innerException);

    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);

        void ShowShareUIForWindow(IntPtr appWindow);
    }
}

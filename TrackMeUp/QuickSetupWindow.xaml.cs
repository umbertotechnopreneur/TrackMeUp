using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Runtime.InteropServices;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Collects one Quick Setup profile choice and delegates its atomic application to the shared facade.</summary>
internal sealed partial class QuickSetupWindow : Window
{
    private const int LogicalWindowWidth = 860;
    private const int LogicalWindowHeight = 630;
    private const int LogicalScreenMargin = 24;
    private const int GwlHwndParent = -8;
    private readonly ITrackMeUpApplication _application;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly LocalizationService _strings;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly bool _firstRun;
    private XamlRoot? _xamlRoot;
    private string _selectedProfileId;
    private bool _applying;

    /// <summary>Occurs after the application layer persists a complete Quick Setup profile.</summary>
    internal event Action<AppSettings>? ProfileApplied;

    /// <summary>Creates the owned acrylic Quick Setup window from the current validated settings snapshot.</summary>
    internal QuickSetupWindow(
        ITrackMeUpApplication application,
        AppSettings settings,
        bool firstRun,
        AppWindow ownerAppWindow,
        IntPtr ownerHandle)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ownerAppWindow);
        _firstRun = firstRun;
        _strings = new LocalizationService(settings.UiLanguage);
        _selectedProfileId = firstRun ? QuickSetupProfileIds.Complete : InferProfile(settings);

        InitializeComponent();
        RootGrid.RequestedTheme = settings.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle));
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.QuickSetup,
            LogicalWindowWidth,
            LogicalWindowHeight,
            LogicalScreenMargin,
            ownerAppWindow.Id);
        SetWindowOwner(windowHandle, ownerHandle);
        ConfigureWindowBehavior();
        _placement.ApplyDefaultBounds(RootGrid);

        StartWithWindowsCheckBox.IsChecked = firstRun || settings.StartWithWindows;
        ApplyLanguage();
        UpdateSelection();
        Closed += QuickSetupWindow_Closed;
    }

    private void ApplyLanguage()
    {
        UiLocalization.Apply(RootGrid, _strings);
        Title = T("QuickSetup.Title");
        PrimaryButton.Content = T(_firstRun ? "QuickSetup.Start" : "QuickSetup.Apply");
        AutomationProperties.SetName(PrimaryButton, PrimaryButton.Content?.ToString() ?? T("QuickSetup.Apply"));
        AutomationProperties.SetName(CompleteProfileButton, T("QuickSetup.Profile.Complete.Accessible"));
        AutomationProperties.SetName(AssistedProfileButton, T("QuickSetup.Profile.Assisted.Accessible"));
        AutomationProperties.SetName(LocalRecordProfileButton, T("QuickSetup.Profile.LocalRecord.Accessible"));
        AutomationProperties.SetName(EssentialOfflineProfileButton, T("QuickSetup.Profile.EssentialOffline.Accessible"));
        SelectedProfileLabelText.Text = T("QuickSetup.SelectedProfile");
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // ToggleButton content enters the visual tree only after its template is realized.
        ApplyLanguage();
        UpdateSelection();
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        await _placement.RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token);
        _placement.ApplyDefaultBounds(RootGrid);
        UpdateTitleBarInsets();
        SelectedButton().Focus(FocusState.Programmatic);
    }

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string profileId })
        {
            throw new InvalidOperationException("A Quick Setup profile button must declare its profile identifier.");
        }

        _selectedProfileId = profileId;
        ApplyInfoBar.IsOpen = false;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        CompleteProfileButton.IsChecked = _selectedProfileId == QuickSetupProfileIds.Complete;
        AssistedProfileButton.IsChecked = _selectedProfileId == QuickSetupProfileIds.Assisted;
        LocalRecordProfileButton.IsChecked = _selectedProfileId == QuickSetupProfileIds.LocalRecord;
        EssentialOfflineProfileButton.IsChecked = _selectedProfileId == QuickSetupProfileIds.EssentialOffline;
        SelectedProfileNameText.Text = T(ProfileTitleKey(_selectedProfileId));
        SelectedProfileDescriptionText.Text = T(ProfileDescriptionKey(_selectedProfileId));
    }

    private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        SetActionsEnabled(false);
        ApplyInfoBar.IsOpen = false;
        try
        {
            var result = await _application.ApplyQuickSetupProfileAsync(
                new QuickSetupProfileRequest(
                    _selectedProfileId,
                    StartWithWindowsCheckBox.IsChecked == true),
                _lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                ApplyInfoBar.Title = T("QuickSetup.Error.Title");
                ApplyInfoBar.Message = result.Issues.Any(issue =>
                        issue.Field == "ai.enabled" && issue.Code == "api_key_required")
                    ? T("QuickSetup.Error.AiKey")
                    : result.Issues.Any(issue => issue.Field == "startup.enabled")
                        ? T("QuickSetup.Error.Startup")
                        : T("QuickSetup.Error.Generic");
                ApplyInfoBar.IsOpen = true;
                return;
            }

            ProfileApplied?.Invoke(result.Value);
            Close();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The window is closing; no further presentation update is safe or useful.
        }
        finally
        {
            _applying = false;
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                SetActionsEnabled(true);
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetActionsEnabled(bool enabled)
    {
        CompleteProfileButton.IsEnabled = enabled;
        AssistedProfileButton.IsEnabled = enabled;
        LocalRecordProfileButton.IsEnabled = enabled;
        EssentialOfflineProfileButton.IsEnabled = enabled;
        StartWithWindowsCheckBox.IsEnabled = enabled;
        CancelButton.IsEnabled = enabled;
        PrimaryButton.IsEnabled = enabled;
    }

    private ToggleButton SelectedButton() => _selectedProfileId switch
    {
        QuickSetupProfileIds.Complete => CompleteProfileButton,
        QuickSetupProfileIds.Assisted => AssistedProfileButton,
        QuickSetupProfileIds.LocalRecord => LocalRecordProfileButton,
        QuickSetupProfileIds.EssentialOffline => EssentialOfflineProfileButton,
        _ => throw new InvalidOperationException("The selected Quick Setup profile is unsupported.")
    };

    private static string InferProfile(AppSettings settings) => (settings.OpenAiEnabled, settings.ScreenshotsEnabled) switch
    {
        (true, true) => QuickSetupProfileIds.Complete,
        (true, false) => QuickSetupProfileIds.Assisted,
        (false, true) => QuickSetupProfileIds.LocalRecord,
        _ => QuickSetupProfileIds.EssentialOffline
    };

    private static string ProfileTitleKey(string profileId) => profileId switch
    {
        QuickSetupProfileIds.Complete => "QuickSetup.Profile.Complete.Title",
        QuickSetupProfileIds.Assisted => "QuickSetup.Profile.Assisted.Title",
        QuickSetupProfileIds.LocalRecord => "QuickSetup.Profile.LocalRecord.Title",
        QuickSetupProfileIds.EssentialOffline => "QuickSetup.Profile.EssentialOffline.Title",
        _ => throw new InvalidOperationException("The selected Quick Setup profile is unsupported.")
    };

    private static string ProfileDescriptionKey(string profileId) => profileId switch
    {
        QuickSetupProfileIds.Complete => "QuickSetup.Profile.Complete.Description",
        QuickSetupProfileIds.Assisted => "QuickSetup.Profile.Assisted.Description",
        QuickSetupProfileIds.LocalRecord => "QuickSetup.Profile.LocalRecord.Description",
        QuickSetupProfileIds.EssentialOffline => "QuickSetup.Profile.EssentialOffline.Description",
        _ => throw new InvalidOperationException("The selected Quick Setup profile is unsupported.")
    };

    private void ConfigureWindowBehavior()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _appWindow.TitleBar.BackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void TitleBarDragRegion_Loaded(object sender, RoutedEventArgs e) => UpdateTitleBarInsets();

    private void TitleBarDragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarInsets();

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        }
    }

    private void UpdateTitleBarInsets()
    {
        if (!ExtendsContentIntoTitleBar || TitleBarDragRegion.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var scale = Math.Max(0.1d, xamlRoot.RasterizationScale);
        TitleBarLeftInsetColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scale);
        TitleBarRightInsetColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scale);
    }

    private async void QuickSetupWindow_Closed(object sender, WindowEventArgs args)
    {
        Closed -= QuickSetupWindow_Closed;
        _lifetimeCancellation.Cancel();
        try
        {
            await _placement.SaveAsync(CancellationToken.None);
        }
        finally
        {
            _placement.Dispose();
            if (_xamlRoot is not null)
            {
                _xamlRoot.Changed -= XamlRoot_Changed;
            }

            _lifetimeCancellation.Dispose();
        }
    }

    private string T(string key) => _strings.Translate(key);

    private static void SetWindowOwner(IntPtr windowHandle, IntPtr ownerHandle)
    {
        if (ownerHandle == IntPtr.Zero)
        {
            return;
        }

        _ = IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, GwlHwndParent, ownerHandle)
            : new IntPtr(SetWindowLongPtr32(windowHandle, GwlHwndParent, ownerHandle.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}

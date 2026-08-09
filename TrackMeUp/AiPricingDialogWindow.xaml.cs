using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Graphics;
using Windows.System;

namespace TrackMeUp;

/// <summary>Shows simplified AI pricing and locally estimated OpenAI costs in a queued acrylic dialog.</summary>
internal sealed partial class AiPricingDialogWindow : Window
{
    private const int LogicalWidth = 680;
    private const int LogicalHeight = 590;
    private const int LogicalScreenMargin = 24;
    private const int GwlHwndParent = -8;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopMost = new(-1);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AiPricingOverview _overview;
    private readonly LocalizationService _strings;
    private readonly AppWindow _appWindow;
    private readonly AppWindow _ownerAppWindow;
    private readonly WindowPlacementService _placement;
    private readonly IntPtr _windowHandle;
    private bool _isCompleting;

    /// <summary>Creates a passive, localized pricing dialog from application-layer data.</summary>
    internal AiPricingDialogWindow(
        ITrackMeUpApplication application,
        AiPricingOverview overview,
        ElementTheme theme,
        LocalizationService strings,
        AppWindow ownerAppWindow,
        IntPtr ownerHandle)
    {
        ArgumentNullException.ThrowIfNull(application);
        _overview = overview ?? throw new ArgumentNullException(nameof(overview));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        _ownerAppWindow = ownerAppWindow ?? throw new ArgumentNullException(nameof(ownerAppWindow));
        InitializeComponent();
        Title = T("AiPricing.Title");
        RootGrid.RequestedTheme = theme;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _placement = new WindowPlacementService(application, this, _appWindow, WindowStateKeys.AiPricing, LogicalWidth, LogicalHeight, LogicalScreenMargin, centerDefault: false);
        SetWindowOwner(_windowHandle, ownerHandle);
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        ApplyContent();
        Closed += (_, _) => _completion.TrySetResult();
    }

    /// <summary>Activates the detached Mica surface and completes after closure.</summary>
    internal Task ShowAsync()
    {
        SetWindowPos(_windowHandle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        Activate();
        return _completion.Task;
    }

    internal IntPtr WindowHandle => _windowHandle;

    internal void DisposePlacement() => _placement.Dispose();

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? 1d);
        var area = DisplayArea.GetFromWindowId(_ownerAppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var width = Math.Clamp(
            (int)Math.Ceiling(LogicalWidth * scale),
            1,
            Math.Max(1, area.Width - (margin * 2)));
        var height = Math.Clamp(
            (int)Math.Ceiling(LogicalHeight * scale),
            1,
            Math.Max(1, area.Height - (margin * 2)));
        _appWindow.Resize(new SizeInt32(width, height));

        var ownerBounds = new RectInt32(
            _ownerAppWindow.Position.X,
            _ownerAppWindow.Position.Y,
            _ownerAppWindow.Size.Width,
            _ownerAppWindow.Size.Height);
        var x = Math.Clamp(ownerBounds.X + ((ownerBounds.Width - width) / 2), area.X, Math.Max(area.X, area.X + area.Width - width));
        var y = Math.Clamp(ownerBounds.Y + ((ownerBounds.Height - height) / 2), area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        _appWindow.Move(new PointInt32(x, y));
        await _placement.RestoreOrKeepCurrentAsync(RootGrid, CancellationToken.None);
        CloseButton.Focus(FocusState.Programmatic);
    }

    private void ApplyContent()
    {
        var culture = CultureInfo.CurrentCulture;
        DialogTitleText.Text = T("AiPricing.Title");
        DialogSubtitleText.Text = T("AiPricing.Subtitle");
        AutomationProperties.SetName(RootGrid, $"{DialogTitleText.Text} dialog");
        AutomationProperties.SetName(DialogTitleText, DialogTitleText.Text);
        AutomationProperties.SetName(DialogSubtitleText, DialogSubtitleText.Text);

        EstimatedTodayLabel.Text = T("AiPricing.EstimatedToday");
        EstimatedTodayValue.Text = FormatOptionalUsd(_overview.EstimatedCostTodayUsd, T("AiPricing.Unavailable"), decimals: 4);
        EstimatedRequestsLabel.Text = T("AiPricing.EstimatedRequests");
        EstimatedRequestsValue.Text = _overview.EstimatedCostTodayRequestCount.ToString("N0", culture);
        TodayTokensLabel.Text = T("AiPricing.TodayTokens");
        TodayTokensValue.Text = _overview.TodayTotalTokens.ToString("N0", culture);
        LastSyncLabel.Text = T("AiPricing.LastSync");
        LastSyncValue.Text = _overview.LastSynchronizedAt is { } synchronizedAt
            ? synchronizedAt.ToLocalTime().ToString("g", culture)
            : T("AiPricing.NeverSynced");
        DisplayedModelsLabel.Text = T("AiPricing.Models");
        DisplayedModelsValue.Text = _overview.DisplayedModelCount.ToString("N0", culture);
        ReportedCostLabel.Text = T("AiPricing.ReportedCost");
        ReportedCostValue.Text = FormatOptionalUsd(_overview.ActualCostTodayUsd, T("AiPricing.Unavailable"), decimals: 4);

        ModelHeaderText.Text = T("AiPricing.Table.Model");
        InputHeaderText.Text = T("AiPricing.Table.Input");
        OutputHeaderText.Text = T("AiPricing.Table.Output");
        CloseButton.Content = T("About.Close");
        AutomationProperties.SetName(CloseButton, T("About.Close"));
        PopulatePricingRows();
    }

    private void PopulatePricingRows()
    {
        PricingRowsPanel.Children.Clear();
        if (_overview.Models.Count == 0)
        {
            PricingRowsPanel.Children.Add(new TextBlock
            {
                Margin = new Thickness(16),
                Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
                IsTextSelectionEnabled = true,
                Text = T("AiPricing.NoPrices"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            return;
        }

        foreach (var row in _overview.Models)
        {
            PricingRowsPanel.Children.Add(CreatePricingRow(row));
        }
    }

    private UIElement CreatePricingRow(AiPricingCostRow row)
    {
        var container = new Border
        {
            MinHeight = 42,
            Padding = new Thickness(16, 0, 16, 0),
            BorderBrush = ResourceBrush("DividerStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var grid = new Grid
        {
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var model = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            IsTextSelectionEnabled = true,
            Text = row.Model,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(model);

        var input = CreatePriceCell(FormatUsd(row.InputUsdPerMillionTokens, decimals: 4));
        Grid.SetColumn(input, 1);
        grid.Children.Add(input);

        var output = CreatePriceCell(FormatUsd(row.OutputUsdPerMillionTokens, decimals: 4));
        Grid.SetColumn(output, 2);
        grid.Children.Add(output);

        container.Child = grid;
        return container;
    }

    private static TextBlock CreatePriceCell(string value) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        IsTextSelectionEnabled = true,
        Text = value,
        TextAlignment = TextAlignment.Right,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };

    private string T(string key) => _strings.Translate(key);

    private static string FormatOptionalUsd(decimal? value, string unavailable, int decimals) =>
        value.HasValue ? FormatUsd(value.Value, decimals) : unavailable;

    private static string FormatUsd(decimal value, int decimals)
    {
        var format = decimals <= 2 ? "0.##" : "0.####";
        return "$" + value.ToString(format, CultureInfo.InvariantCulture);
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        await CompleteAsync();
    }

    private async void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        await CompleteAsync();
    }

    private async Task CompleteAsync()
    {
        if (_isCompleting)
        {
            return;
        }

        _isCompleting = true;
        CloseButton.IsEnabled = false;
        await _placement.SaveAsync(CancellationToken.None);
        Close();
    }

    private static Brush ResourceBrush(string key) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Colors.Transparent);

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}

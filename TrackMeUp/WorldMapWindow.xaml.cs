// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Renders the shared astronomical projection in an independent Acrylic window.</summary>
internal sealed partial class WorldMapWindow : Window
{
    private readonly AstronomyWindowController _controller;
    private LocalizationService _strings = new("system");

    /// <summary>Creates the independently restorable map over the shared application facade.</summary>
    internal WorldMapWindow(ITrackMeUpApplication application, MicaDialogService dialogs, AppSettings settings)
    {
        InitializeComponent();
        _controller = new AstronomyWindowController(
            this, RootGrid, TitleBarDragRegion, TitleBarLeftInsetColumn, TitleBarRightInsetColumn,
            LoadingIndicator, WorldMapNotificationBanner, application, dialogs,
            WindowStateKeys.WorldMap, 1120, 608, RenderSnapshot);
        ApplySettings(settings);
    }

    /// <summary>Applies the current theme and language without changing the selected reference instant.</summary>
    internal void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _strings = new LocalizationService(settings.UiLanguage);
        Title = _strings.Translate("WorldClock.Map.Title");
        TitleBarText.Text = Title.ToUpper(_strings.Culture);
        WorldMapControl.ApplyLanguage(_strings);
        _controller.ApplySettings(settings);
    }

    /// <summary>Mirrors the clock reference instant, including its live or explicitly selected time mode.</summary>
    internal void ApplySnapshot(WorldClockSnapshot snapshot, bool isLive) => _controller.ApplySnapshot(snapshot, isLive);

    /// <summary>Closes the map after the composition root has persisted the open application session.</summary>
    internal void CloseForShutdown() => _controller.CloseForShutdown();

    private void RenderSnapshot(WorldClockSnapshot snapshot)
    {
        WorldMapControl.Apply(snapshot.Map, _strings);
        WorldMapControl.Visibility = Visibility.Visible;
        ReferenceInstantText.Text = $"{snapshot.InstantUtc.ToString("g", _strings.Culture)} UTC";
        var label = $"{_strings.Translate("WorldClock.ReferenceInstant")}: {ReferenceInstantText.Text}";
        AutomationProperties.SetName(ReferenceInstantText, label);
        ToolTipService.SetToolTip(ReferenceInstantText, label);
    }
}

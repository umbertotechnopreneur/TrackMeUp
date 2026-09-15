// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Shows the reference instant's photographed Moon and calculated phase in an independent Acrylic window.</summary>
internal sealed partial class LunarPhaseWindow : Window
{
    private readonly AstronomyWindowController _controller;
    private LocalizationService _strings = new("system");

    /// <summary>Creates the independently restorable lunar display over the shared application facade.</summary>
    internal LunarPhaseWindow(ITrackMeUpApplication application, MicaDialogService dialogs, AppSettings settings)
    {
        InitializeComponent();
        _controller = new AstronomyWindowController(
            this, RootGrid, TitleBarDragRegion, TitleBarLeftInsetColumn, TitleBarRightInsetColumn,
            LoadingIndicator, LunarPhaseNotificationBanner, application, dialogs,
            WindowStateKeys.LunarPhase, 480, 560, RenderSnapshot);
        ApplySettings(settings);
    }

    /// <summary>Applies the current theme and language without changing the selected reference instant.</summary>
    internal void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _strings = new LocalizationService(settings.UiLanguage);
        Title = _strings.Translate("WorldClock.MoonPhase.Title");
        TitleBarText.Text = Title.ToUpper(_strings.Culture);
        _controller.ApplySettings(settings);
    }

    /// <summary>Mirrors the clocks' live or explicitly selected astronomical projection.</summary>
    internal void ApplySnapshot(WorldClockSnapshot snapshot, bool isLive) => _controller.ApplySnapshot(snapshot, isLive);

    /// <summary>Closes the lunar display after the composition root has persisted the open application session.</summary>
    internal void CloseForShutdown() => _controller.CloseForShutdown();

    private void RenderSnapshot(WorldClockSnapshot snapshot)
    {
        var phase = LunarPhaseProjection.Create(snapshot.Map.MoonPhaseAngleDegrees);
        MoonPhaseControl.MoonPhaseAngleDegrees = snapshot.Map.MoonPhaseAngleDegrees;
        PhaseSummaryText.Text = _strings.Format(
            "WorldClock.MoonPhase.Summary", _strings.Translate(phase.LocalizationKey), phase.IlluminatedPercentage);
        AutomationProperties.SetName(PhaseSummaryText, PhaseSummaryText.Text);
        ReferenceInstantText.Text = $"{snapshot.InstantUtc.ToString("g", _strings.Culture)} UTC";
        var label = $"{_strings.Translate("WorldClock.ReferenceInstant")}: {ReferenceInstantText.Text}";
        AutomationProperties.SetName(ReferenceInstantText, label);
        ToolTipService.SetToolTip(ReferenceInstantText, label);
        MoonContent.Visibility = Visibility.Visible;
    }
}

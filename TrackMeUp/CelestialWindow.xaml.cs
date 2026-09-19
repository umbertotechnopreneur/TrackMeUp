// SPDX-License-Identifier: MIT

using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using TrackMeUp.Application;
using TrackMeUp.Controls;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Hosts three independently restorable celestial surfaces with the shared disappearing title bar.</summary>
internal sealed partial class CelestialWindow : Window
{
    private readonly ITrackMeUpApplication _application;
    private readonly MicaDialogService _dialogs;
    private readonly AstronomyWindowController _controller;
    private readonly string _windowKey;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherQueueTimer _resizeTimer;
    private CancellationTokenSource? _projectionCancellation;
    private LocalizationService _strings = new("system");
    private WorldClockSnapshot? _reference;
    private bool _changingCities;
    private bool _closed;

    /// <summary>Creates a sky, agenda, or new flat/spherical Earth window over the application facade.</summary>
    internal CelestialWindow(ITrackMeUpApplication application, MicaDialogService dialogs, AppSettings settings, string windowKey)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _windowKey = windowKey;
        if (windowKey is not (WindowStateKeys.LocalSky or WindowStateKeys.AstronomyAgenda or WindowStateKeys.CelestialMap))
        {
            throw new ArgumentException("Unsupported celestial window.", nameof(windowKey));
        }

        InitializeComponent();
        _resizeTimer = DispatcherQueue.CreateTimer();
        _resizeTimer.IsRepeating = false;
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(250);
        _resizeTimer.Tick += ResizeTimer_Tick;
        _controller = new AstronomyWindowController(
            this, RootGrid, TitleBarDragRegion, TitleBarLeftInsetColumn, TitleBarRightInsetColumn,
            LoadingIndicator, NotificationBanner, application, dialogs, windowKey,
            windowKey == WindowStateKeys.AstronomyAgenda ? 440 : 800,
            windowKey == WindowStateKeys.CelestialMap ? 580 : 740,
            RenderSnapshot, celestialReferenceOnly: true);
        Closed += CelestialWindow_Closed;
        ApplySettings(settings);
    }

    /// <summary>Updates the acrylic theme, localized controls, and current projection.</summary>
    internal void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _strings = new LocalizationService(settings.UiLanguage);
        Title = T(_windowKey switch
        {
            WindowStateKeys.LocalSky => "Celestial.Sky.Title",
            WindowStateKeys.AstronomyAgenda => "Celestial.Agenda.Title",
            _ => "Celestial.Map.Title"
        });
        TitleBarText.Text = Title.ToUpper(_strings.Culture);
        CitySelector.PlaceholderText = T("Celestial.SelectCity");
        UiLocalization.SetAccessibleLabel(CitySelector, T(_windowKey == WindowStateKeys.CelestialMap ? "Celestial.Map.CenterCity" : "Celestial.Observer"));
        GlobeSwitch.OffContent = T("Celestial.Map.Flat");
        GlobeSwitch.OnContent = T("Celestial.Map.Globe");
        UiLocalization.SetAccessibleLabel(GlobeSwitch, T("Celestial.Map.Projection"));
        UiLocalization.SetAccessibleLabel(SkyZoom, T("Celestial.Sky.Zoom"));
        GlobeSwitch.Visibility = _windowKey == WindowStateKeys.CelestialMap ? Visibility.Visible : Visibility.Collapsed;
        SkyZoom.Visibility = _windowKey == WindowStateKeys.LocalSky ? Visibility.Visible : Visibility.Collapsed;
        BodiesScroll.Visibility = _windowKey == WindowStateKeys.LocalSky ? Visibility.Visible : Visibility.Collapsed;
        BodiesFrame.Visibility = BodiesScroll.Visibility;
        AboveHorizonText.Text = T("Celestial.Sky.AboveHorizon");
        ZodiacNoteText.Text = T("Celestial.Zodiac.Note");
        _controller.ApplySettings(settings);
    }

    /// <summary>Mirrors the world's selected live or converted reference instant.</summary>
    internal void ApplySnapshot(WorldClockSnapshot snapshot, bool isLive) => _controller.ApplySnapshot(snapshot, isLive);

    /// <summary>Closes the window after the composition root saved its workspace.</summary>
    internal void CloseForShutdown() => _controller.CloseForShutdown();

    /// <summary>Discards a failed opening without changing persisted placement.</summary>
    internal void CloseAfterFailedOpening() => _controller.CloseAfterFailedOpening();

    private void RenderSnapshot(WorldClockSnapshot snapshot)
    {
        _reference = snapshot;
        var selectedId = CitySelector.SelectedValue as string;
        _changingCities = true;
        try
        {
            CitySelector.ItemsSource = snapshot.Clocks;
            CitySelector.SelectedValue = snapshot.Clocks.Any(city => city.CityId == selectedId)
                ? selectedId : snapshot.Clocks.FirstOrDefault()?.CityId;
        }
        finally
        {
            _changingCities = false;
        }

        RefreshProjection();
    }

    private void CitySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_changingCities)
        {
            RefreshProjection();
        }
    }

    private void GlobeSwitch_Toggled(object sender, RoutedEventArgs e) => RefreshProjection();

    private void SkyZoom_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => SkyControl?.SetZoom(e.NewValue);

    private async void RefreshProjection()
    {
        if (_closed || _reference is null)
        {
            return;
        }

        _projectionCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _projectionCancellation = cancellation;
        var snapshot = _reference;
        var selectedCity = CitySelector.SelectedItem as WorldClockItem;
        var isMap = _windowKey == WindowStateKeys.CelestialMap;
        EmptyState.Visibility = Visibility.Collapsed;
        SkyControl.Visibility = Visibility.Collapsed;
        AgendaScroll.Visibility = Visibility.Collapsed;
        EarthControl.Visibility = Visibility.Collapsed;
        BodyItems.Children.Clear();
        StatusText.Text = string.Empty;
        ZodiacExpander.Visibility = Visibility.Collapsed;
        if (!isMap && selectedCity is null)
        {
            SkyControl.Visibility = Visibility.Collapsed;
            AgendaScroll.Visibility = Visibility.Collapsed;
            BodyItems.Children.Clear();
            StatusText.Text = string.Empty;
            EmptyState.Text = T("Celestial.NoCity");
            EmptyState.Visibility = Visibility.Visible;
            LoadingIndicator.IsActive = false;
            LoadingIndicator.Visibility = Visibility.Collapsed;
            _projectionCancellation = null;
            return;
        }

        ReferenceInstantText.Text = selectedCity is null
            ? $"{snapshot.InstantUtc.ToString("g", _strings.Culture)} UTC"
            : selectedCity.LocalTime.ToString("f", _strings.Culture);
        UiLocalization.SetAccessibleLabel(ReferenceInstantText, $"{T("WorldClock.ReferenceInstant")}: {ReferenceInstantText.Text}");
        LoadingIndicator.IsActive = true;
        LoadingIndicator.Visibility = Visibility.Visible;
        try
        {
            if (isMap)
            {
                var center = snapshot.Map.Cities.FirstOrDefault(city => city.CityId == selectedCity?.CityId);
                var pixelWidth = Math.Clamp((int)Math.Round(Math.Max(256d, ContentGrid.ActualWidth)), 256, 1440);
                var pixelHeight = GlobeSwitch.IsOn
                    ? Math.Clamp((int)Math.Round(Math.Max(160d, ContentGrid.ActualHeight - 100d)), 160, 960)
                    : pixelWidth / 2;
                var request = new CelestialMapRequest(snapshot.InstantUtc,
                    GlobeSwitch.IsOn ? CelestialMapProjection.Globe : CelestialMapProjection.Flat,
                    pixelWidth, pixelHeight,
                    center?.Latitude ?? 20d, center?.Longitude ?? 15d);
                // The application owns textures, solar calculations, and image generation; this window only presents its DTO.
                var result = await _application.GetCelestialMapAsync(request, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!result.Succeeded || result.Value is null)
                {
                    ShowFailure(result.MessageKey);
                    return;
                }

                await EarthControl.ApplyAsync(result.Value, _strings, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                EarthControl.Visibility = Visibility.Visible;
                SetStatus("Celestial.Map.Note");
            }
            else
            {
                // Core returns observer positions and city-local event times. No UI astronomy or time-zone I/O is required.
                var result = await _application.GetCelestialAsync(new CelestialRequest(selectedCity!.CityId, snapshot.InstantUtc), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!result.Succeeded || result.Value is null)
                {
                    ShowFailure(result.MessageKey);
                    return;
                }

                if (_windowKey == WindowStateKeys.LocalSky)
                {
                    SkyControl.Apply(result.Value, _strings);
                    SkyControl.Visibility = Visibility.Visible;
                    RenderBodies(result.Value);
                    SetStatus(result.Value.SunAltitudeDegrees >= 0d ? "Celestial.Sky.DaylightNote" : "Celestial.Sky.NightNote");
                }
                else
                {
                    RenderAgenda(result.Value);
                    RenderZodiac(result.Value.Zodiac);
                    AgendaScroll.Visibility = Visibility.Visible;
                    SetStatus("Celestial.Agenda.Note");
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer city, instant, projection, or close supersedes the pending rendering request.
        }
        catch (Exception exception)
        {
            if (!cancellation.IsCancellationRequested)
            {
                ShowFailure("Celestial.Unavailable", exception);
            }
        }
        finally
        {
            if (ReferenceEquals(_projectionCancellation, cancellation))
            {
                _projectionCancellation = null;
                LoadingIndicator.IsActive = false;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RenderBodies(CelestialSnapshot snapshot)
    {
        BodyItems.Children.Clear();
        foreach (var body in snapshot.Bodies.Where(body => body.IsAboveHorizon))
        {
            var name = T($"CelestialBody{body.Kind}");
            var label = new TextBlock
            {
                Text = name,
                FontSize = 15,
                FontWeight = FontWeights.Light,
                Foreground = ThemeBrush("TextFillColorPrimaryBrush")
            };
            UiLocalization.SetAccessibleLabel(label, _strings.Format("Celestial.Sky.BodyPosition", name, body.AltitudeDegrees, body.AzimuthDegrees));
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (body.Kind is CelestialBodyKind.Sun or CelestialBodyKind.Moon)
            {
                chip.Children.Add(new CelestialPhaseControl
                {
                    Width = 44,
                    Height = 44,
                    IsDaylight = body.Kind == CelestialBodyKind.Sun,
                    MoonPhaseAngleDegrees = snapshot.MoonPhaseAngleDegrees
                });
            }
            else
            {
                chip.Children.Add(new CelestialArtworkControl
                {
                    Width = 44,
                    Height = 44,
                    Kind = CelestialArtworkControl.ForPlanet(body.Kind)
                });
            }

            label.VerticalAlignment = VerticalAlignment.Center;
            var caption = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            caption.Children.Add(label);
            caption.Children.Add(new TextBlock
            {
                Text = body.AltitudeDegrees.ToString("0°", _strings.Culture),
                FontWeight = FontWeights.Light,
                FontSize = 12,
                Foreground = ThemeBrush("AccentTextFillColorPrimaryBrush")
            });
            chip.Children.Add(caption);
            BodyItems.Children.Add(chip);
        }
    }

    private void RenderAgenda(CelestialSnapshot snapshot)
    {
        AgendaRows.Children.Clear();
        DateOnly? previousDate = null;
        foreach (var item in snapshot.Agenda)
        {
            var date = DateOnly.FromDateTime(item.StartLocal.DateTime);
            if (date != previousDate)
            {
                AgendaRows.Children.Add(new TextBlock
                {
                    Text = item.StartLocal.ToString("dddd d MMMM yyyy", _strings.Culture),
                    FontSize = 13,
                    FontWeight = FontWeights.Light,
                    Margin = new Thickness(30, 12, 0, 2),
                    Foreground = ThemeBrush("TextFillColorSecondaryBrush")
                });
                previousDate = date;
            }

            var row = new Grid { ColumnSpacing = 14, MinHeight = 94 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = ThemeBrush("DividerStrokeColorDefaultBrush")
            });
            row.Children.Add(new Ellipse
            {
                Width = 7,
                Height = 7,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = ThemeBrush("AccentTextFillColorPrimaryBrush")
            });
            var surface = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = ThemeBrush("CardBackgroundFillColorDefaultBrush"),
                BorderBrush = ThemeBrush("DividerStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                Opacity = 0.45d,
                IsHitTestVisible = false
            };
            Grid.SetColumn(surface, 1);
            Grid.SetColumnSpan(surface, 2);
            row.Children.Add(surface);
            FrameworkElement illustration;
            if (item.Kind is CelestialEventKind.NewMoon or CelestialEventKind.FirstQuarter or CelestialEventKind.FullMoon or CelestialEventKind.LastQuarter)
            {
                illustration = new CelestialPhaseControl
                {
                    Width = 60,
                    Height = 60,
                    IsDaylight = false,
                    MoonPhaseAngleDegrees = item.Kind switch
                    {
                        CelestialEventKind.NewMoon => 0d,
                        CelestialEventKind.FirstQuarter => 90d,
                        CelestialEventKind.FullMoon => 180d,
                        _ => 270d
                    }
                };
            }
            else if (item.Kind == CelestialEventKind.MoonPlanetConjunction)
            {
                var relatedBody = item.RelatedBody ?? throw new InvalidDataException("A conjunction must identify its related planet.");
                var pair = new Grid { Width = 64, Height = 60 };
                pair.Children.Add(new CelestialPhaseControl
                {
                    Width = 38,
                    Height = 38,
                    IsDaylight = false,
                    MoonPhaseAngleDegrees = item.MoonPhaseAngleDegrees ?? throw new InvalidDataException("A conjunction must contain its lunar phase at the event instant."),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                pair.Children.Add(new CelestialArtworkControl
                {
                    Width = 32,
                    Height = 32,
                    Kind = CelestialArtworkControl.ForPlanet(relatedBody),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top
                });
                illustration = pair;
            }
            else if (item.Kind == CelestialEventKind.MeteorShower)
            {
                illustration = new Image
                {
                    Width = 60,
                    Height = 60,
                    Stretch = Stretch.Uniform,
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/Celestial/Artwork/meteor-shower-v1.png"))
                };
            }
            else
            {
                illustration = new CelestialArtworkControl
                {
                    Width = 60,
                    Height = 60,
                    Kind = item.Kind switch
                    {
                        CelestialEventKind.Sunrise => CelestialArtworkKind.Sunrise,
                        CelestialEventKind.Sunset => CelestialArtworkKind.Sunset,
                        CelestialEventKind.MorningBlueHour or CelestialEventKind.EveningBlueHour => CelestialArtworkKind.BlueHour,
                        CelestialEventKind.CivilDawn or CelestialEventKind.CivilDusk => CelestialArtworkKind.Twilight,
                        _ => CelestialArtworkKind.Seasons
                    }
                };
            }

            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(illustration, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
            Grid.SetColumn(illustration, 1);
            row.Children.Add(illustration);
            var details = new StackPanel { Spacing = 7, Margin = new Thickness(0, 12, 12, 12), VerticalAlignment = VerticalAlignment.Center };
            details.Children.Add(new TextBlock
            {
                Text = EventTitle(item),
                FontWeight = FontWeights.Light,
                FontSize = 19,
                TextWrapping = TextWrapping.Wrap
            });
            details.Children.Add(new TextBlock
            {
                Text = item.IsApproximate ? T("Celestial.Agenda.ApproximatePeak") : item.EndLocal is { } end
                    ? $"{item.StartLocal.ToString("t", _strings.Culture)} – {end.ToString("t", _strings.Culture)}"
                    : item.StartLocal.ToString("t", _strings.Culture),
                FontWeight = FontWeights.Light,
                FontSize = 13,
                Foreground = ThemeBrush("TextFillColorSecondaryBrush")
            });
            if (item.Kind == CelestialEventKind.MoonPlanetConjunction)
            {
                details.Children.Add(new TextBlock
                {
                    Text = _strings.Format("Celestial.Agenda.ConjunctionDetail",
                        item.SeparationDegrees ?? throw new InvalidDataException("A conjunction must contain its angular separation.")),
                    FontWeight = FontWeights.Light,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ThemeBrush("TextFillColorSecondaryBrush")
                });
            }
            else if (item.Kind == CelestialEventKind.MeteorShower)
            {
                details.Children.Add(new TextBlock
                {
                    Text = _strings.Format("Celestial.Agenda.MeteorActivity",
                        (item.ActivityStartDate ?? throw new InvalidDataException("A meteor shower must contain its activity start.")).ToString("d MMM yyyy", _strings.Culture),
                        (item.ActivityEndDate ?? throw new InvalidDataException("A meteor shower must contain its activity end.")).ToString("d MMM yyyy", _strings.Culture)),
                    FontWeight = FontWeights.Light,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ThemeBrush("TextFillColorSecondaryBrush")
                });
            }
            Grid.SetColumn(details, 2);
            row.Children.Add(details);
            AgendaRows.Children.Add(row);
        }

        if (snapshot.Agenda.Count == 0)
        {
            EmptyState.Text = T("Celestial.Agenda.NoEvents");
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private string EventTitle(CelestialAgendaEvent item) => item.Kind switch
    {
        CelestialEventKind.MoonPlanetConjunction => _strings.Format("Celestial.Agenda.ConjunctionTitle",
            T($"CelestialBody{item.RelatedBody ?? throw new InvalidDataException("A conjunction must identify its related planet.")}")),
        CelestialEventKind.MeteorShower => T($"CelestialMeteor{item.MeteorShowerId ?? throw new InvalidDataException("A meteor shower must identify its catalog entry.")}"),
        _ => T($"CelestialEvent{item.Kind}")
    };

    private void RenderZodiac(CelestialZodiacSnapshot zodiac)
    {
        ZodiacExpander.Visibility = Visibility.Visible;
        ZodiacSummaryText.Text = _strings.Format("Celestial.Zodiac.Current", T($"CelestialZodiac{zodiac.CurrentSign}"));
        CurrentZodiacImage.Source = ZodiacImage(zodiac.CurrentSign);
        UiLocalization.SetAccessibleLabel(ZodiacExpander, ZodiacSummaryText.Text);
        ZodiacItems.Children.Clear();
        foreach (var sector in zodiac.Signs)
        {
            var current = sector.Sign == zodiac.CurrentSign;
            var name = T($"CelestialZodiac{sector.Sign}");
            var content = new StackPanel { Spacing = 6 };
            var image = new Image { Source = ZodiacImage(sector.Sign), Width = 44, Height = 44 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(image, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
            content.Children.Add(image);
            content.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                FontWeight = FontWeights.Light,
                TextAlignment = TextAlignment.Center,
                Foreground = ThemeBrush(current ? "AccentTextFillColorPrimaryBrush" : "TextFillColorPrimaryBrush")
            });
            content.Children.Add(new TextBlock
            {
                Text = $"{sector.StartLongitudeDegrees:0}°–{sector.EndLongitudeDegrees:0}°",
                FontSize = 10,
                FontWeight = FontWeights.Light,
                TextAlignment = TextAlignment.Center,
                Foreground = ThemeBrush("TextFillColorSecondaryBrush")
            });
            var tile = new Border
            {
                Child = content,
                MinWidth = 78,
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(current ? 1.5d : 1d),
                BorderBrush = ThemeBrush(current ? "AccentTextFillColorPrimaryBrush" : "DividerStrokeColorDefaultBrush")
            };
            UiLocalization.SetAccessibleLabel(tile, current ? ZodiacSummaryText.Text : name);
            ZodiacItems.Children.Add(tile);
        }
    }

    private static BitmapImage ZodiacImage(TropicalZodiacSign sign) =>
        new(new Uri($"ms-appx:///Assets/Celestial/Zodiac/{sign.ToString().ToLowerInvariant()}.png"));

    private void SetStatus(string key)
    {
        StatusText.Text = T(key);
        UiLocalization.SetAccessibleLabel(StatusText, StatusText.Text);
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 400d || e.NewSize.Height < 320d;
        if (ContentGrid is not null)
        {
            ContentGrid.Margin = compact ? new Thickness(8, 4, 8, 8) : new Thickness(20, 8, 20, 16);
            ContentGrid.RowSpacing = compact ? 6d : 12d;
            TitleBarLogo.Margin = compact ? new Thickness(8, 0, 8, 0) : new Thickness(16, 0, 10, 0);
            TitleBarText.Visibility = e.NewSize.Width < 280d ? Visibility.Collapsed : Visibility.Visible;
            ReferenceInstantText.Visibility = e.NewSize.Height < 260d ? Visibility.Collapsed : Visibility.Visible;
            SkyZoom.Width = e.NewSize.Width < 360d ? 60d : 96d;
            ZodiacSummaryText.MaxWidth = Math.Max(60d, e.NewSize.Width - 160d);
            ZodiacContentScroll.MaxHeight = Math.Clamp(e.NewSize.Height * 0.3d, 80d, 220d);
            Footer.Visibility = e.NewSize.Height < 300d ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_windowKey == WindowStateKeys.CelestialMap && _resizeTimer is not null)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
    }

    private void ResizeTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        RefreshProjection();
    }

    private void ShowFailure(string key, Exception? exception = null)
    {
        if (_closed)
        {
            return;
        }

        // Hide obsolete content on failure so a new observer or reference label cannot describe an older projection.
        SkyControl.Visibility = Visibility.Collapsed;
        AgendaScroll.Visibility = Visibility.Collapsed;
        EarthControl.Visibility = Visibility.Collapsed;
        BodyItems.Children.Clear();
        StatusText.Text = string.Empty;
        EmptyState.Text = T("Celestial.Unavailable");
        EmptyState.Visibility = Visibility.Visible;
        var message = exception is null ? T(key) : $"{T(key)} ({exception.GetType().Name})";
        _dialogs.Notifications.ShowError(NotificationBanner, Title, message);
    }

    private void CelestialWindow_Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        Closed -= CelestialWindow_Closed;
        _resizeTimer.Stop();
        _resizeTimer.Tick -= ResizeTimer_Tick;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private Brush ThemeBrush(string key) => key switch
    {
        "TextFillColorPrimaryBrush" => PrimaryPalette.Background,
        "TextFillColorSecondaryBrush" => SecondaryPalette.Background,
        "AccentTextFillColorPrimaryBrush" => AccentPalette.Background,
        "DividerStrokeColorDefaultBrush" => DividerPalette.Background,
        "CardBackgroundFillColorDefaultBrush" => CardPalette.Background,
        _ => throw new ArgumentException("Unsupported celestial theme brush.", nameof(key))
    };

    private string T(string key) => _strings.Translate(key);
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Passively renders world-clock DTOs and forwards add/remove intent to the host.</summary>
public sealed partial class WorldClockRailControl : UserControl
{
    private readonly ObservableCollection<ClockCardViewModel> _clocks = [];
    private LocalizationService _strings = new("system");
    private bool _canAdd;

    /// <summary>Creates the reusable world-clock rail.</summary>
    public WorldClockRailControl()
    {
        InitializeComponent();
        ClockItems.ItemsSource = _clocks;
    }

    /// <summary>Occurs when the add affordance is invoked.</summary>
    public event EventHandler? AddRequested;

    /// <summary>Occurs when a card's remove affordance is invoked.</summary>
    public event EventHandler<WorldClockCityEventArgs>? RemoveRequested;

    /// <summary>Reconciles the rendered DTO snapshot while preserving per-card interaction state.</summary>
    public void ApplySnapshot(WorldClockRailSnapshot snapshot, LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));

        var cityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Clocks)
        {
            if (string.IsNullOrWhiteSpace(item.CityId))
            {
                throw new InvalidDataException("A world-clock snapshot contains an empty city identifier.");
            }

            if (!cityIds.Add(item.CityId))
            {
                throw new InvalidDataException($"A world-clock snapshot contains duplicate city '{item.CityId}'.");
            }
        }

        // Move existing instances into snapshot order so expansion state survives minute refreshes.
        // Only genuinely new cities allocate a view model and image source.
        for (var index = 0; index < snapshot.Clocks.Count; index++)
        {
            var item = snapshot.Clocks[index];
            var existingIndex = FindClockIndex(item.CityId);
            ClockCardViewModel clock;
            if (existingIndex < 0)
            {
                clock = new ClockCardViewModel(item, _strings, index == 0, isExpanded: false);
                _clocks.Insert(index, clock);
            }
            else
            {
                clock = _clocks[existingIndex];
                if (existingIndex != index)
                {
                    _clocks.Move(existingIndex, index);
                }

                clock.Update(item, _strings, index == 0);
            }
        }

        while (_clocks.Count > snapshot.Clocks.Count)
        {
            _clocks.RemoveAt(_clocks.Count - 1);
        }

        _canAdd = snapshot.Clocks.Count < snapshot.MaximumClocks;
        var addName = _strings.Translate("WorldClock.Add");
        var landmarkName = _strings.Translate("WorldClock.Landmark");
        var addVisibility = _canAdd ? Visibility.Visible : Visibility.Collapsed;
        AddClockHost.Visibility = addVisibility;
        AddClockButton.Visibility = addVisibility;
        AddClockButton.IsEnabled = _canAdd;
        AutomationProperties.SetName(AddClockButton, addName);
        ToolTipService.SetToolTip(AddClockButton, addName);
        AutomationProperties.SetName(RailRoot, landmarkName);
        AutomationProperties.SetLocalizedLandmarkType(RailRoot, landmarkName);
    }

    private int FindClockIndex(string cityId)
    {
        for (var index = 0; index < _clocks.Count; index++)
        {
            if (string.Equals(_clocks[index].CityId, cityId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void AddClockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_canAdd)
        {
            AddRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Fades card actions in without moving any clock content.</summary>
    private void ClockCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClockCardViewModel clock } card)
        {
            clock.IsPointerOver = true;
            SetClockActionsVisible(card, isVisible: true);
        }
    }

    /// <summary>Fades card actions out after pointer exit unless keyboard focus still owns them.</summary>
    private void ClockCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClockCardViewModel clock } card)
        {
            clock.IsPointerOver = false;
            SetClockActionsVisible(card, clock.HasActionFocus);
        }
    }

    private void ClockActionButton_GotFocus(object sender, RoutedEventArgs e)
    {
        if (FindClockCard(sender as DependencyObject) is { DataContext: ClockCardViewModel clock } card)
        {
            clock.HasActionFocus = true;
            SetClockActionsVisible(card, isVisible: true);
        }
    }

    private void ClockActionButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (FindClockCard(sender as DependencyObject) is not { DataContext: ClockCardViewModel clock } card)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            clock.HasActionFocus = IsFocusWithin(card);
            SetClockActionsVisible(card, clock.IsPointerOver || clock.HasActionFocus);
        });
    }

    private static FrameworkElement? FindClockCard(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Name: "ClockCard" } card)
            {
                return card;
            }
        }

        return null;
    }

    private static bool IsFocusWithin(FrameworkElement card)
    {
        var focused = FocusManager.GetFocusedElement(card.XamlRoot) as DependencyObject;
        for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, card))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetClockActionsVisible(FrameworkElement card, bool isVisible)
    {
        foreach (var actionName in new[] { "HeroClockActions", "CompactClockActions" })
        {
            if (card.FindName(actionName) is not FrameworkElement actions)
            {
                throw new InvalidOperationException($"World-clock action host '{actionName}' is missing.");
            }

            actions.IsHitTestVisible = isVisible;
            var animation = new DoubleAnimation
            {
                From = actions.Opacity,
                To = isVisible ? 1d : 0d,
                Duration = new Duration(TimeSpan.FromMilliseconds(140))
            };
            Storyboard.SetTarget(animation, actions);
            Storyboard.SetTargetProperty(animation, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
    }

    private void DetailButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ClockCardViewModel clock)
        {
            clock.IsExpanded = !clock.IsExpanded;
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ClockCardViewModel clock)
        {
            RemoveRequested?.Invoke(this, new WorldClockCityEventArgs(clock.CityId, clock.CityName));
        }
    }

    private sealed class ClockCardViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isDaylight;
        private bool _isHero;
        private string _cityName = string.Empty;
        private double _moonPhaseAngleDegrees;
        private string _localTimeText = string.Empty;
        private string _dayStateText = string.Empty;
        private Brush _dayStateBrush = null!;
        private Brush _ambientGlowBrush = null!;
        private string _sunTimesText = string.Empty;
        private string _moonSummaryText = string.Empty;
        private string _detailAccessibleName = string.Empty;
        private string _removeAccessibleName = string.Empty;
        private string _sunDetailTitle = string.Empty;
        private string _moonDetailTitle = string.Empty;
        private string _sunriseText = string.Empty;
        private string _sunsetText = string.Empty;
        private string _moonriseText = string.Empty;
        private string _moonsetText = string.Empty;
        private Visibility _heroVisibility;
        private Visibility _compactVisibility;
        private double _minimumHeight;
        private double _currentHour;
        private double _sunriseHour;
        private double _sunsetHour;
        private string? _skylineAssetPath;
        private BitmapImage _skylineImage = null!;
        private bool _hasVisualState;

        public bool IsPointerOver { get; set; }
        public bool HasActionFocus { get; set; }

        public ClockCardViewModel(WorldClockItem item, LocalizationService strings, bool isHero, bool isExpanded)
        {
            CityId = item.CityId;
            _isExpanded = isExpanded;
            Update(item, strings, isHero);
        }

        /// <summary>Updates all DTO- and localization-derived presentation values in place.</summary>
        public void Update(WorldClockItem item, LocalizationService strings, bool isHero)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(strings);
            if (!string.Equals(CityId, item.CityId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Cannot update world-clock '{CityId}' with city '{item.CityId}'.");
            }

            var daylightChanged = !_hasVisualState || _isDaylight != item.IsDaylight;
            var heroChanged = !_hasVisualState || _isHero != isHero;
            SetProperty(ref _cityName, item.CityName.ToUpper(strings.Culture), nameof(CityName));
            SetProperty(ref _isDaylight, item.IsDaylight, nameof(IsDaylight));
            SetProperty(ref _moonPhaseAngleDegrees, item.MoonPhaseAngleDegrees, nameof(MoonPhaseAngleDegrees));
            SetProperty(ref _localTimeText, item.LocalTime.ToString("HH:mm", strings.Culture), nameof(LocalTimeText));
            SetProperty(
                ref _dayStateText,
                strings.Translate(item.IsDaylight ? "WorldClock.Day" : "WorldClock.Night"),
                nameof(DayStateText));

            if (daylightChanged)
            {
                _dayStateBrush = CreateSolidBrush(item.IsDaylight ? 0xFFFF9B82u : 0xFF9B94FFu);
                _ambientGlowBrush = CreateAmbientGlowBrush(item.IsDaylight);
                OnPropertyChanged(nameof(DayStateBrush));
                OnPropertyChanged(nameof(AmbientGlowBrush));
            }

            if (heroChanged)
            {
                _isHero = isHero;
                SetProperty(
                    ref _heroVisibility,
                    isHero ? Visibility.Visible : Visibility.Collapsed,
                    nameof(HeroVisibility));
                SetProperty(
                    ref _compactVisibility,
                    isHero ? Visibility.Collapsed : Visibility.Visible,
                    nameof(CompactVisibility));
                SetProperty(ref _minimumHeight, isHero ? 175d : 111d, nameof(MinimumHeight));
            }

            var sunrise = FormatTime(item.Sunrise, strings);
            var sunset = FormatTime(item.Sunset, strings);
            var moonrise = FormatTime(item.Moonrise, strings);
            var moonset = FormatTime(item.Moonset, strings);
            SetProperty(ref _sunTimesText, $"↑ {sunrise}  ↓ {sunset}", nameof(SunTimesText));
            SetProperty(
                ref _moonSummaryText,
                strings.Format("WorldClock.IlluminationCompact", Math.Round(item.MoonIllumination * 100d)),
                nameof(MoonSummaryText));
            SetProperty(
                ref _detailAccessibleName,
                strings.Format("WorldClock.Detail", item.CityName),
                nameof(DetailAccessibleName));
            SetProperty(
                ref _removeAccessibleName,
                strings.Format("WorldClock.Remove", item.CityName),
                nameof(RemoveAccessibleName));
            SetProperty(ref _sunDetailTitle, strings.Translate("WorldClock.Sun"), nameof(SunDetailTitle));
            SetProperty(
                ref _moonDetailTitle,
                strings.Format(
                    "WorldClock.MoonWithPhase",
                    strings.Translate($"WorldClock.MoonPhase.{item.MoonPhaseKey}")),
                nameof(MoonDetailTitle));
            SetProperty(ref _sunriseText, strings.Format("WorldClock.Sunrise", sunrise), nameof(SunriseText));
            SetProperty(ref _sunsetText, strings.Format("WorldClock.Sunset", sunset), nameof(SunsetText));
            SetProperty(ref _moonriseText, strings.Format("WorldClock.Moonrise", moonrise), nameof(MoonriseText));
            SetProperty(ref _moonsetText, strings.Format("WorldClock.Moonset", moonset), nameof(MoonsetText));
            SetProperty(ref _currentHour, ToHour(item.LocalTime), nameof(CurrentHour));
            SetProperty(ref _sunriseHour, ToHour(item.Sunrise), nameof(SunriseHour));
            SetProperty(ref _sunsetHour, ToHour(item.Sunset), nameof(SunsetHour));

            if (!string.Equals(_skylineAssetPath, item.SkylineAssetPath, StringComparison.Ordinal))
            {
                _skylineAssetPath = item.SkylineAssetPath;
                _skylineImage = new BitmapImage(new Uri($"ms-appx:///{item.SkylineAssetPath}"));
                OnPropertyChanged(nameof(SkylineImage));
            }

            _hasVisualState = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public string CityId { get; }
        public string CityName => _cityName;
        public bool IsDaylight => _isDaylight;
        public double MoonPhaseAngleDegrees => _moonPhaseAngleDegrees;
        public string LocalTimeText => _localTimeText;
        public string DayStateText => _dayStateText;
        public Brush DayStateBrush => _dayStateBrush;
        public Brush AmbientGlowBrush => _ambientGlowBrush;
        public string SunTimesText => _sunTimesText;
        public string MoonSummaryText => _moonSummaryText;
        public string DetailAccessibleName => _detailAccessibleName;
        public string RemoveAccessibleName => _removeAccessibleName;
        public string SunDetailTitle => _sunDetailTitle;
        public string MoonDetailTitle => _moonDetailTitle;
        public string SunriseText => _sunriseText;
        public string SunsetText => _sunsetText;
        public string MoonriseText => _moonriseText;
        public string MoonsetText => _moonsetText;
        public Visibility HeroVisibility => _heroVisibility;
        public Visibility CompactVisibility => _compactVisibility;
        public double MinimumHeight => _minimumHeight;
        public double CurrentHour => _currentHour;
        public double SunriseHour => _sunriseHour;
        public double SunsetHour => _sunsetHour;
        public BitmapImage SkylineImage => _skylineImage;
        public Visibility DetailsVisibility => _isExpanded ? Visibility.Visible : Visibility.Collapsed;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged(nameof(DetailsVisibility));
            }
        }

        private static string FormatTime(DateTimeOffset? value, LocalizationService strings) =>
            value?.ToString("HH:mm", strings.Culture) ?? strings.Translate("WorldClock.NoEvent");

        private static double ToHour(DateTimeOffset value) => value.TimeOfDay.TotalHours;

        private static double ToHour(DateTimeOffset? value) => value?.TimeOfDay.TotalHours ?? -1d;

        private static SolidColorBrush CreateSolidBrush(uint argb) => new(Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));

        private static RadialGradientBrush CreateAmbientGlowBrush(bool isDaylight)
        {
            var color = isDaylight
                ? Windows.UI.Color.FromArgb(190, 255, 132, 68)
                : Windows.UI.Color.FromArgb(165, 111, 105, 232);
            var brush = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.5d, 0.5d),
                GradientOrigin = new Windows.Foundation.Point(0.5d, 0.5d),
                RadiusX = 0.5d,
                RadiusY = 0.5d
            };
            brush.GradientStops.Add(new GradientStop { Color = color, Offset = 0d });
            brush.GradientStops.Add(new GradientStop
            {
                Color = Windows.UI.Color.FromArgb(0, color.R, color.G, color.B),
                Offset = 1d
            });
            return brush;
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>Identifies the selected world-clock city without exposing presentation controls.</summary>
public sealed class WorldClockCityEventArgs(string cityId, string cityName) : EventArgs
{
    public string CityId { get; } = cityId;
    public string CityName { get; } = cityName;
}

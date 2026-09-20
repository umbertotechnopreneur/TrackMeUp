// SPDX-License-Identifier: MIT

using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Collects label edits and sends them through the shared application facade.</summary>
public sealed class ActivityLabelsEditor : UserControl
{
    private readonly StackPanel _root = new() { Spacing = 16 };
    private readonly ComboBox _labels = new() { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _name = new() { MaxLength = 20, MinWidth = 0 };
    private readonly Grid _actions = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly Button _appearance = new();
    private readonly Button _add = new();
    private readonly Button _save = new();
    private readonly Button _delete = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private ITrackMeUpApplication? _application;
    private AppSettings? _settings;
    private LocalizationService _strings = new("system");
    private string _id = "";
    private string _icon = "work";
    private string _color = ActivityLabelCatalog.Colors[1];
    private bool _updating;
    private bool _busy;

    /// <summary>Creates a compact editor with a color palette and searchable icon flyout.</summary>
    public ActivityLabelsEditor()
    {
        Content = _root;
        _root.Children.Add(_labels);
        var fields = new Grid { ColumnSpacing = 8 };
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fields.Children.Add(_name);
        Grid.SetColumn(_appearance, 1);
        fields.Children.Add(_appearance);
        _root.Children.Add(fields);
        _appearance.VerticalAlignment = VerticalAlignment.Bottom;
        for (var index = 0; index < 3; index++)
        {
            _actions.ColumnDefinitions.Add(new ColumnDefinition());
            _actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        foreach (var button in new[] { _add, _save, _delete })
        {
            button.MinWidth = 0;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            _actions.Children.Add(button);
        }
        _save.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
        _root.Children.Add(_actions);
        SizeChanged += (_, _) => UpdateActionLayout();
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _root.Children.Add(_status);
        _labels.SelectionChanged += (_, _) =>
        {
            if (!_updating && _labels.SelectedItem is ComboBoxItem { Tag: ActivityLabelDefinition label }) LoadDraft(label);
        };
        _add.Click += (_, _) =>
        {
            _labels.SelectedIndex = -1;
            LoadDraft(null);
            _name.Focus(FocusState.Programmatic);
        };
        _save.Click += async (_, _) => await SaveAsync("activity.label.save",
            JsonSerializer.Serialize(new ActivityLabelDefinition(_id, _name.Text.Trim(), _icon, _color)));
        _delete.Click += async (_, _) => await SaveAsync("activity.label.delete", _id);
    }

    /// <summary>Reports a settings snapshot only after successful application-layer persistence.</summary>
    public event Action<AppSettings>? SettingsSaved;

    /// <summary>Reports when a facade mutation starts or finishes so the host can defer dismissal.</summary>
    internal event Action<bool>? BusyChanged;

    internal bool IsBusy => _busy;

    /// <summary>Moves keyboard focus to the label name without creating or saving a draft.</summary>
    internal void FocusEditor() => _name.Focus(FocusState.Programmatic);

    /// <summary>Refreshes saved choices without overwriting a draft when unrelated settings change.</summary>
    public void ApplySettings(ITrackMeUpApplication application, AppSettings settings)
    {
        _application = application;
        var changed = _settings is null || !(_settings.ActivityLabels ?? []).SequenceEqual(settings.ActivityLabels ?? [])
            || _settings.UiLanguage != settings.UiLanguage;
        _settings = settings;
        if (!changed) return;
        _strings = new LocalizationService(settings.UiLanguage);
        _name.Header = new TextBlock { Text = T("Labels.Name"), TextWrapping = TextWrapping.Wrap };
        _labels.Header = new TextBlock { Text = T("Labels.Existing"), TextWrapping = TextWrapping.Wrap };
        _labels.PlaceholderText = T("Labels.New");
        AutomationProperties.SetName(_name, T("Labels.Name"));
        AutomationProperties.SetName(_labels, T("Labels.Title"));
        SetActionLabel(_add, "Labels.New");
        SetActionLabel(_save, "Labels.Save");
        SetActionLabel(_delete, "Labels.Delete");
        UpdateActionLayout();
        ToolTipService.SetToolTip(_appearance, T("Labels.Appearance"));
        AutomationProperties.SetName(_appearance, T("Labels.Appearance"));
        _updating = true;
        try
        {
            _labels.Items.Clear();
            foreach (var label in settings.ActivityLabels ?? [])
            {
                var item = new ComboBoxItem { Content = ActivityLabelVisuals.Content(label), Tag = label };
                AutomationProperties.SetName(item, label.Name);
                _labels.Items.Add(item);
                if (label.Id == _id) _labels.SelectedItem = item;
            }
            LoadDraft((settings.ActivityLabels ?? []).FirstOrDefault(label => label.Id == _id));
        }
        finally { _updating = false; }
    }

    private string T(string key) => _strings.Translate(key);

    private void SetActionLabel(Button button, string key)
    {
        button.Content = new TextBlock { Text = T(key), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        AutomationProperties.SetName(button, T(key));
    }

    private void UpdateActionLayout()
    {
        // Measure translated, scaled captions instead of assuming English button widths.
        var buttons = new[] { _add, _save, _delete };
        var widest = 0d;
        foreach (var button in buttons)
        {
            if (button.Content is not FrameworkElement content) continue;
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, content.DesiredSize.Width + button.Padding.Left + button.Padding.Right + 4);
        }
        var stacked = ActualWidth < widest * 3 + _actions.ColumnSpacing * 2;
        for (var index = 0; index < buttons.Length; index++)
        {
            Grid.SetRow(buttons[index], stacked ? index : 0);
            Grid.SetColumn(buttons[index], stacked ? 0 : index);
            Grid.SetColumnSpan(buttons[index], stacked ? 3 : 1);
        }
    }

    private void LoadDraft(ActivityLabelDefinition? label)
    {
        _id = label?.Id ?? "";
        _name.Text = label?.Name ?? "";
        _icon = label?.Icon ?? "work";
        _color = label?.Color ?? ActivityLabelCatalog.Colors[1];
        _delete.IsEnabled = _id.Length > 0;
        _status.Text = "";
        UpdateAppearance();
        BuildPicker();
    }

    private void UpdateAppearance() => _appearance.Content = ActivityLabelVisuals.Icon(_icon, _color);

    private void BuildPicker()
    {
        var panel = new StackPanel { Spacing = 12, Width = 280 };
        var colors = new Grid();
        var colorButtons = new List<ToggleButton>();
        var iconButtons = new List<(ToggleButton Button, string Icon)>();
        for (var index = 0; index < ActivityLabelCatalog.Colors.Count; index++)
        {
            var color = ActivityLabelCatalog.Colors[index];
            colors.ColumnDefinitions.Add(new ColumnDefinition());
            var swatch = new ToggleButton
            {
                Padding = new Thickness(3),
                MinWidth = 0,
                IsChecked = color == _color,
                Content = new Border { Width = 23, Height = 23, CornerRadius = new CornerRadius(12), Background = ActivityLabelVisuals.Brush(color) }
            };
            var caption = T($"Labels.Color.{index}");
            AutomationProperties.SetName(swatch, caption);
            ToolTipService.SetToolTip(swatch, caption);
            swatch.Click += (_, _) =>
            {
                _color = color;
                foreach (var button in colorButtons) button.IsChecked = button == swatch;
                foreach (var (button, icon) in iconButtons) button.Content = ActivityLabelVisuals.Icon(icon, color);
                UpdateAppearance();
            };
            colorButtons.Add(swatch);
            Grid.SetColumn(swatch, index);
            colors.Children.Add(swatch);
        }
        panel.Children.Add(colors);
        var search = new TextBox { PlaceholderText = T("Labels.SearchIcons") };
        AutomationProperties.SetName(search, T("Labels.SearchIcons"));
        panel.Children.Add(search);
        var icons = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (var column = 0; column < 4; column++) icons.ColumnDefinitions.Add(new ColumnDefinition());
        for (var row = 0; row < 4; row++) icons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var icon in ActivityLabelCatalog.Icons)
        {
            var button = new ToggleButton { Content = ActivityLabelVisuals.Icon(icon, _color), IsChecked = icon == _icon, HorizontalAlignment = HorizontalAlignment.Stretch, Height = 40 };
            AutomationProperties.SetName(button, T($"Labels.Icon.{icon}"));
            ToolTipService.SetToolTip(button, T($"Labels.Icon.{icon}"));
            button.Click += (_, _) =>
            {
                _icon = icon;
                foreach (var item in iconButtons) item.Button.IsChecked = item.Button == button;
                UpdateAppearance();
            };
            iconButtons.Add((button, icon));
            icons.Children.Add(button);
        }
        void Filter()
        {
            var index = 0;
            foreach (var (button, icon) in iconButtons)
            {
                var visible = T($"Labels.Icon.{icon}").Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase);
                button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible) { Grid.SetColumn(button, index % 4); Grid.SetRow(button, index++ / 4); }
            }
        }
        search.TextChanged += (_, _) => Filter();
        Filter();
        panel.Children.Add(icons);
        _appearance.Flyout = new Flyout { Content = panel };
    }

    private async Task SaveAsync(string key, string value)
    {
        if (_application is null || _busy) return;
        _busy = true;
        BusyChanged?.Invoke(true);
        IsEnabled = false;
        try
        {
            // Persistence and validation remain in Core; keep the draft visible if the request fails.
            var result = await _application.PatchSettingsAsync(new SettingsPatch(new Dictionary<string, string?> { [key] = value }), CancellationToken.None);
            if (!result.Succeeded || result.Value is null) { _status.Text = T(result.MessageKey); return; }
            if (key == "activity.label.save" && _id.Length == 0)
                _id = result.Value.ActivityLabels!.Single(label => label.Name == _name.Text.Trim()).Id;
            ApplySettings(_application, result.Value);
            _status.Text = T("Labels.Saved");
            SettingsSaved?.Invoke(result.Value);
        }
        catch (Exception)
        {
            // Interop or persistence errors must not be presented as a successful edit.
            _status.Text = T("Labels.SaveError");
        }
        finally { _busy = false; IsEnabled = true; BusyChanged?.Invoke(false); }
    }
}

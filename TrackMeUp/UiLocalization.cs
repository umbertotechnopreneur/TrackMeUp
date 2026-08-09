using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Applies the resolved application language to tagged WinUI presentation elements.</summary>
internal static class UiLocalization
{
    /// <summary>Localizes one visual subtree without performing persistence or environment access.</summary>
    public static void Apply(DependencyObject root, LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(strings);

        if (root is FrameworkElement element)
        {
            element.Language = strings.Language;
            if (element.Tag is string key && !string.IsNullOrWhiteSpace(key))
            {
                ApplyElement(element, key, strings);
            }
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            Apply(VisualTreeHelper.GetChild(root, index), strings);
        }
    }

    private static void ApplyElement(FrameworkElement element, string key, LocalizationService strings)
    {
        switch (element)
        {
            case TextBlock textBlock:
                SetIfTranslated(strings, key, value => textBlock.Text = value);
                break;
            case Button button:
                ApplyButtonLabel(button, key, strings);
                break;
            case CheckBox checkBox:
                SetIfTranslated(strings, key, value =>
                {
                    checkBox.Content = value;
                    AutomationProperties.SetName(checkBox, value);
                });
                break;
            case ToggleButton toggleButton:
                ApplyButtonLabel(toggleButton, key, strings);
                break;
            case ToggleSwitch toggle:
                SetIfTranslated(strings, $"{key}.Header", value => toggle.Header = value);
                SetIfTranslated(strings, $"{key}.Off", value => toggle.OffContent = value);
                SetIfTranslated(strings, $"{key}.On", value => toggle.OnContent = value);
                SetIfTranslated(strings, $"{key}.Header", value => AutomationProperties.SetName(toggle, value));
                break;
            case ComboBox comboBox:
                SetIfTranslated(strings, $"{key}.Header", value =>
                {
                    comboBox.Header = value;
                    AutomationProperties.SetName(comboBox, value);
                });
                foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
                {
                    var option = item.Tag?.ToString();
                    if (!string.IsNullOrWhiteSpace(option))
                    {
                        SetIfTranslated(strings, $"{key}.{option}", value => item.Content = value);
                    }
                }
                break;
            case TextBox textBox:
                SetIfTranslated(strings, $"{key}.Header", value =>
                {
                    textBox.Header = value;
                    AutomationProperties.SetName(textBox, value);
                });
                SetIfTranslated(strings, $"{key}.Placeholder", value => textBox.PlaceholderText = value);
                break;
            case PasswordBox passwordBox:
                SetIfTranslated(strings, $"{key}.Header", value =>
                {
                    passwordBox.Header = value;
                    AutomationProperties.SetName(passwordBox, value);
                });
                SetIfTranslated(strings, $"{key}.Placeholder", value => passwordBox.PlaceholderText = value);
                break;
            case CalendarDatePicker datePicker:
                SetIfTranslated(strings, $"{key}.Header", value =>
                {
                    datePicker.Header = value;
                    AutomationProperties.SetName(datePicker, value);
                });
                SetIfTranslated(strings, $"{key}.Placeholder", value => datePicker.PlaceholderText = value);
                break;
            case MenuFlyoutItem menuItem:
                SetIfTranslated(strings, key, value =>
                {
                    menuItem.Text = value;
                    AutomationProperties.SetName(menuItem, value);
                });
                break;
            case DatePicker datePicker:
                SetIfTranslated(strings, $"{key}.Header", value =>
                {
                    datePicker.Header = value;
                    AutomationProperties.SetName(datePicker, value);
                });
                break;
            case Thumb thumb:
                SetIfTranslated(strings, key, value =>
                {
                    AutomationProperties.SetName(thumb, value);
                    ToolTipService.SetToolTip(thumb, value);
                });
                break;
        }
    }

    private static void ApplyButtonLabel(ButtonBase button, string key, LocalizationService strings)
    {
        SetIfTranslated(strings, key, value =>
        {
            if (button.Content is string)
            {
                button.Content = value;
            }
            else
            {
                // Icon-only and visual-content commands retain their content; their label is exposed accessibly.
                ToolTipService.SetToolTip(button, value);
            }

            AutomationProperties.SetName(button, value);
        });
    }

    private static void SetIfTranslated(LocalizationService strings, string key, Action<string> setter)
    {
        if (strings.TryTranslate(key, out var value))
        {
            setter(value);
        }
    }
}

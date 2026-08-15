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

        Apply(root, strings, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance));
    }

    private static void Apply(
        DependencyObject root,
        LocalizationService strings,
        HashSet<DependencyObject> visited)
    {
        if (!visited.Add(root))
        {
            return;
        }

        if (root is FrameworkElement element)
        {
            element.Language = strings.Language;
            if (element.Tag is string key && !string.IsNullOrWhiteSpace(key))
            {
                ApplyElement(element, key, strings);
            }
        }

        // Declared children remain reachable here even while an options page is collapsed and absent
        // from the realized visual tree. This keeps first-open surfaces in the selected language.
        foreach (var child in DeclaredChildren(root))
        {
            Apply(child, strings, visited);
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            Apply(VisualTreeHelper.GetChild(root, index), strings, visited);
        }
    }

    private static IEnumerable<DependencyObject> DeclaredChildren(DependencyObject root)
    {
        switch (root)
        {
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    yield return child;
                }
                break;
            case UserControl userControl when userControl.Content is DependencyObject userContent:
                yield return userContent;
                break;
            case ContentControl contentControl when contentControl.Content is DependencyObject controlContent:
                yield return controlContent;
                break;
            case ContentPresenter presenter when presenter.Content is DependencyObject presenterContent:
                yield return presenterContent;
                break;
            case ItemsControl itemsControl:
                foreach (var child in itemsControl.Items.OfType<DependencyObject>())
                {
                    yield return child;
                }
                break;
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
            case AutoSuggestBox autoSuggestBox:
                SetIfTranslated(strings, key, value =>
                {
                    autoSuggestBox.PlaceholderText = value;
                    AutomationProperties.SetName(autoSuggestBox, value);
                });
                break;
            case NumberBox numberBox:
                if (!SetIfTranslated(strings, $"{key}.Header", value =>
                    {
                        numberBox.Header = value;
                        AutomationProperties.SetName(numberBox, value);
                    }))
                {
                    SetIfTranslated(strings, key, value =>
                    {
                        numberBox.Header = value;
                        AutomationProperties.SetName(numberBox, value);
                    });
                }
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
            case ToggleMenuFlyoutItem toggleMenuItem:
                SetIfTranslated(strings, key, value =>
                {
                    toggleMenuItem.Text = value;
                    AutomationProperties.SetName(toggleMenuItem, value);
                });
                break;
            case MenuFlyoutSubItem menuSubItem:
                SetIfTranslated(strings, key, value =>
                {
                    menuSubItem.Text = value;
                    AutomationProperties.SetName(menuSubItem, value);
                });
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

    private static bool SetIfTranslated(LocalizationService strings, string key, Action<string> setter)
    {
        if (strings.TryTranslate(key, out var value))
        {
            setter(value);
            return true;
        }

        return false;
    }
}

// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Storage.Pickers;

namespace TrackMeUp.Controls;

/// <summary>Collects installation appearance and archive paths while delegating all data work to the application facade.</summary>
public sealed partial class InstallationTransferOperationsControl : UserControl
{
    private const string ArchiveExtension = ".tmuarchive";

    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;
    private InstallationProfile[] _profiles = [];
    private AppearanceOption[] _colorOptions = [];
    private AppearanceOption[] _iconOptions = [];
    private InstallationProfile? _selectedProfile;
    private DataArchiveImportPlan? _importPlan;
    private bool _isApplyingProfile;
    private bool _pickerOpen;
    private bool _confirmationOpen;

    /// <summary>Creates the independent installation and archive operations surface.</summary>
    public InstallationTransferOperationsControl()
    {
        InitializeComponent();
        ApplyAppearanceOptions();
    }

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        ApplyAppearanceOptions();
        ApplyProfiles(_profiles, _selectedProfile?.InstallationId);
        AutomationProperties.SetName(Progress, T("Operations.Status.InProgress.Title", "Operation in progress"));
        AutomationProperties.SetName(InstallationsList, T("Operations.InstallationTransfer.Installations.List", "Known installations"));
        AutomationProperties.SetName(ImportInstallationsList, T("Operations.InstallationTransfer.Import.Installations", "Installations in this archive"));
    }

    /// <summary>Connects the passive surface to the application facade owned by the composition root.</summary>
    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow, TimedInfoBar banner) =>
        _context = new OperationsSectionContext(
            application,
            dialogs,
            ownerWindow,
            banner,
            Progress,
            SectionBody,
            key => _strings.TryTranslate(key, out var value) ? value : null);

    internal async Task LoadAsync()
    {
        var selectedInstallationId = _selectedProfile?.InstallationId;
        var result = await Context.ExecuteAsync(
            (application, token) => application.GetInstallationProfilesAsync(token),
            showSuccess: false);
        if (result is { Succeeded: true, Value: { } profiles })
        {
            ApplyProfiles(profiles.ToArray(), selectedInstallationId);
        }
    }

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("InstallationTransferOperationsControl must be initialized before use.");

    private void InstallationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProfile = (InstallationsList.SelectedItem as InstallationListItem)?.Profile;
        ApplySelectedProfile();
    }

    private void FriendlyNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isApplyingProfile)
        {
            UpdateSaveState();
        }
    }

    private void AppearanceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isApplyingProfile)
        {
            UpdateSaveState();
        }
    }

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile is null
            || ColorBox.SelectedItem is not AppearanceOption color
            || IconBox.SelectedItem is not AppearanceOption icon)
        {
            ShowLocalStatus(
                "Operations.InstallationTransfer.SelectionRequired.Title",
                "Select an installation",
                "Operations.InstallationTransfer.SelectionRequired.Message",
                "Choose an installation before saving its identity.",
                InfoBarSeverity.Warning);
            return;
        }

        var installationId = _selectedProfile.InstallationId;
        var request = new UpdateInstallationProfileRequest(
            installationId,
            FriendlyNameBox.Text,
            color.Value,
            icon.Value);
        var result = await Context.ExecuteAsync((application, token) => application.UpdateInstallationProfileAsync(request, token));
        if (result is not { Succeeded: true, Value: { } updated })
        {
            return;
        }

        var index = Array.FindIndex(_profiles, profile => StringComparer.Ordinal.Equals(profile.InstallationId, installationId));
        if (index < 0 || !StringComparer.Ordinal.Equals(updated.InstallationId, installationId))
        {
            throw new InvalidOperationException("The updated installation profile does not match the selected profile.");
        }

        var profiles = (InstallationProfile[])_profiles.Clone();
        profiles[index] = updated;
        ApplyProfiles(profiles, installationId);
    }

    private async void ExportArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        var destinationPath = await PickExportPathAsync();
        if (destinationPath is null)
        {
            return;
        }

        ExportResultPanel.Visibility = Visibility.Collapsed;
        ExportResultSummaryText.Text = string.Empty;
        ExportResultPathText.Text = string.Empty;
        var result = await Context.ExecuteAsync((application, token) => application.ExportDataArchiveAsync(
            new DataArchiveExportRequest(destinationPath, IncludeScreenshots: true),
            token));
        if (result is { Succeeded: true, Value: { } export })
        {
            ExportResultSummaryText.Text = Format(
                "Operations.InstallationTransfer.Export.Result",
                "Exported {0:N0} installations, {1:N0} activity records, {2:N0} AI records, and {3:N0} screenshots ({4}).",
                export.InstallationCount,
                export.ActivitySampleCount,
                (long)export.AiRequestCount + export.AiAnalysisCount,
                export.ScreenshotFileCount,
                FormatBytes(export.ScreenshotBytes));
            ExportResultPathText.Text = export.Path;
            AutomationProperties.SetName(ExportResultPathText, export.Path);
            AutomationProperties.SetHelpText(ExportResultPathText, export.Path);
            ToolTipService.SetToolTip(ExportResultPathText, export.Path);
            ExportResultPanel.Visibility = Visibility.Visible;
        }
    }

    private async void PreviewImportButton_Click(object sender, RoutedEventArgs e)
    {
        var archivePath = await PickImportPathAsync();
        if (archivePath is null)
        {
            return;
        }

        ClearImportPreview();
        var result = await Context.ExecuteAsync(
            (application, token) => application.PreviewDataArchiveImportAsync(new DataArchiveImportPreviewRequest(archivePath), token),
            showSuccess: false);
        if (result is { Succeeded: true, Value: { } plan })
        {
            _importPlan = plan;
            RenderImportPreview(plan, archivePath);
        }
    }

    private async void MergeImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_importPlan is not { } plan || plan.AlreadyImported || _confirmationOpen)
        {
            return;
        }

        _confirmationOpen = true;
        bool confirmed;
        try
        {
            confirmed = await Context.Dialogs.ConfirmAsync(
                Context.OwnerWindow,
                SystemMessageBoxRequest.Confirmation(
                    T("Operations.InstallationTransfer.Import.Confirm.Title", "Merge archive data?"),
                    Format(
                        "Operations.InstallationTransfer.Import.Confirm.Message",
                        "Merge {0:N0} activity records, {1:N0} AI records, and {2:N0} screenshots ({3}) from {4:N0} installations? Existing matching records will be skipped.",
                        plan.ActivitySampleCount,
                        (long)plan.AiRequestCount + plan.AiAnalysisCount,
                        plan.ScreenshotFileCount,
                        FormatBytes(plan.ScreenshotBytes),
                        plan.Installations.Count)));
        }
        catch (Exception)
        {
            ShowLocalStatus(
                "Operations.Status.RuntimeUnavailable.Title",
                "Operation unavailable",
                "Operations.InstallationTransfer.Import.ConfirmationUnavailable",
                "The confirmation window could not be opened. No data was merged.",
                InfoBarSeverity.Error);
            return;
        }
        finally
        {
            _confirmationOpen = false;
        }

        if (!confirmed)
        {
            return;
        }

        var result = await Context.ExecuteAsync(
            (application, token) => application.ImportDataArchiveAsync(new DataArchiveImportRequest(plan.PlanId), token));
        if (result is { Succeeded: true, Value: { } imported })
        {
            _importPlan = null;
            MergeImportButton.IsEnabled = false;
            ImportResultText.Text = Format(
                "Operations.InstallationTransfer.Import.Result",
                "Merge completed: {0:N0} installations and {1:N0} activity or AI records added; {2:N0} matching activity or AI records skipped; {3:N0} screenshots added ({4}) and {5:N0} screenshots skipped.",
                imported.AddedInstallationCount,
                (long)imported.AddedActivitySampleCount + imported.AddedAiRequestCount + imported.AddedAiAnalysisCount,
                (long)imported.SkippedActivitySampleCount + imported.SkippedAiRequestCount + imported.SkippedAiAnalysisCount,
                imported.AddedScreenshotFileCount,
                FormatBytes(imported.AddedScreenshotBytes),
                imported.SkippedScreenshotFileCount);
            ImportResultText.Visibility = Visibility.Visible;
            await LoadAsync();
        }
    }

    private async Task<string?> PickExportPathAsync()
    {
        if (!BeginPicker())
        {
            return null;
        }

        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "TrackMeUp-data"
            };
            picker.FileTypeChoices.Add(T("Operations.InstallationTransfer.ArchiveFileType", "TrackMeUp archive"), [ArchiveExtension]);
            InitializePicker(picker);
            var destination = await picker.PickSaveFileAsync();
            return destination?.Path;
        }
        catch (Exception)
        {
            ShowPickerFailure();
            return null;
        }
        finally
        {
            EndPicker();
        }
    }

    private async Task<string?> PickImportPathAsync()
    {
        if (!BeginPicker())
        {
            return null;
        }

        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(ArchiveExtension);
            InitializePicker(picker);
            var source = await picker.PickSingleFileAsync();
            return source?.Path;
        }
        catch (Exception)
        {
            ShowPickerFailure();
            return null;
        }
        finally
        {
            EndPicker();
        }
    }

    private bool BeginPicker()
    {
        if (_pickerOpen)
        {
            ShowLocalStatus(
                "Operations.Status.InProgress.Title",
                "Operation in progress",
                "Operations.InstallationTransfer.PickerOpen",
                "Finish the open file selection before starting another one.",
                InfoBarSeverity.Warning);
            return false;
        }

        _pickerOpen = true;
        return true;
    }

    private void EndPicker() => _pickerOpen = false;

    private void InitializePicker(object picker) =>
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Context.OwnerWindow));

    private void ShowPickerFailure() => ShowLocalStatus(
        "Operations.Status.RuntimeUnavailable.Title",
        "Operation unavailable",
        "Operations.InstallationTransfer.PickerUnavailable",
        "The file selection window could not be opened. No data was changed.",
        InfoBarSeverity.Error);

    private void ApplyProfiles(InstallationProfile[] profiles, string? selectedInstallationId)
    {
        _profiles = profiles;
        var items = profiles
            .OrderByDescending(profile => profile.IsCurrent)
            .ThenBy(profile => profile.FriendlyName, StringComparer.Create(_strings.Culture, ignoreCase: true))
            .Select(CreateInstallationListItem)
            .ToArray();
        InstallationsList.ItemsSource = items;
        NoInstallationsText.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallationsList.Visibility = items.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var selected = items.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Profile.InstallationId, selectedInstallationId))
            ?? items.FirstOrDefault(item => item.Profile.IsCurrent)
            ?? items.FirstOrDefault();
        InstallationsList.SelectedItem = selected;
        if (selected is null)
        {
            _selectedProfile = null;
            ApplySelectedProfile();
        }
    }

    private InstallationListItem CreateInstallationListItem(InstallationProfile profile) => new(
        profile,
        profile.FriendlyName,
        profile.IsCurrent
            ? Format("Operations.InstallationTransfer.CurrentMachine", "{0} · current PC", profile.MachineName)
            : profile.MachineName,
        InstallationAppearance.CreateAccentBrush(profile.Color),
        InstallationAppearance.GetIconGlyph(profile.Icon));

    private void ApplySelectedProfile()
    {
        _isApplyingProfile = true;
        try
        {
            if (_selectedProfile is null)
            {
                ProfileEditor.Visibility = Visibility.Collapsed;
                FriendlyNameBox.Text = string.Empty;
                ColorBox.SelectedItem = null;
                IconBox.SelectedItem = null;
                return;
            }

            ProfileEditor.Visibility = Visibility.Visible;
            MachineNameText.Text = Format(
                "Operations.InstallationTransfer.MachineName",
                "Machine name: {0}",
                _selectedProfile.MachineName);
            AutomationProperties.SetName(MachineNameText, MachineNameText.Text);
            ToolTipService.SetToolTip(MachineNameText, MachineNameText.Text);
            FriendlyNameBox.Text = _selectedProfile.FriendlyName;
            ColorBox.SelectedItem = _colorOptions.FirstOrDefault(option => StringComparer.Ordinal.Equals(option.Value, _selectedProfile.Color));
            IconBox.SelectedItem = _iconOptions.FirstOrDefault(option => StringComparer.Ordinal.Equals(option.Value, _selectedProfile.Icon));
        }
        finally
        {
            _isApplyingProfile = false;
            UpdateSaveState();
        }
    }

    private void UpdateSaveState()
    {
        var friendlyName = FriendlyNameBox.Text.Trim();
        SaveProfileButton.IsEnabled = _selectedProfile is { } profile
            && friendlyName.Length > 0
            && ColorBox.SelectedItem is AppearanceOption color
            && IconBox.SelectedItem is AppearanceOption icon
            && (!StringComparer.Ordinal.Equals(friendlyName, profile.FriendlyName)
                || !StringComparer.Ordinal.Equals(color.Value, profile.Color)
                || !StringComparer.Ordinal.Equals(icon.Value, profile.Icon));
    }

    private void ApplyAppearanceOptions()
    {
        var selectedColor = (ColorBox.SelectedItem as AppearanceOption)?.Value;
        var selectedIcon = (IconBox.SelectedItem as AppearanceOption)?.Value;
        _colorOptions = InstallationProfileCatalog.Colors
            .Select(color => new AppearanceOption(color, ColorDisplayName(color), InstallationAppearance.CreateAccentBrush(color), string.Empty))
            .ToArray();
        _iconOptions = InstallationProfileCatalog.Icons
            .Select(icon => new AppearanceOption(icon, IconDisplayName(icon), null, InstallationAppearance.GetIconGlyph(icon)))
            .ToArray();
        ColorBox.ItemsSource = _colorOptions;
        IconBox.ItemsSource = _iconOptions;
        ColorBox.SelectedItem = _colorOptions.FirstOrDefault(option => StringComparer.Ordinal.Equals(option.Value, selectedColor));
        IconBox.SelectedItem = _iconOptions.FirstOrDefault(option => StringComparer.Ordinal.Equals(option.Value, selectedIcon));
    }

    private string ColorDisplayName(string color) => color switch
    {
        "#5B8DEF" => T("Operations.InstallationTransfer.Color.Blue", "Blue"),
        "#6BBF8A" => T("Operations.InstallationTransfer.Color.Green", "Green"),
        "#E88F6B" => T("Operations.InstallationTransfer.Color.Orange", "Orange"),
        "#A97BEA" => T("Operations.InstallationTransfer.Color.Purple", "Purple"),
        "#E0B84D" => T("Operations.InstallationTransfer.Color.Gold", "Gold"),
        "#5CC2C7" => T("Operations.InstallationTransfer.Color.Teal", "Teal"),
        "#E36D8D" => T("Operations.InstallationTransfer.Color.Rose", "Rose"),
        "#8A9AAE" => T("Operations.InstallationTransfer.Color.Slate", "Slate"),
        "#B23A48" => T("Operations.InstallationTransfer.Color.Crimson", "Crimson"),
        "#3157C8" => T("Operations.InstallationTransfer.Color.Cobalt", "Cobalt"),
        "#2D7D46" => T("Operations.InstallationTransfer.Color.Emerald", "Emerald"),
        "#5B4DB7" => T("Operations.InstallationTransfer.Color.Indigo", "Indigo"),
        "#B85C24" => T("Operations.InstallationTransfer.Color.Rust", "Rust"),
        "#167C80" => T("Operations.InstallationTransfer.Color.Petrol", "Petrol"),
        "#A23B72" => T("Operations.InstallationTransfer.Color.Magenta", "Magenta"),
        "#7A553B" => T("Operations.InstallationTransfer.Color.Cocoa", "Cocoa"),
        _ => throw new InvalidOperationException($"Unsupported installation color '{color}'.")
    };

    private string IconDisplayName(string icon) => icon switch
    {
        "desktop" => T("Operations.InstallationTransfer.Icon.Desktop", "Desktop"),
        "laptop" => T("Operations.InstallationTransfer.Icon.Laptop", "Laptop"),
        "workstation" => T("Operations.InstallationTransfer.Icon.Workstation", "Workstation"),
        "home" => T("Operations.InstallationTransfer.Icon.Home", "Home"),
        "tablet" => T("Operations.InstallationTransfer.Icon.Tablet", "Tablet"),
        "phone" => T("Operations.InstallationTransfer.Icon.Phone", "Phone"),
        "server" => T("Operations.InstallationTransfer.Icon.Server", "Server"),
        "cloud" => T("Operations.InstallationTransfer.Icon.Cloud", "Cloud"),
        "office" => T("Operations.InstallationTransfer.Icon.Office", "Office"),
        "briefcase" => T("Operations.InstallationTransfer.Icon.Briefcase", "Briefcase"),
        "terminal" => T("Operations.InstallationTransfer.Icon.Terminal", "Terminal"),
        "gaming" => T("Operations.InstallationTransfer.Icon.Gaming", "Gaming"),
        "travel" => T("Operations.InstallationTransfer.Icon.Travel", "Travel"),
        "school" => T("Operations.InstallationTransfer.Icon.School", "School"),
        "studio" => T("Operations.InstallationTransfer.Icon.Studio", "Studio"),
        "camera" => T("Operations.InstallationTransfer.Icon.Camera", "Camera"),
        _ => throw new InvalidOperationException($"Unsupported installation icon '{icon}'.")
    };

    private void ClearImportPreview()
    {
        _importPlan = null;
        ImportPreviewPanel.Visibility = Visibility.Collapsed;
        ImportResultText.Visibility = Visibility.Collapsed;
        MergeImportButton.IsEnabled = false;
        ImportInstallationsList.ItemsSource = null;
    }

    private void RenderImportPreview(DataArchiveImportPlan plan, string archivePath)
    {
        ImportArchivePathText.Text = archivePath;
        AutomationProperties.SetName(ImportArchivePathText, archivePath);
        AutomationProperties.SetHelpText(ImportArchivePathText, archivePath);
        ToolTipService.SetToolTip(ImportArchivePathText, archivePath);
        ImportPreviewSummaryText.Text = Format(
            "Operations.InstallationTransfer.Import.PreviewResult",
            "Archive created {0:g}: {1:N0} activity records, {2:N0} AI records, and {3:N0} screenshots ({4}).",
            plan.CreatedAt.ToLocalTime(),
            plan.ActivitySampleCount,
            (long)plan.AiRequestCount + plan.AiAnalysisCount,
            plan.ScreenshotFileCount,
            FormatBytes(plan.ScreenshotBytes));
        ImportInstallationsList.ItemsSource = plan.Installations
            .OrderBy(installation => installation.FriendlyName, StringComparer.Create(_strings.Culture, ignoreCase: true))
            .Select(installation => new ArchiveInstallationListItem(
                installation.FriendlyName,
                installation.MachineName,
                InstallationAppearance.CreateAccentBrush(installation.Color),
                InstallationAppearance.GetIconGlyph(installation.Icon)))
            .ToArray();
        AlreadyImportedText.Visibility = plan.AlreadyImported ? Visibility.Visible : Visibility.Collapsed;
        MergeImportButton.IsEnabled = !plan.AlreadyImported;
        ImportPreviewPanel.Visibility = Visibility.Visible;
    }

    private string T(string key, string fallback) => _strings.TryTranslate(key, out var value) ? value : fallback;

    private string Format(string key, string fallback, params object[] arguments) =>
        string.Format(_strings.Culture, T(key, fallback), arguments);

    private void ShowLocalStatus(
        string titleKey,
        string titleFallback,
        string messageKey,
        string messageFallback,
        InfoBarSeverity severity) =>
        Context.ShowStatus(T(titleKey, titleFallback), T(messageKey, messageFallback), severity);

    private string FormatBytes(long bytes)
    {
        var size = Math.Max(0, bytes);
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;
        var value = (double)size;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return $"{value.ToString("0.#", _strings.Culture)} {units[unit]}";
    }

    private sealed record AppearanceOption(string Value, string DisplayName, SolidColorBrush? AccentBrush, string Glyph);

    private sealed record InstallationListItem(
        InstallationProfile Profile,
        string FriendlyName,
        string MachineLabel,
        SolidColorBrush AccentBrush,
        string Glyph);

    private sealed record ArchiveInstallationListItem(
        string FriendlyName,
        string MachineLabel,
        SolidColorBrush AccentBrush,
        string Glyph);
}

using System.Windows;
using System.Windows.Controls;
using STOW.App.Themes;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class SettingsView : UserControl
{
    private readonly IAppSettingsStore settingsStore;
    private readonly ITrayEngine? engine;
    private bool loading;

    public SettingsView(IAppSettingsStore settingsStore, ITrayEngine? engine)
    {
        this.settingsStore = settingsStore;
        this.engine = engine;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        loading = true;
        try
        {
            AppSettings settings = settingsStore.Load();
            KeepRunningCheckBox.IsChecked = settings.KeepRunningInTray;
            StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;

            LightThemeRadio.IsChecked = settings.Theme == ThemePreference.Light;
            DarkThemeRadio.IsChecked = settings.Theme == ThemePreference.Dark;
            SystemThemeRadio.IsChecked = settings.Theme == ThemePreference.System;

            FocusEndComboBox.SelectedIndex =
                settings.FocusEndBehavior == FocusEndBehavior.KeepAppsStowed ? 1 : 0;

            SettingsStatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ApplyDefaultsToControls();
            SettingsStatusBanner.Visibility = Visibility.Visible;
            SettingsStatusText.Text =
                "Settings could not be loaded. STOW is using safe defaults. " + ex.Message;
        }
        finally
        {
            loading = false;
        }
    }

    private void ApplyDefaultsToControls()
    {
        KeepRunningCheckBox.IsChecked = AppSettings.Default.KeepRunningInTray;
        StartWithWindowsCheckBox.IsChecked = AppSettings.Default.StartWithWindows;
        SystemThemeRadio.IsChecked = true;
        FocusEndComboBox.SelectedIndex = 0;
    }

    private void KeepRunning_Click(object sender, RoutedEventArgs e)
    {
        if (loading)
            return;

        Save(settings => settings with
        {
            KeepRunningInTray = KeepRunningCheckBox.IsChecked == true
        });
    }

    private void StartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        if (loading)
            return;

        if (!Save(settings => settings with
        {
            StartWithWindows = StartWithWindowsCheckBox.IsChecked == true
        }))
            return;

        if (engine is not null)
        {
            EngineCommandResult result = engine.RefreshSettings();
            if (!result.Succeeded)
            {
                ShowWarning(
                    result.Message ??
                    "The setting was saved, but STOW could not refresh Windows startup registration.");
            }
        }
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (loading || sender is not RadioButton { Tag: string value })
            return;

        if (!Enum.TryParse(value, out ThemePreference preference))
            return;

        if (Save(settings => settings with { Theme = preference }))
            ThemeManager.ApplyPreference(preference);
    }

    private void FocusEnd_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || FocusEndComboBox.SelectedItem is not ComboBoxItem { Tag: string value })
            return;

        if (!Enum.TryParse(value, out FocusEndBehavior behavior))
            return;

        _ = Save(settings => settings with { FocusEndBehavior = behavior });
    }

    private bool Save(Func<AppSettings, AppSettings> update)
    {
        try
        {
            AppSettings current;
            try { current = settingsStore.Load(); }
            catch { current = AppSettings.Default; }

            settingsStore.Save(update(current));
            SettingsStatusBanner.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception ex)
        {
            ShowWarning("STOW could not save settings. " + ex.Message);
            LoadSettings();
            return false;
        }
    }

    private void ShowWarning(string message)
    {
        SettingsStatusBanner.Visibility = Visibility.Visible;
        SettingsStatusText.Text = message;
    }
}

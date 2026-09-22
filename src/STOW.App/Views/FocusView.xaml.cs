using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class FocusView : UserControl
{
    private readonly ITrayEngine? engine;
    private readonly IAppSettingsStore settingsStore;
    private readonly IFocusPresetStore presetStore;
    private readonly DispatcherTimer refreshTimer;
    private readonly HashSet<string> selectedKeepVisible = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<FocusPresetDefinition> presets = Array.Empty<FocusPresetDefinition>();
    private string? runtimeUnavailableReason;
    private bool runtimeHealthy;

    private sealed record FocusAppRow(
        string Key,
        string Name,
        string ProcessName,
        bool KeepVisible,
        bool CanEdit);

    private sealed record FocusPresetRow(
        string Id,
        string Name,
        string Summary,
        bool CanUse);

    public FocusView(
        ITrayEngine? engine,
        IAppSettingsStore settingsStore,
        IFocusPresetStore presetStore,
        string? runtimeUnavailableReason = null)
    {
        this.engine = engine;
        this.settingsStore = settingsStore;
        this.presetStore = presetStore;
        this.runtimeUnavailableReason = runtimeUnavailableReason;
        runtimeHealthy = engine is not null;

        InitializeComponent();

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        refreshTimer.Tick += (_, _) => RefreshData();
        Loaded += (_, _) => refreshTimer.Start();
        Unloaded += (_, _) => refreshTimer.Stop();

        RefreshData();
    }

    private void RefreshData()
    {
        if (engine is null || !runtimeHealthy)
        {
            ApplyUnavailableState();
            return;
        }

        try
        {
            EngineSnapshot snapshot = engine.GetSnapshot();
            FocusSessionSnapshot focus = engine.GetFocusSession();
            ManagedAppSnapshot[] enabled = snapshot.ManagedApps
                .Where(app => app.Enabled)
                .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            HashSet<string> enabledKeys = enabled
                .Select(app => app.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (focus.Active)
            {
                selectedKeepVisible.Clear();
                selectedKeepVisible.UnionWith(focus.KeepVisibleAppKeys);
            }
            else
            {
                selectedKeepVisible.RemoveWhere(key => !enabledKeys.Contains(key));
            }

            FocusAppsList.ItemsSource = enabled
                .Select(app => new FocusAppRow(
                    app.Key,
                    app.DisplayName,
                    app.ProcessName,
                    selectedKeepVisible.Contains(app.Key),
                    CanEdit: !focus.Active))
                .ToArray();

            ManagedAppsSummaryText.Text = enabled.Length == 1
                ? "1 enabled app"
                : $"{enabled.Length} enabled apps";
            NoManagedAppsText.Visibility = enabled.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            SessionStatusText.Text = focus.Active ? "Active" : "Inactive";
            StowedCountText.Text = focus.StowedByFocusAppKeys.Count.ToString();
            VisibleCountText.Text = (focus.Active
                ? focus.KeepVisibleAppKeys.Count
                : selectedKeepVisible.Count).ToString();

            FocusActionButton.Content = focus.Active ? "End Focus" : "Start Focus";
            FocusActionButton.IsEnabled = focus.Active || enabled.Length > 0;
            DeepWorkButton.IsEnabled = !focus.Active && enabled.Length > 0;
            KeepAllButton.IsEnabled = !focus.Active && enabled.Length > 0;
            SavePresetButton.IsEnabled = !focus.Active && enabled.Length > 0;

            bool presetsHealthy = RefreshPresets(focus.Active, enabledKeys);

            if (presetsHealthy && string.IsNullOrWhiteSpace(runtimeUnavailableReason))
                FocusStatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            runtimeHealthy = false;
            runtimeUnavailableReason = "STOW Focus runtime is unavailable: " + ex.Message;
            ApplyUnavailableState();
        }
    }

    private void ApplyUnavailableState()
    {
        FocusStatusBanner.Visibility = Visibility.Visible;
        FocusStatusText.Text = runtimeUnavailableReason ??
            "Focus is unavailable because the STOW runtime is not active.";
        SessionStatusText.Text = "Unavailable";
        FocusActionButton.IsEnabled = false;
        DeepWorkButton.IsEnabled = false;
        KeepAllButton.IsEnabled = false;
        SavePresetButton.IsEnabled = false;
        FocusAppsList.ItemsSource = Array.Empty<FocusAppRow>();
        SavedPresetsList.ItemsSource = Array.Empty<FocusPresetRow>();
        SavedPresetsSummaryText.Text = string.Empty;
        NoSavedPresetsText.Visibility = Visibility.Visible;
        ManagedAppsSummaryText.Text = "Runtime unavailable";
        NoManagedAppsText.Visibility = Visibility.Visible;
        StowedCountText.Text = "0";
        VisibleCountText.Text = "0";
    }

    private bool RefreshPresets(bool focusActive, HashSet<string> enabledKeys)
    {
        try
        {
            presets = presetStore.Load();

            FocusPresetRow[] rows = presets
                .Select(preset =>
                {
                    int available = preset.KeepVisibleAppKeys.Count(enabledKeys.Contains);
                    int missing = preset.KeepVisibleAppKeys.Count - available;

                    string summary;
                    if (preset.KeepVisibleAppKeys.Count == 0)
                    {
                        summary = "Stows all enabled managed apps";
                    }
                    else if (missing > 0)
                    {
                        summary = $"Keeps {available} visible · {missing} unavailable";
                    }
                    else
                    {
                        summary = available == 1
                            ? "Keeps 1 app visible"
                            : $"Keeps {available} apps visible";
                    }

                    return new FocusPresetRow(
                        preset.Id,
                        preset.Name,
                        summary,
                        CanUse: !focusActive);
                })
                .ToArray();

            SavedPresetsList.ItemsSource = rows;
            NoSavedPresetsText.Visibility = rows.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            SavedPresetsSummaryText.Text = rows.Length == 1
                ? "1 preset"
                : $"{rows.Length} presets";
            return true;
        }
        catch (Exception ex)
        {
            presets = Array.Empty<FocusPresetDefinition>();
            SavedPresetsList.ItemsSource = Array.Empty<FocusPresetRow>();
            SavedPresetsSummaryText.Text = "Unavailable";
            NoSavedPresetsText.Visibility = Visibility.Visible;
            FocusStatusBanner.Visibility = Visibility.Visible;
            FocusStatusText.Text = "Focus presets could not be loaded: " + ex.Message;
            return false;
        }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || !runtimeHealthy)
            return;

        var dialog = new FocusPresetNameWindow($"Preset {presets.Count + 1}")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
            return;

        string name = dialog.PresetName;
        if (presets.Any(preset =>
            preset.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
        {
            ShowFailure(
                "Preset name already exists.",
                "Choose a different name so saved Focus presets remain unambiguous.");
            return;
        }

        FocusPresetDefinition preset =
            FocusPresetDefinition.Create(name, selectedKeepVisible);
        var updated = presets.Append(preset).ToArray();

        try
        {
            presetStore.Save(updated);
            presets = updated;
            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW could not save the Focus preset.", ex.Message);
        }
    }

    private void UsePreset_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null ||
            !runtimeHealthy ||
            sender is not Button { Tag: string presetId })
        {
            return;
        }

        FocusPresetDefinition? preset = presets.FirstOrDefault(item =>
            item.Id.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
            return;

        try
        {
            HashSet<string> enabledKeys = engine.GetSnapshot().ManagedApps
                .Where(app => app.Enabled)
                .Select(app => app.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            selectedKeepVisible.Clear();
            selectedKeepVisible.UnionWith(
                preset.KeepVisibleAppKeys.Where(enabledKeys.Contains));
            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW could not apply the Focus preset.", ex.Message);
        }
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string presetId })
            return;

        FocusPresetDefinition? preset = presets.FirstOrDefault(item =>
            item.Id.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
            return;

        MessageBoxResult confirmation = MessageBox.Show(
            $"Delete the Focus preset \"{preset.Name}\"?",
            "STOW",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
            return;

        FocusPresetDefinition[] updated = presets
            .Where(item => !item.Id.Equals(presetId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        try
        {
            presetStore.Save(updated);
            presets = updated;
            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW could not delete the Focus preset.", ex.Message);
        }
    }

    private void KeepVisible_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string key } checkBox ||
            string.IsNullOrWhiteSpace(key))
            return;

        if (checkBox.IsChecked == true)
            selectedKeepVisible.Add(key);
        else
            selectedKeepVisible.Remove(key);

        RefreshData();
    }

    private void DeepWork_Click(object sender, RoutedEventArgs e)
    {
        selectedKeepVisible.Clear();
        RefreshData();
    }

    private void KeepAll_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || !runtimeHealthy)
            return;

        try
        {
            selectedKeepVisible.Clear();
            selectedKeepVisible.UnionWith(
                engine.GetSnapshot().ManagedApps
                    .Where(app => app.Enabled)
                    .Select(app => app.Key));
            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW could not prepare this Focus preset.", ex.Message);
        }
    }

    private void FocusAction_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || !runtimeHealthy)
            return;

        try
        {
            FocusSessionSnapshot focus = engine.GetFocusSession();
            FocusEndBehavior endBehavior = FocusEndBehavior.RestorePreviousDesktop;
            if (focus.Active)
            {
                try { endBehavior = settingsStore.Load().FocusEndBehavior; }
                catch { endBehavior = FocusEndBehavior.RestorePreviousDesktop; }
            }

            EngineCommandResult result = focus.Active
                ? engine.EndFocusSession(endBehavior)
                : engine.StartFocusSession(selectedKeepVisible.ToArray());

            if (!result.Succeeded && result.Status != EngineCommandStatus.AlreadyExists)
            {
                ShowFailure(
                    focus.Active ? "Focus could not end safely." : "Focus could not start.",
                    result.Message ?? "STOW could not complete the Focus action.");
            }

            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW Focus encountered an error.", ex.Message);
        }
    }

    private static void ShowFailure(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}

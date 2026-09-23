using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Threading;
using STOW.Engine;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class FocusView : UserControl
{
    private readonly ITrayEngine? engine;
    private readonly IAppSettingsStore settingsStore;
    private readonly IFocusPresetStore presetStore;
    private readonly IFocusScheduleStore scheduleStore;
    private readonly FocusScheduleCoordinator? scheduleCoordinator;
    private readonly DispatcherTimer refreshTimer;
    private readonly HashSet<string> selectedKeepVisible = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<FocusPresetDefinition> presets = Array.Empty<FocusPresetDefinition>();
    private IReadOnlyList<FocusScheduleDefinition> schedules = Array.Empty<FocusScheduleDefinition>();
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

    private sealed record FocusScheduleRow(
        string Id,
        string Name,
        string Details,
        string Status,
        bool Enabled,
        bool CanManage);

    public FocusView(
        ITrayEngine? engine,
        IAppSettingsStore settingsStore,
        IFocusPresetStore presetStore,
        IFocusScheduleStore scheduleStore,
        FocusScheduleCoordinator? scheduleCoordinator,
        string? runtimeUnavailableReason = null)
    {
        this.engine = engine;
        this.settingsStore = settingsStore;
        this.presetStore = presetStore;
        this.scheduleStore = scheduleStore;
        this.scheduleCoordinator = scheduleCoordinator;
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

            SessionStatusText.Text = focus.Active ? "Active" : "Ready to start";
            StowedCountText.Text = focus.StowedByFocusAppKeys.Count.ToString();
            VisibleCountText.Text = (focus.Active
                ? focus.KeepVisibleAppKeys.Count
                : selectedKeepVisible.Count).ToString();

            FocusActionButton.Content = focus.Active ? "End focus mode" : "Start focus mode";
            FocusActionButton.IsEnabled = focus.Active || enabled.Length > 0;
            DeepWorkButton.IsEnabled = !focus.Active && enabled.Length > 0;
            KeepAllButton.IsEnabled = !focus.Active && enabled.Length > 0;
            SavePresetButton.IsEnabled = !focus.Active && enabled.Length > 0;

            bool presetsHealthy = RefreshPresets(focus.Active, enabledKeys);
            RefreshSchedules(presetsHealthy);

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
        ScheduleList.ItemsSource = Array.Empty<FocusScheduleRow>();
        SchedulesSummaryText.Text = string.Empty;
        NoSchedulesText.Visibility = Visibility.Visible;
        ScheduleStatusText.Text = "Scheduling is unavailable while the STOW runtime is unavailable.";
        ScheduleStatusText.Visibility = Visibility.Visible;
        AddScheduleButton.IsEnabled = false;
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

    private void RefreshSchedules(bool presetsHealthy)
    {
        try
        {
            schedules = scheduleStore.Load();
            FocusScheduleRuntimeStatus runtimeStatus =
                scheduleCoordinator?.GetStatus() ?? FocusScheduleRuntimeStatus.Idle;
            DateTime now = DateTime.Now;

            var presetsById = presets.ToDictionary(
                preset => preset.Id,
                StringComparer.OrdinalIgnoreCase);

            FocusScheduleRow[] rows = schedules
                .Select(schedule =>
                {
                    bool presetAvailable =
                        presetsById.TryGetValue(schedule.PresetId, out FocusPresetDefinition? preset);
                    string presetName = presetAvailable
                        ? preset!.Name
                        : "Preset unavailable";
                    string details =
                        $"{FormatDays(schedule.Days)} · {FormatStartTime(schedule.StartMinutesLocal)} · " +
                        $"{schedule.DurationMinutes} min · {presetName}";

                    string rowStatus;
                    if (string.Equals(
                        runtimeStatus.ActiveScheduleId,
                        schedule.Id,
                        StringComparison.OrdinalIgnoreCase) &&
                        runtimeStatus.ActiveUntilLocal is DateTime activeUntil)
                    {
                        rowStatus = $"Running until {activeUntil:HH:mm}";
                    }
                    else if (!schedule.Enabled)
                    {
                        rowStatus = "Disabled";
                    }
                    else if (!presetAvailable)
                    {
                        rowStatus = "Preset unavailable";
                    }
                    else
                    {
                        FocusScheduleOccurrence? next =
                            FocusSchedulePlanner.GetNextOccurrence(schedule, now);
                        rowStatus = next is null
                            ? "No upcoming occurrence"
                            : FormatNextOccurrence(next.StartLocal, now);
                    }

                    bool active = string.Equals(
                        runtimeStatus.ActiveScheduleId,
                        schedule.Id,
                        StringComparison.OrdinalIgnoreCase);

                    return new FocusScheduleRow(
                        schedule.Id,
                        schedule.Name,
                        details,
                        rowStatus,
                        schedule.Enabled,
                        CanManage: !active);
                })
                .ToArray();

            ScheduleList.ItemsSource = rows;
            SchedulesSummaryText.Text = rows.Length == 1
                ? "1 schedule"
                : $"{rows.Length} schedules";
            NoSchedulesText.Visibility = rows.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            AddScheduleButton.IsEnabled =
                runtimeHealthy && presetsHealthy && presets.Count > 0;

            if (!runtimeStatus.Healthy || !string.IsNullOrWhiteSpace(runtimeStatus.Message))
            {
                ScheduleStatusText.Text = runtimeStatus.Message ??
                    "Focus scheduling is unavailable.";
                ScheduleStatusText.Visibility = Visibility.Visible;
            }
            else if (!presetsHealthy)
            {
                ScheduleStatusText.Text =
                    "Scheduling is paused until saved Focus presets can be loaded.";
                ScheduleStatusText.Visibility = Visibility.Visible;
            }
            else if (presets.Count == 0)
            {
                ScheduleStatusText.Text =
                    "Save a Focus preset before adding a schedule.";
                ScheduleStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                ScheduleStatusText.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            schedules = Array.Empty<FocusScheduleDefinition>();
            ScheduleList.ItemsSource = Array.Empty<FocusScheduleRow>();
            SchedulesSummaryText.Text = "Unavailable";
            NoSchedulesText.Visibility = Visibility.Visible;
            AddScheduleButton.IsEnabled = false;
            ScheduleStatusText.Text =
                "Focus schedules could not be loaded: " + ex.Message;
            ScheduleStatusText.Visibility = Visibility.Visible;
        }
    }

    private static string FormatStartTime(int startMinutesLocal) =>
        $"{startMinutesLocal / 60:00}:{startMinutesLocal % 60:00}";

    private static string FormatNextOccurrence(DateTime startLocal, DateTime nowLocal)
    {
        if (startLocal.Date == nowLocal.Date)
            return $"Next today at {startLocal:HH:mm}";
        if (startLocal.Date == nowLocal.Date.AddDays(1))
            return $"Next tomorrow at {startLocal:HH:mm}";
        return $"Next {startLocal:ddd HH:mm}";
    }

    private static string FormatDays(IReadOnlyList<DayOfWeek> days)
    {
        HashSet<DayOfWeek> set = days.ToHashSet();
        if (set.Count == 7)
            return "Every day";

        DayOfWeek[] weekdays =
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };
        if (set.SetEquals(weekdays))
            return "Weekdays";
        if (set.SetEquals(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }))
            return "Weekends";

        return string.Join(
            ", ",
            days
                .Distinct()
                .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
                .Select(day => day switch
                {
                    DayOfWeek.Monday => "Mon",
                    DayOfWeek.Tuesday => "Tue",
                    DayOfWeek.Wednesday => "Wed",
                    DayOfWeek.Thursday => "Thu",
                    DayOfWeek.Friday => "Fri",
                    DayOfWeek.Saturday => "Sat",
                    _ => "Sun"
                }));
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

        int referencingSchedules = schedules.Count(schedule =>
            schedule.PresetId.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        string consequence = referencingSchedules == 0
            ? string.Empty
            : $"\n\n{referencingSchedules} Focus schedule(s) use this preset. " +
              "They will stay saved but cannot run until edited or deleted.";

        MessageBoxResult confirmation = MessageBox.Show(
            $"Delete the Focus preset \"{preset.Name}\"?{consequence}",
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

    private void AddSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (!runtimeHealthy || presets.Count == 0)
            return;

        var dialog = new FocusScheduleWindow(presets)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Schedule is null)
            return;

        if (schedules.Any(schedule =>
            schedule.Name.Equals(
                dialog.Schedule.Name,
                StringComparison.CurrentCultureIgnoreCase)))
        {
            ShowFailure(
                "Schedule name already exists.",
                "Choose a different name so Focus schedules remain unambiguous.");
            return;
        }

        SaveSchedules(schedules.Append(dialog.Schedule).ToArray());
    }

    private void EditSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string scheduleId })
            return;

        FocusScheduleDefinition? schedule = schedules.FirstOrDefault(item =>
            item.Id.Equals(scheduleId, StringComparison.OrdinalIgnoreCase));
        if (schedule is null)
            return;

        var dialog = new FocusScheduleWindow(presets, schedule)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Schedule is null)
            return;

        if (schedules.Any(item =>
            !item.Id.Equals(schedule.Id, StringComparison.OrdinalIgnoreCase) &&
            item.Name.Equals(
                dialog.Schedule.Name,
                StringComparison.CurrentCultureIgnoreCase)))
        {
            ShowFailure(
                "Schedule name already exists.",
                "Choose a different name so Focus schedules remain unambiguous.");
            return;
        }

        SaveSchedules(schedules
            .Select(item => item.Id.Equals(
                    schedule.Id,
                    StringComparison.OrdinalIgnoreCase)
                ? dialog.Schedule
                : item)
            .ToArray());
    }

    private void ScheduleEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string scheduleId } checkBox)
            return;

        FocusScheduleDefinition? schedule = schedules.FirstOrDefault(item =>
            item.Id.Equals(scheduleId, StringComparison.OrdinalIgnoreCase));
        if (schedule is null)
            return;

        bool enabled = checkBox.IsChecked == true;
        SaveSchedules(schedules
            .Select(item => item.Id.Equals(
                    schedule.Id,
                    StringComparison.OrdinalIgnoreCase)
                ? item with { Enabled = enabled }
                : item)
            .ToArray());
    }

    private void DeleteSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string scheduleId })
            return;

        FocusScheduleDefinition? schedule = schedules.FirstOrDefault(item =>
            item.Id.Equals(scheduleId, StringComparison.OrdinalIgnoreCase));
        if (schedule is null)
            return;

        MessageBoxResult confirmation = MessageBox.Show(
            $"Delete the Focus schedule \"{schedule.Name}\"?",
            "STOW",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
            return;

        SaveSchedules(schedules
            .Where(item => !item.Id.Equals(
                schedule.Id,
                StringComparison.OrdinalIgnoreCase))
            .ToArray());
    }

    private void SaveSchedules(IReadOnlyCollection<FocusScheduleDefinition> updated)
    {
        try
        {
            scheduleStore.Save(updated);
            schedules = updated.ToArray();
            scheduleCoordinator?.Tick(DateTime.Now);
            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW could not save Focus schedules.", ex.Message);
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

using STOW.Engine.Contracts;

namespace STOW.Engine;

public sealed class FocusScheduleCoordinator
{
    private readonly object gate = new();
    private readonly ITrayEngine engine;
    private readonly IFocusScheduleStore scheduleStore;
    private readonly IFocusPresetStore presetStore;
    private readonly IAppSettingsStore settingsStore;
    private readonly IUserNotificationSink? notificationSink;

    private string? activeScheduleId;
    private string? activeScheduleName;
    private DateTime? activeUntilLocal;
    private FocusScheduleRuntimeStatus status = FocusScheduleRuntimeStatus.Idle;

    public FocusScheduleCoordinator(
        ITrayEngine engine,
        IFocusScheduleStore scheduleStore,
        IFocusPresetStore presetStore,
        IAppSettingsStore settingsStore,
        IUserNotificationSink? notificationSink = null)
    {
        this.engine = engine;
        this.scheduleStore = scheduleStore;
        this.presetStore = presetStore;
        this.settingsStore = settingsStore;
        this.notificationSink = notificationSink;
    }

    public FocusScheduleRuntimeStatus GetStatus()
    {
        lock (gate)
            return status;
    }
    public void Tick(DateTime nowLocal)
    {
        lock (gate)
        {
            try
            {
                TickCore(DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified));
            }
            catch (Exception ex)
            {
                status = new FocusScheduleRuntimeStatus(
                    Healthy: false,
                    Message: "Focus scheduling is paused: " + ex.Message,
                    ActiveScheduleId: activeScheduleId,
                    ActiveUntilLocal: activeUntilLocal);
            }
        }
    }

    private void TickCore(DateTime nowLocal)
    {
        IReadOnlyList<FocusScheduleDefinition> schedules = scheduleStore.Load();
        FocusSessionSnapshot focus = engine.GetFocusSession();

        if (activeScheduleId is not null && activeUntilLocal is not null)
        {
            schedules = ConsumeCurrentOccurrences(
                schedules,
                nowLocal,
                exceptScheduleId: activeScheduleId,
                out bool overlapsConsumed);
            if (overlapsConsumed)
                scheduleStore.Save(schedules);
            if (!focus.Active)
            {
                string? completedName = activeScheduleName;
                ClearActiveSchedule();
                status = FocusScheduleRuntimeStatus.Idle;
                if (!string.IsNullOrWhiteSpace(completedName))
                {
                    NotifyIfEnabled(
                        "Focus ended",
                        $"{completedName} ended.");
                }
                return;
            }

            if (nowLocal < activeUntilLocal.Value)
            {
                status = new FocusScheduleRuntimeStatus(
                    Healthy: true,
                    Message: null,
                    ActiveScheduleId: activeScheduleId,
                    ActiveUntilLocal: activeUntilLocal);
                return;
            }

            FocusEndBehavior behavior = FocusEndBehavior.RestorePreviousDesktop;
            try { behavior = settingsStore.Load().FocusEndBehavior; }
            catch { }

            EngineCommandResult endResult = engine.EndFocusSession(behavior);
            if (endResult.Succeeded)
            {
                string? completedName = activeScheduleName;
                ClearActiveSchedule();
                status = FocusScheduleRuntimeStatus.Idle;
                if (!string.IsNullOrWhiteSpace(completedName))
                {
                    NotifyIfEnabled(
                        "Focus ended",
                        $"{completedName} finished.");
                }
            }
            else
            {
                status = new FocusScheduleRuntimeStatus(
                    Healthy: false,
                    Message: endResult.Message ?? "Scheduled Focus could not end safely.",
                    ActiveScheduleId: activeScheduleId,
                    ActiveUntilLocal: activeUntilLocal);
            }
            return;
        }
        if (focus.Active)
        {
            schedules = ConsumeCurrentOccurrences(
                schedules,
                nowLocal,
                exceptScheduleId: null,
                out bool manualOverlapsConsumed);
            if (manualOverlapsConsumed)
                scheduleStore.Save(schedules);

            status = new FocusScheduleRuntimeStatus(
                Healthy: true,
                Message: manualOverlapsConsumed
                    ? "A manual Focus session is active; overlapping schedules were skipped."
                    : null,
                ActiveScheduleId: null,
                ActiveUntilLocal: null);
            return;
        }

        var dueCandidates = schedules
            .Select(schedule => new
            {
                Schedule = schedule,
                Occurrence = FocusSchedulePlanner.GetActiveOccurrence(schedule, nowLocal)
            })
            .Where(item =>
                item.Occurrence is not null &&
                !string.Equals(
                    item.Schedule.LastConsumedOccurrenceKey,
                    item.Occurrence.Key,
                    StringComparison.Ordinal))
            .OrderBy(item => item.Occurrence!.StartLocal)
            .ThenBy(item => item.Schedule.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (dueCandidates.Length == 0)
        {
            status = FocusScheduleRuntimeStatus.Idle;
            return;
        }

        IReadOnlyList<FocusPresetDefinition> presets = presetStore.Load();
        var due = dueCandidates.FirstOrDefault(candidate =>
            presets.Any(preset => preset.Id.Equals(
                candidate.Schedule.PresetId,
                StringComparison.OrdinalIgnoreCase)));

        if (due?.Occurrence is null)
        {
            status = new FocusScheduleRuntimeStatus(
                Healthy: false,
                Message:
                    $"Schedule \"{dueCandidates[0].Schedule.Name}\" cannot run because its Focus preset is unavailable.",
                ActiveScheduleId: null,
                ActiveUntilLocal: null);
            return;
        }

        FocusPresetDefinition preset = presets.First(item =>
            item.Id.Equals(due.Schedule.PresetId, StringComparison.OrdinalIgnoreCase));

        HashSet<string> enabledKeys = engine.GetSnapshot().ManagedApps
            .Where(app => app.Enabled)
            .Select(app => app.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] keepVisible = preset.KeepVisibleAppKeys
            .Where(enabledKeys.Contains)
            .ToArray();

        FocusScheduleDefinition[] marked = schedules
            .Select(schedule => schedule.Id.Equals(
                    due.Schedule.Id,
                    StringComparison.OrdinalIgnoreCase)
                ? schedule with { LastConsumedOccurrenceKey = due.Occurrence.Key }
                : schedule)
            .ToArray();

        // Persist the occurrence marker before acting. This makes schedules
        // at-most-once across crashes/restarts instead of risking repeated stows.
        scheduleStore.Save(marked);

        EngineCommandResult startResult = engine.StartFocusSession(keepVisible);
        if (!startResult.Succeeded)
        {
            status = new FocusScheduleRuntimeStatus(
                Healthy: false,
                Message: startResult.Message ?? "Scheduled Focus could not start.",
                ActiveScheduleId: null,
                ActiveUntilLocal: null);
            return;
        }

        activeScheduleId = due.Schedule.Id;
        activeScheduleName = due.Schedule.Name;
        activeUntilLocal = due.Occurrence.EndLocal;
        status = new FocusScheduleRuntimeStatus(
            Healthy: true,
            Message: null,
            ActiveScheduleId: activeScheduleId,
            ActiveUntilLocal: activeUntilLocal);
        NotifyIfEnabled(
            "Focus started",
            $"{due.Schedule.Name} is running until {due.Occurrence.EndLocal:HH:mm}.");
    }

    private static IReadOnlyList<FocusScheduleDefinition> ConsumeCurrentOccurrences(
        IReadOnlyList<FocusScheduleDefinition> schedules,
        DateTime nowLocal,
        string? exceptScheduleId,
        out bool changed)
    {
        changed = false;
        var updated = new FocusScheduleDefinition[schedules.Count];

        for (int index = 0; index < schedules.Count; index++)
        {
            FocusScheduleDefinition schedule = schedules[index];
            FocusScheduleOccurrence? occurrence =
                FocusSchedulePlanner.GetActiveOccurrence(schedule, nowLocal);

            bool skip = exceptScheduleId is not null &&
                schedule.Id.Equals(exceptScheduleId, StringComparison.OrdinalIgnoreCase);
            if (!skip &&
                occurrence is not null &&
                !string.Equals(
                    schedule.LastConsumedOccurrenceKey,
                    occurrence.Key,
                    StringComparison.Ordinal))
            {
                updated[index] = schedule with
                {
                    LastConsumedOccurrenceKey = occurrence.Key
                };
                changed = true;
            }
            else
            {
                updated[index] = schedule;
            }
        }

        return changed ? updated : schedules;
    }

    private void NotifyIfEnabled(
        string title,
        string message,
        UserNotificationKind kind = UserNotificationKind.Information)
    {
        if (notificationSink is null)
            return;

        try
        {
            if (!settingsStore.Load().NotificationsEnabled)
                return;

            notificationSink.Show(title, message, kind);
        }
        catch
        {
            // Notifications are best-effort and must never affect Focus safety.
        }
    }

    private void ClearActiveSchedule()
    {
        activeScheduleId = null;
        activeScheduleName = null;
        activeUntilLocal = null;
    }
}

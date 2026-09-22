using STOW.Engine.Contracts;

namespace STOW.Engine.Tests;

public sealed class FocusScheduleCoordinatorTests
{
    [Fact]
    public void Due_schedule_starts_once_and_filters_unavailable_apps()
    {
        var scheduleStore = new FakeScheduleStore(Schedule());
        var presetStore = new FakePresetStore(new FocusPresetDefinition(
            "preset",
            "Work",
            new[] { "enabled-app", "missing-app" }));
        var engine = new FakeTrayEngine();

        var coordinator = new FocusScheduleCoordinator(
            engine,
            scheduleStore,
            presetStore,
            new FakeSettingsStore());

        DateTime now = new(2026, 9, 21, 9, 15, 0);
        coordinator.Tick(now);
        coordinator.Tick(now.AddMinutes(5));

        Assert.Equal(1, engine.StartCalls);
        Assert.Equal(new[] { "enabled-app" }, engine.LastStartedKeepVisible);
        Assert.Equal(
            "2026-09-21T09:00",
            Assert.Single(scheduleStore.Load()).LastConsumedOccurrenceKey);
        Assert.Equal("schedule", coordinator.GetStatus().ActiveScheduleId);
    }

    [Fact]
    public void Manual_focus_consumes_overlapping_occurrence_without_retrigger()
    {
        var scheduleStore = new FakeScheduleStore(Schedule());
        var engine = new FakeTrayEngine { FocusActive = true };
        var coordinator = new FocusScheduleCoordinator(
            engine,
            scheduleStore,
            new FakePresetStore(Preset()),
            new FakeSettingsStore());

        DateTime now = new(2026, 9, 21, 9, 15, 0);
        coordinator.Tick(now);

        engine.FocusActive = false;
        coordinator.Tick(now.AddMinutes(10));

        Assert.Equal(0, engine.StartCalls);
        Assert.Equal(
            "2026-09-21T09:00",
            Assert.Single(scheduleStore.Load()).LastConsumedOccurrenceKey);
    }

    [Fact]
    public void Scheduled_focus_ends_with_saved_focus_end_behavior()
    {
        var scheduleStore = new FakeScheduleStore(Schedule());
        var engine = new FakeTrayEngine();
        var coordinator = new FocusScheduleCoordinator(
            engine,
            scheduleStore,
            new FakePresetStore(Preset()),
            new FakeSettingsStore(FocusEndBehavior.KeepAppsStowed));

        coordinator.Tick(new DateTime(2026, 9, 21, 9, 15, 0));
        coordinator.Tick(new DateTime(2026, 9, 21, 10, 0, 0));

        Assert.Equal(1, engine.EndCalls);
        Assert.Equal(FocusEndBehavior.KeepAppsStowed, engine.LastEndBehavior);
        Assert.False(engine.FocusActive);
        Assert.Null(coordinator.GetStatus().ActiveScheduleId);
    }
    [Fact]
    public void Missing_preset_does_not_block_another_due_schedule()
    {
        FocusScheduleDefinition missing = Schedule() with
        {
            Id = "missing-schedule",
            Name = "A missing preset",
            PresetId = "missing-preset"
        };
        FocusScheduleDefinition valid = Schedule() with
        {
            Id = "valid-schedule",
            Name = "B valid preset"
        };
        var scheduleStore = new FakeScheduleStore(missing, valid);
        var engine = new FakeTrayEngine();
        var coordinator = new FocusScheduleCoordinator(
            engine,
            scheduleStore,
            new FakePresetStore(Preset()),
            new FakeSettingsStore());

        coordinator.Tick(new DateTime(2026, 9, 21, 9, 15, 0));

        Assert.Equal(1, engine.StartCalls);
        Assert.Equal("valid-schedule", coordinator.GetStatus().ActiveScheduleId);
        Assert.Null(scheduleStore.Load()
            .Single(item => item.Id == "missing-schedule")
            .LastConsumedOccurrenceKey);
        Assert.Equal(
            "2026-09-21T09:00",
            scheduleStore.Load()
                .Single(item => item.Id == "valid-schedule")
                .LastConsumedOccurrenceKey);
    }

    [Fact]
    public void Missing_preset_does_not_consume_occurrence()
    {
        var scheduleStore = new FakeScheduleStore(Schedule());
        var coordinator = new FocusScheduleCoordinator(
            new FakeTrayEngine(),
            scheduleStore,
            new FakePresetStore(),
            new FakeSettingsStore());

        coordinator.Tick(new DateTime(2026, 9, 21, 9, 15, 0));

        Assert.Null(Assert.Single(scheduleStore.Load()).LastConsumedOccurrenceKey);
        Assert.False(coordinator.GetStatus().Healthy);
    }

    private static FocusScheduleDefinition Schedule() =>
        FocusScheduleDefinition.Create(
            "Morning",
            "preset",
            new[] { DayOfWeek.Monday },
            startMinutesLocal: 9 * 60,
            durationMinutes: 60) with { Id = "schedule" };

    private static FocusPresetDefinition Preset() =>
        new("preset", "Work", new[] { "enabled-app" });

    private sealed class FakeScheduleStore : IFocusScheduleStore
    {
        private IReadOnlyList<FocusScheduleDefinition> schedules;

        public FakeScheduleStore(params FocusScheduleDefinition[] schedules)
        {
            this.schedules = schedules;
        }

        public IReadOnlyList<FocusScheduleDefinition> Load() => schedules;

        public void Save(IReadOnlyCollection<FocusScheduleDefinition> schedules)
        {
            this.schedules = schedules.ToArray();
        }
    }
    private sealed class FakePresetStore : IFocusPresetStore
    {
        private readonly IReadOnlyList<FocusPresetDefinition> presets;

        public FakePresetStore(params FocusPresetDefinition[] presets)
        {
            this.presets = presets;
        }

        public IReadOnlyList<FocusPresetDefinition> Load() => presets;

        public void Save(IReadOnlyCollection<FocusPresetDefinition> presets) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        private AppSettings settings;

        public FakeSettingsStore(
            FocusEndBehavior behavior = FocusEndBehavior.RestorePreviousDesktop)
        {
            settings = AppSettings.Default with { FocusEndBehavior = behavior };
        }

        public AppSettings Load() => settings;

        public void Save(AppSettings settings)
        {
            this.settings = settings;
        }
    }

    private sealed class FakeTrayEngine : ITrayEngine
    {
        public bool FocusActive { get; set; }
        public int StartCalls { get; private set; }
        public int EndCalls { get; private set; }
        public IReadOnlyCollection<string> LastStartedKeepVisible { get; private set; } =
            Array.Empty<string>();
        public FocusEndBehavior LastEndBehavior { get; private set; }
        public EngineSnapshot GetSnapshot() =>
            new(
                new[]
                {
                    new ManagedAppSnapshot(
                        "enabled-app",
                        "Enabled",
                        @"C:\enabled.exe",
                        "enabled",
                        Enabled: true,
                        ManagedAppRuntimeState.Visible),
                    new ManagedAppSnapshot(
                        "disabled-app",
                        "Disabled",
                        @"C:\disabled.exe",
                        "disabled",
                        Enabled: false,
                        ManagedAppRuntimeState.Disabled)
                },
                Array.Empty<AvailableAppSnapshot>());

        public FocusSessionSnapshot GetFocusSession() =>
            new(
                FocusActive,
                LastStartedKeepVisible,
                Array.Empty<string>());

        public EngineCommandResult StartFocusSession(
            IReadOnlyCollection<string> keepVisibleAppKeys)
        {
            StartCalls++;
            if (FocusActive)
                return new(EngineCommandStatus.AlreadyExists);

            FocusActive = true;
            LastStartedKeepVisible = keepVisibleAppKeys.ToArray();
            return new(EngineCommandStatus.Succeeded);
        }

        public EngineCommandResult EndFocusSession(
            FocusEndBehavior behavior = FocusEndBehavior.RestorePreviousDesktop)
        {
            EndCalls++;
            LastEndBehavior = behavior;
            FocusActive = false;
            return new(EngineCommandStatus.Succeeded);
        }
        public EngineCommandResult AddManagedApp(
            DiscoveredAppSnapshot app,
            bool enabled = true) =>
            throw new NotSupportedException();

        public EngineCommandResult RemoveManagedApp(string appKey) =>
            throw new NotSupportedException();

        public EngineCommandResult SetEnabled(string appKey, bool enabled) =>
            throw new NotSupportedException();

        public EngineCommandResult Restore(string appKey) =>
            throw new NotSupportedException();

        public EngineCommandResult RefreshSettings() =>
            new(EngineCommandStatus.Succeeded);
    }
}

using STOW.Engine.Contracts;
using STOW.Platform.Windows.Runtime;

namespace STOW.IntegrationTests;

public sealed class LegacyCompatibleTrayEngineTests
{
    private static readonly ManagedAppDefinition Grok = new(
        "GrokBot",
        @"C:\Apps\GrokBot.exe",
        "GrokBot",
        "Grok",
        "Chrome_WidgetWin_1",
        true);

    [Fact]
    public void Minimized_managed_window_is_hidden_and_tracked_after_full_scan()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);

        engine.Start();
        TickFullScan(engine);

        Assert.False(runtime.IsWindowVisible(101));
        Assert.True(icons.Contains(Grok.Key));
        Assert.Equal(ManagedAppRuntimeState.Stowed, Assert.Single(engine.GetSnapshot().ManagedApps).State);
    }

    [Fact]
    public void Recreated_visible_window_for_hidden_pid_is_immediately_rehidden()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        runtime.Add(Window(202, 42, iconic: false));
        engine.Tick();

        Assert.False(runtime.IsWindowVisible(202));
    }

    [Fact]
    public void Restore_prefers_original_hidden_hwnd()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        EngineCommandResult result = engine.Restore(Grok.Key);

        Assert.True(result.Succeeded);
        Assert.Equal((nint)101, runtime.LastRestoredHandle);
        Assert.False(icons.Contains(Grok.Key));
    }

    [Fact]
    public void Electron_replacement_hwnd_restores_from_tracked_pid_when_original_is_gone()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        runtime.Remove(101);
        runtime.Add(Window(202, 42, iconic: false, visible: false));
        EngineCommandResult result = engine.Restore(Grok.Key);

        Assert.True(result.Succeeded);
        Assert.Equal((nint)202, runtime.LastRestoredHandle);
        Assert.True(runtime.IsWindowVisible(202));
    }

    [Fact]
    public void Failed_restore_keeps_hidden_tracking_and_tray_icon()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true)) { RestoreSucceeds = false };
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        EngineCommandResult result = engine.Restore(Grok.Key);

        Assert.Equal(EngineCommandStatus.RestoreTargetUnavailable, result.Status);
        Assert.True(icons.Contains(Grok.Key));
        Assert.Equal(ManagedAppRuntimeState.Stowed, Assert.Single(engine.GetSnapshot().ManagedApps).State);
    }

    [Fact]
    public void Disable_while_hidden_is_blocked_when_restore_fails()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true)) { RestoreSucceeds = false };
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        EngineCommandResult result = engine.SetEnabled(Grok.Key, false);

        Assert.Equal(EngineCommandStatus.RestoreTargetUnavailable, result.Status);
        Assert.True(Assert.Single(store.Apps).Enabled);
        Assert.True(icons.Contains(Grok.Key));
    }

    [Fact]
    public void Shutdown_is_blocked_when_any_hidden_window_cannot_be_restored()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true)) { RestoreSucceeds = false };
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        EngineCommandResult result = engine.Shutdown();

        Assert.Equal(EngineCommandStatus.RestoreTargetUnavailable, result.Status);
        Assert.True(engine.IsRunning);
        Assert.True(icons.Contains(Grok.Key));
    }

    [Fact]
    public void Successful_shutdown_restores_hidden_apps_and_clears_tray_icons()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        EngineCommandResult result = engine.Shutdown();

        Assert.True(result.Succeeded);
        Assert.False(engine.IsRunning);
        Assert.True(runtime.IsWindowVisible(101));
        Assert.False(icons.Contains(Grok.Key));
    }

    [Fact]
    public void Keep_visible_rule_prevents_minimized_window_from_being_stowed()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        var rules = new FakeRuleStore(new RuleDefinition(
            "keep",
            "Keep Grok visible",
            Grok.Key,
            RuleTrigger.Minimize,
            RuleAction.KeepVisible,
            true,
            80));
        using var engine = NewEngine(store, runtime, icons, ruleStore: rules);

        engine.Start();
        TickFullScan(engine);

        Assert.True(runtime.IsWindowVisible(101));
        Assert.False(icons.Contains(Grok.Key));
        Assert.Equal(ManagedAppRuntimeState.Visible, Assert.Single(engine.GetSnapshot().ManagedApps).State);
    }

    [Fact]
    public void Rule_store_failure_falls_back_to_legacy_stow_behavior()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        var rules = new FakeRuleStore { ThrowOnLoad = true };
        using var engine = NewEngine(store, runtime, icons, ruleStore: rules);

        engine.Start();
        TickFullScan(engine);

        Assert.False(runtime.IsWindowVisible(101));
        Assert.True(icons.Contains(Grok.Key));
    }

    [Fact]
    public void Startup_registration_tracks_whether_any_managed_app_is_enabled()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: false));
        var icons = new FakeTrayIcons();
        var startup = new FakeStartupRegistration();
        using var engine = NewEngine(store, runtime, icons, startup);

        Assert.Equal(new[] { true }, startup.Updates);

        EngineCommandResult disabled = engine.SetEnabled(Grok.Key, false);
        Assert.True(disabled.Succeeded);
        Assert.Equal(new[] { true, false }, startup.Updates);

        EngineCommandResult enabled = engine.SetEnabled(Grok.Key, true);
        Assert.True(enabled.Succeeded);
        Assert.Equal(new[] { true, false, true }, startup.Updates);
    }

    [Fact]
    public void Exited_hidden_process_is_removed_from_tracking_on_next_full_scan()
    {
        var store = new FakeStore(Grok);
        var runtime = new FakeRuntime(Window(101, 42, iconic: true));
        var icons = new FakeTrayIcons();
        using var engine = NewEngine(store, runtime, icons);
        engine.Start();
        TickFullScan(engine);

        runtime.MarkProcessExited(42);
        TickFullScan(engine);

        Assert.False(icons.Contains(Grok.Key));
        Assert.Equal(ManagedAppRuntimeState.NotRunning, Assert.Single(engine.GetSnapshot().ManagedApps).State);
    }

    private static LegacyCompatibleTrayEngine NewEngine(
        FakeStore store,
        FakeRuntime runtime,
        FakeTrayIcons icons,
        IStartupRegistration? startupRegistration = null,
        IRuleStore? ruleStore = null) =>
        new(
            store,
            runtime,
            icons,
            useTimer: false,
            startupRegistration: startupRegistration,
            ruleStore: ruleStore);

    private static void TickFullScan(LegacyCompatibleTrayEngine engine)
    {
        engine.Tick();
        engine.Tick();
        engine.Tick();
        engine.Tick();
    }

    private static FakeWindow Window(
        nint handle,
        int pid,
        bool iconic,
        bool visible = true) => new(
            handle,
            pid,
            "Grok",
            "Chrome_WidgetWin_1",
            @"C:\Apps\GrokBot.exe",
            "GrokBot",
            "GrokBot",
            visible,
            iconic);

    private sealed class FakeStore : IManagedAppStore
    {
        public List<ManagedAppDefinition> Apps { get; private set; }
        public bool ThrowOnSave { get; set; }

        public FakeStore(params ManagedAppDefinition[] apps) => Apps = apps.ToList();

        public IReadOnlyList<ManagedAppDefinition> Load() => Apps.ToArray();

        public void Save(IReadOnlyCollection<ManagedAppDefinition> apps)
        {
            if (ThrowOnSave)
                throw new IOException("simulated save failure");
            Apps = apps.ToList();
        }
    }

    private sealed class FakeStartupRegistration : IStartupRegistration
    {
        public List<bool> Updates { get; } = new();
        public void Update(bool shouldStartWithWindows) => Updates.Add(shouldStartWithWindows);
    }

    private sealed class FakeRuleStore : IRuleStore
    {
        private IReadOnlyList<RuleDefinition> rules;

        public bool ThrowOnLoad { get; set; }

        public FakeRuleStore(params RuleDefinition[] rules)
        {
            this.rules = rules;
        }

        public IReadOnlyList<RuleDefinition> Load()
        {
            if (ThrowOnLoad)
                throw new IOException("simulated rule-store failure");
            return rules;
        }

        public void Save(IReadOnlyCollection<RuleDefinition> rules)
        {
            this.rules = rules.ToArray();
        }
    }

    private sealed class FakeTrayIcons : ITrayIconRegistry
    {
        private readonly Dictionary<string, (Func<EngineCommandResult> restore, Func<EngineCommandResult> disable)> icons =
            new(StringComparer.OrdinalIgnoreCase);

        public bool Contains(string key) => icons.ContainsKey(key);

        public void Ensure(
            ManagedAppDefinition app,
            Func<EngineCommandResult> restore,
            Func<EngineCommandResult> disable) => icons.TryAdd(app.Key, (restore, disable));

        public void Remove(string appKey) => icons.Remove(appKey);

        public void RemoveAll() => icons.Clear();

        public void Dispose() => icons.Clear();
    }

    private sealed class FakeWindow
    {
        public nint Handle { get; }
        public int Pid { get; }
        public string Title { get; }
        public string ClassName { get; }
        public string ExecutablePath { get; }
        public string ProcessName { get; }
        public string DisplayName { get; }
        public bool Visible { get; set; }
        public bool Iconic { get; set; }

        public FakeWindow(
            nint handle,
            int pid,
            string title,
            string className,
            string executablePath,
            string processName,
            string displayName,
            bool visible,
            bool iconic)
        {
            Handle = handle;
            Pid = pid;
            Title = title;
            ClassName = className;
            ExecutablePath = executablePath;
            ProcessName = processName;
            DisplayName = displayName;
            Visible = visible;
            Iconic = iconic;
        }

        public RuntimeWindow Snapshot() => new(
            Handle, Pid, Title, ClassName, ExecutablePath, ProcessName, DisplayName);
    }

    private sealed class FakeRuntime : ITrayWindowRuntime
    {
        private readonly Dictionary<nint, FakeWindow> windows = new();
        private readonly HashSet<int> livePids = new();

        public bool RestoreSucceeds { get; set; } = true;
        public nint? LastRestoredHandle { get; private set; }

        public FakeRuntime(params FakeWindow[] initial)
        {
            foreach (FakeWindow window in initial)
                Add(window);
        }

        public void Add(FakeWindow window)
        {
            windows[window.Handle] = window;
            livePids.Add(window.Pid);
        }

        public void Remove(nint handle) => windows.Remove(handle);

        public void MarkProcessExited(int pid)
        {
            livePids.Remove(pid);
            foreach (nint handle in windows.Values.Where(window => window.Pid == pid).Select(window => window.Handle).ToArray())
                windows.Remove(handle);
        }

        public IReadOnlyList<RuntimeWindow> EnumerateUserFacingWindows() => windows.Values
            .Where(window => window.Visible && livePids.Contains(window.Pid))
            .Select(window => window.Snapshot())
            .ToArray();

        public IEnumerable<nint> EnumerateTopLevelWindows() => windows.Keys.ToArray();

        public bool IsWindow(nint handle) => windows.ContainsKey(handle);

        public bool IsWindowVisible(nint handle) => windows.TryGetValue(handle, out FakeWindow? window) && window.Visible;

        public bool IsIconic(nint handle) => windows.TryGetValue(handle, out FakeWindow? window) && window.Iconic;

        public void Hide(nint handle)
        {
            if (windows.TryGetValue(handle, out FakeWindow? window))
                window.Visible = false;
        }

        public void RestoreAndShow(nint handle)
        {
            LastRestoredHandle = handle;
            if (RestoreSucceeds && windows.TryGetValue(handle, out FakeWindow? window))
            {
                window.Visible = true;
                window.Iconic = false;
            }
        }

        public void BringToForeground(nint handle) { }

        public int GetProcessId(nint handle) => windows[handle].Pid;

        public string GetTitle(nint handle) => windows[handle].Title;

        public string GetClassName(nint handle) => windows[handle].ClassName;

        public bool ProcessExists(int pid) => livePids.Contains(pid);

        public IReadOnlySet<int> GetProcessIdsByName(string processName) => windows.Values
            .Where(window => livePids.Contains(window.Pid) &&
                             window.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            .Select(window => window.Pid)
            .ToHashSet();
    }
}

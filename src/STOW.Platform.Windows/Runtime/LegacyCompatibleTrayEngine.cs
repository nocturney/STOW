using STOW.Engine.Contracts;
using STOW.Platform.Windows.Compatibility;

namespace STOW.Platform.Windows.Runtime;

public sealed class LegacyCompatibleTrayEngine : ITrayEngineRuntime
{
    private readonly object gate = new();
    private readonly IManagedAppStore store;
    private readonly ITrayWindowRuntime runtime;
    private readonly ITrayIconRegistry trayIcons;
    private readonly IStartupRegistration startupRegistration;
    private readonly IRuleStore? ruleStore;
    private readonly IActivityStore? activityStore;
    private readonly bool useTimer;
    private readonly Dictionary<string, ManagedAppDefinition> managed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<nint>> hiddenHandles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<int>> hiddenPids = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> focusKeepVisibleAppKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> focusHiddenAppKeys = new(StringComparer.OrdinalIgnoreCase);

    private System.Threading.Timer? timer;
    private SynchronizationContext? synchronizationContext;
    private int tickScheduled;
    private int engineTicks;
    private bool running;
    private bool exiting;
    private bool focusActive;

    public LegacyCompatibleTrayEngine(
        IManagedAppStore store,
        IRuleStore? ruleStore = null,
        IActivityStore? activityStore = null)
        : this(
            store,
            new Win32TrayWindowRuntime(),
            new WinFormsTrayIconRegistry(),
            useTimer: true,
            startupRegistration: new WindowsStartupRegistration(),
            ruleStore: ruleStore,
            activityStore: activityStore)
    {
    }

    internal LegacyCompatibleTrayEngine(
        IManagedAppStore store,
        ITrayWindowRuntime runtime,
        ITrayIconRegistry trayIcons,
        bool useTimer = true,
        IStartupRegistration? startupRegistration = null,
        IRuleStore? ruleStore = null,
        IActivityStore? activityStore = null)
    {
        this.store = store;
        this.runtime = runtime;
        this.trayIcons = trayIcons;
        this.startupRegistration = startupRegistration ?? new NoOpStartupRegistration();
        this.ruleStore = ruleStore;
        this.activityStore = activityStore;
        this.useTimer = useTimer;
        ReloadManaged();
        UpdateStartupRegistration();
    }

    public bool IsRunning
    {
        get { lock (gate) return running; }
    }

    public void Start()
    {
        lock (gate)
        {
            if (running)
                return;

            exiting = false;
            running = true;
            synchronizationContext = SynchronizationContext.Current;
            if (useTimer)
                timer = new System.Threading.Timer(_ => ScheduleTick(), null, 100, 100);
        }
    }

    public EngineSnapshot GetSnapshot()
    {
        lock (gate)
        {
            IReadOnlyList<RuntimeWindow> windows = runtime.EnumerateUserFacingWindows();
            var managedSnapshots = managed.Values
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(app => ToManagedSnapshot(app, windows))
                .ToArray();

            var managedKeys = managed.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var available = windows
                .Where(window => !managedKeys.Contains(window.Key))
                .GroupBy(window => window.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(window => new AvailableAppSnapshot(window.Key, window.DisplayName, window.ExecutablePath, window.ProcessName))
                .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return new EngineSnapshot(managedSnapshots, available);
        }
    }

    public EngineCommandResult AddManagedApp(DiscoveredAppSnapshot app, bool enabled = true)
    {
        lock (gate)
        {
            if (managed.ContainsKey(app.Key))
                return new(EngineCommandStatus.AlreadyExists);

            var definition = new ManagedAppDefinition(
                app.DisplayName,
                app.ExecutablePath,
                app.ProcessName,
                app.WindowTitle,
                app.WindowClass,
                enabled);

            managed[definition.Key] = definition;
            try
            {
                SaveManaged();
                return new(EngineCommandStatus.Succeeded);
            }
            catch (Exception ex)
            {
                managed.Remove(definition.Key);
                return new(EngineCommandStatus.Failed, ex.Message);
            }
        }
    }

    public EngineCommandResult RemoveManagedApp(string appKey)
    {
        lock (gate)
        {
            if (!managed.TryGetValue(appKey, out ManagedAppDefinition? app))
                return new(EngineCommandStatus.NotFound);

            if (IsManagedHidden(app) && !RestoreManaged(app, foreground: true, source: "Remove"))
                return RestoreUnavailable(app);

            managed.Remove(appKey);
            try
            {
                SaveManaged();
                trayIcons.Remove(appKey);
                focusKeepVisibleAppKeys.Remove(appKey);
                focusHiddenAppKeys.Remove(appKey);
                return new(EngineCommandStatus.Succeeded);
            }
            catch (Exception ex)
            {
                managed[appKey] = app;
                return new(EngineCommandStatus.Failed, ex.Message);
            }
        }
    }

    public EngineCommandResult SetEnabled(string appKey, bool enabled)
    {
        lock (gate)
        {
            if (!managed.TryGetValue(appKey, out ManagedAppDefinition? app))
                return new(EngineCommandStatus.NotFound);

            if (!enabled && IsManagedHidden(app) && !RestoreManaged(app, foreground: true, source: "Disable"))
                return RestoreUnavailable(app);

            ManagedAppDefinition updated = app with { Enabled = enabled };
            managed[appKey] = updated;
            try
            {
                SaveManaged();
                if (!enabled)
                {
                    focusKeepVisibleAppKeys.Remove(appKey);
                    focusHiddenAppKeys.Remove(appKey);
                }
                return new(EngineCommandStatus.Succeeded);
            }
            catch (Exception ex)
            {
                managed[appKey] = app;
                return new(EngineCommandStatus.Failed, ex.Message);
            }
        }
    }

    public EngineCommandResult Restore(string appKey)
    {
        lock (gate)
        {
            if (!managed.TryGetValue(appKey, out ManagedAppDefinition? app))
                return new(EngineCommandStatus.NotFound);

            if (!IsManagedHidden(app))
                return new(EngineCommandStatus.Succeeded);

            if (!RestoreManaged(app, foreground: true, source: "Manual"))
                return RestoreUnavailable(app);

            if (focusActive)
            {
                focusHiddenAppKeys.Remove(app.Key);
                focusKeepVisibleAppKeys.Add(app.Key);
            }

            return new(EngineCommandStatus.Succeeded);
        }
    }

    public FocusSessionSnapshot GetFocusSession()
    {
        lock (gate)
        {
            return new FocusSessionSnapshot(
                focusActive,
                focusKeepVisibleAppKeys.ToArray(),
                focusHiddenAppKeys.ToArray());
        }
    }

    public EngineCommandResult StartFocusSession(IReadOnlyCollection<string> keepVisibleAppKeys)
    {
        lock (gate)
        {
            if (!running)
                return new(EngineCommandStatus.Failed, "STOW runtime is not running.");
            if (focusActive)
                return new(EngineCommandStatus.AlreadyExists, "A Focus session is already active.");

            focusKeepVisibleAppKeys.Clear();
            foreach (string key in keepVisibleAppKeys)
            {
                if (managed.ContainsKey(key))
                    focusKeepVisibleAppKeys.Add(key);
            }

            focusHiddenAppKeys.Clear();
            focusActive = true;
            RecordActivity(ActivityEventType.FocusStarted, source: "Focus");

            IReadOnlyList<RuntimeWindow> windows = runtime.EnumerateUserFacingWindows();
            foreach (ManagedAppDefinition app in managed.Values)
            {
                if (!app.Enabled || focusKeepVisibleAppKeys.Contains(app.Key))
                    continue;

                bool wasHidden = IsManagedHidden(app);
                bool hiddenAny = false;
                foreach (RuntimeWindow window in windows.Where(window => MatchesIdentity(app, window)))
                {
                    runtime.Hide(window.Handle);
                    TrackHidden(app, window);
                    hiddenAny = true;
                }

                if (!hiddenAny)
                    continue;

                EnsureTrayIcon(app);
                if (!wasHidden)
                {
                    focusHiddenAppKeys.Add(app.Key);
                    RecordActivity(ActivityEventType.AppStowed, app, "Focus");
                }
            }

            return new(EngineCommandStatus.Succeeded);
        }
    }

    public EngineCommandResult EndFocusSession()
    {
        lock (gate)
        {
            if (!focusActive)
                return new(EngineCommandStatus.Succeeded);

            var failures = new List<string>();
            foreach (string key in focusHiddenAppKeys.ToArray())
            {
                if (!managed.TryGetValue(key, out ManagedAppDefinition? app))
                {
                    focusHiddenAppKeys.Remove(key);
                    continue;
                }

                if (!IsManagedHidden(app))
                {
                    focusHiddenAppKeys.Remove(key);
                    continue;
                }

                if (hiddenPids.TryGetValue(app.Key, out HashSet<int>? pids) &&
                    pids.Count > 0 &&
                    pids.All(pid => !runtime.ProcessExists(pid)))
                {
                    hiddenPids.Remove(app.Key);
                    hiddenHandles.Remove(app.Key);
                    trayIcons.Remove(app.Key);
                    focusHiddenAppKeys.Remove(app.Key);
                    continue;
                }

                if (RestoreManaged(app, foreground: false, source: "Focus"))
                    focusHiddenAppKeys.Remove(app.Key);
                else
                    failures.Add(app.Name);
            }

            if (failures.Count > 0)
            {
                return new(
                    EngineCommandStatus.RestoreTargetUnavailable,
                    "Focus is still active because STOW could not safely restore: " + string.Join(", ", failures));
            }

            focusActive = false;
            focusKeepVisibleAppKeys.Clear();
            focusHiddenAppKeys.Clear();
            RecordActivity(ActivityEventType.FocusEnded, source: "Focus");
            return new(EngineCommandStatus.Succeeded);
        }
    }

    public EngineCommandResult Shutdown()
    {
        lock (gate)
        {
            if (!running)
                return new(EngineCommandStatus.Succeeded);

            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            var failures = new List<string>();

            foreach (ManagedAppDefinition app in managed.Values.ToArray())
            {
                if (IsManagedHidden(app) && !RestoreManaged(app, foreground: false, source: "Shutdown"))
                    failures.Add(app.Name);
            }

            if (failures.Count > 0)
            {
                timer?.Change(100, 100);
                return new(
                    EngineCommandStatus.RestoreTargetUnavailable,
                    "STOW did not exit because it could not safely restore: " + string.Join(", ", failures));
            }

            exiting = true;
            running = false;
            timer?.Dispose();
            timer = null;
            trayIcons.RemoveAll();
            focusActive = false;
            focusKeepVisibleAppKeys.Clear();
            focusHiddenAppKeys.Clear();
            return new(EngineCommandStatus.Succeeded);
        }
    }

    public void Dispose()
    {
        _ = Shutdown();
    }

    private void ScheduleTick()
    {
        if (Interlocked.Exchange(ref tickScheduled, 1) != 0)
            return;

        void RunTick()
        {
            try { Tick(); }
            finally { Volatile.Write(ref tickScheduled, 0); }
        }

        SynchronizationContext? context;
        lock (gate) context = synchronizationContext;

        if (context is null)
            RunTick();
        else
            context.Post(_ => RunTick(), null);
    }

    internal void Tick()
    {
        lock (gate)
        {
            if (!running || exiting)
                return;

            if (hiddenPids.Count > 0)
            {
                var trayPids = hiddenPids.Values.SelectMany(set => set).ToHashSet();
                foreach (nint handle in runtime.EnumerateTopLevelWindows())
                {
                    int pid = runtime.GetProcessId(handle);
                    if (trayPids.Contains(pid) && runtime.IsWindowVisible(handle))
                        runtime.Hide(handle);
                }
            }

            engineTicks++;
            if ((engineTicks % 4) != 0)
                return;

            IReadOnlyList<RuntimeWindow> windows = runtime.EnumerateUserFacingWindows();
            foreach (ManagedAppDefinition app in managed.Values)
            {
                if (!app.Enabled)
                    continue;

                foreach (RuntimeWindow window in windows)
                {
                    if (!MatchesIdentity(app, window))
                        continue;

                    if (hiddenPids.TryGetValue(app.Key, out HashSet<int>? activePids) && activePids.Contains(window.Pid))
                    {
                        if (runtime.IsWindowVisible(window.Handle))
                            runtime.Hide(window.Handle);
                        continue;
                    }

                    if (focusActive)
                    {
                        if (focusKeepVisibleAppKeys.Contains(app.Key))
                            continue;

                        bool wasHiddenBeforeFocus = IsManagedHidden(app);
                        runtime.Hide(window.Handle);
                        TrackHidden(app, window);
                        EnsureTrayIcon(app);
                        if (!wasHiddenBeforeFocus)
                        {
                            focusHiddenAppKeys.Add(app.Key);
                            RecordActivity(ActivityEventType.AppStowed, app, "Focus");
                        }
                        continue;
                    }

                    if (!runtime.IsIconic(window.Handle))
                        continue;

                    if (ResolveRuleAction(app.Key, RuleTrigger.Minimize) == RuleAction.KeepVisible)
                        continue;

                    bool wasHiddenBeforeMinimize = IsManagedHidden(app);
                    runtime.Hide(window.Handle);
                    TrackHidden(app, window);
                    EnsureTrayIcon(app);
                    if (!wasHiddenBeforeMinimize)
                        RecordActivity(ActivityEventType.AppStowed, app, "Minimize");
                }
            }

            foreach (string key in hiddenPids.Keys.ToArray())
            {
                hiddenPids[key].RemoveWhere(pid => !runtime.ProcessExists(pid));
                if (hiddenPids[key].Count != 0)
                    continue;

                hiddenPids.Remove(key);
                hiddenHandles.Remove(key);
                trayIcons.Remove(key);
                focusHiddenAppKeys.Remove(key);
            }
        }
    }

    private RuleAction ResolveRuleAction(string appKey, RuleTrigger trigger)
    {
        if (ruleStore is null)
            return RuleAction.Stow;

        try
        {
            return RuleEvaluator.Resolve(ruleStore.Load(), appKey, trigger, RuleAction.Stow);
        }
        catch
        {
            return RuleAction.Stow;
        }
    }

    private void TrackHidden(ManagedAppDefinition app, RuntimeWindow window)
    {
        if (!hiddenPids.TryGetValue(app.Key, out HashSet<int>? pids))
        {
            pids = new HashSet<int>();
            hiddenPids[app.Key] = pids;
        }
        pids.Add(window.Pid);

        if (!hiddenHandles.TryGetValue(app.Key, out HashSet<nint>? handles))
        {
            handles = new HashSet<nint>();
            hiddenHandles[app.Key] = handles;
        }
        handles.Add(window.Handle);
    }

    private void EnsureTrayIcon(ManagedAppDefinition app)
    {
        trayIcons.Ensure(
            app,
            () => Restore(app.Key),
            () => SetEnabled(app.Key, false));
    }

    private bool IsManagedHidden(ManagedAppDefinition app)
    {
        return (hiddenPids.TryGetValue(app.Key, out HashSet<int>? pids) && pids.Count > 0) ||
               (hiddenHandles.TryGetValue(app.Key, out HashSet<nint>? handles) && handles.Count > 0);
    }

    private bool RestoreManaged(ManagedAppDefinition app, bool foreground, string source)
    {
        hiddenHandles.TryGetValue(app.Key, out HashSet<nint>? remembered);
        hiddenPids.TryGetValue(app.Key, out HashSet<int>? trackedPids);

        nint? target = RestoreTargetResolver.Resolve(
            remembered,
            trackedPids,
            app.TitleHint,
            app.ClassHint,
            runtime);

        if (target is null)
            return false;

        HashSet<int>? savedPids = trackedPids is null ? null : new HashSet<int>(trackedPids);
        HashSet<nint>? savedHandles = remembered is null ? null : new HashSet<nint>(remembered);

        hiddenPids.Remove(app.Key);
        hiddenHandles.Remove(app.Key);

        runtime.RestoreAndShow(target.Value);
        if (foreground)
            runtime.BringToForeground(target.Value);

        bool restored = runtime.IsWindow(target.Value) && runtime.IsWindowVisible(target.Value);
        if (restored)
        {
            trayIcons.Remove(app.Key);
            RecordActivity(ActivityEventType.AppRestored, app, source);
            return true;
        }

        if (savedPids is { Count: > 0 })
            hiddenPids[app.Key] = savedPids;
        if (savedHandles is { Count: > 0 })
            hiddenHandles[app.Key] = savedHandles;

        EnsureTrayIcon(app);
        return false;
    }

    private static bool MatchesIdentity(ManagedAppDefinition app, RuntimeWindow window)
    {
        if (!string.IsNullOrWhiteSpace(app.ExecutablePath) &&
            !string.IsNullOrWhiteSpace(window.ExecutablePath) &&
            string.Equals(app.ExecutablePath, window.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(app.ProcessName) &&
            string.Equals(app.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(app.ClassHint) ||
             string.Equals(app.ClassHint, window.ClassName, StringComparison.Ordinal)))
            return true;

        return !string.IsNullOrWhiteSpace(app.TitleHint) &&
               !string.IsNullOrWhiteSpace(app.ClassHint) &&
               string.Equals(app.TitleHint, window.Title, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(app.ClassHint, window.ClassName, StringComparison.Ordinal);
    }

    private ManagedAppSnapshot ToManagedSnapshot(
        ManagedAppDefinition app,
        IReadOnlyList<RuntimeWindow> windows)
    {
        ManagedAppRuntimeState state;
        if (!app.Enabled)
            state = ManagedAppRuntimeState.Disabled;
        else if (hiddenPids.TryGetValue(app.Key, out HashSet<int>? pids) && pids.Count > 0)
            state = ManagedAppRuntimeState.Stowed;
        else if (windows.Any(window => MatchesIdentity(app, window)))
            state = ManagedAppRuntimeState.Visible;
        else
            state = ManagedAppRuntimeState.NotRunning;

        return new ManagedAppSnapshot(
            app.Key,
            app.Name,
            app.ExecutablePath,
            app.ProcessName,
            app.Enabled,
            state);
    }

    private void ReloadManaged()
    {
        managed.Clear();
        foreach (ManagedAppDefinition app in store.Load())
            managed[app.Key] = app;
    }

    private void SaveManaged()
    {
        store.Save(managed.Values.ToArray());
        UpdateStartupRegistration();
    }

    private void UpdateStartupRegistration()
    {
        startupRegistration.Update(managed.Values.Any(app => app.Enabled));
    }

    private void RecordActivity(
        ActivityEventType type,
        ManagedAppDefinition? app = null,
        string? source = null)
    {
        if (activityStore is null)
            return;

        try
        {
            activityStore.Append(new ActivityEvent(
                DateTimeOffset.UtcNow,
                type,
                app?.Key,
                app?.Name,
                source));
        }
        catch
        {
            // Insights history is best-effort and must never affect tray safety.
        }
    }

    private static EngineCommandResult RestoreUnavailable(ManagedAppDefinition app) => new(
        EngineCommandStatus.RestoreTargetUnavailable,
        $"The hidden window for {app.Name} could not be restored. The app remains tracked by STOW.");
}

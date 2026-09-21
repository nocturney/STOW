using System.Diagnostics;
using System.Runtime.InteropServices;
using STOW.Engine.Contracts;
using STOW.Platform.Windows.Runtime;
using STOW.WindowTestTarget;

namespace STOW.IntegrationTests;

public sealed class LiveWin32ParityTests
{
    private const int SwMinimize = 6;

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int command);

    [LiveWin32Fact]
    public void Real_window_minimize_hide_and_restore_matches_v033_contract()
    {
        using TargetProcess target = TargetProcess.Start();
        var runtime = new Win32TrayWindowRuntime();
        RuntimeWindow window = WaitForWindow(runtime, target.Pid);
        var store = new FakeStore(ToManaged(window));
        var icons = new FakeTrayIcons();
        using var engine = new LegacyCompatibleTrayEngine(store, runtime, icons, useTimer: false);

        engine.Start();
        ShowWindow(window.Handle, SwMinimize);
        WaitUntil(() => runtime.IsIconic(window.Handle));
        TickFullScan(engine);

        Assert.False(runtime.IsWindowVisible(window.Handle));
        Assert.True(icons.Contains(store.Apps[0].Key));
        Assert.Equal(ManagedAppRuntimeState.Stowed, Assert.Single(engine.GetSnapshot().ManagedApps).State);

        EngineCommandResult restored = engine.Restore(store.Apps[0].Key);
        Assert.True(restored.Succeeded, restored.Message);
        Assert.True(runtime.IsWindowVisible(window.Handle));
        Assert.False(runtime.IsIconic(window.Handle));
    }

    [LiveWin32Fact]
    public void Real_window_recreated_hwnd_restores_from_tracked_pid()
    {
        using TargetProcess target = TargetProcess.Start();
        var runtime = new Win32TrayWindowRuntime();
        RuntimeWindow original = WaitForWindow(runtime, target.Pid);
        var store = new FakeStore(ToManaged(original));
        var icons = new FakeTrayIcons();
        using var engine = new LegacyCompatibleTrayEngine(store, runtime, icons, useTimer: false);

        engine.Start();
        ShowWindow(original.Handle, SwMinimize);
        WaitUntil(() => runtime.IsIconic(original.Handle));
        TickFullScan(engine);
        Assert.False(runtime.IsWindowVisible(original.Handle));

        nint recreated = target.RecreateHiddenWindow(original.Handle, runtime);
        Assert.NotEqual(original.Handle, recreated);
        Assert.False(runtime.IsWindow(original.Handle));
        Assert.False(runtime.IsWindowVisible(recreated));

        EngineCommandResult restored = engine.Restore(store.Apps[0].Key);

        Assert.True(restored.Succeeded, restored.Message);
        Assert.True(runtime.IsWindowVisible(recreated));
        Assert.False(runtime.IsIconic(recreated));
        Assert.False(icons.Contains(store.Apps[0].Key));
    }

    private static ManagedAppDefinition ToManaged(RuntimeWindow window) => new(
        window.DisplayName,
        window.ExecutablePath,
        window.ProcessName,
        window.Title,
        window.ClassName,
        true);

    private static void TickFullScan(LegacyCompatibleTrayEngine engine)
    {
        engine.Tick();
        engine.Tick();
        engine.Tick();
        engine.Tick();
    }

    private static RuntimeWindow WaitForWindow(Win32TrayWindowRuntime runtime, int pid)
    {
        RuntimeWindow? found = null;
        WaitUntil(() =>
        {
            found = runtime.EnumerateUserFacingWindows().FirstOrDefault(window => window.Pid == pid);
            return found is not null;
        });
        return found!;
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return;
            Thread.Sleep(50);
        }
        Assert.True(condition(), "Condition did not become true within the timeout.");
    }

    private sealed class FakeStore : IManagedAppStore
    {
        public List<ManagedAppDefinition> Apps { get; private set; }
        public FakeStore(params ManagedAppDefinition[] apps) => Apps = apps.ToList();
        public IReadOnlyList<ManagedAppDefinition> Load() => Apps.ToArray();
        public void Save(IReadOnlyCollection<ManagedAppDefinition> apps) => Apps = apps.ToList();
    }

    private sealed class FakeTrayIcons : ITrayIconRegistry
    {
        private readonly HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);

        public bool Contains(string key) => keys.Contains(key);

        public void Ensure(
            ManagedAppDefinition app,
            Func<EngineCommandResult> restore,
            Func<EngineCommandResult> disable) => keys.Add(app.Key);

        public void Remove(string appKey) => keys.Remove(appKey);
        public void RemoveAll() => keys.Clear();
        public void Dispose() => keys.Clear();
    }

    private sealed class TargetProcess : IDisposable
    {
        private readonly string directory;
        private readonly string controlPath;
        private readonly string statePath;
        private readonly Process process;

        public int Pid => process.Id;

        private TargetProcess(string directory, string controlPath, string statePath, Process process)
        {
            this.directory = directory;
            this.controlPath = controlPath;
            this.statePath = statePath;
            this.process = process;
        }

        public static TargetProcess Start()
        {
            string directory = Path.Combine(Path.GetTempPath(), "stow-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string control = Path.Combine(directory, "control.txt");
            string state = Path.Combine(directory, "state.txt");
            string assemblyPath = typeof(ParityTargetMarker).Assembly.Location;
            string executablePath = Path.ChangeExtension(assemblyPath, ".exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            startInfo.ArgumentList.Add("--control");
            startInfo.ArgumentList.Add(control);
            startInfo.ArgumentList.Add("--state");
            startInfo.ArgumentList.Add(state);

            Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start parity target.");
            WaitUntil(() => File.Exists(state));
            return new TargetProcess(directory, control, state, process);
        }

        public nint RecreateHiddenWindow(nint original, Win32TrayWindowRuntime runtime)
        {
            File.WriteAllText(controlPath, "recreate-hidden");
            nint recreated = 0;
            WaitUntil(() =>
            {
                recreated = ReadHandle();
                return recreated != 0 && recreated != original && runtime.IsWindow(recreated);
            });
            return recreated;
        }

        private nint ReadHandle()
        {
            try
            {
                string[] parts = File.ReadAllText(statePath).Trim().Split('|');
                return parts.Length == 2 && long.TryParse(parts[1], out long raw) ? (nint)raw : 0;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    try { File.WriteAllText(controlPath, "exit"); } catch { }
                    if (!process.WaitForExit(1500))
                        process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            finally
            {
                process.Dispose();
                try { Directory.Delete(directory, recursive: true); } catch { }
            }
        }
    }
}

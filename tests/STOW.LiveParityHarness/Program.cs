using System.Diagnostics;
using System.Runtime.InteropServices;
using STOW.Engine.Contracts;
using STOW.Platform.Windows.Runtime;
using STOW.WindowTestTarget;

namespace STOW.LiveParityHarness;

internal static class Program
{
    private const int SwMinimize = 6;

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int command);

    [STAThread]
    private static int Main(string[] args)
    {
        string resultPath = ValueAfter(args, "--result") ??
            Path.Combine(AppContext.BaseDirectory, "live-parity-harness.log");
        var log = new List<string> { $"STOW live parity {DateTimeOffset.Now:O}" };
        int failures = 0;

        failures += RunScenario("minimize-hide-restore", RunMinimizeRestore, log);
        failures += RunScenario("electron-hwnd-recreation", RunRecreatedHwndRestore, log);

        log.Add($"RESULT={(failures == 0 ? "PASS" : "FAIL")}");
        log.Add($"EXIT_CODE={(failures == 0 ? 0 : 1)}");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");
        File.WriteAllLines(resultPath, log);
        return failures == 0 ? 0 : 1;
    }

    private static int RunScenario(string name, Action action, List<string> log)
    {
        try
        {
            action();
            log.Add($"PASS {name}");
            return 0;
        }
        catch (Exception ex)
        {
            log.Add($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static void RunMinimizeRestore()
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
        Ensure(!runtime.IsWindowVisible(window.Handle), "Managed minimized window was not hidden.");
        Ensure(icons.Contains(store.Apps[0].Key), "Tray tracking was not created.");
        Ensure(engine.Restore(store.Apps[0].Key).Succeeded, "Restore command failed.");
        Ensure(runtime.IsWindowVisible(window.Handle), "Original HWND was not visible after restore.");
        Ensure(!runtime.IsIconic(window.Handle), "Original HWND remained minimized after restore.");
    }

    private static void RunRecreatedHwndRestore()
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
        Ensure(!runtime.IsWindowVisible(original.Handle), "Original HWND was not hidden.");

        nint recreated = target.RecreateHiddenWindow(original.Handle, runtime);
        Ensure(recreated != original.Handle, "HWND was not recreated.");
        Ensure(!runtime.IsWindow(original.Handle), "Original HWND still exists.");
        Ensure(!runtime.IsWindowVisible(recreated), "Replacement HWND should remain hidden before restore.");

        EngineCommandResult result = engine.Restore(store.Apps[0].Key);
        Ensure(result.Succeeded, result.Message ?? "Restore command failed.");
        Ensure(runtime.IsWindowVisible(recreated), "Replacement HWND was not restored by tracked PID.");
        Ensure(!runtime.IsIconic(recreated), "Replacement HWND remained minimized.");
        Ensure(!icons.Contains(store.Apps[0].Key), "Tray tracking remained after successful restore.");
    }

    private static ManagedAppDefinition ToManaged(RuntimeWindow window) => new(
        window.DisplayName,
        window.ExecutablePath,
        window.ProcessName,
        window.Title,
        window.ClassName,
        true);

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

    private static void TickFullScan(LegacyCompatibleTrayEngine engine)
    {
        engine.Tick();
        engine.Tick();
        engine.Tick();
        engine.Tick();
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 6000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return;
            Thread.Sleep(50);
        }
        if (!condition())
            throw new TimeoutException("Condition did not become true within the timeout.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string? ValueAfter(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
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
        public void Ensure(ManagedAppDefinition app, Func<EngineCommandResult> restore, Func<EngineCommandResult> disable) => keys.Add(app.Key);
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
            string directory = Path.Combine(Path.GetTempPath(), "stow-live-harness-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string control = Path.Combine(directory, "control.txt");
            string state = Path.Combine(directory, "state.txt");
            string assemblyPath = typeof(ParityTargetMarker).Assembly.Location;
            string executablePath = Path.ChangeExtension(assemblyPath, ".exe");

            var info = new ProcessStartInfo { FileName = executablePath, UseShellExecute = false };
            info.ArgumentList.Add("--control");
            info.ArgumentList.Add(control);
            info.ArgumentList.Add("--state");
            info.ArgumentList.Add(state);

            Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not start parity target.");
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

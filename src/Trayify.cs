using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.Win32;

[assembly: AssemblyTitle("Trayify")]
[assembly: AssemblyDescription("Minimize Windows applications to the system tray")]
[assembly: AssemblyProduct("Trayify")]
[assembly: AssemblyCompany("Christian Velvet")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Christian Velvet")]
[assembly: AssemblyVersion("0.3.1.0")]
[assembly: AssemblyFileVersion("0.3.1.0")]

internal static class NativeMethods
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] public static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int attrValue, int attrSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;
    public const uint GW_OWNER = 4;
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_APPWINDOW = 0x00040000L;
    public const int DWMWA_CLOAKED = 14;

    public static long GetWindowExStyle(IntPtr hWnd)
    {
        if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64();
        return GetWindowLong32(hWnd, GWL_EXSTYLE);
    }
}

internal sealed class ManagedApp
{
    public string Name = "";
    public string ExePath = "";
    public string ProcessName = "";
    public string TitleHint = "";
    public string ClassHint = "";
    public bool Enabled = true;

    public string Key
    {
        get
        {
            if (!String.IsNullOrEmpty(ExePath)) return "P|" + ExePath.ToLowerInvariant();
            return "N|" + ProcessName.ToLowerInvariant() + "|" + ClassHint.ToLowerInvariant();
        }
    }
}

internal sealed class WindowInfo
{
    public IntPtr Handle;
    public int Pid;
    public string Title = "";
    public string ClassName = "";
    public string ExePath = "";
    public string ProcessName = "";
    public string DisplayName = "";

    public string Key
    {
        get
        {
            if (!String.IsNullOrEmpty(ExePath)) return "P|" + ExePath.ToLowerInvariant();
            return "N|" + ProcessName.ToLowerInvariant() + "|" + ClassName.ToLowerInvariant();
        }
    }
}

internal sealed class TrayifyContext : ApplicationContext
{
    public const string VersionString = "0.3.1";
    public const string RepoUrl = "https://github.com/nocturney/trayify";
    public const string ReleasesUrl = "https://github.com/nocturney/trayify/releases";
    public const string LatestReleaseApi = "https://api.github.com/repos/nocturney/trayify/releases/latest";
    public const string ChangelogUrl = "https://github.com/nocturney/trayify/blob/main/CHANGELOG.md";
    public const string LicenseUrl = "https://github.com/nocturney/trayify/blob/main/LICENSE";
    public const string PrivacyUrl = "https://github.com/nocturney/trayify/blob/main/PRIVACY.md";
    public const string SecurityUrl = "https://github.com/nocturney/trayify/security/policy";
    private readonly string appDir;
    private readonly string configPath;
    private readonly string exePath;
    private readonly string runKeyName = "Trayify";
    private readonly Dictionary<string, ManagedApp> managed = new Dictionary<string, ManagedApp>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<IntPtr>> hidden = new Dictionary<string, HashSet<IntPtr>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<int>> hiddenPids = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NotifyIcon> appTrayIcons = new Dictionary<string, NotifyIcon>(StringComparer.OrdinalIgnoreCase);

    private readonly NotifyIcon mainTray;
    private readonly System.Windows.Forms.Timer engineTimer;
    private readonly System.Windows.Forms.Timer uiTimer;
    private readonly System.Windows.Forms.Timer signalTimer;
    private readonly EventWaitHandle showEvent;
    private MainForm form;
    private bool exiting = false;
    private int engineTicks = 0;

    public TrayifyContext(bool background)
    {
        exePath = Application.ExecutablePath;
        appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trayify");
        configPath = Path.Combine(appDir, "config.txt");
        Directory.CreateDirectory(appDir);
        LoadConfig();
        UpdateWindowsStartup();

        mainTray = new NotifyIcon();
        mainTray.Text = "Trayify";
        mainTray.Icon = GetIconFor(Application.ExecutablePath);
        mainTray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem open = new ToolStripMenuItem("Open Trayify");
        open.Click += delegate { ShowManager(); };
        ToolStripMenuItem exit = new ToolStripMenuItem("Exit Trayify");
        exit.Click += delegate { ExitTrayify(); };
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        mainTray.ContextMenuStrip = menu;
        mainTray.DoubleClick += delegate { ShowManager(); };

        form = new MainForm(this);
        form.FormClosed += delegate { if (!exiting) form = null; };
        IntPtr hiddenFormHandle = form.Handle;

        engineTimer = new System.Windows.Forms.Timer();
        engineTimer.Interval = 100;
        engineTimer.Tick += delegate { EngineTick(); };
        engineTimer.Start();

        uiTimer = new System.Windows.Forms.Timer();
        uiTimer.Interval = 1500;
        uiTimer.Tick += delegate { if (form != null && form.Visible) form.RefreshGrid(true); };
        uiTimer.Start();

        bool eventCreated;
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TrayifyShowManager", out eventCreated);
        signalTimer = new System.Windows.Forms.Timer();
        signalTimer.Interval = 200;
        signalTimer.Tick += delegate
        {
            if (showEvent.WaitOne(0)) ShowManager();
        };
        signalTimer.Start();

        if (!background) ShowManager();
    }

    public List<ManagedApp> ManagedApps
    {
        get { return new List<ManagedApp>(managed.Values); }
    }
    private static string Enc(string s)
    {
        if (s == null) s = "";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    private static string Dec(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch { return ""; }
    }

    private void LoadConfig()
    {
        managed.Clear();
        if (!File.Exists(configPath)) return;
        foreach (string raw in File.ReadAllLines(configPath, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] p = line.Split('|');
            if (p.Length < 6) continue;
            ManagedApp a = new ManagedApp();
            a.Enabled = p[0] == "1";
            a.Name = Dec(p[1]);
            a.ExePath = Dec(p[2]);
            a.ProcessName = Dec(p[3]);
            a.TitleHint = Dec(p[4]);
            a.ClassHint = Dec(p[5]);
            managed[a.Key] = a;
        }
    }

    private void SaveConfig()
    {
        List<string> lines = new List<string>();
        lines.Add("# Trayify config v1");
        foreach (ManagedApp a in managed.Values)
        {
            lines.Add((a.Enabled ? "1" : "0") + "|" + Enc(a.Name) + "|" + Enc(a.ExePath) + "|" +
                      Enc(a.ProcessName) + "|" + Enc(a.TitleHint) + "|" + Enc(a.ClassHint));
        }
        File.WriteAllLines(configPath, lines.ToArray(), Encoding.UTF8);
        UpdateWindowsStartup();
    }

    private void UpdateWindowsStartup()
    {
        bool any = false;
        foreach (ManagedApp a in managed.Values) if (a.Enabled) { any = true; break; }
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;
                if (any)
                {
                    string startupExe = exePath;
                    string wingetPackages = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Microsoft", "WinGet", "Packages");

                    if (exePath.StartsWith(wingetPackages, StringComparison.OrdinalIgnoreCase))
                    {
                        string wingetLink = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Microsoft", "WinGet", "Links", "Trayify.exe");
                        if (File.Exists(wingetLink)) startupExe = wingetLink;
                    }

                    key.SetValue(runKeyName, "\"" + startupExe + "\" --background");
                }
                else key.DeleteValue(runKeyName, false);
            }
        }
        catch { }
    }

    public bool IsEnabled(WindowInfo w)
    {
        ManagedApp a;
        if (managed.TryGetValue(w.Key, out a)) return a.Enabled;
        foreach (ManagedApp x in managed.Values)
        {
            if (!String.IsNullOrEmpty(x.ExePath) && !String.IsNullOrEmpty(w.ExePath) &&
                String.Equals(x.ExePath, w.ExePath, StringComparison.OrdinalIgnoreCase))
                return x.Enabled;
            if (String.IsNullOrEmpty(w.ExePath) &&
                String.Equals(x.ProcessName, w.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                (String.IsNullOrEmpty(x.ClassHint) || String.Equals(x.ClassHint, w.ClassName, StringComparison.Ordinal)))
                return x.Enabled;
        }
        return false;
    }

    public string GetStatus(WindowInfo w)
    {
        ManagedApp a = FindManaged(w);
        if (a == null || !a.Enabled) return "Off";
        HashSet<int> hp;
        if (hiddenPids.TryGetValue(a.Key, out hp) && hp.Count > 0) return "Hidden in tray";
        return "On";
    }

    private ManagedApp FindManaged(WindowInfo w)
    {
        ManagedApp a;
        if (managed.TryGetValue(w.Key, out a)) return a;
        foreach (ManagedApp x in managed.Values)
        {
            if (!String.IsNullOrEmpty(x.ExePath) && !String.IsNullOrEmpty(w.ExePath) &&
                String.Equals(x.ExePath, w.ExePath, StringComparison.OrdinalIgnoreCase))
                return x;
            if (String.IsNullOrEmpty(w.ExePath) &&
                String.Equals(x.ProcessName, w.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                (String.IsNullOrEmpty(x.ClassHint) || String.Equals(x.ClassHint, w.ClassName, StringComparison.Ordinal)))
                return x;
        }
        return null;
    }

    public void SetEnabled(WindowInfo w, bool enabled)
    {
        ManagedApp a = FindManaged(w);
        if (a == null)
        {
            a = new ManagedApp();
            a.Name = w.DisplayName;
            a.ExePath = w.ExePath;
            a.ProcessName = w.ProcessName;
            a.TitleHint = w.Title;
            a.ClassHint = w.ClassName;
            a.Enabled = enabled;
            managed[a.Key] = a;
        }
        else
        {
            a.Enabled = enabled;
            if (!String.IsNullOrEmpty(w.ExePath)) a.ExePath = w.ExePath;
            if (!String.IsNullOrEmpty(w.ProcessName)) a.ProcessName = w.ProcessName;
            if (!String.IsNullOrEmpty(w.DisplayName)) a.Name = w.DisplayName;
            if (!String.IsNullOrEmpty(w.ClassName)) a.ClassHint = w.ClassName;
        }

        if (!enabled) RestoreManaged(a, true);
        SaveConfig();
    }

    public void ToggleConfigured(string key, bool enabled)
    {
        ManagedApp a;
        if (!managed.TryGetValue(key, out a)) return;
        a.Enabled = enabled;
        if (!enabled) RestoreManaged(a, true);
        SaveConfig();
    }
    public List<WindowInfo> EnumerateAppWindows()
    {
        // GUI list: show only real user-facing application windows that belong on the taskbar.
        // Hidden/background/helper windows are intentionally excluded here.
        List<WindowInfo> list = new List<WindowInfo>();
        int selfPid = Process.GetCurrentProcess().Id;

        NativeMethods.EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pidRaw;
            NativeMethods.GetWindowThreadProcessId(h, out pidRaw);
            int pid = (int)pidRaw;
            if (pid == 0 || pid == selfPid) return true;

            bool visible = NativeMethods.IsWindowVisible(h);
            if (!visible) return true;

            long exStyle = NativeMethods.GetWindowExStyle(h);
            bool appWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
            bool toolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
            IntPtr owner = NativeMethods.GetWindow(h, NativeMethods.GW_OWNER);

            // Standard taskbar eligibility rules:
            // owned/tool windows are skipped unless explicitly marked APPWINDOW.
            if (!appWindow && owner != IntPtr.Zero) return true;
            if (!appWindow && toolWindow) return true;

            try
            {
                int cloaked = 0;
                int hr = NativeMethods.DwmGetWindowAttribute(
                    h, NativeMethods.DWMWA_CLOAKED, out cloaked, sizeof(int));
                if (hr == 0 && cloaked != 0) return true;
            }
            catch { }

            StringBuilder tb = new StringBuilder(1024);
            StringBuilder cb = new StringBuilder(256);
            NativeMethods.GetWindowText(h, tb, tb.Capacity);
            NativeMethods.GetClassName(h, cb, cb.Capacity);
            string title = tb.ToString().Trim();
            string cls = cb.ToString();
            if (title.Length == 0) return true;

            string procName = "";
            string path = "";
            string display = "";
            try
            {
                using (Process pr = Process.GetProcessById(pid))
                {
                    procName = pr.ProcessName;
                    display = pr.ProcessName;
                    try { path = pr.MainModule.FileName; } catch { }
                    try
                    {
                        FileVersionInfo vi = pr.MainModule.FileVersionInfo;
                        if (vi != null && !String.IsNullOrEmpty(vi.FileDescription))
                            display = vi.FileDescription;
                    }
                    catch { }
                }
            }
            catch { }

            if (String.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(procName, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(procName, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(procName, "SearchHost", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(procName, "TextInputHost", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(procName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) && title.Length == 0) return true;

            WindowInfo w = new WindowInfo();
            w.Handle = h;
            w.Pid = pid;
            w.Title = title;
            w.ClassName = cls;
            w.ExePath = path;
            w.ProcessName = procName;
            w.DisplayName = String.IsNullOrEmpty(display) ? title : display;
            list.Add(w);
            return true;
        }, IntPtr.Zero);

        return list;
    }

    private List<WindowInfo> EnumerateRuntimeWindows()
    {
        List<WindowInfo> list = new List<WindowInfo>();
        int selfPid = Process.GetCurrentProcess().Id;

        Dictionary<string, HashSet<int>> appPids = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (ManagedApp a in managed.Values)
        {
            if (!a.Enabled) continue;
            HashSet<int> ids = new HashSet<int>();
            if (!String.IsNullOrEmpty(a.ProcessName))
            {
                try
                {
                    Process[] ps = Process.GetProcessesByName(a.ProcessName);
                    foreach (Process pr in ps)
                    {
                        try { ids.Add(pr.Id); }
                        finally { pr.Dispose(); }
                    }
                }
                catch { }
            }
            appPids[a.Key] = ids;
        }

        NativeMethods.EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pidRaw;
            NativeMethods.GetWindowThreadProcessId(h, out pidRaw);
            int pid = (int)pidRaw;
            if (pid == 0 || pid == selfPid) return true;

            StringBuilder tb = new StringBuilder(512);
            StringBuilder cb = new StringBuilder(256);
            NativeMethods.GetWindowText(h, tb, tb.Capacity);
            NativeMethods.GetClassName(h, cb, cb.Capacity);
            string title = tb.ToString();
            string cls = cb.ToString();
            if (title.Length == 0) return true;

            ManagedApp matched = null;
            foreach (ManagedApp a in managed.Values)
            {
                if (!a.Enabled) continue;

                HashSet<int> ids;
                bool pidMatch = appPids.TryGetValue(a.Key, out ids) && ids.Contains(pid);
                bool classOk = String.IsNullOrEmpty(a.ClassHint) || String.Equals(a.ClassHint, cls, StringComparison.Ordinal);

                if (pidMatch && classOk)
                {
                    matched = a;
                    break;
                }

                if (!String.IsNullOrEmpty(a.TitleHint) &&
                    !String.IsNullOrEmpty(a.ClassHint) &&
                    String.Equals(a.TitleHint, title, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(a.ClassHint, cls, StringComparison.Ordinal))
                {
                    matched = a;
                    break;
                }
            }

            if (matched == null) return true;

            WindowInfo w = new WindowInfo();
            w.Handle = h;
            w.Pid = pid;
            w.Title = title;
            w.ClassName = cls;
            w.ProcessName = matched.ProcessName;
            w.DisplayName = matched.Name;
            list.Add(w);
            return true;
        }, IntPtr.Zero);

        return list;
    }

    private bool MatchesIdentity(ManagedApp a, WindowInfo w)
    {
        if (!String.IsNullOrEmpty(a.ExePath) && !String.IsNullOrEmpty(w.ExePath) &&
            String.Equals(a.ExePath, w.ExePath, StringComparison.OrdinalIgnoreCase)) return true;

        if (!String.IsNullOrEmpty(a.ProcessName) && !String.IsNullOrEmpty(w.ProcessName) &&
            String.Equals(a.ProcessName, w.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            if (String.IsNullOrEmpty(a.ClassHint) || String.Equals(a.ClassHint, w.ClassName, StringComparison.Ordinal))
                return true;
        }

        if (!String.IsNullOrEmpty(a.TitleHint) && !String.IsNullOrEmpty(a.ClassHint) &&
            String.Equals(a.TitleHint, w.Title, StringComparison.OrdinalIgnoreCase) &&
            String.Equals(a.ClassHint, w.ClassName, StringComparison.Ordinal))
            return true;

        return false;
    }

    private bool Matches(ManagedApp a, WindowInfo w)
    {
        return a.Enabled && MatchesIdentity(a, w);
    }

    private void EngineTick()
    {
        if (exiting) return;

        // Fast enforcement for apps already in tray mode: match by PID only.
        // This survives Electron/Chromium recreating or re-showing its main HWND.
        if (hiddenPids.Count > 0)
        {
            HashSet<int> trayPids = new HashSet<int>();
            foreach (HashSet<int> set in hiddenPids.Values)
                foreach (int pid in set) trayPids.Add(pid);

            NativeMethods.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint pidRaw;
                NativeMethods.GetWindowThreadProcessId(h, out pidRaw);
                if (trayPids.Contains((int)pidRaw) && NativeMethods.IsWindowVisible(h))
                    NativeMethods.ShowWindow(h, NativeMethods.SW_HIDE);
                return true;
            }, IntPtr.Zero);
        }

        engineTicks++;
        if ((engineTicks % 4) != 0) return;

        List<WindowInfo> windows = EnumerateAppWindows();

        foreach (ManagedApp a in managed.Values)
        {
            if (!a.Enabled) continue;

            foreach (WindowInfo w in windows)
            {
                if (!Matches(a, w)) continue;

                HashSet<int> activePids;
                if (hiddenPids.TryGetValue(a.Key, out activePids) && activePids.Contains(w.Pid))
                {
                    if (NativeMethods.IsWindowVisible(w.Handle))
                        NativeMethods.ShowWindow(w.Handle, NativeMethods.SW_HIDE);
                    continue;
                }

                if (NativeMethods.IsIconic(w.Handle))
                {
                    NativeMethods.ShowWindow(w.Handle, NativeMethods.SW_HIDE);

                    if (!hiddenPids.TryGetValue(a.Key, out activePids))
                    {
                        activePids = new HashSet<int>();
                        hiddenPids[a.Key] = activePids;
                    }
                    activePids.Add(w.Pid);

                    HashSet<IntPtr> hs;
                    if (!hidden.TryGetValue(a.Key, out hs))
                    {
                        hs = new HashSet<IntPtr>();
                        hidden[a.Key] = hs;
                    }
                    hs.Add(w.Handle);
                    EnsureAppTrayIcon(a);
                }
            }
        }

        List<string> keys = new List<string>(hiddenPids.Keys);
        foreach (string key in keys)
        {
            HashSet<int> pids = hiddenPids[key];
            pids.RemoveWhere(delegate(int pid)
            {
                try
                {
                    using (Process p = Process.GetProcessById(pid))
                        return p.HasExited;
                }
                catch { return true; }
            });

            if (pids.Count == 0)
            {
                hiddenPids.Remove(key);
                hidden.Remove(key);
                DisposeAppTrayIcon(key);
            }
        }
    }

    private bool IsWindowHandleAlive(IntPtr handle)
    {
        return NativeMethods.IsWindow(handle);
    }
    private void EnsureAppTrayIcon(ManagedApp a)
    {
        if (appTrayIcons.ContainsKey(a.Key)) return;
        NotifyIcon ni = new NotifyIcon();
        ni.Text = a.Name.Length > 60 ? a.Name.Substring(0, 60) : a.Name;
        ni.Icon = GetIconFor(a.ExePath);
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem open = new ToolStripMenuItem("Restore");
        open.Click += delegate { RestoreManaged(a, true); };
        ToolStripMenuItem disable = new ToolStripMenuItem("Disable minimize-to-tray");
        disable.Click += delegate
        {
            a.Enabled = false;
            RestoreManaged(a, true);
            SaveConfig();
            if (form != null && form.Visible) form.RefreshGrid(true);
        };
        menu.Items.Add(open);
        menu.Items.Add(disable);
        ni.ContextMenuStrip = menu;
        ni.Visible = true;
        ni.DoubleClick += delegate { RestoreManaged(a, true); };
        appTrayIcons[a.Key] = ni;
    }

    private Icon GetIconFor(string path)
    {
        try
        {
            if (!String.IsNullOrEmpty(path) && File.Exists(path))
            {
                Icon i = Icon.ExtractAssociatedIcon(path);
                if (i != null) return i;
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    private void DisposeAppTrayIcon(string key)
    {
        NotifyIcon ni;
        if (!appTrayIcons.TryGetValue(key, out ni)) return;
        ni.Visible = false;
        ni.Dispose();
        appTrayIcons.Remove(key);
    }

    private void RestoreManaged(ManagedApp a, bool foreground)
    {
        // Remove tray enforcement first so restored windows are not immediately hidden again.
        hiddenPids.Remove(a.Key);
        hidden.Remove(a.Key);
        DisposeAppTrayIcon(a.Key);

        List<WindowInfo> windows = EnumerateAppWindows();
        IntPtr target = IntPtr.Zero;
        foreach (WindowInfo w in windows)
        {
            if (!MatchesIdentity(a, w)) continue;
            NativeMethods.ShowWindow(w.Handle, NativeMethods.SW_RESTORE);
            NativeMethods.ShowWindow(w.Handle, NativeMethods.SW_SHOW);
            if (target == IntPtr.Zero) target = w.Handle;
        }

        if (foreground && target != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(target);
    }

    public bool IsConfiguredRunning(ManagedApp a)
    {
        foreach (WindowInfo w in EnumerateAppWindows()) if (Matches(a, w)) return true;
        return false;
    }

    public string GetConfiguredStatus(ManagedApp a)
    {
        if (!a.Enabled) return "Off";
        HashSet<int> hp;
        if (hiddenPids.TryGetValue(a.Key, out hp) && hp.Count > 0) return "Hidden in tray";
        return IsConfiguredRunning(a) ? "On" : "Not running";
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not open the link.\n\n" + ex.Message,
                            "Trayify", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Version ParseReleaseVersion(string value)
    {
        if (String.IsNullOrEmpty(value)) return new Version(0, 0, 0);
        value = value.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(1);
        int dash = value.IndexOf('-');
        if (dash >= 0) value = value.Substring(0, dash);
        Version result;
        if (!Version.TryParse(value, out result)) return new Version(0, 0, 0);
        return result;
    }

    private string ReleaseAssetUrl(string tag, string fileName)
    {
        return RepoUrl + "/releases/download/" + tag + "/" + fileName;
    }

    private string ComputeSha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private string ParseExpectedHash(string sumsText, string fileName)
    {
        foreach (string raw in sumsText.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = Regex.Split(line, "\\s+");
            if (parts.Length < 2) continue;

            string a = parts[0].Trim().TrimStart('*');
            string b = parts[1].Trim().TrimStart('*');

            if (Regex.IsMatch(a, "^[0-9a-fA-F]{64}$") &&
                String.Equals(Path.GetFileName(b), fileName, StringComparison.OrdinalIgnoreCase))
                return a.ToLowerInvariant();

            if (String.Equals(Path.GetFileName(a), fileName, StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(b, "^[0-9a-fA-F]{64}$"))
                return b.ToLowerInvariant();
        }
        throw new Exception("SHA256SUMS.txt does not contain a checksum for " + fileName + ".");
    }

    private bool IsStandardInstalledLocation()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Trayify", "Trayify.exe");
        return String.Equals(
            Path.GetFullPath(exePath),
            Path.GetFullPath(expected),
            StringComparison.OrdinalIgnoreCase);
    }

    private void DownloadAndInstallUpdate(string tag)
    {
        if (!IsStandardInstalledLocation())
        {
            DialogResult openPortable = MessageBox.Show(
                "This copy of Trayify is running in portable mode.\\n\\n" +
                "Automatic installation updates are available for the standard installed version. " +
                "Open the latest release page?",
                "Trayify Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (openPortable == DialogResult.Yes) OpenUrl(ReleasesUrl + "/latest");
            return;
        }

        string updateRoot = Path.Combine(Path.GetTempPath(), "Trayify", "updates", tag);
        Directory.CreateDirectory(updateRoot);

        string setupName = "TrayifySetup.exe";
        string setupPath = Path.Combine(updateRoot, setupName);
        string sumsFile = Path.Combine(updateRoot, "SHA256SUMS.txt");

        using (WebClient wc = new WebClient())
        {
            wc.Headers["User-Agent"] = "Trayify/" + VersionString;
            wc.DownloadFile(ReleaseAssetUrl(tag, setupName), setupPath);
        }

        using (WebClient wc = new WebClient())
        {
            wc.Headers["User-Agent"] = "Trayify/" + VersionString;
            wc.DownloadFile(ReleaseAssetUrl(tag, "SHA256SUMS.txt"), sumsFile);
        }

        string expected = ParseExpectedHash(File.ReadAllText(sumsFile, Encoding.UTF8), setupName);
        string actual = ComputeSha256(setupPath);
        if (!String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new Exception("The downloaded installer failed SHA-256 verification. Nothing was installed.");

        FileVersionInfo vi = FileVersionInfo.GetVersionInfo(setupPath);
        Version downloadedVersion = ParseReleaseVersion(vi.FileVersion);
        Version releaseVersion = ParseReleaseVersion(tag);
        if (downloadedVersion != releaseVersion)
            throw new Exception("The downloaded installer version does not match the GitHub release tag.");

        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = setupPath;
        psi.Arguments = "/VERYSILENT /LAUNCH";
        psi.UseShellExecute = true;
        Process.Start(psi);

        ExitTrayify();
    }

    public void CheckForUpdates()
    {
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string json;
            using (WebClient wc = new WebClient())
            {
                wc.Headers["User-Agent"] = "Trayify/" + VersionString;
                wc.Headers["Accept"] = "application/vnd.github+json";
                json = wc.DownloadString(LatestReleaseApi);
            }

            Match m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!m.Success) throw new Exception("GitHub did not return a release version.");

            string tag = m.Groups[1].Value;
            Version latest = ParseReleaseVersion(tag);
            Version current = ParseReleaseVersion(VersionString);

            if (latest > current)
            {
                DialogResult answer = MessageBox.Show(
                    "A newer version of Trayify is available.\n\n" +
                    "Installed: v" + VersionString + "\n" +
                    "Latest: " + tag + "\n\n" +
                    "Download, verify, and install it now?",
                    "Trayify Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (answer == DialogResult.Yes)
                    DownloadAndInstallUpdate(tag);
            }
            else
            {
                MessageBox.Show("Trayify v" + VersionString + " is up to date.",
                                "Trayify Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (WebException ex)
        {
            DialogResult open = MessageBox.Show(
                "Could not check or download the update from GitHub.\n\n" +
                ex.Message + "\n\nOpen the releases page instead?",
                "Trayify Update", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (open == DialogResult.Yes) OpenUrl(ReleasesUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Update failed safely; the installed version was not replaced.\n\n" + ex.Message,
                            "Trayify Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public void OpenRepository() { OpenUrl(RepoUrl); }
    public void OpenChangelog() { OpenUrl(ChangelogUrl); }
    public void OpenLicense() { OpenUrl(LicenseUrl); }
    public void OpenPrivacy() { OpenUrl(PrivacyUrl); }
    public void OpenSecurity() { OpenUrl(SecurityUrl); }

    public void ShowAbout()
    {
        MessageBox.Show(
            "Trayify v" + VersionString + "\n\n" +
            "A lightweight Windows utility that adds minimize-to-tray behavior " +
            "to applications that do not provide it themselves.\n\n" +
            "License: MIT\n" +
            "Telemetry: None\n" +
            "Update source: GitHub Releases\n\n" +
            "Repository:\n" + RepoUrl,
            "About Trayify", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void ShowManager()
    {
        if (form == null || form.IsDisposed)
        {
            form = new MainForm(this);
            form.FormClosed += delegate { if (!exiting) form = null; };
        }
        form.RefreshGrid(true);
        form.Show();
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.BringToFront();
        form.Activate();
    }

    public void HideManager()
    {
        if (form != null) form.Hide();
    }

    public void ExitTrayify()
    {
        exiting = true;
        engineTimer.Stop();
        uiTimer.Stop();
        signalTimer.Stop();
        foreach (ManagedApp a in new List<ManagedApp>(managed.Values)) RestoreManaged(a, false);
        foreach (NotifyIcon ni in new List<NotifyIcon>(appTrayIcons.Values))
        {
            ni.Visible = false;
            ni.Dispose();
        }
        appTrayIcons.Clear();
        mainTray.Visible = false;
        mainTray.Dispose();
        showEvent.Dispose();
        signalTimer.Dispose();
        if (form != null && !form.IsDisposed) form.Close();
        ExitThread();
    }
}

internal sealed class MainForm : Form
{
    private readonly TrayifyContext ctx;
    private readonly DataGridView grid;
    private readonly Button refreshButton;
    private readonly Label hint;
    private readonly TextBox searchBox;
    private readonly Label summaryLabel;
    private bool loading = false;

    public MainForm(TrayifyContext context)
    {
        ctx = context;

        Font = new Font("Segoe UI", 9.5f);
        Text = "Trayify";
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(780, 520);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 247, 249);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        MenuStrip menuStrip = new MenuStrip();
        menuStrip.Dock = DockStyle.Top;
        menuStrip.BackColor = Color.White;
        menuStrip.ForeColor = Color.FromArgb(40, 42, 46);
        menuStrip.Font = new Font("Segoe UI", 9.25f);
        menuStrip.Padding = new Padding(12, 4, 0, 4);

        ToolStripMenuItem fileMenu = new ToolStripMenuItem("&File");
        ToolStripMenuItem refreshMenu = new ToolStripMenuItem("&Refresh");
        refreshMenu.ShortcutKeys = Keys.F5;
        refreshMenu.Click += delegate { RefreshGrid(true); };
        ToolStripMenuItem exitMenu = new ToolStripMenuItem("E&xit");
        exitMenu.Click += delegate { ctx.ExitTrayify(); };
        fileMenu.DropDownItems.Add(refreshMenu);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitMenu);

        ToolStripMenuItem helpMenu = new ToolStripMenuItem("&Help");
        ToolStripMenuItem updateMenu = new ToolStripMenuItem("Check for &Updates...");
        updateMenu.Click += delegate { ctx.CheckForUpdates(); };
        ToolStripMenuItem changelogMenu = new ToolStripMenuItem("View &Changelog");
        changelogMenu.Click += delegate { ctx.OpenChangelog(); };
        ToolStripMenuItem repoMenu = new ToolStripMenuItem("GitHub &Repository");
        repoMenu.Click += delegate { ctx.OpenRepository(); };
        ToolStripMenuItem licenseMenu = new ToolStripMenuItem("&License");
        licenseMenu.Click += delegate { ctx.OpenLicense(); };
        ToolStripMenuItem privacyMenu = new ToolStripMenuItem("&Privacy");
        privacyMenu.Click += delegate { ctx.OpenPrivacy(); };
        ToolStripMenuItem securityMenu = new ToolStripMenuItem("&Security Policy");
        securityMenu.Click += delegate { ctx.OpenSecurity(); };
        ToolStripMenuItem aboutMenu = new ToolStripMenuItem("&About Trayify");
        aboutMenu.Click += delegate { ctx.ShowAbout(); };
        helpMenu.DropDownItems.Add(updateMenu);
        helpMenu.DropDownItems.Add(changelogMenu);
        helpMenu.DropDownItems.Add(repoMenu);
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add(licenseMenu);
        helpMenu.DropDownItems.Add(privacyMenu);
        helpMenu.DropDownItems.Add(securityMenu);
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add(aboutMenu);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(helpMenu);
        MainMenuStrip = menuStrip;

        Panel header = new Panel();
        header.Dock = DockStyle.Top;
        header.Height = 106;
        header.BackColor = Color.White;
        header.Padding = new Padding(22, 18, 22, 12);

        PictureBox logo = new PictureBox();
        logo.Image = Icon.ToBitmap();
        logo.SizeMode = PictureBoxSizeMode.Zoom;
        logo.SetBounds(22, 20, 58, 58);
        header.Controls.Add(logo);

        Label titleLabel = new Label();
        titleLabel.Text = "Trayify";
        titleLabel.Font = new Font("Segoe UI", 19f, FontStyle.Bold);
        titleLabel.ForeColor = Color.FromArgb(30, 32, 36);
        titleLabel.AutoSize = true;
        titleLabel.Location = new Point(98, 18);
        header.Controls.Add(titleLabel);

        hint = new Label();
        hint.Text = "Choose which apps should disappear into the system tray when minimized.";
        hint.Font = new Font("Segoe UI", 10f);
        hint.ForeColor = Color.FromArgb(92, 96, 105);
        hint.AutoSize = true;
        hint.Location = new Point(101, 58);
        header.Controls.Add(hint);

        Label version = new Label();
        version.Text = "v" + TrayifyContext.VersionString;
        version.ForeColor = Color.FromArgb(112, 116, 124);
        version.AutoSize = true;
        version.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        version.Location = new Point(ClientSize.Width - 74, 25);
        header.Controls.Add(version);

        Panel toolbar = new Panel();
        toolbar.Dock = DockStyle.Top;
        toolbar.Height = 62;
        toolbar.BackColor = Color.FromArgb(246, 247, 249);

        Label filterLabel = new Label();
        filterLabel.Text = "Filter";
        filterLabel.AutoSize = true;
        filterLabel.ForeColor = Color.FromArgb(82, 86, 94);
        filterLabel.Location = new Point(22, 22);
        toolbar.Controls.Add(filterLabel);

        searchBox = new TextBox();
        searchBox.Font = new Font("Segoe UI", 9.5f);
        searchBox.BorderStyle = BorderStyle.FixedSingle;
        searchBox.BackColor = Color.White;
        searchBox.SetBounds(68, 17, 330, 28);
        searchBox.TextChanged += delegate { RefreshGrid(true); };
        toolbar.Controls.Add(searchBox);

        summaryLabel = new Label();
        summaryLabel.Text = "";
        summaryLabel.AutoSize = true;
        summaryLabel.ForeColor = Color.FromArgb(105, 109, 118);
        summaryLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        summaryLabel.Location = new Point(ClientSize.Width - 330, 22);
        toolbar.Controls.Add(summaryLabel);

        refreshButton = new Button();
        refreshButton.Text = "Refresh";
        refreshButton.FlatStyle = FlatStyle.Flat;
        refreshButton.FlatAppearance.BorderColor = Color.FromArgb(208, 211, 218);
        refreshButton.BackColor = Color.White;
        refreshButton.ForeColor = Color.FromArgb(42, 45, 50);
        refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        refreshButton.SetBounds(ClientSize.Width - 120, 13, 92, 34);
        refreshButton.Click += delegate { RefreshGrid(true); };
        toolbar.Controls.Add(refreshButton);

        grid = new DataGridView();
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(232, 234, 238);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = 38;
        grid.RowTemplate.Height = 42;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(38, 41, 46);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 242, 255);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 34, 40);
        grid.DefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 246, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(72, 76, 84);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 0, 5, 0);

        DataGridViewCheckBoxColumn enabled = new DataGridViewCheckBoxColumn();
        enabled.Name = "Enabled";
        enabled.HeaderText = "Tray";
        enabled.Width = 58;
        enabled.FlatStyle = FlatStyle.Flat;
        grid.Columns.Add(enabled);

        DataGridViewTextBoxColumn app = new DataGridViewTextBoxColumn();
        app.Name = "App";
        app.HeaderText = "Application";
        app.Width = 220;
        app.ReadOnly = true;
        grid.Columns.Add(app);

        DataGridViewTextBoxColumn title = new DataGridViewTextBoxColumn();
        title.Name = "Title";
        title.HeaderText = "Window";
        title.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        title.ReadOnly = true;
        grid.Columns.Add(title);

        DataGridViewTextBoxColumn pid = new DataGridViewTextBoxColumn();
        pid.Name = "Pid";
        pid.HeaderText = "PID";
        pid.Width = 72;
        pid.ReadOnly = true;
        grid.Columns.Add(pid);

        DataGridViewTextBoxColumn status = new DataGridViewTextBoxColumn();
        status.Name = "Status";
        status.HeaderText = "Status";
        status.Width = 125;
        status.ReadOnly = true;
        grid.Columns.Add(status);

        grid.CurrentCellDirtyStateChanged += delegate
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += GridCellValueChanged;

        Panel gridHost = new Panel();
        gridHost.Dock = DockStyle.Fill;
        gridHost.Padding = new Padding(22, 0, 22, 0);
        gridHost.BackColor = BackColor;
        gridHost.Controls.Add(grid);

        Panel bottom = new Panel();
        bottom.Dock = DockStyle.Bottom;
        bottom.Height = 48;
        bottom.BackColor = Color.FromArgb(246, 247, 249);

        Label local = new Label();
        local.Text = "Runs locally  •  No telemetry  •  Startup follows enabled apps";
        local.AutoSize = true;
        local.ForeColor = Color.FromArgb(105, 109, 118);
        local.Location = new Point(22, 15);
        bottom.Controls.Add(local);

        Controls.Add(gridHost);
        Controls.Add(bottom);
        Controls.Add(toolbar);
        Controls.Add(header);
        Controls.Add(menuStrip);

        FormClosing += delegate(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                ctx.HideManager();
            }
        };

        Shown += delegate { RefreshGrid(true); };
    }

    private string TagKey(object tag)
    {
        WindowInfo w = tag as WindowInfo;
        if (w != null) return w.Key;
        ManagedApp a = tag as ManagedApp;
        if (a != null) return a.Key;
        return "";
    }

    public void RefreshGrid(bool preserveSelection)
    {
        if (loading) return;
        loading = true;

        string selectedKey = "";
        string currentKey = "";
        int currentColumn = 0;
        int firstDisplayed = -1;

        if (preserveSelection && grid.SelectedRows.Count > 0)
            selectedKey = TagKey(grid.SelectedRows[0].Tag);

        if (preserveSelection && grid.CurrentCell != null)
        {
            currentKey = TagKey(grid.Rows[grid.CurrentCell.RowIndex].Tag);
            currentColumn = grid.CurrentCell.ColumnIndex;
        }

        if (preserveSelection && grid.Rows.Count > 0)
        {
            try { firstDisplayed = grid.FirstDisplayedScrollingRowIndex; }
            catch { firstDisplayed = -1; }
        }

        string filter = searchBox == null ? "" : searchBox.Text.Trim();
        List<WindowInfo> windows = ctx.EnumerateAppWindows();
        Dictionary<string, WindowInfo> unique = new Dictionary<string, WindowInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (WindowInfo w in windows)
        {
            string key = w.Key;
            if (String.IsNullOrEmpty(key)) key = "H|" + w.Handle.ToInt64().ToString();
            if (!unique.ContainsKey(key)) unique[key] = w;
        }

        grid.Rows.Clear();
        HashSet<string> shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int selectedRow = -1;
        int currentRow = -1;

        foreach (WindowInfo w in unique.Values)
        {
            if (filter.Length > 0)
            {
                string hay = (w.DisplayName + " " + w.Title + " " + w.ProcessName).ToLowerInvariant();
                if (hay.IndexOf(filter.ToLowerInvariant()) < 0) continue;
            }
            int idx = grid.Rows.Add(ctx.IsEnabled(w), w.DisplayName, w.Title, w.Pid.ToString(), ctx.GetStatus(w));
            DataGridViewRow row = grid.Rows[idx];
            row.Tag = w;
            string key = TagKey(row.Tag);
            shown.Add(w.Key);
            if (key == selectedKey) selectedRow = idx;
            if (key == currentKey) currentRow = idx;
        }

        foreach (ManagedApp a in ctx.ManagedApps)
        {
            if (shown.Contains(a.Key)) continue;
            if (filter.Length > 0)
            {
                string hay = (a.Name + " " + a.TitleHint + " " + a.ProcessName).ToLowerInvariant();
                if (hay.IndexOf(filter.ToLowerInvariant()) < 0) continue;
            }
            int idx = grid.Rows.Add(a.Enabled, a.Name, a.TitleHint, "-", ctx.GetConfiguredStatus(a));
            DataGridViewRow row = grid.Rows[idx];
            row.Tag = a;
            string key = TagKey(row.Tag);
            if (key == selectedKey) selectedRow = idx;
            if (key == currentKey) currentRow = idx;
        }

        grid.ClearSelection();

        if (preserveSelection && selectedRow >= 0 && selectedRow < grid.Rows.Count)
            grid.Rows[selectedRow].Selected = true;

        int focusRow = currentRow >= 0 ? currentRow : selectedRow;
        if (preserveSelection && focusRow >= 0 && focusRow < grid.Rows.Count && grid.Columns.Count > 0)
        {
            int col = Math.Max(0, Math.Min(currentColumn, grid.Columns.Count - 1));
            grid.CurrentCell = grid.Rows[focusRow].Cells[col];
        }

        if (preserveSelection && firstDisplayed >= 0 && grid.Rows.Count > 0)
        {
            try
            {
                int max = grid.Rows.Count - 1;
                grid.FirstDisplayedScrollingRowIndex = Math.Min(firstDisplayed, max);
            }
            catch { }
        }

        if (summaryLabel != null)
        {
            int enabledCount = 0;
            foreach (ManagedApp a in ctx.ManagedApps) if (a.Enabled) enabledCount++;
            summaryLabel.Text = grid.Rows.Count + " apps  •  " + enabledCount + " managed";
        }

        loading = false;
    }

    private void GridCellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (loading || e.RowIndex < 0 || e.ColumnIndex != grid.Columns["Enabled"].Index) return;
        DataGridViewRow row = grid.Rows[e.RowIndex];
        bool enabled = false;
        if (row.Cells["Enabled"].Value != null)
            Boolean.TryParse(row.Cells["Enabled"].Value.ToString(), out enabled);

        WindowInfo w = row.Tag as WindowInfo;
        if (w != null) ctx.SetEnabled(w, enabled);
        else
        {
            ManagedApp a = row.Tag as ManagedApp;
            if (a != null) ctx.ToggleConfigured(a.Key, enabled);
        }
        RefreshGrid(true);
    }
}

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        bool created;
        using (Mutex mutex = new Mutex(true, @"Local\TrayifySingleInstance", out created))
        {
            if (!created)
            {
                try
                {
                    using (EventWaitHandle ev = EventWaitHandle.OpenExisting(@"Local\TrayifyShowManager"))
                        ev.Set();
                }
                catch { }
                return;
            }
            bool background = false;
            foreach (string a in args)
                if (String.Equals(a, "--background", StringComparison.OrdinalIgnoreCase)) background = true;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayifyContext(background));
        }
    }
}










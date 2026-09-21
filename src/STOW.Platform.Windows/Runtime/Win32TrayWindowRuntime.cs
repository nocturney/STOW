using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using STOW.Platform.Windows.Discovery;

namespace STOW.Platform.Windows.Runtime;

internal sealed class Win32TrayWindowRuntime : ITrayWindowRuntime
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowNative(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisibleNative(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconicNative(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint hWnd, int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hWnd, int attribute, out int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder buffer, int maxCount);

    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const uint GwOwner = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const int DwmwaCloaked = 14;

    public IReadOnlyList<RuntimeWindow> EnumerateUserFacingWindows()
    {
        int selfPid = Environment.ProcessId;
        var windows = new List<RuntimeWindow>();

        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out uint rawPid);
            int pid = unchecked((int)rawPid);
            if (pid <= 0 || pid == selfPid || !IsWindowVisibleNative(handle))
                return true;

            long exStyle = nint.Size == 8 ? GetWindowLongPtr64(handle, GwlExStyle).ToInt64() : GetWindowLong32(handle, GwlExStyle);
            bool appWindow = (exStyle & WsExAppWindow) != 0;
            bool toolWindow = (exStyle & WsExToolWindow) != 0;
            bool hasOwner = GetWindow(handle, GwOwner) != 0;
            bool cloaked = IsCloaked(handle);
            string title = GetTitle(handle).Trim();

            if (!WindowEligibilityRules.IsTaskbarCandidate(appWindow, toolWindow, hasOwner, cloaked, title))
                return true;

            RuntimeWindow? window = CreateRuntimeWindow(handle, pid, title);
            if (window is null || WindowEligibilityRules.IsShellProcess(window.ProcessName, title))
                return true;

            windows.Add(window);
            return true;
        }, 0);

        return windows;
    }

    public IEnumerable<nint> EnumerateTopLevelWindows()
    {
        var handles = new List<nint>();
        EnumWindows((handle, _) =>
        {
            handles.Add(handle);
            return true;
        }, 0);
        return handles;
    }

    public bool IsWindow(nint handle) => IsWindowNative(handle);

    public bool IsWindowVisible(nint handle) => IsWindowVisibleNative(handle);

    public bool IsIconic(nint handle) => IsIconicNative(handle);

    public void Hide(nint handle) => ShowWindow(handle, SwHide);

    public void RestoreAndShow(nint handle)
    {
        ShowWindow(handle, SwRestore);
        ShowWindow(handle, SwShow);
    }

    public void BringToForeground(nint handle) => SetForegroundWindow(handle);

    public int GetProcessId(nint handle)
    {
        GetWindowThreadProcessId(handle, out uint pid);
        return unchecked((int)pid);
    }

    public string GetTitle(nint handle)
    {
        var buffer = new StringBuilder(1024);
        GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public string GetClassName(nint handle)
    {
        var buffer = new StringBuilder(256);
        GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public bool ProcessExists(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlySet<int> GetProcessIdsByName(string processName)
    {
        var ids = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(processName))
            return ids;

        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try { ids.Add(process.Id); }
                finally { process.Dispose(); }
            }
        }
        catch { }

        return ids;
    }

    private static RuntimeWindow? CreateRuntimeWindow(nint handle, int pid, string title)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            string processName = process.ProcessName;
            string executablePath = string.Empty;
            string displayName = processName;

            try
            {
                executablePath = process.MainModule?.FileName ?? string.Empty;
                string? description = process.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                    displayName = description;
            }
            catch
            {
                // Access to another process' module can legitimately fail.
            }

            return new RuntimeWindow(
                handle,
                pid,
                title,
                GetClassNameStatic(handle),
                executablePath,
                processName,
                string.IsNullOrWhiteSpace(displayName) ? title : displayName);
        }
        catch
        {
            return null;
        }
    }

    private static string GetClassNameStatic(nint handle)
    {
        var buffer = new StringBuilder(256);
        GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static bool IsCloaked(nint handle)
    {
        try
        {
            return DwmGetWindowAttribute(handle, DwmwaCloaked, out int value, sizeof(int)) == 0 && value != 0;
        }
        catch
        {
            return false;
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using STOW.Engine.Contracts;

namespace STOW.Platform.Windows.Discovery;

public sealed class Win32AppDiscovery : IAppDiscovery
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

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

    private const uint GwOwner = 4;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const int DwmwaCloaked = 14;

    public IReadOnlyList<DiscoveredAppSnapshot> DiscoverUserFacingApps()
    {
        int selfPid = Environment.ProcessId;
        var unique = new Dictionary<string, DiscoveredAppSnapshot>(StringComparer.OrdinalIgnoreCase);

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
                return true;

            GetWindowThreadProcessId(handle, out uint rawPid);
            int pid = unchecked((int)rawPid);
            if (pid <= 0 || pid == selfPid)
                return true;

            long exStyle = nint.Size == 8 ? GetWindowLongPtr64(handle, GwlExStyle).ToInt64() : GetWindowLong32(handle, GwlExStyle);
            bool appWindow = (exStyle & WsExAppWindow) != 0;
            bool toolWindow = (exStyle & WsExToolWindow) != 0;
            bool hasOwner = GetWindow(handle, GwOwner) != 0;

            int cloaked = 0;
            try
            {
                if (DwmGetWindowAttribute(handle, DwmwaCloaked, out int value, sizeof(int)) == 0)
                    cloaked = value;
            }
            catch
            {
                cloaked = 0;
            }

            string title = ReadWindowText(handle);
            if (!WindowEligibilityRules.IsTaskbarCandidate(appWindow, toolWindow, hasOwner, cloaked != 0, title))
                return true;

            string className = ReadClassName(handle);
            string processName = string.Empty;
            string executablePath = string.Empty;
            string displayName = string.Empty;

            try
            {
                using Process process = Process.GetProcessById(pid);
                processName = process.ProcessName;
                displayName = processName;

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
            }
            catch
            {
                return true;
            }

            if (WindowEligibilityRules.IsShellProcess(processName, title))
                return true;

            string key = !string.IsNullOrWhiteSpace(executablePath)
                ? "P|" + executablePath.ToLowerInvariant()
                : "N|" + processName.ToLowerInvariant() + "|" + className.ToLowerInvariant();

            if (!unique.ContainsKey(key))
            {
                unique[key] = new DiscoveredAppSnapshot(
                    key,
                    string.IsNullOrWhiteSpace(displayName) ? title : displayName,
                    executablePath,
                    processName,
                    title);
            }

            return true;
        }, 0);

        return unique.Values
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string ReadWindowText(nint handle)
    {
        var buffer = new StringBuilder(1024);
        GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString().Trim();
    }

    private static string ReadClassName(nint handle)
    {
        var buffer = new StringBuilder(256);
        GetClassName(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}

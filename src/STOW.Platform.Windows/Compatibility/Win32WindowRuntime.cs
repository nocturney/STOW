using System.Runtime.InteropServices;
using System.Text;

namespace STOW.Platform.Windows.Compatibility;

public sealed class Win32WindowRuntime : IWindowRuntime
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowNative(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder buffer, int maxCount);

    public bool IsWindow(nint handle) => IsWindowNative(handle);

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
}

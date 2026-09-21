using STOW.Engine.Contracts;
using STOW.Platform.Windows.Compatibility;

namespace STOW.Platform.Windows.Runtime;

internal sealed record RuntimeWindow(
    nint Handle,
    int Pid,
    string Title,
    string ClassName,
    string ExecutablePath,
    string ProcessName,
    string DisplayName)
{
    public string Key => !string.IsNullOrWhiteSpace(ExecutablePath)
        ? "P|" + ExecutablePath.ToLowerInvariant()
        : "N|" + ProcessName.ToLowerInvariant() + "|" + ClassName.ToLowerInvariant();
}

internal interface ITrayWindowRuntime : IWindowRuntime
{
    IReadOnlyList<RuntimeWindow> EnumerateUserFacingWindows();
    bool IsWindowVisible(nint handle);
    bool IsIconic(nint handle);
    void Hide(nint handle);
    void RestoreAndShow(nint handle);
    void BringToForeground(nint handle);
    bool ProcessExists(int pid);
    IReadOnlySet<int> GetProcessIdsByName(string processName);
}

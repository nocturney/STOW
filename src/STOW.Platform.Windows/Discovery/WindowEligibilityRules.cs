namespace STOW.Platform.Windows.Discovery;

internal static class WindowEligibilityRules
{
    public static bool IsTaskbarCandidate(bool appWindow, bool toolWindow, bool hasOwner, bool cloaked, string title)
    {
        if (!appWindow && hasOwner)
            return false;
        if (!appWindow && toolWindow)
            return false;
        if (cloaked)
            return false;
        if (string.IsNullOrWhiteSpace(title))
            return false;
        return true;
    }

    public static bool IsShellProcess(string processName, string title)
    {
        if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) return true;
        if (processName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)) return true;
        if (processName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)) return true;
        if (processName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase)) return true;
        if (processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase)) return true;
        if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) && title.Length == 0) return true;
        return false;
    }
}

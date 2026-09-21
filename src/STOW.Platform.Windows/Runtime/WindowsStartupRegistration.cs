using Microsoft.Win32;

namespace STOW.Platform.Windows.Runtime;

internal enum StartupExecutableKind
{
    Unsupported,
    Installed,
    PackageManaged
}

internal sealed class WindowsStartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "STOW";
    private readonly string? executablePath;
    private readonly string localAppData;
    private readonly string userProfile;

    public WindowsStartupRegistration(
        string? executablePath = null,
        string? localAppData = null,
        string? userProfile = null)
    {
        this.executablePath = executablePath ?? Environment.ProcessPath;
        this.localAppData = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        this.userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public void Update(bool shouldStartWithWindows)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        StartupExecutableKind kind = ResolveKind(executablePath, localAppData, userProfile);
        if (kind == StartupExecutableKind.Unsupported)
            return;

        try
        {
            using RegistryKey? run = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (run is null)
                return;

            if (!shouldStartWithWindows)
            {
                run.DeleteValue(RunValueName, throwOnMissingValue: false);
                return;
            }

            string command = kind == StartupExecutableKind.Installed
                ? BuildInstalledCommand(executablePath)
                : BuildPackageManagedCommand();
            run.SetValue(RunValueName, command, RegistryValueKind.String);
        }
        catch
        {
            // Startup registration must never make app configuration unsafe or unsavable.
        }
    }

    internal static StartupExecutableKind ResolveKind(
        string executablePath,
        string localAppData,
        string userProfile)
    {
        string fullPath;
        try { fullPath = Path.GetFullPath(executablePath); }
        catch { return StartupExecutableKind.Unsupported; }

        string installed = Path.Combine(localAppData, "STOW", "STOW.exe");
        if (string.Equals(fullPath, installed, StringComparison.OrdinalIgnoreCase))
            return StartupExecutableKind.Installed;

        if (!string.Equals(Path.GetFileName(fullPath), "STOW.exe", StringComparison.OrdinalIgnoreCase))
            return StartupExecutableKind.Unsupported;

        string wingetRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages") + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(wingetRoot, StringComparison.OrdinalIgnoreCase))
            return StartupExecutableKind.PackageManaged;

        string scoopMarker = Path.DirectorySeparatorChar + "scoop" + Path.DirectorySeparatorChar +
                             "apps" + Path.DirectorySeparatorChar + "stow" + Path.DirectorySeparatorChar;
        if (fullPath.Contains(scoopMarker, StringComparison.OrdinalIgnoreCase))
            return StartupExecutableKind.PackageManaged;

        string defaultScoop = Path.Combine(userProfile, "scoop", "apps", "stow") + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(defaultScoop, StringComparison.OrdinalIgnoreCase))
            return StartupExecutableKind.PackageManaged;

        return StartupExecutableKind.Unsupported;
    }

    internal static string BuildInstalledCommand(string executablePath) =>
        "\"" + executablePath + "\" --background";

    internal static string BuildPackageManagedCommand(string? comspec = null)
    {
        string shell = string.IsNullOrWhiteSpace(comspec)
            ? Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe"
            : comspec;

        return "\"" + shell + "\" /d /c \"" +
               "where STOW.exe >nul 2>&1 && start \"\" STOW.exe --background " +
               "|| reg delete HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run " +
               "/v STOW /f >nul 2>&1\"";
    }
}

using STOW.Engine.Contracts;
using STOW.Platform.Windows.Runtime;

namespace STOW.Platform.Windows.Discovery;

public sealed class Win32AppDiscovery : IAppDiscovery
{
    private readonly ITrayWindowRuntime runtime;

    public Win32AppDiscovery()
        : this(new Win32TrayWindowRuntime())
    {
    }

    internal Win32AppDiscovery(ITrayWindowRuntime runtime)
    {
        this.runtime = runtime;
    }

    public IReadOnlyList<DiscoveredAppSnapshot> DiscoverUserFacingApps()
    {
        return runtime.EnumerateUserFacingWindows()
            .GroupBy(window => window.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(window => new DiscoveredAppSnapshot(
                window.Key,
                window.DisplayName,
                window.ExecutablePath,
                window.ProcessName,
                window.Title,
                window.ClassName))
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}

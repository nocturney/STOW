namespace STOW.Engine.Contracts;

public sealed record DiscoveredAppSnapshot(
    string Key,
    string DisplayName,
    string ExecutablePath,
    string ProcessName,
    string WindowTitle,
    string WindowClass);

public interface IAppDiscovery
{
    IReadOnlyList<DiscoveredAppSnapshot> DiscoverUserFacingApps();
}

namespace STOW.Engine.Contracts;

public sealed record DiscoveredAppSnapshot(
    string Key,
    string DisplayName,
    string ExecutablePath,
    string ProcessName,
    string WindowTitle);

public interface IAppDiscovery
{
    IReadOnlyList<DiscoveredAppSnapshot> DiscoverUserFacingApps();
}

namespace STOW.Engine.Contracts;

public enum ManagedAppRuntimeState
{
    Disabled,
    NotRunning,
    Visible,
    Stowed
}

public sealed record ManagedAppSnapshot(
    string Key,
    string DisplayName,
    string ExecutablePath,
    string ProcessName,
    bool Enabled,
    ManagedAppRuntimeState State);

public sealed record AvailableAppSnapshot(
    string Key,
    string DisplayName,
    string ExecutablePath,
    string ProcessName);

public sealed record EngineSnapshot(
    IReadOnlyList<ManagedAppSnapshot> ManagedApps,
    IReadOnlyList<AvailableAppSnapshot> AvailableApps);

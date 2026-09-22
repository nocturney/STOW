namespace STOW.Engine.Contracts;

public sealed record FocusSessionSnapshot(
    bool Active,
    IReadOnlyCollection<string> KeepVisibleAppKeys,
    IReadOnlyCollection<string> StowedByFocusAppKeys);

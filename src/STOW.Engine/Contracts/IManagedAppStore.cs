namespace STOW.Engine.Contracts;

public sealed record ManagedAppDefinition(
    string Name,
    string ExecutablePath,
    string ProcessName,
    string TitleHint,
    string ClassHint,
    bool Enabled)
{
    public string Key => !string.IsNullOrWhiteSpace(ExecutablePath)
        ? "P|" + ExecutablePath.ToLowerInvariant()
        : "N|" + ProcessName.ToLowerInvariant() + "|" + ClassHint.ToLowerInvariant();
}

public interface IManagedAppStore
{
    IReadOnlyList<ManagedAppDefinition> Load();
    void Save(IReadOnlyCollection<ManagedAppDefinition> apps);
}

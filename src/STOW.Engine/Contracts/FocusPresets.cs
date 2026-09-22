namespace STOW.Engine.Contracts;

public sealed record FocusPresetDefinition(
    string Id,
    string Name,
    IReadOnlyList<string> KeepVisibleAppKeys)
{
    public static FocusPresetDefinition Create(
        string name,
        IEnumerable<string> keepVisibleAppKeys) => new(
            Guid.NewGuid().ToString("N"),
            name.Trim(),
            keepVisibleAppKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray());
}

public interface IFocusPresetStore
{
    IReadOnlyList<FocusPresetDefinition> Load();
    void Save(IReadOnlyCollection<FocusPresetDefinition> presets);
}

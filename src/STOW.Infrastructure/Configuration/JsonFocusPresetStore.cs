using System.Text.Json;
using STOW.Engine.Contracts;

namespace STOW.Infrastructure.Configuration;

public sealed class JsonFocusPresetStore : IFocusPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public JsonFocusPresetStore(string path)
    {
        this.path = path;
    }

    public static JsonFocusPresetStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonFocusPresetStore(Path.Combine(roaming, "STOW", "focus-presets.json"));
    }

    public IReadOnlyList<FocusPresetDefinition> Load()
    {
        if (!File.Exists(path))
            return Array.Empty<FocusPresetDefinition>();

        string json = File.ReadAllText(path);
        FocusPresetDefinition[]? presets =
            JsonSerializer.Deserialize<FocusPresetDefinition[]>(json, JsonOptions);

        return (presets ?? Array.Empty<FocusPresetDefinition>())
            .Select(Normalize)
            .OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public void Save(IReadOnlyCollection<FocusPresetDefinition> presets)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("STOW Focus preset path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temp = path + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            FocusPresetDefinition[] normalized = presets
                .Select(Normalize)
                .OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            File.WriteAllText(temp, JsonSerializer.Serialize(normalized, JsonOptions));
            if (File.Exists(path))
                File.Replace(temp, path, destinationBackupFileName: null);
            else
                File.Move(temp, path);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch { }
        }
    }

    private static FocusPresetDefinition Normalize(FocusPresetDefinition preset) => preset with
    {
        Id = string.IsNullOrWhiteSpace(preset.Id)
            ? Guid.NewGuid().ToString("N")
            : preset.Id.Trim(),
        Name = string.IsNullOrWhiteSpace(preset.Name)
            ? "Untitled preset"
            : preset.Name.Trim(),
        KeepVisibleAppKeys = (preset.KeepVisibleAppKeys ?? Array.Empty<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray()
    };
}

using System.Text.Json;
using System.Text.Json.Serialization;
using STOW.Engine.Contracts;

namespace STOW.Infrastructure.Configuration;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string path;

    public JsonAppSettingsStore(string path)
    {
        this.path = path;
    }

    public static JsonAppSettingsStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonAppSettingsStore(Path.Combine(roaming, "STOW", "settings.json"));
    }

    public AppSettings Load()
    {
        if (!File.Exists(path))
            return AppSettings.Default;

        string json = File.ReadAllText(path);
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        return Normalize(settings ?? AppSettings.Default);
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("STOW settings path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temp = path + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            AppSettings normalized = Normalize(settings);
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

    private static AppSettings Normalize(AppSettings settings)
    {
        int scale = settings.AccessibilityTextScalePercent switch
        {
            100 or 125 or 150 or 200 => settings.AccessibilityTextScalePercent,
            _ => 100
        };

        return settings with { AccessibilityTextScalePercent = scale };
    }
}

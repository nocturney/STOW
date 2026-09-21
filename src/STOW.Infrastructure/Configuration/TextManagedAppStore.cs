using System.Text;
using STOW.Engine.Contracts;

namespace STOW.Infrastructure.Configuration;

public sealed class TextManagedAppStore : IManagedAppStore
{
    private readonly string configPath;

    public TextManagedAppStore(string configPath)
    {
        this.configPath = configPath;
    }

    public static TextManagedAppStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new TextManagedAppStore(Path.Combine(roaming, "STOW", "config.txt"));
    }

    public static TextManagedAppStore ForLegacyTrayifyCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new TextManagedAppStore(Path.Combine(roaming, "Trayify", "config.txt"));
    }

    public IReadOnlyList<ManagedAppDefinition> Load()
    {
        if (!File.Exists(configPath))
            return Array.Empty<ManagedAppDefinition>();

        var apps = new List<ManagedAppDefinition>();
        foreach (string raw in File.ReadAllLines(configPath, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            string[] parts = line.Split('|');
            if (parts.Length < 6)
                throw new InvalidDataException("Invalid STOW managed-app config record.");

            apps.Add(new ManagedAppDefinition(
                Decode(parts[1]),
                Decode(parts[2]),
                Decode(parts[3]),
                Decode(parts[4]),
                Decode(parts[5]),
                parts[0] == "1"));
        }

        return apps;
    }

    public void Save(IReadOnlyCollection<ManagedAppDefinition> apps)
    {
        string? directory = Path.GetDirectoryName(configPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("STOW config path has no parent directory.");

        Directory.CreateDirectory(directory);
        string tempPath = configPath + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            var lines = new List<string> { "# STOW config v1" };
            foreach (ManagedAppDefinition app in apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                lines.Add((app.Enabled ? "1" : "0") + "|" +
                          Encode(app.Name) + "|" +
                          Encode(app.ExecutablePath) + "|" +
                          Encode(app.ProcessName) + "|" +
                          Encode(app.TitleHint) + "|" +
                          Encode(app.ClassHint));
            }

            File.WriteAllLines(tempPath, lines, new UTF8Encoding(false));
            if (File.Exists(configPath))
                File.Replace(tempPath, configPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, configPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static string Decode(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Invalid Base64 value in STOW managed-app config.", ex);
        }
    }
}

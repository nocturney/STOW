using System.Text.Json;
using STOW.Engine.Contracts;

namespace STOW.Infrastructure.Configuration;

public sealed class JsonRuleStore : IRuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public JsonRuleStore(string path)
    {
        this.path = path;
    }

    public static JsonRuleStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonRuleStore(Path.Combine(roaming, "STOW", "rules.json"));
    }

    public IReadOnlyList<RuleDefinition> Load()
    {
        if (!File.Exists(path))
            return Array.Empty<RuleDefinition>();

        string json = File.ReadAllText(path);
        RuleDefinition[]? rules = JsonSerializer.Deserialize<RuleDefinition[]>(json, JsonOptions);
        return (rules ?? Array.Empty<RuleDefinition>())
            .Select(Normalize)
            .ToArray();
    }

    public void Save(IReadOnlyCollection<RuleDefinition> rules)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("STOW rules path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temp = path + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            RuleDefinition[] normalized = rules
                .Select(Normalize)
                .OrderByDescending(rule => rule.Priority)
                .ThenBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
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

    private static RuleDefinition Normalize(RuleDefinition rule) => rule with
    {
        Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id.Trim(),
        Name = string.IsNullOrWhiteSpace(rule.Name) ? "Untitled rule" : rule.Name.Trim(),
        AppKey = rule.AppKey?.Trim() ?? string.Empty,
        Priority = Math.Clamp(rule.Priority, 0, 100)
    };
}

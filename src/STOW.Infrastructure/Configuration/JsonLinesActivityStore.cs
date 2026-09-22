using System.Text;
using System.Text.Json;
using STOW.Engine.Contracts;

namespace STOW.Infrastructure.Configuration;

public sealed class JsonLinesActivityStore : IActivityStore
{
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private const int CompactedLineCount = 1500;

    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly object gate = new();
    private readonly string path;

    public JsonLinesActivityStore(string path)
    {
        this.path = path;
    }

    public static JsonLinesActivityStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonLinesActivityStore(Path.Combine(roaming, "STOW", "activity.jsonl"));
    }

    public void Append(ActivityEvent activity)
    {
        lock (gate)
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("STOW activity path has no parent directory.");

            Directory.CreateDirectory(directory);
            CompactIfNeeded();

            string json = JsonSerializer.Serialize(activity, JsonOptions);
            File.AppendAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    public IReadOnlyList<ActivityEvent> LoadRecent(int maxCount = 1000)
    {
        if (maxCount <= 0 || !File.Exists(path))
            return Array.Empty<ActivityEvent>();

        lock (gate)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            var events = new List<ActivityEvent>(Math.Min(maxCount, lines.Length));

            for (int i = lines.Length - 1; i >= 0 && events.Count < maxCount; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                try
                {
                    ActivityEvent? activity = JsonSerializer.Deserialize<ActivityEvent>(lines[i], JsonOptions);
                    if (activity is not null)
                        events.Add(activity);
                }
                catch (JsonException)
                {
                    // A partially written final line must not make Insights unusable.
                }
            }

            events.Reverse();
            return events;
        }
    }

    private void CompactIfNeeded()
    {
        if (!File.Exists(path) || new FileInfo(path).Length <= MaxFileBytes)
            return;

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string[] keep = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(CompactedLineCount)
            .ToArray();

        string temp = path + ".compact-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllLines(temp, keep, new UTF8Encoding(false));
            File.Replace(temp, path, destinationBackupFileName: null);
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
}

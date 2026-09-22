using System.Text.Json;
using STOW.Engine.Contracts;

namespace STOW.Infrastructure.Configuration;

public sealed class JsonFocusScheduleStore : IFocusScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;

    public JsonFocusScheduleStore(string path)
    {
        this.path = path;
    }

    public static JsonFocusScheduleStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonFocusScheduleStore(
            Path.Combine(roaming, "STOW", "focus-schedules.json"));
    }

    public IReadOnlyList<FocusScheduleDefinition> Load()
    {
        if (!File.Exists(path))
            return Array.Empty<FocusScheduleDefinition>();
        string json = File.ReadAllText(path);
        FocusScheduleDefinition[]? schedules =
            JsonSerializer.Deserialize<FocusScheduleDefinition[]>(json, JsonOptions);

        return NormalizeCollection(
            schedules ?? Array.Empty<FocusScheduleDefinition>());
    }

    public void Save(IReadOnlyCollection<FocusScheduleDefinition> schedules)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("STOW Focus schedule path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temp = path + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            FocusScheduleDefinition[] normalized =
                NormalizeCollection(schedules);

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

    private static FocusScheduleDefinition[] NormalizeCollection(
        IEnumerable<FocusScheduleDefinition> schedules)
    {
        FocusScheduleDefinition[] normalized = schedules
            .Select(ValidateAndNormalize)
            .OrderBy(schedule => schedule.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (normalized
            .GroupBy(schedule => schedule.Id, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Focus schedule IDs must be unique.");
        }

        if (normalized
            .GroupBy(schedule => schedule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Focus schedule names must be unique.");
        }

        return normalized;
    }

    private static FocusScheduleDefinition ValidateAndNormalize(
        FocusScheduleDefinition schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.Id))
            throw new InvalidDataException("Focus schedule ID is missing.");
        if (string.IsNullOrWhiteSpace(schedule.Name))
            throw new InvalidDataException("Focus schedule name is missing.");
        if (string.IsNullOrWhiteSpace(schedule.PresetId))
            throw new InvalidDataException("Focus schedule preset is missing.");
        if (schedule.StartMinutesLocal is < 0 or > 1439)
            throw new InvalidDataException("Focus schedule start time is invalid.");
        if (schedule.DurationMinutes is < 5 or > 720)
            throw new InvalidDataException("Focus schedule duration is invalid.");

        DayOfWeek[] days = (schedule.Days ?? Array.Empty<DayOfWeek>())
            .Distinct()
            .OrderBy(day => (int)day)
            .ToArray();
        if (days.Length == 0)
            throw new InvalidDataException("Focus schedule has no active days.");

        return schedule with
        {
            Id = schedule.Id.Trim(),
            Name = schedule.Name.Trim(),
            PresetId = schedule.PresetId.Trim(),
            Days = days
        };
    }
}

namespace STOW.Engine.Contracts;

public sealed record FocusScheduleDefinition(
    string Id,
    string Name,
    bool Enabled,
    IReadOnlyList<DayOfWeek> Days,
    int StartMinutesLocal,
    int DurationMinutes,
    string PresetId,
    string? LastConsumedOccurrenceKey)
{
    public static FocusScheduleDefinition Create(
        string name,
        string presetId,
        IEnumerable<DayOfWeek> days,
        int startMinutesLocal,
        int durationMinutes)
    {
        string normalizedName = name.Trim();
        string normalizedPresetId = presetId.Trim();
        DayOfWeek[] normalizedDays = days
            .Distinct()
            .OrderBy(day => (int)day)
            .ToArray();

        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("Schedule name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(normalizedPresetId))
            throw new ArgumentException("A Focus preset is required.", nameof(presetId));
        if (normalizedDays.Length == 0)
            throw new ArgumentException("At least one day is required.", nameof(days));
        if (startMinutesLocal is < 0 or > 1439)
            throw new ArgumentOutOfRangeException(nameof(startMinutesLocal));
        if (durationMinutes is < 5 or > 720)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));

        return new FocusScheduleDefinition(
            Guid.NewGuid().ToString("N"),
            normalizedName,
            Enabled: true,
            normalizedDays,
            startMinutesLocal,
            durationMinutes,
            normalizedPresetId,
            LastConsumedOccurrenceKey: null);
    }
}

public interface IFocusScheduleStore
{
    IReadOnlyList<FocusScheduleDefinition> Load();

    void Save(IReadOnlyCollection<FocusScheduleDefinition> schedules);
}
public sealed record FocusScheduleRuntimeStatus(
    bool Healthy,
    string? Message,
    string? ActiveScheduleId,
    DateTime? ActiveUntilLocal)
{
    public static FocusScheduleRuntimeStatus Idle { get; } =
        new(
            Healthy: true,
            Message: null,
            ActiveScheduleId: null,
            ActiveUntilLocal: null);
}

namespace STOW.Engine.Contracts;

public enum ActivityEventType
{
    AppStowed,
    AppRestored,
    FocusStarted,
    FocusEnded
}

public sealed record ActivityEvent(
    DateTimeOffset TimestampUtc,
    ActivityEventType Type,
    string? AppKey = null,
    string? AppName = null,
    string? Source = null);

public interface IActivityStore
{
    void Append(ActivityEvent activity);
    IReadOnlyList<ActivityEvent> LoadRecent(int maxCount = 1000);
}

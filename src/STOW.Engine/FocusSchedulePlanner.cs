using System.Globalization;
using STOW.Engine.Contracts;

namespace STOW.Engine;

public sealed record FocusScheduleOccurrence(
    string ScheduleId,
    DateTime StartLocal,
    DateTime EndLocal)
{
    public string Key =>
        StartLocal.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}

public static class FocusSchedulePlanner
{
    public static FocusScheduleOccurrence? GetActiveOccurrence(
        FocusScheduleDefinition schedule,
        DateTime nowLocal)
    {
        if (!schedule.Enabled)
            return null;

        DateTime now = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);
        foreach (DateTime date in new[] { now.Date, now.Date.AddDays(-1) })
        {
            if (!schedule.Days.Contains(date.DayOfWeek))
                continue;
            DateTime start = date.AddMinutes(schedule.StartMinutesLocal);
            DateTime end = start.AddMinutes(schedule.DurationMinutes);
            if (now >= start && now < end)
                return new FocusScheduleOccurrence(schedule.Id, start, end);
        }

        return null;
    }

    public static FocusScheduleOccurrence? GetNextOccurrence(
        FocusScheduleDefinition schedule,
        DateTime nowLocal)
    {
        if (!schedule.Enabled || schedule.Days.Count == 0)
            return null;

        DateTime now = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);
        FocusScheduleOccurrence? active = GetActiveOccurrence(schedule, now);
        if (active is not null)
            return active;

        for (int offset = 0; offset <= 7; offset++)
        {
            DateTime date = now.Date.AddDays(offset);
            if (!schedule.Days.Contains(date.DayOfWeek))
                continue;

            DateTime start = date.AddMinutes(schedule.StartMinutesLocal);
            if (start <= now)
                continue;

            return new FocusScheduleOccurrence(
                schedule.Id,
                start,
                start.AddMinutes(schedule.DurationMinutes));
        }

        return null;
    }
}

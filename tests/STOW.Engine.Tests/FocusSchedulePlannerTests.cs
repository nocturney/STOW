using STOW.Engine.Contracts;

namespace STOW.Engine.Tests;

public sealed class FocusSchedulePlannerTests
{
    [Fact]
    public void Finds_active_same_day_occurrence()
    {
        FocusScheduleDefinition schedule = Schedule(
            new[] { DayOfWeek.Monday }, 9 * 60, 60);

        FocusScheduleOccurrence? occurrence =
            FocusSchedulePlanner.GetActiveOccurrence(
                schedule,
                new DateTime(2026, 9, 21, 9, 30, 0));

        Assert.NotNull(occurrence);
        Assert.Equal(new DateTime(2026, 9, 21, 9, 0, 0), occurrence.StartLocal);
        Assert.Equal(new DateTime(2026, 9, 21, 10, 0, 0), occurrence.EndLocal);
    }

    [Fact]
    public void Finds_previous_day_occurrence_that_crosses_midnight()
    {
        FocusScheduleDefinition schedule = Schedule(
            new[] { DayOfWeek.Monday }, 23 * 60 + 30, 120);

        FocusScheduleOccurrence? occurrence =
            FocusSchedulePlanner.GetActiveOccurrence(
                schedule,
                new DateTime(2026, 9, 22, 0, 15, 0));

        Assert.NotNull(occurrence);
        Assert.Equal(new DateTime(2026, 9, 21, 23, 30, 0), occurrence.StartLocal);
        Assert.Equal(new DateTime(2026, 9, 22, 1, 30, 0), occurrence.EndLocal);
    }

    [Fact]
    public void Disabled_schedule_has_no_occurrence()
    {
        FocusScheduleDefinition schedule = Schedule(
            new[] { DayOfWeek.Monday }, 9 * 60, 60) with { Enabled = false };

        Assert.Null(FocusSchedulePlanner.GetActiveOccurrence(
            schedule,
            new DateTime(2026, 9, 21, 9, 30, 0)));
    }

    [Fact]
    public void Finds_next_enabled_day()
    {
        FocusScheduleDefinition schedule = Schedule(
            new[] { DayOfWeek.Wednesday }, 8 * 60, 45);

        FocusScheduleOccurrence? occurrence =
            FocusSchedulePlanner.GetNextOccurrence(
                schedule,
                new DateTime(2026, 9, 21, 12, 0, 0));

        Assert.NotNull(occurrence);
        Assert.Equal(new DateTime(2026, 9, 23, 8, 0, 0), occurrence.StartLocal);
    }

    private static FocusScheduleDefinition Schedule(
        IReadOnlyList<DayOfWeek> days,
        int start,
        int duration) =>
        FocusScheduleDefinition.Create(
            "Test",
            "preset",
            days,
            start,
            duration);
}

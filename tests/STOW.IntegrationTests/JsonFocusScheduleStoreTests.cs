using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class JsonFocusScheduleStoreTests
{
    [Fact]
    public void Missing_schedule_file_loads_as_empty()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "focus-schedules.json");
        var store = new JsonFocusScheduleStore(path);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Schedules_round_trip_with_occurrence_marker()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-focus-schedules-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "focus-schedules.json");

        try
        {
            var store = new JsonFocusScheduleStore(path);
            FocusScheduleDefinition schedule = FocusScheduleDefinition.Create(
                "Weekday deep work",
                "preset-1",
                new[] { DayOfWeek.Monday, DayOfWeek.Wednesday },
                startMinutesLocal: 9 * 60 + 30,
                durationMinutes: 90) with
            {
                LastConsumedOccurrenceKey = "2026-09-21T09:30"
            };

            store.Save(new[] { schedule });
            FocusScheduleDefinition loaded = Assert.Single(store.Load());

            Assert.Equal(schedule.Id, loaded.Id);
            Assert.Equal("Weekday deep work", loaded.Name);
            Assert.True(loaded.Enabled);
            Assert.Equal(
                new[] { DayOfWeek.Monday, DayOfWeek.Wednesday },
                loaded.Days);
            Assert.Equal(570, loaded.StartMinutesLocal);
            Assert.Equal(90, loaded.DurationMinutes);
            Assert.Equal("preset-1", loaded.PresetId);
            Assert.Equal("2026-09-21T09:30", loaded.LastConsumedOccurrenceKey);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
    [Fact]
    public void Duplicate_ids_or_names_are_rejected()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-focus-schedules-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "focus-schedules.json");

        try
        {
            var store = new JsonFocusScheduleStore(path);
            FocusScheduleDefinition first = FocusScheduleDefinition.Create(
                "Morning",
                "preset-1",
                new[] { DayOfWeek.Monday },
                9 * 60,
                60);
            FocusScheduleDefinition duplicateId = FocusScheduleDefinition.Create(
                "Second",
                "preset-1",
                new[] { DayOfWeek.Tuesday },
                10 * 60,
                60) with { Id = first.Id };
            Assert.Throws<InvalidDataException>(() =>
                store.Save(new[] { first, duplicateId }));

            FocusScheduleDefinition duplicateName = FocusScheduleDefinition.Create(
                "morning",
                "preset-1",
                new[] { DayOfWeek.Wednesday },
                11 * 60,
                60);
            Assert.Throws<InvalidDataException>(() =>
                store.Save(new[] { first, duplicateName }));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Invalid_schedule_is_rejected_instead_of_becoming_active()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-focus-schedules-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "focus-schedules.json");

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                path,
                """
                [
                  {
                    "Id": "unsafe",
                    "Name": "Unsafe",
                    "Enabled": true,
                    "Days": [1],
                    "StartMinutesLocal": -1,
                    "DurationMinutes": 60,
                    "PresetId": "preset-1",
                    "LastConsumedOccurrenceKey": null
                  }
                ]
                """);

            var store = new JsonFocusScheduleStore(path);
            Assert.Throws<InvalidDataException>(() => store.Load());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

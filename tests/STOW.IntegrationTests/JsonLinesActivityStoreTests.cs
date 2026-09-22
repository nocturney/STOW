using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class JsonLinesActivityStoreTests
{
    [Fact]
    public void Activity_round_trips_in_order()
    {
        string dir = Path.Combine(Path.GetTempPath(), "stow-activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "activity.jsonl");

        try
        {
            var store = new JsonLinesActivityStore(path);
            var first = new ActivityEvent(
                DateTimeOffset.UtcNow.AddMinutes(-2),
                ActivityEventType.AppStowed,
                "app",
                "Editor",
                "Minimize");
            var second = new ActivityEvent(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                ActivityEventType.AppRestored,
                "app",
                "Editor",
                "Manual");

            store.Append(first);
            store.Append(second);

            IReadOnlyList<ActivityEvent> loaded = store.LoadRecent(10);
            Assert.Equal(2, loaded.Count);
            Assert.Equal(ActivityEventType.AppStowed, loaded[0].Type);
            Assert.Equal(ActivityEventType.AppRestored, loaded[1].Type);
            Assert.Equal("Manual", loaded[1].Source);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Malformed_line_is_skipped_without_losing_valid_history()
    {
        string dir = Path.Combine(Path.GetTempPath(), "stow-activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "activity.jsonl");

        try
        {
            var store = new JsonLinesActivityStore(path);
            store.Append(new ActivityEvent(
                DateTimeOffset.UtcNow,
                ActivityEventType.FocusStarted,
                Source: "Focus"));
            File.AppendAllText(path, "{not valid json" + Environment.NewLine);

            IReadOnlyList<ActivityEvent> loaded = store.LoadRecent(10);

            ActivityEvent activity = Assert.Single(loaded);
            Assert.Equal(ActivityEventType.FocusStarted, activity.Type);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Load_recent_respects_requested_limit()
    {
        string dir = Path.Combine(Path.GetTempPath(), "stow-activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "activity.jsonl");

        try
        {
            var store = new JsonLinesActivityStore(path);
            for (int i = 0; i < 5; i++)
            {
                store.Append(new ActivityEvent(
                    DateTimeOffset.UtcNow.AddMinutes(i),
                    ActivityEventType.FocusStarted,
                    Source: "Focus"));
            }

            IReadOnlyList<ActivityEvent> loaded = store.LoadRecent(2);

            Assert.Equal(2, loaded.Count);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}

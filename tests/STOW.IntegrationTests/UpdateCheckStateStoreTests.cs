using STOW.Infrastructure.Updates;

namespace STOW.IntegrationTests;

public sealed class UpdateCheckStateStoreTests
{
    [Fact]
    public void Last_check_round_trips_locally()
    {
        string dir = Path.Combine(Path.GetTempPath(), "stow-update-state-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "update-check.txt");
        try
        {
            var store = new UpdateCheckStateStore(path);
            DateTimeOffset expected = DateTimeOffset.Parse("2026-09-22T06:30:00+03:00");
            store.SaveLastCheck(expected);

            DateTimeOffset? actual = store.LoadLastCheck();

            Assert.NotNull(actual);
            Assert.Equal(expected, actual!.Value);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}

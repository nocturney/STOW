using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class LegalAcceptanceStoreTests
{
    [Fact]
    public void Missing_file_is_not_accepted()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "stow-legal-" + Guid.NewGuid().ToString("N"),
            "legal-acceptance.txt");

        var store = new LegalAcceptanceStore(path);

        Assert.False(store.IsAccepted());
    }

    [Fact]
    public void Current_revision_round_trips()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-legal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "legal-acceptance.txt");

        try
        {
            var store = new LegalAcceptanceStore(path);

            store.Accept("test");

            Assert.True(store.IsAccepted());
            string text = File.ReadAllText(path);
            Assert.Contains(
                "REVISION=" + LegalAcceptanceStore.CurrentRevision,
                text);
            Assert.Contains("SOURCE=test", text);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Older_revision_is_not_treated_as_current()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-legal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "legal-acceptance.txt");

        try
        {
            File.WriteAllText(path, "REVISION=older-revision\n");
            var store = new LegalAcceptanceStore(path);

            Assert.False(store.IsAccepted());
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}

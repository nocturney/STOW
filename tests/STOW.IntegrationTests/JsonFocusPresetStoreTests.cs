using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class JsonFocusPresetStoreTests
{
    [Fact]
    public void Missing_preset_file_loads_as_empty()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "stow-focus-presets-" + Guid.NewGuid().ToString("N"),
            "focus-presets.json");

        var store = new JsonFocusPresetStore(path);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Presets_round_trip_with_normalized_unique_app_keys()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-focus-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "focus-presets.json");

        try
        {
            var store = new JsonFocusPresetStore(path);
            var preset = new FocusPresetDefinition(
                "focus-one",
                "  Writing  ",
                new[] { "APP-B", "app-a", "APP-B", " " });

            store.Save(new[] { preset });
            FocusPresetDefinition loaded = Assert.Single(store.Load());

            Assert.Equal("focus-one", loaded.Id);
            Assert.Equal("Writing", loaded.Name);
            Assert.Equal(
                new[] { "app-a", "APP-B" },
                loaded.KeepVisibleAppKeys,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Create_generates_id_and_deduplicates_keys()
    {
        FocusPresetDefinition preset = FocusPresetDefinition.Create(
            "Deep work",
            new[] { "one", "ONE", "two" });

        Assert.False(string.IsNullOrWhiteSpace(preset.Id));
        Assert.Equal("Deep work", preset.Name);
        Assert.Equal(2, preset.KeepVisibleAppKeys.Count);
    }
}

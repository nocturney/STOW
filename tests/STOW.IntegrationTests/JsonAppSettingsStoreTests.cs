using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public void Missing_settings_file_returns_compatibility_defaults()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "stow-settings-" + Guid.NewGuid().ToString("N"),
            "settings.json");

        var store = new JsonAppSettingsStore(path);

        Assert.Equal(AppSettings.Default, store.Load());
        Assert.True(store.Load().KeepRunningInTray);
        Assert.True(store.Load().StartWithWindows);
        Assert.Equal(ThemePreference.System, store.Load().Theme);
        Assert.Equal(
            FocusEndBehavior.RestorePreviousDesktop,
            store.Load().FocusEndBehavior);
    }

    [Fact]
    public void Settings_round_trip_atomically()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");

        try
        {
            var store = new JsonAppSettingsStore(path);
            var expected = new AppSettings(
                KeepRunningInTray: false,
                StartWithWindows: false,
                Theme: ThemePreference.Dark,
                FocusEndBehavior: FocusEndBehavior.KeepAppsStowed);

            store.Save(expected);
            AppSettings actual = store.Load();

            Assert.Equal(expected, actual);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}

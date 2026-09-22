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
        Assert.False(store.Load().NotificationsEnabled);
        Assert.Equal(ThemePreference.System, store.Load().Theme);
        Assert.Equal(
            FocusEndBehavior.RestorePreviousDesktop,
            store.Load().FocusEndBehavior);
    }

    [Fact]
    public void Legacy_settings_without_notifications_field_default_to_disabled()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "stow-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");

        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "KeepRunningInTray": true,
                  "StartWithWindows": false,
                  "Theme": "Dark",
                  "FocusEndBehavior": "KeepAppsStowed"
                }
                """);

            var store = new JsonAppSettingsStore(path);
            AppSettings settings = store.Load();

            Assert.True(settings.KeepRunningInTray);
            Assert.False(settings.StartWithWindows);
            Assert.Equal(ThemePreference.Dark, settings.Theme);
            Assert.Equal(FocusEndBehavior.KeepAppsStowed, settings.FocusEndBehavior);
            Assert.False(settings.NotificationsEnabled);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
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
                FocusEndBehavior: FocusEndBehavior.KeepAppsStowed,
                NotificationsEnabled: true);

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

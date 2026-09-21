using System.Text;
using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class TextManagedAppStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "stow-store-tests-" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(root, "config.txt");

    [Fact]
    public void Loads_Trayify_v1_format_without_conversion_loss()
    {
        Directory.CreateDirectory(root);
        static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        string legacy = "# Trayify config v1" + Environment.NewLine +
                        $"1|{B64("GrokBot")}|{B64(@"C:\\Apps\\GrokBot.exe")}|{B64("GrokBot")}|{B64("Grok")}|{B64("Chrome_WidgetWin_1")}";
        File.WriteAllText(ConfigPath, legacy, new UTF8Encoding(false));

        IReadOnlyList<ManagedAppDefinition> apps = new TextManagedAppStore(ConfigPath).Load();

        ManagedAppDefinition app = Assert.Single(apps);
        Assert.Equal("GrokBot", app.Name);
        Assert.True(app.Enabled);
        Assert.Equal("Chrome_WidgetWin_1", app.ClassHint);
    }

    [Fact]
    public void Save_then_load_round_trips_managed_apps()
    {
        var expected = new[]
        {
            new ManagedAppDefinition("Editor", @"C:\\Apps\\Editor.exe", "Editor", "Project", "EditorWindow", true),
            new ManagedAppDefinition("Chat", @"C:\\Apps\\Chat.exe", "Chat", "Chat", "Chrome_WidgetWin_1", false)
        };
        var store = new TextManagedAppStore(ConfigPath);

        store.Save(expected);
        IReadOnlyList<ManagedAppDefinition> actual = store.Load();

        Assert.Equal(2, actual.Count);
        Assert.Contains(actual, app => app.Name == "Editor" && app.Enabled);
        Assert.Contains(actual, app => app.Name == "Chat" && !app.Enabled);
        Assert.StartsWith("# STOW config v1", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void Save_replaces_existing_config_atomically_from_the_consumers_view()
    {
        var store = new TextManagedAppStore(ConfigPath);
        store.Save([new ManagedAppDefinition("Old", "", "Old", "", "OldClass", true)]);

        store.Save([new ManagedAppDefinition("New", "", "New", "", "NewClass", false)]);

        ManagedAppDefinition app = Assert.Single(store.Load());
        Assert.Equal("New", app.Name);
        Assert.False(app.Enabled);
        Assert.Empty(Directory.GetFiles(root, "*.writing-*"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Test cleanup should not hide assertion failures.
        }
    }
}

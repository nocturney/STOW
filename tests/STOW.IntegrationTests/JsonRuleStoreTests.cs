using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;

namespace STOW.IntegrationTests;

public sealed class JsonRuleStoreTests
{
    [Fact]
    public void Rules_round_trip_with_priority_and_action()
    {
        string dir = Path.Combine(Path.GetTempPath(), "stow-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "rules.json");

        try
        {
            var store = new JsonRuleStore(path);
            RuleDefinition[] expected =
            [
                new("one", "Keep editor visible", @"P|c:\apps\editor.exe",
                    RuleTrigger.Minimize, RuleAction.KeepVisible, true, 80),
                new("two", "Stow chat", @"P|c:\apps\chat.exe",
                    RuleTrigger.Minimize, RuleAction.Stow, false, 20),
                new("three", "Keep editor during Focus", "P|editor",
                    RuleTrigger.Focus, RuleAction.KeepVisible, true, 60)
            ];

            store.Save(expected);
            IReadOnlyList<RuleDefinition> loaded = store.Load();

            Assert.Equal(3, loaded.Count);
            Assert.Equal("one", loaded[0].Id);
            Assert.Equal(RuleAction.KeepVisible, loaded[0].Action);
            Assert.Equal(80, loaded[0].Priority);
            Assert.Equal("three", loaded[1].Id);
            Assert.Equal(RuleTrigger.Focus, loaded[1].Trigger);
            Assert.Equal(RuleAction.KeepVisible, loaded[1].Action);
            Assert.Equal("two", loaded[2].Id);
            Assert.False(loaded[2].Enabled);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Missing_rules_file_loads_as_empty()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "rules.json");
        var store = new JsonRuleStore(path);

        Assert.Empty(store.Load());
    }
}

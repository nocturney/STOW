using STOW.Engine.Contracts;

namespace STOW.Engine.Tests;

public sealed class RuleEvaluatorTests
{
    [Fact]
    public void No_matching_rule_uses_stow_fallback()
    {
        RuleAction action = RuleEvaluator.Resolve(
            Array.Empty<RuleDefinition>(),
            "app",
            RuleTrigger.Minimize);

        Assert.Equal(RuleAction.Stow, action);
    }

    [Fact]
    public void Highest_priority_enabled_rule_wins()
    {
        RuleDefinition[] rules =
        [
            new("low", "Low", "app", RuleTrigger.Minimize, RuleAction.Stow, true, 10),
            new("high", "High", "app", RuleTrigger.Minimize, RuleAction.KeepVisible, true, 90)
        ];

        RuleAction action = RuleEvaluator.Resolve(rules, "app", RuleTrigger.Minimize);

        Assert.Equal(RuleAction.KeepVisible, action);
    }

    [Fact]
    public void Disabled_rule_is_ignored()
    {
        RuleDefinition[] rules =
        [
            new("disabled", "Disabled", "app", RuleTrigger.Minimize, RuleAction.KeepVisible, false, 100)
        ];

        RuleAction action = RuleEvaluator.Resolve(rules, "app", RuleTrigger.Minimize);

        Assert.Equal(RuleAction.Stow, action);
    }
}

namespace STOW.Engine.Contracts;

public enum RuleTrigger
{
    Minimize
}

public enum RuleAction
{
    Stow,
    KeepVisible
}

public sealed record RuleDefinition(
    string Id,
    string Name,
    string AppKey,
    RuleTrigger Trigger,
    RuleAction Action,
    bool Enabled,
    int Priority)
{
    public static RuleDefinition Create(
        string name,
        string appKey,
        RuleAction action,
        int priority = 50,
        bool enabled = true) => new(
            Guid.NewGuid().ToString("N"),
            name,
            appKey,
            RuleTrigger.Minimize,
            action,
            enabled,
            Math.Clamp(priority, 0, 100));
}

public interface IRuleStore
{
    IReadOnlyList<RuleDefinition> Load();
    void Save(IReadOnlyCollection<RuleDefinition> rules);
}

public static class RuleEvaluator
{
    public static RuleAction Resolve(
        IEnumerable<RuleDefinition> rules,
        string appKey,
        RuleTrigger trigger,
        RuleAction fallback = RuleAction.Stow)
    {
        RuleDefinition? match = rules
            .Where(rule => rule.Enabled &&
                           rule.Trigger == trigger &&
                           string.Equals(rule.AppKey, appKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();

        return match?.Action ?? fallback;
    }
}

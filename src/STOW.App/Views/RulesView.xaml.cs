using System.Windows;
using System.Windows.Controls;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class RulesView : UserControl
{
    private readonly IRuleStore ruleStore;
    private readonly IManagedAppStore managedAppStore;
    private readonly bool canEdit;
    private readonly string? unavailableReason;

    private IReadOnlyList<RuleDefinition> rules = Array.Empty<RuleDefinition>();
    private IReadOnlyList<ManagedOption> apps = Array.Empty<ManagedOption>();
    private string? editingRuleId;
    private RuleTrigger? triggerFilter;

    private sealed record ManagedOption(string Key, string Name, string ProcessName);
    private sealed record RuleRow(
        string Id,
        string Name,
        string Summary,
        string Type,
        string Status);

    public RulesView(
        IRuleStore ruleStore,
        IManagedAppStore managedAppStore,
        bool canEdit,
        string? unavailableReason = null)
    {
        this.ruleStore = ruleStore;
        this.managedAppStore = managedAppStore;
        this.canEdit = canEdit;
        this.unavailableReason = unavailableReason;

        InitializeComponent();
        LoadManagedApps();
        RefreshRules();
        ApplyEditAvailability();
    }

    private void ApplyEditAvailability()
    {
        NewRuleButton.IsEnabled = canEdit;
        SaveRuleButton.IsEnabled = canEdit;
        DeleteRuleButton.IsEnabled = canEdit;
        DuplicateRuleButton.IsEnabled = canEdit;

        if (!canEdit)
        {
            RulesStatusBanner.Visibility = Visibility.Visible;
            RulesStatusText.Text = unavailableReason ??
                "Rules are read-only because the STOW runtime is not available.";
        }
    }

    private void LoadManagedApps()
    {
        try
        {
            apps = managedAppStore.Load()
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(app => new ManagedOption(app.Key, app.Name, app.ProcessName))
                .ToArray();
        }
        catch
        {
            apps = Array.Empty<ManagedOption>();
        }

        AppComboBox.ItemsSource = apps;
    }

    private void RefreshRules(string? selectId = null)
    {
        try
        {
            rules = ruleStore.Load();
        }
        catch (Exception ex)
        {
            rules = Array.Empty<RuleDefinition>();
            RulesStatusBanner.Visibility = Visibility.Visible;
            RulesStatusText.Text = "Rules could not be loaded: " + ex.Message;
        }

        string filter = SearchBox?.Text?.Trim() ?? string.Empty;
        RuleRow[] rows = rules
            .Where(rule => triggerFilter is null || rule.Trigger == triggerFilter)
            .Where(rule => string.IsNullOrEmpty(filter) ||
                           rule.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                           AppName(rule.AppKey).Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToRow)
            .ToArray();

        RulesList.ItemsSource = rows;
        EmptyRulesCard.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyRulesTitleText.Text = rules.Count == 0 ? "No rules yet" : "No matching rules";
        EmptyRulesBodyText.Text = triggerFilter switch
        {
            RuleTrigger.Focus => "Create a Focus rule to control whether a managed app stays visible when Focus starts.",
            RuleTrigger.Minimize => "Create an app rule to override what happens when a managed app is minimized.",
            _ => "Create an app or Focus rule for a managed application."
        };

        string? target = selectId ?? editingRuleId;
        if (!string.IsNullOrWhiteSpace(target))
        {
            RuleRow? row = rows.FirstOrDefault(item =>
                item.Id.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
                RulesList.SelectedItem = row;
        }
    }

    private RuleRow ToRow(RuleDefinition rule)
    {
        string action = rule.Action == RuleAction.KeepVisible
            ? "keep visible"
            : "stow in tray";
        string appName = AppName(rule.AppKey);
        string summary = rule.Trigger == RuleTrigger.Focus
            ? $"When Focus starts, {action} {appName} · priority {rule.Priority}"
            : $"When {appName} is minimized, {action} · priority {rule.Priority}";
        string type = rule.Trigger == RuleTrigger.Focus ? "Focus rule" : "App rule";

        return new RuleRow(
            rule.Id,
            rule.Name,
            summary,
            type,
            rule.Enabled ? "Enabled" : "Off");
    }

    private string AppName(string key) =>
        apps.FirstOrDefault(app => app.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Name
        ?? "Unknown app";

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshRules();

    private void RulesFilter_Click(object sender, RoutedEventArgs e)
    {
        triggerFilter = sender is Button { Tag: string tag }
            ? tag switch
            {
                "Minimize" => RuleTrigger.Minimize,
                "Focus" => RuleTrigger.Focus,
                _ => null
            }
            : null;

        editingRuleId = null;
        RulesList.SelectedItem = null;
        EditorPanel.Visibility = Visibility.Collapsed;
        EditorPlaceholder.Visibility = Visibility.Visible;
        RefreshRules();
    }

    private void RulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RulesList.SelectedItem is not RuleRow row)
            return;

        RuleDefinition? rule = rules.FirstOrDefault(item =>
            item.Id.Equals(row.Id, StringComparison.OrdinalIgnoreCase));
        if (rule is null)
            return;

        editingRuleId = rule.Id;
        ShowEditor(rule);
    }

    private void NewRule_Click(object sender, RoutedEventArgs e)
    {
        if (!canEdit)
            return;

        if (apps.Count == 0)
        {
            MessageBox.Show(
                "Add at least one managed app before creating a rule.",
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        editingRuleId = null;
        RulesList.SelectedItem = null;
        ShowEditor(RuleDefinition.Create(
            "New rule",
            apps[0].Key,
            RuleAction.Stow,
            priority: 50,
            enabled: true,
            trigger: triggerFilter ?? RuleTrigger.Minimize));
        RuleNameTextBox.SelectAll();
        RuleNameTextBox.Focus();
    }

    private void ShowEditor(RuleDefinition rule)
    {
        EditorPlaceholder.Visibility = Visibility.Collapsed;
        EditorPanel.Visibility = Visibility.Visible;
        EditorTitleText.Text = string.IsNullOrWhiteSpace(rule.Name) ? "Rule" : rule.Name;
        RuleNameTextBox.Text = rule.Name;
        EnabledCheckBox.IsChecked = rule.Enabled;
        PrioritySlider.Value = rule.Priority;
        TriggerComboBox.SelectedIndex = rule.Trigger == RuleTrigger.Focus ? 1 : 0;
        ActionComboBox.SelectedIndex = rule.Action == RuleAction.KeepVisible ? 1 : 0;

        ManagedOption? app = apps.FirstOrDefault(item =>
            item.Key.Equals(rule.AppKey, StringComparison.OrdinalIgnoreCase));
        AppComboBox.SelectedItem = app;
        DeleteRuleButton.IsEnabled = canEdit && editingRuleId is not null;
        DuplicateRuleButton.IsEnabled = canEdit && editingRuleId is not null;
        SaveRuleButton.IsEnabled = canEdit;
    }

    private void SaveRule_Click(object sender, RoutedEventArgs e)
    {
        if (!canEdit)
            return;

        string name = RuleNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Give the rule a name.", "STOW",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (AppComboBox.SelectedItem is not ManagedOption app)
        {
            MessageBox.Show("Choose a managed app for this rule.", "STOW",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RuleTrigger trigger = TriggerComboBox.SelectedIndex == 1
            ? RuleTrigger.Focus
            : RuleTrigger.Minimize;
        RuleAction action = ActionComboBox.SelectedIndex == 1
            ? RuleAction.KeepVisible
            : RuleAction.Stow;
        int priority = (int)Math.Round(PrioritySlider.Value);

        var updated = rules.ToList();
        RuleDefinition saved;
        if (editingRuleId is null)
        {
            saved = RuleDefinition.Create(
                name,
                app.Key,
                action,
                priority,
                EnabledCheckBox.IsChecked == true,
                trigger);
            updated.Add(saved);
        }
        else
        {
            RuleDefinition? existing = updated.FirstOrDefault(rule =>
                rule.Id.Equals(editingRuleId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                return;

            saved = existing with
            {
                Name = name,
                AppKey = app.Key,
                Trigger = trigger,
                Action = action,
                Enabled = EnabledCheckBox.IsChecked == true,
                Priority = Math.Clamp(priority, 0, 100)
            };

            int index = updated.FindIndex(rule =>
                rule.Id.Equals(editingRuleId, StringComparison.OrdinalIgnoreCase));
            updated[index] = saved;
        }

        if (TrySave(updated))
        {
            editingRuleId = saved.Id;
            RefreshRules(saved.Id);
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (!canEdit || editingRuleId is null)
            return;

        var updated = rules
            .Where(rule => !rule.Id.Equals(editingRuleId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!TrySave(updated))
            return;

        editingRuleId = null;
        EditorPanel.Visibility = Visibility.Collapsed;
        EditorPlaceholder.Visibility = Visibility.Visible;
        RefreshRules();
    }

    private void DuplicateRule_Click(object sender, RoutedEventArgs e)
    {
        if (!canEdit || editingRuleId is null)
            return;

        RuleDefinition? source = rules.FirstOrDefault(rule =>
            rule.Id.Equals(editingRuleId, StringComparison.OrdinalIgnoreCase));
        if (source is null)
            return;

        RuleDefinition copy = source with
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Copy of " + source.Name
        };

        var updated = rules.Append(copy).ToArray();
        if (TrySave(updated))
        {
            editingRuleId = copy.Id;
            RefreshRules(copy.Id);
        }
    }

    private bool TrySave(IReadOnlyCollection<RuleDefinition> updated)
    {
        try
        {
            ruleStore.Save(updated);
            rules = updated.ToArray();
            RulesStatusBanner.Visibility = canEdit && string.IsNullOrWhiteSpace(unavailableReason)
                ? Visibility.Collapsed
                : RulesStatusBanner.Visibility;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "STOW could not save rules.\n\n" + ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private void PrioritySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PriorityValueText is not null)
            PriorityValueText.Text = ((int)Math.Round(e.NewValue)).ToString();
    }
}

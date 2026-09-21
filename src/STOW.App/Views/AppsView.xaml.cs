using System.Windows;
using System.Windows.Controls;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class AppsView : UserControl
{
    private IReadOnlyList<ManagedAppDefinition> managedApps = Array.Empty<ManagedAppDefinition>();
    private IReadOnlyList<DiscoveredAppSnapshot> availableApps = Array.Empty<DiscoveredAppSnapshot>();

    public AppsView()
    {
        InitializeComponent();
        ApplyFilter();
    }

    public AppsView(
        IEnumerable<ManagedAppDefinition> managed,
        IEnumerable<DiscoveredAppSnapshot> available)
    {
        InitializeComponent();
        managedApps = managed.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var managedKeys = managedApps.Select(app => app.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        availableApps = available
            .Where(app => !managedKeys.Contains(app.Key))
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (ManagedAppsList is null || AvailableAppsList is null ||
            NoManagedApps is null || NoAvailableApps is null || AvailableSummaryText is null)
            return;

        string filter = SearchBox?.Text?.Trim() ?? string.Empty;
        IReadOnlyList<ManagedAppDefinition> visibleManaged = string.IsNullOrEmpty(filter)
            ? managedApps
            : managedApps.Where(app => Matches(filter, app.Name, app.ProcessName, app.TitleHint)).ToArray();
        IReadOnlyList<DiscoveredAppSnapshot> visibleAvailable = string.IsNullOrEmpty(filter)
            ? availableApps
            : availableApps.Where(app => Matches(filter, app.DisplayName, app.WindowTitle, app.ProcessName)).ToArray();

        ManagedAppsList.ItemsSource = visibleManaged;
        AvailableAppsList.ItemsSource = visibleAvailable;
        NoManagedApps.Visibility = managedApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoAvailableApps.Visibility = visibleAvailable.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AvailableSummaryText.Text = visibleAvailable.Count == 1
            ? "1 running desktop app"
            : $"{visibleAvailable.Count} running desktop apps";
    }

    private static bool Matches(string filter, params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value) && value.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }
        return false;
    }
}

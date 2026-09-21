using System.Windows;
using System.Windows.Controls;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class AppsView : UserControl
{
    private IReadOnlyList<DiscoveredAppSnapshot> allApps = Array.Empty<DiscoveredAppSnapshot>();

    public AppsView()
    {
        InitializeComponent();
        ApplyFilter();
    }

    public AppsView(IEnumerable<DiscoveredAppSnapshot> apps)
    {
        InitializeComponent();
        allApps = apps
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (AvailableAppsList is null || AvailableSummaryText is null || NoAvailableApps is null)
            return;

        string filter = SearchBox?.Text?.Trim() ?? string.Empty;
        IReadOnlyList<DiscoveredAppSnapshot> visible = string.IsNullOrEmpty(filter)
            ? allApps
            : allApps.Where(app =>
                (app.DisplayName + " " + app.WindowTitle + " " + app.ProcessName)
                    .Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .ToArray();

        AvailableAppsList.ItemsSource = visible;
        AvailableSummaryText.Text = visible.Count == 1
            ? "1 running desktop app"
            : $"{visible.Count} running desktop apps";
        NoAvailableApps.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

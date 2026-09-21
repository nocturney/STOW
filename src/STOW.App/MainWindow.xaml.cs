using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using STOW.App.Views;
using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;
using STOW.Infrastructure.Migration;
using STOW.Platform.Windows.Discovery;

namespace STOW.App;

public partial class MainWindow : Window
{
    private readonly IAppDiscovery appDiscovery = new Win32AppDiscovery();
    private readonly IManagedAppStore managedAppStore;
    private readonly TrayifyMigrationResult migrationResult;
    private Button? activeButton;

    public MainWindow()
    {
        migrationResult = TrayifyConfigMigrator.ForCurrentUser().MigrateIfNeeded();
        managedAppStore = TextManagedAppStore.ForCurrentUser();
        InitializeComponent();
        activeButton = AppsNavButton;
        PageHost.Content = CreateAppsView();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string destination)
            return;

        if (activeButton is not null)
        {
            activeButton.Background = Brushes.Transparent;
            activeButton.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }

        activeButton = button;
        activeButton.Background = (Brush)FindResource("SelectionBrush");
        activeButton.Foreground = (Brush)FindResource("TextPrimaryBrush");

        (PageTitleText.Text, PageSubtitleText.Text) = destination switch
        {
            "Apps" => ("Apps", "Choose what STOW keeps quietly out of your way."),
            "Rules" => ("Rules", "Automate when and how apps are stowed."),
            "Focus" => ("Focus", "Create a calmer desktop for the task at hand."),
            "Insights" => ("Insights", "Understand local desktop patterns without telemetry."),
            "Settings" => ("Settings", "Adjust STOW behavior and appearance."),
            "About" => ("About", "Version, updates, privacy, diagnostics and project information."),
            _ => (destination, string.Empty)
        };

        PageHost.Content = destination switch
        {
            "Apps" => CreateAppsView(),
            "Rules" => new RulesView(),
            "Focus" => new FocusView(),
            "Insights" => new InsightsView(),
            "Settings" => new SettingsView(),
            "About" => new AboutView(),
            _ => CreateAppsView()
        };
    }

    private AppsView CreateAppsView() => new(LoadManagedApps(), appDiscovery.DiscoverUserFacingApps());

    private IReadOnlyList<ManagedAppDefinition> LoadManagedApps()
    {
        try
        {
            return managedAppStore.Load();
        }
        catch
        {
            return Array.Empty<ManagedAppDefinition>();
        }
    }
}

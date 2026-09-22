using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using STOW.App.Views;
using STOW.Engine.Contracts;
using STOW.Infrastructure.Configuration;
using STOW.Infrastructure.Migration;
using STOW.Platform.Windows.Discovery;
using STOW.Platform.Windows.Runtime;

namespace STOW.App;

public partial class MainWindow : Window
{
    private readonly IAppDiscovery appDiscovery = new Win32AppDiscovery();
    private readonly IManagedAppStore managedAppStore;
    private readonly IRuleStore ruleStore = JsonRuleStore.ForCurrentUser();
    private readonly IActivityStore activityStore = JsonLinesActivityStore.ForCurrentUser();
    private readonly IAppSettingsStore settingsStore = JsonAppSettingsStore.ForCurrentUser();
    private readonly IFocusPresetStore focusPresetStore = JsonFocusPresetStore.ForCurrentUser();
    private readonly TrayifyMigrationResult? migrationResult;
    private ITrayEngineRuntime? trayEngine;
    private string? engineUnavailableReason;
    private Button? activeButton;
    private bool applicationExitRequested;

    public MainWindow()
    {
        bool legacyRunning = new LegacyTrayifyPresence().IsRunning();
        if (legacyRunning)
        {
            migrationResult = null;
            managedAppStore = TextManagedAppStore.ForLegacyTrayifyCurrentUser();
            engineUnavailableReason =
                "Trayify v0.3.3 is currently running. STOW is read-only and reads the live Trayify configuration until the legacy runtime is stopped.";
        }
        else
        {
            migrationResult = TrayifyConfigMigrator.ForCurrentUser().MigrateIfNeeded();
            managedAppStore = TextManagedAppStore.ForCurrentUser();
            ConfigureRuntime();
        }

        InitializeComponent();
        StartRuntimeSafely();
        activeButton = AppsNavButton;
        PageHost.Content = CreateAppsView();
        Closing += MainWindow_Closing;
    }

    private void ConfigureRuntime()
    {
        if (migrationResult is null)
        {
            engineUnavailableReason = "STOW runtime is blocked because migration state is unavailable.";
            return;
        }

        engineUnavailableReason = MigrationBlockReason(migrationResult);
        if (engineUnavailableReason is not null)
            return;

        try
        {
            trayEngine = new LegacyCompatibleTrayEngine(managedAppStore, ruleStore, activityStore, settingsStore);
        }
        catch (Exception ex)
        {
            engineUnavailableReason = "STOW runtime could not be initialized: " + ex.Message;
            trayEngine = null;
        }
    }

    private void StartRuntimeSafely()
    {
        if (trayEngine is null)
            return;

        try
        {
            trayEngine.Start();
        }
        catch (Exception ex)
        {
            try { trayEngine.Dispose(); } catch { }
            trayEngine = null;
            engineUnavailableReason = "STOW runtime could not be started: " + ex.Message;
        }
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
            "Rules" => new RulesView(ruleStore, managedAppStore, trayEngine is not null, engineUnavailableReason),
            "Focus" => new FocusView(trayEngine, settingsStore, focusPresetStore, engineUnavailableReason),
            "Insights" => new InsightsView(activityStore),
            "Settings" => new SettingsView(settingsStore, trayEngine),
            "About" => new AboutView(),
            _ => CreateAppsView()
        };
    }

    private AppsView CreateAppsView() => new(
        trayEngine,
        managedAppStore,
        appDiscovery,
        engineUnavailableReason);

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (applicationExitRequested)
            return;

        bool keepRunning = true;
        try { keepRunning = settingsStore.Load().KeepRunningInTray; }
        catch { keepRunning = true; }

        e.Cancel = true;
        if (keepRunning)
        {
            Hide();
            return;
        }

        _ = RequestApplicationExit();
    }

    public bool RequestApplicationExit()
    {
        if (applicationExitRequested)
            return true;

        if (trayEngine is not null && trayEngine.IsRunning)
        {
            EngineCommandResult result = trayEngine.Shutdown();
            if (!result.Succeeded)
            {
                MessageBox.Show(
                    result.Message ?? "STOW could not safely restore every hidden application, so it will stay running.",
                    "STOW",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        applicationExitRequested = true;
        trayEngine?.Dispose();
        trayEngine = null;
        Close();
        return true;
    }

    private static string? MigrationBlockReason(TrayifyMigrationResult result) => result.Status switch
    {
        TrayifyMigrationStatus.InvalidLegacyData =>
            "Trayify settings could not be validated. STOW is read-only until the legacy configuration can be migrated safely.",
        TrayifyMigrationStatus.Failed =>
            "Trayify settings migration failed. STOW is read-only so the legacy configuration remains untouched.",
        _ => null
    };
}

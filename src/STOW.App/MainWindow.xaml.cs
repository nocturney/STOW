using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using STOW.App.Themes;
using STOW.App.Views;
using STOW.Engine;
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
    private readonly IFocusScheduleStore focusScheduleStore = JsonFocusScheduleStore.ForCurrentUser();
    private readonly IUserNotificationSink? notificationSink;
    private readonly TrayifyMigrationResult? migrationResult;
    private FocusScheduleCoordinator? focusScheduleCoordinator;
    private DispatcherTimer? focusScheduleTimer;
    private ITrayEngineRuntime? trayEngine;
    private string? engineUnavailableReason;
    private Button? activeButton;
    private string currentDestination = "Apps";
    private bool applicationExitRequested;

    public MainWindow(IUserNotificationSink? notificationSink = null)
    {
        this.notificationSink = notificationSink;
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
        ApplyAdaptiveShellLayout();
        SourceInitialized += (_, _) =>
        {
            ApplyNativeWindowAttributes(ThemeManager.Current);
            UpdateWindowFrameState();
        };
        StateChanged += (_, _) => UpdateWindowFrameState();
        ThemeManager.Applied += ThemeManager_Applied;
        Closed += MainWindow_Closed;
        StartRuntimeSafely();
        StartFocusScheduling();
        NavigateTo("Apps");
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

    private void StartFocusScheduling()
    {
        if (trayEngine is null)
            return;

        focusScheduleCoordinator = new FocusScheduleCoordinator(
            trayEngine,
            focusScheduleStore,
            focusPresetStore,
            settingsStore,
            notificationSink);
        focusScheduleCoordinator.Tick(DateTime.Now);

        focusScheduleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        focusScheduleTimer.Tick += (_, _) =>
            focusScheduleCoordinator?.Tick(DateTime.Now);
        focusScheduleTimer.Start();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string destination)
            return;

        NavigateTo(destination);
    }

    private void NavigateTo(string destination)
    {
        Button? nextButton = destination switch
        {
            "Apps" => AppsNavButton,
            "Rules" => RulesNavButton,
            "Focus" => FocusNavButton,
            "Insights" => InsightsNavButton,
            "Settings" => SettingsNavButton,
            "About" => AboutNavButton,
            _ => null
        };

        if (activeButton is not null)
        {
            activeButton.Background = Brushes.Transparent;
            activeButton.Foreground = (Brush)FindResource("TextSecondaryBrush");
            activeButton.BorderBrush = Brushes.Transparent;
            activeButton.BorderThickness = new Thickness(0);
        }

        activeButton = nextButton;
        if (activeButton is not null)
        {
            activeButton.Background = (Brush)FindResource("SelectionBrush");
            activeButton.Foreground = (Brush)FindResource("SelectionForegroundBrush");
            activeButton.BorderBrush = (Brush)FindResource("AccentBlueStrongBrush");
            activeButton.BorderThickness = new Thickness(3, 0, 0, 0);
        }

        currentDestination = destination;
        PageIconBadge.Visibility = string.Equals(destination, "Focus", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

        (PageTitleText.Text, PageSubtitleText.Text) = destination switch
        {
            "Apps" => ("Apps", "Keep what matters in view. Stow the rest."),
            "Rules" => ("Rules", "Automate your desktop with clear conditions and actions."),
            "Focus" => ("Focus", "Block distractions. Make space for what matters."),
            "Insights" => ("Insights", "See local desktop patterns without telemetry."),
            "Settings" => ("Settings", "Customize STOW to fit your workflow."),
            "About" => ("About & Updates", "Version info, updates, privacy and more."),
            _ => (destination, string.Empty)
        };

        ShellSearchBox.Tag = destination switch
        {
            "Apps" => "Search apps...",
            "Rules" => "Search rules...",
            "Settings" => "Search settings...",
            _ => "Search apps, rules, or settings..."
        };
        ShellSearchBox.Text = string.Empty;

        PageHost.Content = destination switch
        {
            "Apps" => CreateAppsView(),
            "Rules" => new RulesView(ruleStore, managedAppStore, trayEngine is not null, engineUnavailableReason),
            "Focus" => new FocusView(
                trayEngine,
                settingsStore,
                focusPresetStore,
                focusScheduleStore,
                focusScheduleCoordinator,
                engineUnavailableReason),
            "Insights" => new InsightsView(activityStore),
            "Settings" => CreateSettingsView(),
            "About" => new AboutView(),
            _ => CreateAppsView()
        };
    }

    private void ShellSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = ShellSearchBox.Text ?? string.Empty;
        switch (PageHost.Content)
        {
            case AppsView apps:
                apps.SetSearchText(query);
                break;
            case RulesView rules:
                rules.SetSearchText(query);
                break;
            case SettingsView settings:
                settings.SetSearchText(query);
                break;
        }
    }

    private void ShellSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        string query = (ShellSearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (query.Length == 0)
            return;

        string? destination =
            query.Contains("rule") ? "Rules" :
            query.Contains("focus") || query.Contains("preset") || query.Contains("schedule") ? "Focus" :
            query.Contains("insight") || query.Contains("activity") ? "Insights" :
            query.Contains("setting") || query.Contains("theme") || query.Contains("access") ||
            query.Contains("startup") || query.Contains("notification") ? "Settings" :
            query.Contains("about") || query.Contains("update") || query.Contains("privacy") ||
            query.Contains("license") || query.Contains("release") ? "About" :
            query.Contains("app") ? "Apps" :
            null;

        if (destination is not null && !string.Equals(destination, currentDestination, StringComparison.Ordinal))
        {
            NavigateTo(destination);
            e.Handled = true;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        Close();

    private AppsView CreateAppsView()
    {
        var view = new AppsView(
            trayEngine,
            managedAppStore,
            appDiscovery,
            engineUnavailableReason);
        view.FocusRequested += (_, _) => NavigateTo("Focus");
        return view;
    }

    private SettingsView CreateSettingsView() =>
        new(settingsStore, trayEngine);

    private void ThemeManager_Applied(STOW.App.Themes.ThemeMode mode) =>
        Dispatcher.BeginInvoke(() =>
        {
            ApplyNativeWindowAttributes(mode);
            ApplyAdaptiveShellLayout();
        });

    private void ApplyAdaptiveShellLayout()
    {
        if (SidebarColumn is null)
            return;

        double width = AccessibilityManager.CurrentTextScalePercent switch
        {
            >= 200 => 286,
            >= 150 => 252,
            _ => 228
        };
        SidebarColumn.Width = new GridLength(width);
    }

    private void ApplyNativeWindowAttributes(STOW.App.Themes.ThemeMode mode)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        int enabled = mode == STOW.App.Themes.ThemeMode.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(
            handle,
            DwmWindowAttributeUseImmersiveDarkMode,
            ref enabled,
            sizeof(int));

        int cornerPreference = DwmWindowCornerPreferenceRound;
        _ = DwmSetWindowAttribute(
            handle,
            DwmWindowAttributeWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));
    }

    private void UpdateWindowFrameState()
    {
        if (WindowFrame is null)
            return;

        bool maximized = WindowState == WindowState.Maximized;
        WindowFrame.CornerRadius = maximized
            ? new CornerRadius(0)
            : new CornerRadius(12);
        WindowFrame.BorderThickness = maximized
            ? new Thickness(0)
            : new Thickness(1);
    }

    private void MainWindow_Closed(object? sender, EventArgs e) =>
        ThemeManager.Applied -= ThemeManager_Applied;

    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
    private const int DwmWindowAttributeWindowCornerPreference = 33;
    private const int DwmWindowCornerPreferenceRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);

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
        focusScheduleTimer?.Stop();
        focusScheduleTimer = null;
        focusScheduleCoordinator = null;
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

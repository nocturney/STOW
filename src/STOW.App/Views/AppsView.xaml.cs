using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using STOW.App.Themes;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class AppsView : UserControl
{
    public event EventHandler? FocusRequested;
    private readonly ITrayEngine? engine;
    private readonly IManagedAppStore fallbackStore;
    private readonly IAppDiscovery appDiscovery;
    private readonly DispatcherTimer refreshTimer;
    private readonly Dictionary<string, ImageSource?> iconCache =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DiscoveredAppSnapshot> lastDiscoveredApps = Array.Empty<DiscoveredAppSnapshot>();
    private string? runtimeUnavailableReason;
    private bool runtimeHealthy;

    private sealed record ManagedRow(
        string Key,
        string Name,
        string ProcessName,
        ImageSource? Icon,
        bool Enabled,
        string StatusText,
        bool CanManage,
        bool CanRestore);

    private sealed record AvailableRow(
        string Key,
        string DisplayName,
        string WindowTitle,
        string ProcessName,
        ImageSource? Icon,
        bool CanAdd);

    public AppsView(
        ITrayEngine? engine,
        IManagedAppStore fallbackStore,
        IAppDiscovery appDiscovery,
        string? runtimeUnavailableReason = null)
    {
        this.engine = engine;
        this.fallbackStore = fallbackStore;
        this.appDiscovery = appDiscovery;
        this.runtimeUnavailableReason = runtimeUnavailableReason;
        runtimeHealthy = engine is not null;
        InitializeComponent();
        ApplyAccessibleLayout();

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        refreshTimer.Tick += (_, _) => RefreshData();
        Loaded += (_, _) => refreshTimer.Start();
        Unloaded += (_, _) => refreshTimer.Stop();
        RefreshData();
    }

    private void ApplyAccessibleLayout()
    {
        bool stackCards = AccessibilityManager.CurrentTextScalePercent >= 150;

        Grid.SetRow(ManagedAppsCard, 0);
        Grid.SetColumn(ManagedAppsCard, 0);
        Grid.SetColumnSpan(ManagedAppsCard, stackCards ? 3 : 1);
        ManagedAppsCard.Margin = new Thickness(0);

        Grid.SetRow(AvailableAppsCard, stackCards ? 1 : 0);
        Grid.SetColumn(AvailableAppsCard, stackCards ? 0 : 2);
        Grid.SetColumnSpan(AvailableAppsCard, stackCards ? 3 : 1);
        AvailableAppsCard.Margin = stackCards
            ? new Thickness(0, 14, 0, 0)
            : new Thickness(0);
    }

    public void SetSearchText(string text)
    {
        string value = text ?? string.Empty;
        if (string.Equals(SearchBox?.Text, value, StringComparison.Ordinal))
            return;

        if (SearchBox is not null)
            SearchBox.Text = value;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshData();

    private void RefreshData()
    {
        if (ManagedAppsList is null || AvailableAppsList is null ||
            NoManagedApps is null || NoAvailableApps is null ||
            ManagedSummaryText is null || AvailableSummaryText is null)
            return;

        IReadOnlyList<ManagedRow> managedRows;
        HashSet<string> managedKeys;

        if (engine is not null && runtimeHealthy)
        {
            try
            {
                EngineSnapshot snapshot = engine.GetSnapshot();
                managedRows = snapshot.ManagedApps.Select(ToManagedRow).ToArray();
                managedKeys = snapshot.ManagedApps.Select(app => app.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                runtimeHealthy = false;
                runtimeUnavailableReason = "STOW runtime is unavailable: " + ex.Message;
                (managedRows, managedKeys) = CreateFallbackManagedRows();
            }
        }
        else
        {
            (managedRows, managedKeys) = CreateFallbackManagedRows();
        }

        bool canManage = engine is not null && runtimeHealthy;
        try
        {
            lastDiscoveredApps = appDiscovery.DiscoverUserFacingApps();
        }
        catch
        {
            lastDiscoveredApps = Array.Empty<DiscoveredAppSnapshot>();
        }

        var availableRows = lastDiscoveredApps
            .Where(app => !managedKeys.Contains(app.Key))
            .Select(app => new AvailableRow(
                app.Key,
                app.DisplayName,
                app.WindowTitle,
                app.ProcessName,
                LoadAppIcon(app.ExecutablePath),
                canManage))
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        string filter = SearchBox?.Text?.Trim() ?? string.Empty;
        IReadOnlyList<ManagedRow> visibleManaged = string.IsNullOrEmpty(filter)
            ? managedRows
            : managedRows.Where(row => Matches(filter, row.Name, row.ProcessName, row.StatusText)).ToArray();
        IReadOnlyList<AvailableRow> visibleAvailable = string.IsNullOrEmpty(filter)
            ? availableRows
            : availableRows.Where(row => Matches(filter, row.DisplayName, row.WindowTitle, row.ProcessName)).ToArray();

        ManagedAppsList.ItemsSource = visibleManaged;
        AvailableAppsList.ItemsSource = visibleAvailable;
        NoManagedApps.Visibility = managedRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoAvailableApps.Visibility = visibleAvailable.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ManagedSummaryText.Text = managedRows.Count == 1 ? "1 managed app" : $"{managedRows.Count} managed apps";
        AvailableSummaryText.Text = visibleAvailable.Count == 1
            ? "1 running desktop app"
            : $"{visibleAvailable.Count} running desktop apps";

        bool showBanner = !canManage && !string.IsNullOrWhiteSpace(runtimeUnavailableReason);
        EngineStatusBanner.Visibility = showBanner ? Visibility.Visible : Visibility.Collapsed;
        EngineStatusText.Text = runtimeUnavailableReason ?? string.Empty;
    }

    private void StartFocus_Click(object sender, RoutedEventArgs e) =>
        FocusRequested?.Invoke(this, EventArgs.Empty);

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKey(sender, out string key) || engine is null || !runtimeHealthy)
            return;

        DiscoveredAppSnapshot? app = lastDiscoveredApps.FirstOrDefault(
            candidate => candidate.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (app is null)
            return;

        HandleResult(engine.AddManagedApp(app));
    }

    private void ManagedEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox ||
            !TryGetKey(sender, out string key) ||
            engine is null || !runtimeHealthy)
            return;

        HandleResult(engine.SetEnabled(key, checkBox.IsChecked == true));
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKey(sender, out string key) || engine is null || !runtimeHealthy)
            return;

        HandleResult(engine.Restore(key));
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetKey(sender, out string key) || engine is null || !runtimeHealthy)
            return;

        HandleResult(engine.RemoveManagedApp(key));
    }

    private void HandleResult(EngineCommandResult result)
    {
        if (!result.Succeeded && result.Status != EngineCommandStatus.AlreadyExists)
        {
            MessageBox.Show(
                result.Message ?? "STOW could not complete the requested action.",
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RefreshData();
    }

    private (IReadOnlyList<ManagedRow> rows, HashSet<string> keys) CreateFallbackManagedRows()
    {
        string enabledStatus = runtimeUnavailableReason?.Contains("Trayify", StringComparison.OrdinalIgnoreCase) == true
            ? "Managed by Trayify"
            : "Configured";

        IReadOnlyList<ManagedAppDefinition> fallbackManaged;
        try
        {
            fallbackManaged = fallbackStore.Load();
        }
        catch
        {
            fallbackManaged = Array.Empty<ManagedAppDefinition>();
        }

        ManagedRow[] rows = fallbackManaged
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(app => new ManagedRow(
                app.Key,
                app.Name,
                app.ProcessName,
                LoadAppIcon(app.ExecutablePath),
                app.Enabled,
                app.Enabled ? enabledStatus : "Off",
                CanManage: false,
                CanRestore: false))
            .ToArray();

        return (rows, rows.Select(row => row.Key).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private ManagedRow ToManagedRow(ManagedAppSnapshot app) => new(
        app.Key,
        app.DisplayName,
        app.ProcessName,
        LoadAppIcon(app.ExecutablePath),
        app.Enabled,
        app.State switch
        {
            ManagedAppRuntimeState.Disabled => "Off",
            ManagedAppRuntimeState.NotRunning => "Not running",
            ManagedAppRuntimeState.Visible => "On",
            ManagedAppRuntimeState.Stowed => "Stowed",
            _ => "Unknown"
        },
        CanManage: runtimeHealthy,
        CanRestore: runtimeHealthy && app.State == ManagedAppRuntimeState.Stowed);

    private ImageSource? LoadAppIcon(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return null;

        if (iconCache.TryGetValue(executablePath, out ImageSource? cached))
            return cached;

        ImageSource? source = null;
        try
        {
            if (File.Exists(executablePath))
            {
                using Icon? icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bitmap.Freeze();
                    source = bitmap;
                }
            }
        }
        catch
        {
            source = null;
        }

        iconCache[executablePath] = source;
        return source;
    }

    private static bool TryGetKey(object sender, out string key)
    {
        key = string.Empty;
        if (sender is not FrameworkElement { Tag: string value } || string.IsNullOrWhiteSpace(value))
            return false;
        key = value;
        return true;
    }

    private static bool Matches(string filter, params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value) &&
                value.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }
        return false;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class FocusView : UserControl
{
    private readonly ITrayEngine? engine;
    private readonly DispatcherTimer refreshTimer;
    private readonly HashSet<string> selectedKeepVisible = new(StringComparer.OrdinalIgnoreCase);
    private string? runtimeUnavailableReason;
    private bool runtimeHealthy;

    private sealed record FocusAppRow(
        string Key,
        string Name,
        string ProcessName,
        bool KeepVisible,
        bool CanEdit);

    public FocusView(ITrayEngine? engine, string? runtimeUnavailableReason = null)
    {
        this.engine = engine;
        this.runtimeUnavailableReason = runtimeUnavailableReason;
        runtimeHealthy = engine is not null;

        InitializeComponent();

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        refreshTimer.Tick += (_, _) => RefreshData();
        Loaded += (_, _) => refreshTimer.Start();
        Unloaded += (_, _) => refreshTimer.Stop();

        RefreshData();
    }

    private void RefreshData()
    {
        if (engine is null || !runtimeHealthy)
        {
            ApplyUnavailableState();
            return;
        }

        try
        {
            EngineSnapshot snapshot = engine.GetSnapshot();
            FocusSessionSnapshot focus = engine.GetFocusSession();
            ManagedAppSnapshot[] enabled = snapshot.ManagedApps
                .Where(app => app.Enabled)
                .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            HashSet<string> enabledKeys = enabled
                .Select(app => app.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (focus.Active)
            {
                selectedKeepVisible.Clear();
                selectedKeepVisible.UnionWith(focus.KeepVisibleAppKeys);
            }
            else
            {
                selectedKeepVisible.RemoveWhere(key => !enabledKeys.Contains(key));
            }

            FocusAppsList.ItemsSource = enabled
                .Select(app => new FocusAppRow(
                    app.Key,
                    app.DisplayName,
                    app.ProcessName,
                    selectedKeepVisible.Contains(app.Key),
                    CanEdit: !focus.Active))
                .ToArray();

            ManagedAppsSummaryText.Text = enabled.Length == 1
                ? "1 enabled app"
                : $"{enabled.Length} enabled apps";
            NoManagedAppsText.Visibility = enabled.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            SessionStatusText.Text = focus.Active ? "Active" : "Inactive";
            StowedCountText.Text = focus.StowedByFocusAppKeys.Count.ToString();
            VisibleCountText.Text = (focus.Active
                ? focus.KeepVisibleAppKeys.Count
                : selectedKeepVisible.Count).ToString();

            FocusActionButton.Content = focus.Active ? "End Focus" : "Start Focus";
            FocusActionButton.IsEnabled = focus.Active || enabled.Length > 0;
            DeepWorkButton.IsEnabled = !focus.Active && enabled.Length > 0;
            KeepAllButton.IsEnabled = !focus.Active && enabled.Length > 0;

            if (string.IsNullOrWhiteSpace(runtimeUnavailableReason))
                FocusStatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            runtimeHealthy = false;
            runtimeUnavailableReason = "STOW Focus runtime is unavailable: " + ex.Message;
            ApplyUnavailableState();
        }
    }

    private void ApplyUnavailableState()
    {
        FocusStatusBanner.Visibility = Visibility.Visible;
        FocusStatusText.Text = runtimeUnavailableReason ??
            "Focus is unavailable because the STOW runtime is not active.";
        SessionStatusText.Text = "Unavailable";
        FocusActionButton.IsEnabled = false;
        DeepWorkButton.IsEnabled = false;
        KeepAllButton.IsEnabled = false;
        FocusAppsList.ItemsSource = Array.Empty<FocusAppRow>();
        ManagedAppsSummaryText.Text = "Runtime unavailable";
        NoManagedAppsText.Visibility = Visibility.Visible;
        StowedCountText.Text = "0";
        VisibleCountText.Text = "0";
    }

    private void KeepVisible_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string key } checkBox ||
            string.IsNullOrWhiteSpace(key))
            return;

        if (checkBox.IsChecked == true)
            selectedKeepVisible.Add(key);
        else
            selectedKeepVisible.Remove(key);

        RefreshData();
    }

    private void DeepWork_Click(object sender, RoutedEventArgs e)
    {
        selectedKeepVisible.Clear();
        RefreshData();
    }

    private void KeepAll_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || !runtimeHealthy)
            return;

        try
        {
            selectedKeepVisible.Clear();
            selectedKeepVisible.UnionWith(
                engine.GetSnapshot().ManagedApps
                    .Where(app => app.Enabled)
                    .Select(app => app.Key));
            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW could not prepare this Focus preset.", ex.Message);
        }
    }

    private void FocusAction_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || !runtimeHealthy)
            return;

        try
        {
            FocusSessionSnapshot focus = engine.GetFocusSession();
            EngineCommandResult result = focus.Active
                ? engine.EndFocusSession()
                : engine.StartFocusSession(selectedKeepVisible.ToArray());

            if (!result.Succeeded && result.Status != EngineCommandStatus.AlreadyExists)
            {
                ShowFailure(
                    focus.Active ? "Focus could not end safely." : "Focus could not start.",
                    result.Message ?? "STOW could not complete the Focus action.");
            }

            RefreshData();
        }
        catch (Exception ex)
        {
            ShowFailure("STOW Focus encountered an error.", ex.Message);
        }
    }

    private static void ShowFailure(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}

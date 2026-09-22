using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using STOW.Engine.Contracts;

namespace STOW.App.Views;

public partial class InsightsView : UserControl
{
    private readonly IActivityStore activityStore;
    private readonly DispatcherTimer refreshTimer;

    private sealed record ActivityRow(
        string TimeText,
        string Title,
        string Detail);

    public InsightsView(IActivityStore activityStore)
    {
        this.activityStore = activityStore;
        InitializeComponent();

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        refreshTimer.Tick += (_, _) => RefreshData();
        Loaded += (_, _) => refreshTimer.Start();
        Unloaded += (_, _) => refreshTimer.Stop();

        RefreshData();
    }

    private void RefreshData()
    {
        IReadOnlyList<ActivityEvent> events;
        try
        {
            events = activityStore.LoadRecent(2000);
            InsightsStatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            events = Array.Empty<ActivityEvent>();
            InsightsStatusBanner.Visibility = Visibility.Visible;
            InsightsStatusText.Text = "Local Insights history could not be read: " + ex.Message;
        }

        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        ActivityEvent[] recentWeek = events
            .Where(activity => activity.TimestampUtc >= cutoff)
            .ToArray();

        StowsCountText.Text = recentWeek.Count(activity =>
            activity.Type == ActivityEventType.AppStowed).ToString();
        RestoresCountText.Text = recentWeek.Count(activity =>
            activity.Type == ActivityEventType.AppRestored).ToString();
        FocusCountText.Text = recentWeek.Count(activity =>
            activity.Type == ActivityEventType.FocusStarted).ToString();

        bool hasHistory = events.Count > 0;
        EmptyHistoryCard.Visibility = hasHistory ? Visibility.Collapsed : Visibility.Visible;
        HistoryContent.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;

        if (!hasHistory)
        {
            MostStowedAppText.Text = "—";
            MostStowedCountText.Text = string.Empty;
            RecentActivityList.ItemsSource = Array.Empty<ActivityRow>();
            RecentCountText.Text = string.Empty;
            HistoryRangeText.Text = string.Empty;
            return;
        }

        var topStowed = events
            .Where(activity =>
                activity.Type == ActivityEventType.AppStowed &&
                !string.IsNullOrWhiteSpace(activity.AppName))
            .GroupBy(activity => activity.AppName!, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();

        MostStowedAppText.Text = topStowed?.Name ?? "No app stows yet";
        MostStowedCountText.Text = topStowed is null
            ? "Focus/history events exist, but no app has been stowed yet."
            : topStowed.Count == 1
                ? "1 local stow event"
                : $"{topStowed.Count} local stow events";

        ActivityEvent first = events.MinBy(activity => activity.TimestampUtc)!;
        HistoryRangeText.Text =
            $"Local history retained since {first.TimestampUtc.ToLocalTime():g}.";

        ActivityRow[] rows = events
            .OrderByDescending(activity => activity.TimestampUtc)
            .Take(12)
            .Select(ToRow)
            .ToArray();

        RecentActivityList.ItemsSource = rows;
        RecentCountText.Text = events.Count == 1
            ? "1 retained event"
            : $"{events.Count} retained events";
    }

    private static ActivityRow ToRow(ActivityEvent activity)
    {
        string app = string.IsNullOrWhiteSpace(activity.AppName)
            ? string.Empty
            : activity.AppName!;

        (string title, string detail) = activity.Type switch
        {
            ActivityEventType.AppStowed => (
                string.IsNullOrEmpty(app) ? "App stowed" : $"{app} stowed",
                SourceDetail(activity.Source, "Moved out of the taskbar.")),
            ActivityEventType.AppRestored => (
                string.IsNullOrEmpty(app) ? "App restored" : $"{app} restored",
                SourceDetail(activity.Source, "Returned to the desktop.")),
            ActivityEventType.FocusStarted => (
                "Focus started",
                "Temporary desktop state activated."),
            ActivityEventType.FocusEnded => (
                "Focus ended",
                "Focus-hidden apps were restored safely."),
            _ => ("STOW activity", activity.Source ?? string.Empty)
        };

        return new ActivityRow(
            activity.TimestampUtc.ToLocalTime().ToString("g"),
            title,
            detail);
    }

    private static string SourceDetail(string? source, string fallback) =>
        string.IsNullOrWhiteSpace(source)
            ? fallback
            : source switch
            {
                "Minimize" => "Triggered by app minimize.",
                "Focus" => "Triggered by Focus.",
                "Manual" => "Restored manually.",
                "Shutdown" => "Restored before STOW exit.",
                "Disable" => "Restored before management was disabled.",
                "Remove" => "Restored before the app was removed from STOW.",
                _ => source
            };
}

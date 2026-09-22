namespace STOW.Engine.Contracts;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum FocusEndBehavior
{
    RestorePreviousDesktop,
    KeepAppsStowed
}

public sealed record AppSettings(
    bool KeepRunningInTray,
    bool StartWithWindows,
    ThemePreference Theme,
    FocusEndBehavior FocusEndBehavior,
    bool NotificationsEnabled = false)
{
    public static AppSettings Default { get; } = new(
        KeepRunningInTray: true,
        StartWithWindows: true,
        Theme: ThemePreference.System,
        FocusEndBehavior: FocusEndBehavior.RestorePreviousDesktop,
        NotificationsEnabled: false);
}

public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

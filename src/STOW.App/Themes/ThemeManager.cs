using Microsoft.Win32;
using System.Windows;

namespace STOW.App.Themes;

public enum ThemeMode
{
    Light,
    Dark
}

public static class ThemeManager
{
    public static ThemeMode Current { get; private set; } = ThemeMode.Light;

    public static void Apply(ThemeMode mode)
    {
        if (Application.Current is null)
            return;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (dictionaries.Count == 0)
            return;

        dictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"Themes/{mode}.xaml", UriKind.Relative)
        };
        Current = mode;
    }

    public static void ApplySystemTheme()
    {
        const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        object? raw = Registry.GetValue(key, "AppsUseLightTheme", 1);
        bool light = raw is int value ? value != 0 : true;
        Apply(light ? ThemeMode.Light : ThemeMode.Dark);
    }
}

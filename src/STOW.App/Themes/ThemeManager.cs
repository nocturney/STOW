using Microsoft.Win32;
using System.Windows;
using STOW.Engine.Contracts;

namespace STOW.App.Themes;

public enum ThemeMode
{
    Light,
    Dark,
    HighContrast
}

public static class ThemeManager
{
    public static event Action<ThemeMode>? Applied;

    public static ThemeMode Current { get; private set; } = ThemeMode.Light;
    public static ThemePreference Preference { get; private set; } = ThemePreference.System;
    public static bool EnhancedContrast { get; private set; }

    public static void ApplyPreference(
        ThemePreference preference,
        bool enhancedContrast = false)
    {
        Preference = preference;
        EnhancedContrast = enhancedContrast;

        if (SystemParameters.HighContrast || enhancedContrast)
        {
            Apply(ThemeMode.HighContrast);
            return;
        }

        switch (preference)
        {
            case ThemePreference.Light:
                Apply(ThemeMode.Light);
                break;
            case ThemePreference.Dark:
                Apply(ThemeMode.Dark);
                break;
            default:
                ApplySystemThemeCore();
                break;
        }
    }

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
        Applied?.Invoke(mode);
    }

    public static void ApplySystemTheme()
    {
        ApplyPreference(ThemePreference.System, EnhancedContrast);
    }

    private static void ApplySystemThemeCore()
    {
        const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        object? raw = Registry.GetValue(key, "AppsUseLightTheme", 1);
        bool light = raw is int value ? value != 0 : true;
        Apply(light ? ThemeMode.Light : ThemeMode.Dark);
    }
}

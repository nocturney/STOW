using System.Windows;
using System.Windows.Media;
using STOW.Engine.Contracts;

namespace STOW.App.Themes;

public static class AccessibilityManager
{
    private static readonly IReadOnlyDictionary<string, double> BaseFontSizes =
        new Dictionary<string, double>
        {
            ["FontSize11"] = 11,
            ["FontSize115"] = 11.5,
            ["FontSize12"] = 12,
            ["FontSize13"] = 13,
            ["FontSize14"] = 14,
            ["FontSize16"] = 16,
            ["FontSize17"] = 17,
            ["FontSize18"] = 18,
            ["FontSize19"] = 19,
            ["FontSize20"] = 20,
            ["FontSize22"] = 22,
            ["FontSize24"] = 24,
            ["FontSize26"] = 26,
            ["FontSize30"] = 30
        };

    public static int CurrentTextScalePercent { get; private set; } = 100;

    public static double Scale(double value) =>
        value * CurrentTextScalePercent / 100d;

    public static void Apply(AppSettings settings)
    {
        if (Application.Current is null)
            return;

        int scalePercent = settings.AccessibilityTextScalePercent switch
        {
            100 or 125 or 150 or 200 => settings.AccessibilityTextScalePercent,
            _ => 100
        };
        CurrentTextScalePercent = scalePercent;

        double scale = scalePercent / 100d;
        foreach ((string key, double baseSize) in BaseFontSizes)
            Application.Current.Resources[key] = baseSize * scale;

        Application.Current.Resources["StowFont"] = settings.UseSystemFont
            ? new FontFamily("Segoe UI Variable Text, Segoe UI")
            : new FontFamily("Inter, Segoe UI Variable Text, Segoe UI");

        ThemeManager.ApplyPreference(settings.Theme, settings.EnhancedContrast);
    }

    public static void RefreshSystemAccessibility(AppSettings settings)
    {
        ThemeManager.ApplyPreference(settings.Theme, settings.EnhancedContrast);
    }
}

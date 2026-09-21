using System.Windows;
using System.Windows.Controls;
using STOW.App.Themes;
using StowThemeMode = STOW.App.Themes.ThemeMode;

namespace STOW.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string mode })
            return;

        if (mode == "System")
            ThemeManager.ApplySystemTheme();
        else if (Enum.TryParse<StowThemeMode>(mode, out StowThemeMode theme))
            ThemeManager.Apply(theme);
    }
}

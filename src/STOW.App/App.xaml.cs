using System.Windows;
using STOW.App.Themes;

namespace STOW.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeManager.ApplySystemTheme();
        base.OnStartup(e);
    }
}

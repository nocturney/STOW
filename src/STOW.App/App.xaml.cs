using System.Windows;
using System.Windows.Threading;
using STOW.App.Themes;
using STOW.App.Views;
using STOW.Infrastructure.Configuration;
using STOW.Infrastructure.Runtime;
using STOW.Platform.Windows.Runtime;

namespace STOW.App;

public partial class App : Application
{
    private SingleInstanceCoordinator? singleInstance;
    private DispatcherTimer? showSignalTimer;
    private MainTrayIconHost? mainTrayIcon;
    private MainWindow? managerWindow;
    private bool shuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ApplyConfiguredTheme();

        bool background = e.Args.Any(arg =>
            string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));

        singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.TryAcquire())
        {
            if (!background)
                _ = singleInstance.SignalExisting();
            singleInstance.Dispose();
            singleInstance = null;
            Shutdown(0);
            return;
        }

        try
        {
            if (!EnsureLegalAcceptance())
            {
                singleInstance.Dispose();
                singleInstance = null;
                Shutdown(0);
                return;
            }

            managerWindow = new MainWindow();
            MainWindow = managerWindow;
            CreateMainTrayIcon();

            showSignalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            showSignalTimer.Tick += (_, _) =>
            {
                if (singleInstance?.ConsumeExitRequest() == true)
                {
                    RequestExit();
                    return;
                }

                if (singleInstance?.ConsumeShowRequest() == true)
                    ShowManager();
            };
            showSignalTimer.Start();

            if (!background)
                ShowManager();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "STOW could not start safely.\n\n" + ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            DisposeShell();
            Shutdown(1);
        }
    }

    private bool EnsureLegalAcceptance()
    {
        var store = LegalAcceptanceStore.ForCurrentUser();
        if (store.IsAccepted())
            return true;

        var window = new LegalAcceptanceWindow();
        bool safeExitRequested = false;
        var exitTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        exitTimer.Tick += (_, _) =>
        {
            if (singleInstance?.ConsumeExitRequest() != true)
                return;

            safeExitRequested = true;
            foreach (Window owned in window.OwnedWindows.OfType<Window>().ToArray())
                owned.Close();
            window.Close();
        };
        exitTimer.Start();

        bool accepted;
        try
        {
            accepted = window.ShowDialog() == true;
        }
        finally
        {
            exitTimer.Stop();
        }

        if (safeExitRequested || !accepted)
            return false;

        try
        {
            store.Accept("in-app");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "STOW could not save your license acceptance safely.\n\n" +
                ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private static void ApplyConfiguredTheme()
    {
        try
        {
            ThemeManager.ApplyPreference(JsonAppSettingsStore.ForCurrentUser().Load().Theme);
        }
        catch
        {
            ThemeManager.ApplySystemTheme();
        }
    }

    private void ShowManager()
    {
        if (managerWindow is null)
            return;

        if (!managerWindow.IsVisible)
            managerWindow.Show();
        if (managerWindow.WindowState == WindowState.Minimized)
            managerWindow.WindowState = WindowState.Normal;
        managerWindow.ShowInTaskbar = true;
        managerWindow.Activate();
        managerWindow.Topmost = true;
        managerWindow.Topmost = false;
        managerWindow.Focus();
    }

    private void CreateMainTrayIcon()
    {
        mainTrayIcon = new MainTrayIconHost(
            open: () => Dispatcher.BeginInvoke(ShowManager),
            exit: () => Dispatcher.BeginInvoke(RequestExit));
    }

    private void RequestExit()
    {
        if (shuttingDown)
            return;

        if (managerWindow is not null && !managerWindow.RequestApplicationExit())
            return;

        shuttingDown = true;
        DisposeShell();
        Shutdown(0);
    }

    private void DisposeShell()
    {
        showSignalTimer?.Stop();
        showSignalTimer = null;

        mainTrayIcon?.Dispose();
        mainTrayIcon = null;

        singleInstance?.Dispose();
        singleInstance = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeShell();
        base.OnExit(e);
    }
}

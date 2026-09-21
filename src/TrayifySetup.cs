using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Trayify Setup")]
[assembly: AssemblyDescription("Installer and uninstaller for Trayify")]
[assembly: AssemblyCompany("Christian Velvet")]
[assembly: AssemblyProduct("Trayify")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Christian Velvet")]
[assembly: AssemblyVersion("0.3.1.0")]
[assembly: AssemblyFileVersion("0.3.1.0")]

internal static class SetupConstants
{
    public const string Version = "0.3.1";
    public const string Publisher = "Christian Velvet";
    public const string ProductName = "Trayify";
    public const string RepoUrl = "https://github.com/nocturney/trayify";
    public const string PayloadResource = "Trayify.Payload.exe";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Trayify";
    public const string InstallerKey = @"Software\Trayify\Installer";
}

internal sealed class SetupOptions
{
    public bool Silent;
    public bool Uninstall;
    public bool DesktopShortcut;
    public bool StartMenuShortcut = true;
    public bool PinAssist;
    public bool LaunchAfterInstall = true;
    public bool RemoveSettings;
}

internal static class SetupUtil
{
    public static string InstallDir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Trayify"); }
    }

    public static string AppExe
    {
        get { return Path.Combine(InstallDir, "Trayify.exe"); }
    }

    public static string UninstallExe
    {
        get { return Path.Combine(InstallDir, "Uninstall.exe"); }
    }

    public static string StartMenuDir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Trayify"); }
    }

    public static string DesktopShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Trayify.lnk"); }
    }

    public static void StopTrayify()
    {
        foreach (Process p in Process.GetProcessesByName("Trayify"))
        {
            try
            {
                if (p.Id == Process.GetCurrentProcess().Id) continue;
                try { p.CloseMainWindow(); } catch { }
                if (!p.WaitForExit(1200))
                {
                    try { p.Kill(); } catch { }
                    try { p.WaitForExit(1200); } catch { }
                }
            }
            catch { }
            finally { try { p.Dispose(); } catch { } }
        }
    }

    public static void ExtractPayload(string target)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        using (Stream input = asm.GetManifestResourceStream(SetupConstants.PayloadResource))
        {
            if (input == null) throw new Exception("The Trayify payload is missing from this installer.");
            using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
        }
    }

    public static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) throw new Exception("Windows Script Host is not available.");

        object shell = Activator.CreateInstance(shellType);
        object shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            Type t = shortcut.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            t.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments ?? "" });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null && System.Runtime.InteropServices.Marshal.IsComObject(shortcut))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    public static bool ReadInstallerBool(string name, bool fallback)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SetupConstants.InstallerKey))
            {
                if (key == null) return fallback;
                object value = key.GetValue(name);
                if (value == null) return fallback;
                return Convert.ToInt32(value) != 0;
            }
        }
        catch { return fallback; }
    }

    public static void SaveInstallerOptions(SetupOptions o)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SetupConstants.InstallerKey))
        {
            key.SetValue("DesktopShortcut", o.DesktopShortcut ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("StartMenuShortcut", o.StartMenuShortcut ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    public static void RegisterUninstall()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SetupConstants.UninstallKey))
        {
            FileInfo exe = new FileInfo(AppExe);
            int estimatedKb = (int)Math.Max(1, exe.Length / 1024);
            key.SetValue("DisplayName", SetupConstants.ProductName);
            key.SetValue("DisplayVersion", SetupConstants.Version);
            key.SetValue("Publisher", SetupConstants.Publisher);
            key.SetValue("DisplayIcon", AppExe);
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("UninstallString", "\"" + UninstallExe + "\" /UNINSTALL");
            key.SetValue("QuietUninstallString", "\"" + UninstallExe + "\" /UNINSTALL /VERYSILENT");
            key.SetValue("URLInfoAbout", SetupConstants.RepoUrl);
            key.SetValue("HelpLink", SetupConstants.RepoUrl + "/issues");
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("EstimatedSize", estimatedKb, RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    public static void ConfigureShortcuts(SetupOptions o)
    {
        if (o.StartMenuShortcut)
        {
            CreateShortcut(Path.Combine(StartMenuDir, "Trayify.lnk"), AppExe, "", "Open Trayify");
            CreateShortcut(Path.Combine(StartMenuDir, "Uninstall Trayify.lnk"), UninstallExe, "/UNINSTALL", "Uninstall Trayify");
        }
        else
        {
            try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch { }
        }

        if (o.DesktopShortcut)
            CreateShortcut(DesktopShortcut, AppExe, "", "Open Trayify");
        else
            try { if (File.Exists(DesktopShortcut)) File.Delete(DesktopShortcut); } catch { }
    }

    public static void Install(SetupOptions o)
    {
        StopTrayify();
        Directory.CreateDirectory(InstallDir);

        string staged = Path.Combine(InstallDir, "Trayify.exe.new");
        ExtractPayload(staged);

        string payloadVersion = FileVersionInfo.GetVersionInfo(staged).FileVersion;
        if (!String.Equals(payloadVersion, SetupConstants.Version + ".0", StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(staged); } catch { }
            throw new Exception("Installer payload version does not match setup version.");
        }

        if (File.Exists(AppExe)) File.Delete(AppExe);
        File.Move(staged, AppExe);

        string currentSetup = Application.ExecutablePath;
        if (!String.Equals(currentSetup, UninstallExe, StringComparison.OrdinalIgnoreCase))
            File.Copy(currentSetup, UninstallExe, true);

        SaveInstallerOptions(o);
        ConfigureShortcuts(o);
        RegisterUninstall();

        if (o.LaunchAfterInstall)
        {
            try { Process.Start(new ProcessStartInfo { FileName = AppExe, UseShellExecute = true }); } catch { }
        }
    }

    public static void OpenPinAssist()
    {
        try
        {
            MessageBox.Show(
                "Windows does not expose a supported installer API for pinning a desktop app to Start.\n\n" +
                "Trayify is now available in Start > All apps. In the window that opens, right-click Trayify and choose 'Pin to Start'.",
                "Pin Trayify to Start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:AppsFolder",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void RemoveStartup()
    {
        try
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                if (run != null) run.DeleteValue("Trayify", false);
        }
        catch { }
    }

    public static void Uninstall(SetupOptions o)
    {
        StopTrayify();
        RemoveStartup();

        try { if (File.Exists(DesktopShortcut)) File.Delete(DesktopShortcut); } catch { }
        try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch { }

        try { Registry.CurrentUser.DeleteSubKeyTree(SetupConstants.UninstallKey, false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(SetupConstants.InstallerKey, false); } catch { }

        if (o.RemoveSettings)
        {
            try
            {
                string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trayify");
                if (Directory.Exists(settings)) Directory.Delete(settings, true);
            }
            catch { }
        }

        string cleanup = Path.Combine(Path.GetTempPath(), "Trayify-uninstall-" + Guid.NewGuid().ToString("N") + ".cmd");
        File.WriteAllText(cleanup,
            "@echo off\r\n" +
            "setlocal\r\n" +
            "ping 127.0.0.1 -n 3 >nul\r\n" +
            "rmdir /s /q \"" + InstallDir + "\"\r\n" +
            "del /q \"%~f0\"\r\n",
            Encoding.ASCII);

        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "cmd.exe";
        psi.Arguments = "/c \"" + cleanup + "\"";
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        Process.Start(psi);
    }
}

internal sealed class SetupForm : Form
{
    private readonly SetupOptions options;
    private readonly CheckBox desktop;
    private readonly CheckBox startMenu;
    private readonly CheckBox pinAssist;
    private readonly CheckBox launch;
    private readonly Button installButton;
    private readonly Label status;

    public SetupForm(SetupOptions o)
    {
        options = o;
        Font = new Font("Segoe UI", 9.5f);
        Text = "Install Trayify";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(650, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 249, 251);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Panel header = new Panel();
        header.Dock = DockStyle.Top;
        header.Height = 118;
        header.BackColor = Color.FromArgb(32, 39, 52);

        PictureBox logo = new PictureBox();
        logo.Image = Icon.ToBitmap();
        logo.SizeMode = PictureBoxSizeMode.Zoom;
        logo.SetBounds(26, 24, 64, 64);
        header.Controls.Add(logo);

        Label title = new Label();
        title.Text = "Install Trayify";
        title.ForeColor = Color.White;
        title.Font = new Font("Segoe UI", 19f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(108, 25);
        header.Controls.Add(title);

        Label subtitle = new Label();
        subtitle.Text = "Minimize any desktop app to the system tray.";
        subtitle.ForeColor = Color.FromArgb(205, 213, 226);
        subtitle.Font = new Font("Segoe UI", 10f);
        subtitle.AutoSize = true;
        subtitle.Location = new Point(111, 68);
        header.Controls.Add(subtitle);

        Controls.Add(header);

        Label destinationLabel = new Label();
        destinationLabel.Text = "Install location";
        destinationLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        destinationLabel.AutoSize = true;
        destinationLabel.Location = new Point(30, 145);
        Controls.Add(destinationLabel);

        TextBox destination = new TextBox();
        destination.ReadOnly = true;
        destination.Text = SetupUtil.InstallDir;
        destination.BackColor = Color.White;
        destination.BorderStyle = BorderStyle.FixedSingle;
        destination.SetBounds(30, 168, 590, 28);
        Controls.Add(destination);

        desktop = NewCheck("Create a shortcut on the desktop", 30, 222);
        startMenu = NewCheck("Add Trayify to the Start menu", 30, 258);
        pinAssist = NewCheck("Help me pin Trayify to Start after installation", 30, 294);
        launch = NewCheck("Launch Trayify when setup finishes", 30, 330);

        desktop.Checked = options.DesktopShortcut;
        startMenu.Checked = options.StartMenuShortcut;
        launch.Checked = options.LaunchAfterInstall;

        Controls.Add(desktop);
        Controls.Add(startMenu);
        Controls.Add(pinAssist);
        Controls.Add(launch);

        Label note = new Label();
        note.Text = "Per-user installation · No administrator permission required";
        note.ForeColor = Color.FromArgb(95, 101, 112);
        note.AutoSize = true;
        note.Location = new Point(30, 374);
        Controls.Add(note);

        installButton = new Button();
        installButton.Text = "Install";
        installButton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        installButton.FlatStyle = FlatStyle.Flat;
        installButton.FlatAppearance.BorderSize = 0;
        installButton.BackColor = Color.FromArgb(0, 103, 192);
        installButton.ForeColor = Color.White;
        installButton.SetBounds(500, 410, 120, 36);
        installButton.Click += InstallClick;
        Controls.Add(installButton);

        Button cancel = new Button();
        cancel.Text = "Cancel";
        cancel.FlatStyle = FlatStyle.Flat;
        cancel.SetBounds(372, 410, 116, 36);
        cancel.Click += delegate { Close(); };
        Controls.Add(cancel);

        status = new Label();
        status.AutoSize = true;
        status.Location = new Point(30, 420);
        status.ForeColor = Color.FromArgb(95, 101, 112);
        Controls.Add(status);
    }

    private CheckBox NewCheck(string text, int x, int y)
    {
        CheckBox cb = new CheckBox();
        cb.Text = text;
        cb.AutoSize = true;
        cb.Location = new Point(x, y);
        return cb;
    }

    private void InstallClick(object sender, EventArgs e)
    {
        installButton.Enabled = false;
        status.Text = "Installing...";

        try
        {
            options.DesktopShortcut = desktop.Checked;
            options.StartMenuShortcut = startMenu.Checked;
            options.PinAssist = pinAssist.Checked;
            options.LaunchAfterInstall = launch.Checked;

            SetupUtil.Install(options);
            status.Text = "Installation complete.";

            if (options.PinAssist) SetupUtil.OpenPinAssist();

            MessageBox.Show(
                "Trayify " + SetupConstants.Version + " was installed successfully.",
                "Trayify Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            status.Text = "Installation failed.";
            MessageBox.Show(ex.Message, "Trayify Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            installButton.Enabled = true;
        }
    }
}

internal sealed class UninstallForm : Form
{
    private readonly SetupOptions options;
    private readonly CheckBox removeSettings;

    public UninstallForm(SetupOptions o)
    {
        options = o;
        Font = new Font("Segoe UI", 9.5f);
        Text = "Uninstall Trayify";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 249, 251);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Label title = new Label();
        title.Text = "Uninstall Trayify?";
        title.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(28, 28);
        Controls.Add(title);

        Label body = new Label();
        body.Text = "This removes Trayify, its Start menu/desktop shortcuts,\nand its Windows startup entry.";
        body.AutoSize = true;
        body.ForeColor = Color.FromArgb(75, 80, 90);
        body.Location = new Point(31, 75);
        Controls.Add(body);

        removeSettings = new CheckBox();
        removeSettings.Text = "Also remove my Trayify settings";
        removeSettings.AutoSize = true;
        removeSettings.Location = new Point(32, 135);
        Controls.Add(removeSettings);

        Label settingsNote = new Label();
        settingsNote.Text = "Leave this unchecked if you may reinstall Trayify later.";
        settingsNote.AutoSize = true;
        settingsNote.ForeColor = Color.FromArgb(105, 110, 120);
        settingsNote.Location = new Point(53, 160);
        Controls.Add(settingsNote);

        Button uninstall = new Button();
        uninstall.Text = "Uninstall";
        uninstall.FlatStyle = FlatStyle.Flat;
        uninstall.FlatAppearance.BorderSize = 0;
        uninstall.BackColor = Color.FromArgb(196, 43, 28);
        uninstall.ForeColor = Color.White;
        uninstall.SetBounds(386, 225, 122, 36);
        uninstall.Click += delegate
        {
            try
            {
                options.RemoveSettings = removeSettings.Checked;
                SetupUtil.Uninstall(options);
                MessageBox.Show("Trayify has been removed.", "Trayify", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Trayify Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Controls.Add(uninstall);

        Button cancel = new Button();
        cancel.Text = "Cancel";
        cancel.FlatStyle = FlatStyle.Flat;
        cancel.SetBounds(256, 225, 118, 36);
        cancel.Click += delegate { Close(); };
        Controls.Add(cancel);
    }
}

internal static class SetupProgram
{
    private static SetupOptions Parse(string[] args)
    {
        SetupOptions o = new SetupOptions();
        bool hadExisting = File.Exists(SetupUtil.AppExe);
        o.DesktopShortcut = SetupUtil.ReadInstallerBool("DesktopShortcut", false);
        o.StartMenuShortcut = SetupUtil.ReadInstallerBool("StartMenuShortcut", true);
        o.LaunchAfterInstall = !hadExisting;

        foreach (string raw in args)
        {
            string a = raw.Trim();
            if (String.Equals(a, "/SILENT", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(a, "/VERYSILENT", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase))
                o.Silent = true;
            else if (String.Equals(a, "/UNINSTALL", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase))
                o.Uninstall = true;
            else if (String.Equals(a, "/NOLAUNCH", StringComparison.OrdinalIgnoreCase))
                o.LaunchAfterInstall = false;
            else if (String.Equals(a, "/LAUNCH", StringComparison.OrdinalIgnoreCase))
                o.LaunchAfterInstall = true;
            else if (String.Equals(a, "/DESKTOP=1", StringComparison.OrdinalIgnoreCase))
                o.DesktopShortcut = true;
            else if (String.Equals(a, "/DESKTOP=0", StringComparison.OrdinalIgnoreCase))
                o.DesktopShortcut = false;
            else if (String.Equals(a, "/STARTMENU=1", StringComparison.OrdinalIgnoreCase))
                o.StartMenuShortcut = true;
            else if (String.Equals(a, "/STARTMENU=0", StringComparison.OrdinalIgnoreCase))
                o.StartMenuShortcut = false;
            else if (String.Equals(a, "/PINSTART=1", StringComparison.OrdinalIgnoreCase))
                o.PinAssist = true;
            else if (String.Equals(a, "/REMOVESETTINGS=1", StringComparison.OrdinalIgnoreCase))
                o.RemoveSettings = true;
        }
        return o;
    }

    [STAThread]
    public static void Main(string[] args)
    {
        SetupOptions o = Parse(args);

        if (o.Silent)
        {
            try
            {
                if (o.Uninstall) SetupUtil.Uninstall(o);
                else SetupUtil.Install(o);
                Environment.ExitCode = 0;
            }
            catch
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (o.Uninstall) Application.Run(new UninstallForm(o));
        else Application.Run(new SetupForm(o));
    }
}

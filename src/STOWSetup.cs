using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("STOW Setup")]
[assembly: AssemblyDescription("Installer and uninstaller for STOW")]
[assembly: AssemblyCompany("Christian Velvet")]
[assembly: AssemblyProduct("STOW")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Christian Velvet")]
[assembly: AssemblyVersion("0.4.0.0")]
[assembly: AssemblyFileVersion("0.4.0.0")]
[assembly: AssemblyInformationalVersion("0.4.0-preview.5")]

internal static class SetupConstants
{
    public const string Version = "0.4.0-preview.5";
    public const string FileVersion = "0.4.0.0";
    public const string Publisher = "Christian Velvet";
    public const string ProductName = "STOW";
    public const string LegalTermsRevision = "2026-09-22-v1";
    public const string RepoUrl = "https://github.com/nocturney/STOW";
    public const string PayloadResource = "STOW.Payload.exe";
    public const string LegalResource = "STOW.Legal.zip";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\STOW";
    public const string InstallerKey = @"Software\STOW\Installer";
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
    public bool AcceptLicenses;
    public bool ExistingInstall;
}

internal static class SetupUtil
{
    public static void Log(string message)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "STOWSetup.log");
            File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }

    public static string InstallDir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STOW"); }
    }

    public static string AppExe
    {
        get { return Path.Combine(InstallDir, "STOW.exe"); }
    }

    public static string LegalDir
    {
        get { return Path.Combine(InstallDir, "legal"); }
    }

    public static string LegalAcceptancePath
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "STOW",
                "legal-acceptance.txt");
        }
    }

    public static string UninstallExe
    {
        get { return Path.Combine(InstallDir, "Uninstall.exe"); }
    }

    public static string StartMenuDir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "STOW"); }
    }

    public static string DesktopShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "STOW.lnk"); }
    }

    public static void StopSTOWSafely()
    {
        Process[] processes = Process.GetProcessesByName("STOW");
        try
        {
            bool hasOther = false;
            foreach (Process p in processes)
                if (p.Id != Process.GetCurrentProcess().Id) hasOther = true;

            if (!hasOther) return;

            try
            {
                using (EventWaitHandle exitEvent = EventWaitHandle.OpenExisting(@"Local\STOWRequestExit"))
                    exitEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                throw new Exception("STOW is running but does not expose the safe-exit signal. Exit STOW from its tray menu, then run setup again.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new Exception("Setup could not request a safe STOW shutdown. Exit STOW from its tray menu, then run setup again.");
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            foreach (Process p in processes)
            {
                if (p.Id == Process.GetCurrentProcess().Id) continue;
                int remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                try { if (!p.WaitForExit(remaining)) throw new Exception("STOW refused to exit because a hidden app could not be restored safely. Resolve that app and run setup again."); }
                catch (InvalidOperationException) { }
            }
        }
        finally
        {
            foreach (Process p in processes) try { p.Dispose(); } catch { }
        }
    }

    public static void EnsureLegacyTrayifyStopped()
    {
        Process[] processes = Process.GetProcessesByName("Trayify");
        try
        {
            if (processes.Length > 0)
                throw new Exception("Trayify is still running. Exit Trayify from its tray menu so it can restore any hidden apps, then run STOW Setup again.");
        }
        finally
        {
            foreach (Process p in processes) try { p.Dispose(); } catch { }
        }
    }

    public static void ExtractPayload(string target)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        using (Stream input = asm.GetManifestResourceStream(SetupConstants.PayloadResource))
        {
            if (input == null) throw new Exception("The STOW payload is missing from this installer.");
            using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
        }
    }

    public static void ExtractLegalBundle()
    {
        ExtractLegalBundleTo(LegalDir);
    }

    public static void ExtractLegalBundleTo(string targetDirectory)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        using (Stream input = asm.GetManifestResourceStream(SetupConstants.LegalResource))
        {
            if (input == null) throw new Exception("The STOW legal bundle is missing from this installer.");

            if (Directory.Exists(targetDirectory))
                Directory.Delete(targetDirectory, true);
            Directory.CreateDirectory(targetDirectory);

            string legalRoot = Path.GetFullPath(targetDirectory);
            if (!legalRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                legalRoot += Path.DirectorySeparatorChar;

            using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (String.IsNullOrWhiteSpace(entry.Name))
                        continue;

                    string destination = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
                    if (!destination.StartsWith(legalRoot, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("The embedded legal bundle contains an invalid path.");

                    string parent = Path.GetDirectoryName(destination);
                    if (!String.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);

                    entry.ExtractToFile(destination);
                }
            }
        }
    }

    public static void OpenLegalTerms()
    {
        string preview = Path.Combine(
            Path.GetTempPath(),
            "STOW",
            "legal-" + SetupConstants.Version.Replace(Path.DirectorySeparatorChar, '_'));

        ExtractLegalBundleTo(preview);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "\"" + preview + "\"",
            UseShellExecute = true
        });
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

    public static bool HasRecordedLegalAcceptance()
    {
        try
        {
            if (!File.Exists(LegalAcceptancePath))
                return false;

            foreach (string line in File.ReadAllLines(LegalAcceptancePath))
            {
                if (String.Equals(
                    line.Trim(),
                    "REVISION=" + SetupConstants.LegalTermsRevision,
                    StringComparison.Ordinal))
                    return true;
            }
        }
        catch { }

        return false;
    }

    public static void RecordLegalAcceptance(string source)
    {
        string directory = Path.GetDirectoryName(LegalAcceptancePath);
        if (String.IsNullOrWhiteSpace(directory))
            throw new Exception("Could not resolve the STOW legal acceptance directory.");

        Directory.CreateDirectory(directory);
        string temp = LegalAcceptancePath + ".writing-" + Guid.NewGuid().ToString("N");

        try
        {
            File.WriteAllLines(
                temp,
                new[]
                {
                    "REVISION=" + SetupConstants.LegalTermsRevision,
                    "ACCEPTED_AT_UTC=" + DateTime.UtcNow.ToString("O"),
                    "SOURCE=" + (source ?? "installer").Replace("\r", String.Empty).Replace("\n", String.Empty)
                },
                Encoding.UTF8);

            if (File.Exists(LegalAcceptancePath))
            {
                File.Replace(temp, LegalAcceptancePath, null);
            }
            else
            {
                File.Move(temp, LegalAcceptancePath);
            }
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public static void SaveInstallerOptions(SetupOptions o)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SetupConstants.InstallerKey))
        {
            key.SetValue("DesktopShortcut", o.DesktopShortcut ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("StartMenuShortcut", o.StartMenuShortcut ? 1 : 0, RegistryValueKind.DWord);
            if (o.AcceptLicenses)
            {
                key.SetValue("AcceptedLicensesVersion", SetupConstants.Version, RegistryValueKind.String);
                key.SetValue("AcceptedLicensesAtUtc", DateTime.UtcNow.ToString("O"), RegistryValueKind.String);
            }
        }

        if (o.AcceptLicenses)
            RecordLegalAcceptance("installer");
    }

    public static void RegisterUninstall()
    {
        Log("RegisterUninstall begin: " + SetupConstants.UninstallKey);
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SetupConstants.UninstallKey))
        {
            if (key == null) throw new Exception("Could not create the STOW uninstall registry key.");
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
        Log("RegisterUninstall complete");
    }

    public static void ConfigureShortcuts(SetupOptions o)
    {
        if (o.StartMenuShortcut)
        {
            CreateShortcut(Path.Combine(StartMenuDir, "STOW.lnk"), AppExe, "", "Open STOW");
            CreateShortcut(Path.Combine(StartMenuDir, "Uninstall STOW.lnk"), UninstallExe, "/UNINSTALL", "Uninstall STOW");
        }
        else
        {
            try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch { }
        }

        if (o.DesktopShortcut)
            CreateShortcut(DesktopShortcut, AppExe, "", "Open STOW");
        else
            try { if (File.Exists(DesktopShortcut)) File.Delete(DesktopShortcut); } catch { }
    }

    public static void Install(SetupOptions o)
    {
        if (!o.ExistingInstall && !o.AcceptLicenses && !HasRecordedLegalAcceptance())
            throw new Exception(
                "First-time installation requires acceptance of STOW's MIT License and applicable third-party terms. " +
                "For silent installation, add /ACCEPTLICENSES=1.");

        EnsureLegacyTrayifyStopped();
        StopSTOWSafely();
        Directory.CreateDirectory(InstallDir);

        string staged = Path.Combine(InstallDir, "STOW.exe.new");
        string backup = Path.Combine(InstallDir, "STOW.exe.rollback");
        try { if (File.Exists(staged)) File.Delete(staged); } catch { }
        try { if (File.Exists(backup)) File.Delete(backup); } catch { }
        ExtractPayload(staged);

        string payloadVersion = FileVersionInfo.GetVersionInfo(staged).FileVersion;
        if (!String.Equals(payloadVersion, SetupConstants.FileVersion, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(staged); } catch { }
            throw new Exception("Installer payload version does not match setup version.");
        }

        if (File.Exists(AppExe))
        {
            File.Replace(staged, AppExe, backup, true);
            try { File.Delete(backup); } catch { }
        }
        else
        {
            File.Move(staged, AppExe);
        }

        string currentSetup = Application.ExecutablePath;
        if (!String.Equals(currentSetup, UninstallExe, StringComparison.OrdinalIgnoreCase))
            File.Copy(currentSetup, UninstallExe, true);

        ExtractLegalBundle();

        SaveInstallerOptions(o);
        ConfigureShortcuts(o);
        RegisterUninstall();
        ConfigureStartupFromManagedApps();

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
                "STOW is now available in Start > All apps. In the window that opens, right-click STOW and choose 'Pin to Start'.",
                "Pin STOW to Start",
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

    public static void ConfigureStartupFromManagedApps()
    {
        try
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string stowConfig = Path.Combine(roaming, "STOW", "config.txt");
            string legacyConfig = Path.Combine(roaming, "Trayify", "config.txt");
            string sourceConfig = File.Exists(stowConfig) ? stowConfig : legacyConfig;
            bool shouldStart = false;

            if (File.Exists(sourceConfig))
            {
                foreach (string raw in File.ReadAllLines(sourceConfig, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("1|", StringComparison.Ordinal))
                    {
                        shouldStart = true;
                        break;
                    }
                }
            }

            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (run == null) return;
                object legacy = run.GetValue("Trayify");
                if (shouldStart)
                    run.SetValue("STOW", "\"" + AppExe + "\" --background", RegistryValueKind.String);
                else
                    run.DeleteValue("STOW", false);
                run.DeleteValue("Trayify", false);

                if (legacy != null)
                {
                    try
                    {
                        string settings = Path.Combine(roaming, "STOW");
                        Directory.CreateDirectory(settings);
                        File.WriteAllText(
                            Path.Combine(settings, "installer-startup-migration.txt"),
                            "migration=trayify-startup-v1" + Environment.NewLine +
                            "legacyTrayifyRun=" + Convert.ToString(legacy) + Environment.NewLine +
                            "stowRun=" + (shouldStart ? "\"" + AppExe + "\" --background" : "(disabled)") + Environment.NewLine +
                            "migratedUtc=" + DateTime.UtcNow.ToString("O") + Environment.NewLine,
                            Encoding.UTF8);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public static void RemoveStartup()
    {
        try
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                if (run != null) run.DeleteValue("STOW", false);
        }
        catch { }
    }

    public static void Uninstall(SetupOptions o)
    {
        StopSTOWSafely();
        RemoveStartup();

        try { if (File.Exists(DesktopShortcut)) File.Delete(DesktopShortcut); } catch { }
        try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch { }

        try { Registry.CurrentUser.DeleteSubKeyTree(SetupConstants.UninstallKey, false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(SetupConstants.InstallerKey, false); } catch { }

        if (o.RemoveSettings)
        {
            try
            {
                string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STOW");
                if (Directory.Exists(settings)) Directory.Delete(settings, true);
            }
            catch { }
        }

        string cleanup = Path.Combine(Path.GetTempPath(), "STOW-uninstall-" + Guid.NewGuid().ToString("N") + ".cmd");
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
    private readonly CheckBox acceptLicenses;
    private readonly Button installButton;
    private readonly Label status;

    public SetupForm(SetupOptions o)
    {
        options = o;
        Font = new Font("Segoe UI", 9.5f);
        Text = "Install STOW";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(650, 585);
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
        title.Text = "Install STOW";
        title.ForeColor = Color.White;
        title.Font = new Font("Segoe UI", 19f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(108, 25);
        header.Controls.Add(title);

        Label subtitle = new Label();
        subtitle.Text = "A calmer desktop starts here.";
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
        startMenu = NewCheck("Add STOW to the Start menu", 30, 258);
        pinAssist = NewCheck("Help me pin STOW to Start after installation", 30, 294);
        launch = NewCheck("Launch STOW when setup finishes", 30, 330);

        desktop.Checked = options.DesktopShortcut;
        startMenu.Checked = options.StartMenuShortcut;
        launch.Checked = options.LaunchAfterInstall;

        Controls.Add(desktop);
        Controls.Add(startMenu);
        Controls.Add(pinAssist);
        Controls.Add(launch);

        acceptLicenses = NewCheck(
            "I agree to STOW's MIT License and the applicable third-party terms.",
            30,
            378);
        acceptLicenses.CheckedChanged += delegate { installButton.Enabled = acceptLicenses.Checked; };
        Controls.Add(acceptLicenses);

        Button reviewTerms = new Button();
        reviewTerms.Text = "Review licenses & notices";
        reviewTerms.FlatStyle = FlatStyle.Flat;
        reviewTerms.SetBounds(30, 410, 210, 30);
        reviewTerms.Click += delegate
        {
            try { SetupUtil.OpenLegalTerms(); }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open the bundled legal terms.\n\n" + ex.Message,
                    "STOW Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };
        Controls.Add(reviewTerms);

        Label legalNote = new Label();
        legalNote.Text = "STOW code is MIT licensed. Bundled Microsoft/.NET components retain their own terms.";
        legalNote.ForeColor = Color.FromArgb(95, 101, 112);
        legalNote.AutoSize = true;
        legalNote.Location = new Point(30, 450);
        Controls.Add(legalNote);

        Label note = new Label();
        note.Text = "Per-user installation · No administrator permission required";
        note.ForeColor = Color.FromArgb(95, 101, 112);
        note.AutoSize = true;
        note.Location = new Point(30, 478);
        Controls.Add(note);

        installButton = new Button();
        installButton.Text = "Install";
        installButton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        installButton.FlatStyle = FlatStyle.Flat;
        installButton.FlatAppearance.BorderSize = 0;
        installButton.BackColor = Color.FromArgb(0, 103, 192);
        installButton.ForeColor = Color.White;
        installButton.Enabled = false;
        bool alreadyAccepted = SetupUtil.HasRecordedLegalAcceptance();
        acceptLicenses.Checked = alreadyAccepted;
        installButton.Enabled = alreadyAccepted;
        installButton.SetBounds(500, 520, 120, 36);
        installButton.Click += InstallClick;
        Controls.Add(installButton);

        Button cancel = new Button();
        cancel.Text = "Cancel";
        cancel.FlatStyle = FlatStyle.Flat;
        cancel.SetBounds(372, 520, 116, 36);
        cancel.Click += delegate { Close(); };
        Controls.Add(cancel);

        status = new Label();
        status.AutoSize = true;
        status.Location = new Point(30, 530);
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
            options.AcceptLicenses = acceptLicenses.Checked;

            SetupUtil.Install(options);
            status.Text = "Installation complete.";

            if (options.PinAssist) SetupUtil.OpenPinAssist();

            MessageBox.Show(
                "STOW " + SetupConstants.Version + " was installed successfully.",
                "STOW Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            status.Text = "Installation failed.";
            MessageBox.Show(ex.Message, "STOW Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        Text = "Uninstall STOW";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 249, 251);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Label title = new Label();
        title.Text = "Uninstall STOW?";
        title.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(28, 28);
        Controls.Add(title);

        Label body = new Label();
        body.Text = "This removes STOW, its Start menu/desktop shortcuts,\nand its Windows startup entry.";
        body.AutoSize = true;
        body.ForeColor = Color.FromArgb(75, 80, 90);
        body.Location = new Point(31, 75);
        Controls.Add(body);

        removeSettings = new CheckBox();
        removeSettings.Text = "Also remove my STOW settings";
        removeSettings.AutoSize = true;
        removeSettings.Location = new Point(32, 135);
        Controls.Add(removeSettings);

        Label settingsNote = new Label();
        settingsNote.Text = "Leave this unchecked if you may reinstall STOW later.";
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
                MessageBox.Show("STOW has been removed.", "STOW", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "STOW Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        o.ExistingInstall = hadExisting;
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
            else if (String.Equals(a, "/ACCEPTLICENSES=1", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(a, "--accept-licenses", StringComparison.OrdinalIgnoreCase))
                o.AcceptLicenses = true;
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
            catch (Exception ex)
            {
                SetupUtil.Log("Silent setup failure: " + ex);
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


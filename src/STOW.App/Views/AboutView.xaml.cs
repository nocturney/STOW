using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using STOW.Infrastructure.Updates;

namespace STOW.App.Views;

public partial class AboutView : UserControl
{
    private static readonly Uri RepositoryUri = new("https://github.com/nocturney/STOW");
    private static readonly Uri SupportUri = new("https://github.com/nocturney/STOW/issues");

    private readonly string currentVersion;
    private readonly string buildLabel;
    private readonly UpdateChannel updateChannel;
    private readonly UpdateCheckStateStore updateState = UpdateCheckStateStore.ForCurrentUser();

    public AboutView()
    {
        InitializeComponent();
        (currentVersion, buildLabel, updateChannel) = ReadBuildIdentity();
        VersionText.Text = "Version  " + currentVersion;
        BuildText.Text = "Build  " + buildLabel;
        ChannelText.Text = "Channel  " + updateChannel;
        DateTimeOffset? lastCheck = updateState.LoadLastCheck();
        if (lastCheck is not null)
            LastCheckText.Text = "Last update check  " + lastCheck.Value.LocalDateTime.ToString("g");
    }

    private static (string Version, string Build, UpdateChannel Channel) ReadBuildIdentity()
    {
        string product = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion ?? "0.0.0";
        string version = product;
        string build = "Local";
        int plus = product.IndexOf('+');
        if (plus >= 0)
        {
            version = product[..plus];
            string metadata = product[(plus + 1)..];
            build = metadata.Length > 8 ? metadata[..8] : metadata;
        }

        UpdateChannel channel = SemanticVersion.TryParse(version, out SemanticVersion? parsed) && parsed!.IsPrerelease
            ? UpdateChannel.Preview
            : UpdateChannel.Stable;
        return (version, build, channel);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        LastCheckText.Text = "Last update check  Checking…";

        try
        {
            using var updater = new GitHubReleaseUpdater(userAgentVersion: currentVersion);
            UpdateCheckResult result = await updater.CheckAsync(currentVersion, updateChannel);
            DateTimeOffset checkedAt = DateTimeOffset.Now;
            updateState.SaveLastCheck(checkedAt);
            LastCheckText.Text = "Last update check  " + checkedAt.LocalDateTime.ToString("g");

            if (result.Status == UpdateCheckStatus.NoEligibleRelease)
            {
                ShowNoRelease();
                return;
            }

            if (result.Status == UpdateCheckStatus.UpToDate)
            {
                MessageBox.Show(
                    $"STOW {currentVersion} is up to date on the {updateChannel} channel.",
                    "STOW Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ReleaseInfo release = result.Release!;
            if (!GitHubReleaseUpdater.IsStandardInstalledLocation(Environment.ProcessPath))
            {
                MessageBoxResult open = MessageBox.Show(
                    $"STOW {release.Tag} is available.\n\nAutomatic installer updates are enabled only for the standard per-user install. If this copy came from a package manager, update it there.\n\nOpen the release page?",
                    "STOW Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (open == MessageBoxResult.Yes)
                    OpenUri(release.HtmlUri);
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                $"A newer STOW release is available.\n\nInstalled: {currentVersion}\nLatest: {release.Tag}\n\nDownload, verify, and install it now?",
                "STOW Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes)
                return;

            LastCheckText.Text = "Last update check  Downloading and verifying…";
            PreparedUpdate prepared = await updater.PrepareAsync(release);
            LastCheckText.Text = "Last update check  Verified — starting installer…";
            GitHubReleaseUpdater.LaunchInstaller(prepared);
        }
        catch (Exception ex)
        {
            LastCheckText.Text = "Last update check  Failed";
            MessageBoxResult open = MessageBox.Show(
                "The update check failed safely. Nothing was installed.\n\n" +
                ex.Message + "\n\nOpen GitHub Releases instead?",
                "STOW Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (open == MessageBoxResult.Yes)
                OpenUri(GitHubReleaseUpdater.ReleasesUri);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void ShowNoRelease()
    {
        MessageBoxResult open = MessageBox.Show(
            $"No published {updateChannel} release is available yet.\n\nOpen GitHub Releases?",
            "STOW Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (open == MessageBoxResult.Yes)
            OpenUri(GitHubReleaseUpdater.ReleasesUri);
    }

    private async void WhatsNew_Click(object sender, RoutedEventArgs e)
    {
        WhatsNewButton.IsEnabled = false;
        try
        {
            using var updater = new GitHubReleaseUpdater(userAgentVersion: currentVersion);
            ReleaseInfo? release = await updater.GetReleaseAsync(currentVersion);

            if (release is null)
            {
                UpdateCheckResult fallback = await updater.CheckAsync(currentVersion, updateChannel);
                release = fallback.Release;
            }

            if (release is null)
            {
                MessageBox.Show(
                    "No published release notes are available for this STOW build yet.",
                    "STOW Release Notes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var window = new ReleaseNotesWindow(release)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBoxResult open = MessageBox.Show(
                "STOW could not fetch release notes from GitHub.\n\n" +
                ex.Message +
                "\n\nOpen the Releases page in your browser instead?",
                "STOW Release Notes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (open == MessageBoxResult.Yes)
                OpenUri(GitHubReleaseUpdater.ReleasesUri);
        }
        finally
        {
            WhatsNewButton.IsEnabled = true;
        }
    }

    private void Privacy_Click(object sender, RoutedEventArgs e) =>
        ShowBundledDocument(
            "Privacy",
            LegalDocumentWindow.ReadEmbedded("STOW.Legal.PRIVACY.md"));

    private void License_Click(object sender, RoutedEventArgs e) =>
        ShowBundledDocument(
            "STOW License",
            LegalDocumentWindow.ReadEmbedded("STOW.Legal.STOW_LICENSE.txt"));

    private void Notices_Click(object sender, RoutedEventArgs e) =>
        ShowBundledDocument(
            "Open-source notices & credits",
            LegalDocumentWindow.BuildNoticesBundle());

    private void ShowBundledDocument(string title, string content)
    {
        string legalFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STOW",
            "legal");

        var window = new LegalDocumentWindow(title, content, legalFolder)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void Support_Click(object sender, RoutedEventArgs e) => OpenUri(SupportUri);

    private void GitHubReleases_Click(object sender, RoutedEventArgs e) =>
        OpenUri(RepositoryUri);

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        string diagnostics = BuildDiagnosticsSnapshot();
        try
        {
            Clipboard.SetText(diagnostics);
            MessageBox.Show(
                "Local STOW diagnostics were copied to the clipboard.\n\nNo diagnostic data was uploaded or sent anywhere.",
                "STOW Diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                diagnostics + "\n\nClipboard copy failed: " + ex.Message,
                "STOW Diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private string BuildDiagnosticsSnapshot()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string stowRoot = Path.Combine(roaming, "STOW");
        string legacyRoot = Path.Combine(roaming, "Trayify");

        var lines = new StringBuilder();
        lines.AppendLine("STOW diagnostics");
        lines.AppendLine($"Version: {currentVersion}");
        lines.AppendLine($"Build: {buildLabel}");
        lines.AppendLine($"Channel: {updateChannel}");
        lines.AppendLine($"Install mode: {DetectInstallMode(Environment.ProcessPath)}");
        lines.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        lines.AppendLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        lines.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        lines.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        lines.AppendLine($"STOW config present: {File.Exists(Path.Combine(stowRoot, "config.txt"))}");
        lines.AppendLine($"Rules present: {File.Exists(Path.Combine(stowRoot, "rules.json"))}");
        lines.AppendLine($"Focus presets present: {File.Exists(Path.Combine(stowRoot, "focus-presets.json"))}");
        lines.AppendLine($"Settings present: {File.Exists(Path.Combine(stowRoot, "settings.json"))}");
        lines.AppendLine($"Insights history present: {File.Exists(Path.Combine(stowRoot, "activity.jsonl"))}");
        lines.AppendLine($"Legacy Trayify config present: {File.Exists(Path.Combine(legacyRoot, "config.txt"))}");

        DateTimeOffset? lastCheck = updateState.LoadLastCheck();
        lines.AppendLine("Last update check: " +
                         (lastCheck is null ? "Never" : lastCheck.Value.LocalDateTime.ToString("O")));
        return lines.ToString().TrimEnd();
    }

    private static string DetectInstallMode(string? executablePath)
    {
        if (GitHubReleaseUpdater.IsStandardInstalledLocation(executablePath))
            return "Standard per-user install";
        if (string.IsNullOrWhiteSpace(executablePath))
            return "Unknown";

        string normalized = executablePath.Replace('/', '\\');
        if (normalized.Contains("\\Microsoft\\WinGet\\Packages\\", StringComparison.OrdinalIgnoreCase))
            return "WinGet";
        if (normalized.Contains("\\scoop\\apps\\stow\\", StringComparison.OrdinalIgnoreCase))
            return "Scoop";

        return "Development / portable";
    }

    private static void OpenUri(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not open the link.\n\n" + ex.Message,
                "STOW",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using STOW.Infrastructure.Updates;

namespace STOW.App.Views;

public partial class AboutView : UserControl
{
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

    private void WhatsNew_Click(object sender, RoutedEventArgs e) =>
        OpenUri(GitHubReleaseUpdater.ReleasesUri);

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

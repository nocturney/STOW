using System.Net;
using System.Security.Cryptography;
using STOW.Infrastructure.Updates;
using STOW.WindowTestTarget;

namespace STOW.IntegrationTests;

public sealed class UpdaterTests
{
    [Theory]
    [InlineData("0.4.0-preview.1", "0.4.0-preview.2", -1)]
    [InlineData("0.4.0-preview.2", "0.4.0", -1)]
    [InlineData("0.4.0", "0.4.1-preview.1", -1)]
    [InlineData("0.4.1", "0.4.1", 0)]
    [InlineData("0.5.0", "0.4.9", 1)]
    public void Semantic_version_order_matches_release_expectations(string left, string right, int expectedSign)
    {
        Assert.True(SemanticVersion.TryParse(left, out SemanticVersion? a));
        Assert.True(SemanticVersion.TryParse(right, out SemanticVersion? b));
        int actual = Math.Sign(a!.CompareTo(b));
        Assert.Equal(expectedSign, actual);
    }

    [Fact]
    public void Checksum_parser_accepts_standard_sha256sums_format()
    {
        string hash = new string('a', 64);
        string result = GitHubReleaseUpdater.ParseExpectedHash($"{hash}  STOWSetup.exe\n", "STOWSetup.exe");
        Assert.Equal(hash, result);
    }

    [Fact]
    public async Task Preview_channel_selects_newer_prerelease()
    {
        string json = """
        [
          {"tag_name":"v0.4.0-preview.2","draft":false,"prerelease":true,"html_url":"https://example.test/p2","assets":[]},
          {"tag_name":"v0.4.0-preview.1","draft":false,"prerelease":true,"html_url":"https://example.test/p1","assets":[]}
        ]
        """;
        using var updater = new GitHubReleaseUpdater(
            new HttpClient(new StaticJsonHandler(json)),
            "0.4.0-preview.1");

        UpdateCheckResult result = await updater.CheckAsync("0.4.0-preview.1", UpdateChannel.Preview);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("v0.4.0-preview.2", result.Release!.Tag);
    }

    [Fact]
    public async Task Stable_channel_ignores_semver_prerelease_even_if_github_flag_is_wrong()
    {
        string json = """
        [
          {"tag_name":"v0.5.0-preview.1","draft":false,"prerelease":false,"html_url":"https://example.test/p","assets":[]},
          {"tag_name":"v0.4.1","draft":false,"prerelease":false,"html_url":"https://example.test/s","assets":[]}
        ]
        """;
        using var updater = new GitHubReleaseUpdater(
            new HttpClient(new StaticJsonHandler(json)),
            "0.4.0");

        UpdateCheckResult result = await updater.CheckAsync("0.4.0", UpdateChannel.Stable);

        Assert.Equal("v0.4.1", result.Release!.Tag);
    }

    [Fact]
    public async Task Stable_channel_ignores_prereleases()
    {
        string json = """
        [
          {"tag_name":"v0.5.0-preview.1","draft":false,"prerelease":true,"html_url":"https://example.test/p","assets":[]},
          {"tag_name":"v0.4.1","draft":false,"prerelease":false,"html_url":"https://example.test/s","assets":[]}
        ]
        """;
        using var updater = new GitHubReleaseUpdater(
            new HttpClient(new StaticJsonHandler(json)),
            "0.4.0");

        UpdateCheckResult result = await updater.CheckAsync("0.4.0", UpdateChannel.Stable);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("v0.4.1", result.Release!.Tag);
    }

    [Fact]
    public void Standard_install_location_is_recognized()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STOW",
            "STOW.exe");
        Assert.True(GitHubReleaseUpdater.IsStandardInstalledLocation(expected));
        Assert.False(GitHubReleaseUpdater.IsStandardInstalledLocation(Path.Combine(Path.GetTempPath(), "STOW.exe")));
    }

    [Fact]
    public async Task Prepare_update_rejects_corrupted_checksum()
    {
        string fixtureExe = Path.ChangeExtension(typeof(ParityTargetMarker).Assembly.Location, ".exe");
        byte[] fixtureBytes = await File.ReadAllBytesAsync(fixtureExe);
        Assert.True(SemanticVersion.TryParse("v9.9.9-preview.2", out SemanticVersion? version));
        var assets = new Dictionary<string, ReleaseAsset>(StringComparer.OrdinalIgnoreCase)
        {
            ["STOWSetup.exe"] = new("STOWSetup.exe", new Uri("https://example.test/STOWSetup.exe")),
            ["SHA256SUMS.txt"] = new("SHA256SUMS.txt", new Uri("https://example.test/SHA256SUMS.txt"))
        };
        var release = new ReleaseInfo("v9.9.9-preview.2", version!, true, new Uri("https://example.test/release"), assets);
        using var updater = new GitHubReleaseUpdater(
            new HttpClient(new AssetHandler(fixtureBytes, new string('0', 64))),
            "9.9.9-preview.1");

        await Assert.ThrowsAsync<InvalidDataException>(() => updater.PrepareAsync(release));
    }

    [Fact]
    public async Task Prepare_update_verifies_real_executable_hash_and_version()
    {
        string fixtureExe = Path.ChangeExtension(typeof(ParityTargetMarker).Assembly.Location, ".exe");
        byte[] fixtureBytes = await File.ReadAllBytesAsync(fixtureExe);
        string hash = Convert.ToHexString(SHA256.HashData(fixtureBytes)).ToLowerInvariant();
        Assert.True(SemanticVersion.TryParse("v9.9.9-preview.2", out SemanticVersion? version));

        var assets = new Dictionary<string, ReleaseAsset>(StringComparer.OrdinalIgnoreCase)
        {
            ["STOWSetup.exe"] = new("STOWSetup.exe", new Uri("https://example.test/STOWSetup.exe")),
            ["SHA256SUMS.txt"] = new("SHA256SUMS.txt", new Uri("https://example.test/SHA256SUMS.txt"))
        };
        var release = new ReleaseInfo(
            "v9.9.9-preview.2",
            version!,
            true,
            new Uri("https://example.test/release"),
            assets);

        using var updater = new GitHubReleaseUpdater(
            new HttpClient(new AssetHandler(fixtureBytes, hash)),
            "9.9.9-preview.1");
        PreparedUpdate prepared = await updater.PrepareAsync(release);

        Assert.True(File.Exists(prepared.InstallerPath));
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(prepared.InstallerPath))).ToLowerInvariant());
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly string json;
        public StaticJsonHandler(string json) => this.json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
    }

    private sealed class AssetHandler : HttpMessageHandler
    {
        private readonly byte[] installer;
        private readonly string hash;

        public AssetHandler(byte[] installer, string hash)
        {
            this.installer = installer;
            this.hash = hash;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpContent content = request.RequestUri!.AbsolutePath.EndsWith("STOWSetup.exe", StringComparison.OrdinalIgnoreCase)
                ? new ByteArrayContent(installer)
                : new StringContent($"{hash}  STOWSetup.exe\n");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}

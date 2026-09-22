using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace STOW.Infrastructure.Updates;

public sealed class GitHubReleaseUpdater : IDisposable
{
    public const string Repository = "nocturney/STOW";
    public static readonly Uri ReleasesUri = new("https://github.com/nocturney/STOW/releases");
    private static readonly Uri ReleasesApi = new("https://api.github.com/repos/nocturney/STOW/releases?per_page=20");
    private const string InstallerName = "STOWSetup.exe";
    private const string ChecksumsName = "SHA256SUMS.txt";

    private readonly HttpClient http;
    private readonly bool ownsHttpClient;

    public GitHubReleaseUpdater(HttpClient? httpClient = null, string? userAgentVersion = null)
    {
        http = httpClient ?? new HttpClient();
        ownsHttpClient = httpClient is null;
        if (!http.DefaultRequestHeaders.UserAgent.Any())
        {
            string version = string.IsNullOrWhiteSpace(userAgentVersion) ? "development" : userAgentVersion;
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("STOW", version.Replace('+', '-')));
        }
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        UpdateChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (!SemanticVersion.TryParse(currentVersion, out SemanticVersion? current) || current is null)
            throw new InvalidOperationException($"Invalid current STOW version: {currentVersion}");

        IReadOnlyList<ReleaseInfo> releases = await FetchReleasesAsync(cancellationToken);

        IEnumerable<ReleaseInfo> eligible = releases.Where(release =>
            channel == UpdateChannel.Preview || !release.IsPrerelease);
        ReleaseInfo? latest = eligible
            .OrderByDescending(release => release.Version)
            .FirstOrDefault();

        if (latest is null)
            return new(UpdateCheckStatus.NoEligibleRelease, current, null);
        if (latest.Version.CompareTo(current) > 0)
            return new(UpdateCheckStatus.UpdateAvailable, current, latest);
        return new(UpdateCheckStatus.UpToDate, current, latest);
    }

    public async Task<ReleaseInfo?> GetReleaseAsync(
        string version,
        CancellationToken cancellationToken = default)
    {
        if (!SemanticVersion.TryParse(version, out SemanticVersion? target) || target is null)
            throw new InvalidOperationException($"Invalid STOW version: {version}");

        IReadOnlyList<ReleaseInfo> releases = await FetchReleasesAsync(cancellationToken);
        return releases.FirstOrDefault(release => release.Version.CompareTo(target) == 0);
    }

    private async Task<IReadOnlyList<ReleaseInfo>> FetchReleasesAsync(
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(ReleasesApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseReleasesJson(json);
    }

    internal static IReadOnlyList<ReleaseInfo> ParseReleasesJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub releases response was not an array.");

        var releases = new List<ReleaseInfo>();
        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean())
                continue;
            if (!release.TryGetProperty("tag_name", out JsonElement tagElement))
                continue;
            string? tag = tagElement.GetString();
            if (!SemanticVersion.TryParse(tag, out SemanticVersion? version) || version is null)
                continue;

            bool prerelease = version.IsPrerelease ||
                              (release.TryGetProperty("prerelease", out JsonElement pre) && pre.GetBoolean());
            if (!release.TryGetProperty("html_url", out JsonElement htmlElement) ||
                !Uri.TryCreate(htmlElement.GetString(), UriKind.Absolute, out Uri? htmlUri))
                htmlUri = ReleasesUri;

            string? releaseName = release.TryGetProperty("name", out JsonElement releaseNameElement)
                ? releaseNameElement.GetString()
                : null;
            string? body = release.TryGetProperty("body", out JsonElement bodyElement)
                ? bodyElement.GetString()
                : null;
            DateTimeOffset? publishedAt = null;
            if (release.TryGetProperty("published_at", out JsonElement publishedElement) &&
                DateTimeOffset.TryParse(publishedElement.GetString(), out DateTimeOffset parsedPublished))
                publishedAt = parsedPublished;

            var assets = new Dictionary<string, ReleaseAsset>(StringComparer.OrdinalIgnoreCase);
            if (release.TryGetProperty("assets", out JsonElement assetsElement) &&
                assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assetsElement.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString()
                        : null;
                    string? url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                        ? urlElement.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(name) && Uri.TryCreate(url, UriKind.Absolute, out Uri? downloadUri))
                        assets[name] = new ReleaseAsset(name, downloadUri);
                }
            }

            releases.Add(new ReleaseInfo(tag!, version, prerelease, htmlUri, assets, releaseName, body, publishedAt));
        }
        return releases;
    }

    public async Task<PreparedUpdate> PrepareAsync(
        ReleaseInfo release,
        CancellationToken cancellationToken = default)
    {
        if (!release.Assets.TryGetValue(InstallerName, out ReleaseAsset? installerAsset))
            throw new InvalidDataException($"Release {release.Tag} does not contain {InstallerName}.");
        if (!release.Assets.TryGetValue(ChecksumsName, out ReleaseAsset? checksumAsset))
            throw new InvalidDataException($"Release {release.Tag} does not contain {ChecksumsName}.");

        string safeTag = string.Concat(release.Tag.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        string updateRoot = Path.Combine(Path.GetTempPath(), "STOW", "updates", safeTag);
        Directory.CreateDirectory(updateRoot);
        string installerPath = Path.Combine(updateRoot, InstallerName);
        string checksumPath = Path.Combine(updateRoot, ChecksumsName);

        byte[] installerBytes = await http.GetByteArrayAsync(installerAsset.DownloadUri, cancellationToken);
        byte[] checksumBytes = await http.GetByteArrayAsync(checksumAsset.DownloadUri, cancellationToken);
        await File.WriteAllBytesAsync(installerPath, installerBytes, cancellationToken);
        await File.WriteAllBytesAsync(checksumPath, checksumBytes, cancellationToken);

        string sumsText = Encoding.UTF8.GetString(checksumBytes);
        string expected = ParseExpectedHash(sumsText, InstallerName);
        string actual = ComputeSha256(installerBytes);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Downloaded STOWSetup.exe failed SHA-256 verification. Nothing was installed.");

        VerifyInstallerVersion(installerPath, release.Version);
        return new PreparedUpdate(release, installerPath, checksumPath);
    }

    internal static string ParseExpectedHash(string sumsText, string fileName)
    {
        foreach (string raw in sumsText.Replace("\r", string.Empty).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string left = parts[0].Trim().TrimStart('*');
            string right = parts[1].Trim().TrimStart('*');
            if (IsSha256(left) && string.Equals(Path.GetFileName(right), fileName, StringComparison.OrdinalIgnoreCase))
                return left.ToLowerInvariant();
            if (string.Equals(Path.GetFileName(left), fileName, StringComparison.OrdinalIgnoreCase) && IsSha256(right))
                return right.ToLowerInvariant();
        }
        throw new InvalidDataException($"{ChecksumsName} does not contain a checksum for {fileName}.");
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static void VerifyInstallerVersion(string installerPath, SemanticVersion releaseVersion)
    {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(installerPath);
        if (!string.Equals(info.FileVersion, releaseVersion.NumericFileVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Downloaded installer FileVersion does not match the GitHub release tag.");

        string product = info.ProductVersion ?? string.Empty;
        if (!SemanticVersion.TryParse(product, out SemanticVersion? productVersion) ||
            productVersion is null || productVersion.CompareTo(releaseVersion) != 0)
            throw new InvalidDataException("Downloaded installer ProductVersion does not match the GitHub release tag.");
    }

    public static bool IsStandardInstalledLocation(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STOW",
            "STOW.exe");
        try
        {
            return string.Equals(
                Path.GetFullPath(executablePath),
                Path.GetFullPath(expected),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void LaunchInstaller(PreparedUpdate update)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = update.InstallerPath,
            Arguments = "/VERYSILENT /LAUNCH",
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        if (ownsHttpClient)
            http.Dispose();
    }
}

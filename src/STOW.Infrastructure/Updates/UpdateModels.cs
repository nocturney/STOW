namespace STOW.Infrastructure.Updates;

public enum UpdateChannel
{
    Stable,
    Preview
}

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NoEligibleRelease
}

public sealed record ReleaseAsset(string Name, Uri DownloadUri);

public sealed record ReleaseInfo(
    string Tag,
    SemanticVersion Version,
    bool IsPrerelease,
    Uri HtmlUri,
    IReadOnlyDictionary<string, ReleaseAsset> Assets);

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    SemanticVersion CurrentVersion,
    ReleaseInfo? Release);

public sealed record PreparedUpdate(
    ReleaseInfo Release,
    string InstallerPath,
    string ChecksumPath);

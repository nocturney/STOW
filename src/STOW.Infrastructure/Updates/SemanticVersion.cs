namespace STOW.Infrastructure.Updates;

public sealed record SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> PreRelease,
    string Original) : IComparable<SemanticVersion>
{
    public bool IsPrerelease => PreRelease.Count > 0;

    public static bool TryParse(string? value, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];
        int plus = normalized.IndexOf('+');
        if (plus >= 0)
            normalized = normalized[..plus];

        string core = normalized;
        string prerelease = string.Empty;
        int dash = normalized.IndexOf('-');
        if (dash >= 0)
        {
            core = normalized[..dash];
            prerelease = normalized[(dash + 1)..];
        }

        string[] parts = core.Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out int major) ||
            !int.TryParse(parts[1], out int minor) ||
            !int.TryParse(parts[2], out int patch))
            return false;

        string[] pre = prerelease.Length == 0
            ? Array.Empty<string>()
            : prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (prerelease.Length > 0 && pre.Length == 0)
            return false;

        version = new SemanticVersion(major, minor, patch, pre, value.Trim());
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        int core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;

        if (!IsPrerelease && !other.IsPrerelease) return 0;
        if (!IsPrerelease) return 1;
        if (!other.IsPrerelease) return -1;

        int count = Math.Max(PreRelease.Count, other.PreRelease.Count);
        for (int i = 0; i < count; i++)
        {
            if (i >= PreRelease.Count) return -1;
            if (i >= other.PreRelease.Count) return 1;

            string left = PreRelease[i];
            string right = other.PreRelease[i];
            bool leftNumeric = int.TryParse(left, out int leftNumber);
            bool rightNumeric = int.TryParse(right, out int rightNumber);

            if (leftNumeric && rightNumeric)
            {
                int numeric = leftNumber.CompareTo(rightNumber);
                if (numeric != 0) return numeric;
                continue;
            }
            if (leftNumeric != rightNumeric)
                return leftNumeric ? -1 : 1;

            int text = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            if (text != 0) return text;
        }
        return 0;
    }

    public string NumericFileVersion => $"{Major}.{Minor}.{Patch}.0";
}

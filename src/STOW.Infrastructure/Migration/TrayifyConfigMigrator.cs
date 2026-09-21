using System.Text;

namespace STOW.Infrastructure.Migration;

public enum TrayifyMigrationStatus
{
    Migrated,
    NoLegacyData,
    ExistingStowData,
    InvalidLegacyData,
    Failed
}

public sealed record TrayifyMigrationResult(
    TrayifyMigrationStatus Status,
    string? Message = null);

public sealed class TrayifyConfigMigrator
{
    private readonly string trayifyConfigPath;
    private readonly string stowConfigPath;
    private readonly string markerPath;

    public TrayifyConfigMigrator(string trayifyConfigPath, string stowConfigPath, string markerPath)
    {
        this.trayifyConfigPath = trayifyConfigPath;
        this.stowConfigPath = stowConfigPath;
        this.markerPath = markerPath;
    }

    public static TrayifyConfigMigrator ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string targetDirectory = Path.Combine(roaming, "STOW");
        return new TrayifyConfigMigrator(
            Path.Combine(roaming, "Trayify", "config.txt"),
            Path.Combine(targetDirectory, "config.txt"),
            Path.Combine(targetDirectory, "migration-v1.txt"));
    }

    public TrayifyMigrationResult MigrateIfNeeded()
    {
        if (File.Exists(stowConfigPath))
            return new(TrayifyMigrationStatus.ExistingStowData);

        if (!File.Exists(trayifyConfigPath))
            return new(TrayifyMigrationStatus.NoLegacyData);

        string[] lines;
        byte[] sourceBytes;
        try
        {
            lines = File.ReadAllLines(trayifyConfigPath, Encoding.UTF8);
            sourceBytes = File.ReadAllBytes(trayifyConfigPath);
        }
        catch (Exception ex)
        {
            return new(TrayifyMigrationStatus.Failed, ex.Message);
        }

        if (!ValidateLegacyConfig(lines, out string? validationError))
            return new(TrayifyMigrationStatus.InvalidLegacyData, validationError);

        string? targetDirectory = Path.GetDirectoryName(stowConfigPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return new(TrayifyMigrationStatus.Failed, "STOW config path has no parent directory.");

        string tempPath = stowConfigPath + ".migrating-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllBytes(tempPath, sourceBytes);
            File.Move(tempPath, stowConfigPath);

            try
            {
                string marker = "migration=trayify-config-v1" + Environment.NewLine +
                                "source=" + trayifyConfigPath + Environment.NewLine +
                                "migratedUtc=" + DateTimeOffset.UtcNow.ToString("O") + Environment.NewLine;
                File.WriteAllText(markerPath, marker, new UTF8Encoding(false));
            }
            catch
            {
                // The migrated config itself is authoritative; marker failure is non-destructive.
            }

            return new(TrayifyMigrationStatus.Migrated);
        }
        catch (IOException) when (File.Exists(stowConfigPath))
        {
            TryDelete(tempPath);
            return new(TrayifyMigrationStatus.ExistingStowData);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return new(TrayifyMigrationStatus.Failed, ex.Message);
        }
    }

    internal static bool ValidateLegacyConfig(IEnumerable<string> lines, out string? error)
    {
        int lineNumber = 0;
        foreach (string raw in lines)
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            string[] parts = line.Split('|');
            if (parts.Length < 6 || (parts[0] != "0" && parts[0] != "1"))
            {
                error = $"Invalid Trayify config record at line {lineNumber}.";
                return false;
            }

            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    _ = Convert.FromBase64String(parts[i]);
                }
                catch (FormatException)
                {
                    error = $"Invalid Base64 value in Trayify config at line {lineNumber}.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only; legacy and target configs are never deleted here.
        }
    }
}

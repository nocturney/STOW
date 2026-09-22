using System.Globalization;

namespace STOW.Infrastructure.Configuration;

public sealed class LegalAcceptanceStore
{
    public const string CurrentRevision = "2026-09-22-v1";

    private readonly string path;

    public LegalAcceptanceStore(string path)
    {
        this.path = path;
    }

    public static LegalAcceptanceStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new LegalAcceptanceStore(Path.Combine(roaming, "STOW", "legal-acceptance.txt"));
    }

    public bool IsAccepted()
    {
        if (!File.Exists(path))
            return false;

        try
        {
            string[] lines = File.ReadAllLines(path);
            return lines.Any(line =>
                string.Equals(
                    line.Trim(),
                    "REVISION=" + CurrentRevision,
                    StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    public void Accept(string source)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("STOW legal acceptance path has no parent directory.");

        Directory.CreateDirectory(directory);

        string temp = path + ".writing-" + Guid.NewGuid().ToString("N");
        string[] lines =
        [
            "REVISION=" + CurrentRevision,
            "ACCEPTED_AT_UTC=" + DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            "SOURCE=" + Sanitize(source)
        ];

        try
        {
            File.WriteAllLines(temp, lines);
            if (File.Exists(path))
                File.Replace(temp, path, destinationBackupFileName: null);
            else
                File.Move(temp, path);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch { }
        }
    }

    private static string Sanitize(string value) =>
        new(value.Where(ch => ch != '\r' && ch != '\n').ToArray());
}

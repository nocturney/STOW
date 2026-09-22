namespace STOW.Infrastructure.Updates;

public sealed class UpdateCheckStateStore
{
    private readonly string path;

    public UpdateCheckStateStore(string path)
    {
        this.path = path;
    }

    public static UpdateCheckStateStore ForCurrentUser()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new UpdateCheckStateStore(Path.Combine(roaming, "STOW", "update-check.txt"));
    }

    public DateTimeOffset? LoadLastCheck()
    {
        try
        {
            if (!File.Exists(path)) return null;
            string value = File.ReadAllText(path).Trim();
            return DateTimeOffset.TryParse(value, out DateTimeOffset result) ? result : null;
        }
        catch
        {
            return null;
        }
    }

    public void SaveLastCheck(DateTimeOffset value)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, value.ToString("O"));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch
        {
            // Update-check metadata is best-effort only.
        }
    }
}

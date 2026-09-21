using System.Text;
using STOW.Infrastructure.Migration;

namespace STOW.IntegrationTests;

public sealed class TrayifyConfigMigratorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "stow-tests-" + Guid.NewGuid().ToString("N"));

    private string LegacyConfig => Path.Combine(root, "Trayify", "config.txt");
    private string StowConfig => Path.Combine(root, "STOW", "config.txt");
    private string Marker => Path.Combine(root, "STOW", "migration-v1.txt");

    [Fact]
    public void Migrates_valid_config_and_preserves_legacy_source()
    {
        WriteLegacy(ValidConfig());
        byte[] original = File.ReadAllBytes(LegacyConfig);
        var migrator = NewMigrator();

        TrayifyMigrationResult result = migrator.MigrateIfNeeded();

        Assert.Equal(TrayifyMigrationStatus.Migrated, result.Status);
        Assert.True(File.Exists(LegacyConfig));
        Assert.True(File.Exists(StowConfig));
        Assert.Equal(original, File.ReadAllBytes(StowConfig));
        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public void Is_idempotent_after_successful_migration()
    {
        WriteLegacy(ValidConfig());
        var migrator = NewMigrator();

        Assert.Equal(TrayifyMigrationStatus.Migrated, migrator.MigrateIfNeeded().Status);
        Assert.Equal(TrayifyMigrationStatus.ExistingStowData, migrator.MigrateIfNeeded().Status);
    }

    [Fact]
    public void Never_overwrites_existing_stow_config()
    {
        WriteLegacy(ValidConfig());
        Directory.CreateDirectory(Path.GetDirectoryName(StowConfig)!);
        File.WriteAllText(StowConfig, "stow-owned-data", Encoding.UTF8);

        TrayifyMigrationResult result = NewMigrator().MigrateIfNeeded();

        Assert.Equal(TrayifyMigrationStatus.ExistingStowData, result.Status);
        Assert.Contains("stow-owned-data", File.ReadAllText(StowConfig));
        Assert.True(File.Exists(LegacyConfig));
    }

    [Fact]
    public void Rejects_invalid_legacy_data_without_creating_stow_config()
    {
        WriteLegacy("1|not-base64|still-not|bad|data|here");
        TrayifyMigrationResult result = NewMigrator().MigrateIfNeeded();

        Assert.Equal(TrayifyMigrationStatus.InvalidLegacyData, result.Status);
        Assert.False(File.Exists(StowConfig));
        Assert.True(File.Exists(LegacyConfig));
    }

    [Fact]
    public void Reports_no_legacy_data_when_trayify_config_is_absent()
    {
        TrayifyMigrationResult result = NewMigrator().MigrateIfNeeded();

        Assert.Equal(TrayifyMigrationStatus.NoLegacyData, result.Status);
        Assert.False(File.Exists(StowConfig));
    }

    private TrayifyConfigMigrator NewMigrator() => new(LegacyConfig, StowConfig, Marker);

    private void WriteLegacy(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyConfig)!);
        File.WriteAllText(LegacyConfig, content, new UTF8Encoding(false));
    }

    private static string ValidConfig()
    {
        static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return "# Trayify config v1" + Environment.NewLine +
               $"1|{B64("GrokBot")}|{B64(@"C:\\Apps\\GrokBot.exe")}|{B64("GrokBot")}|{B64("Grok")}|{B64("Chrome_WidgetWin_1")}" + Environment.NewLine;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Test cleanup should not hide the assertion result.
        }
    }
}

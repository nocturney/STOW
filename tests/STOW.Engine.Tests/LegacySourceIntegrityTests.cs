using System.Security.Cryptography;

namespace STOW.Engine.Tests;

public sealed class LegacySourceIntegrityTests
{
    [Theory]
    [InlineData("src/Trayify.cs", "3E1C0AC4280D55084E02AA493B0B026645B2A8318C54BC401ED906608FF8038C")]
    [InlineData("src/TrayifySetup.cs", "71EB6CF863271BE440D233AF540EB4CFDB69300D0F28892CCF1704927DE5D51F")]
    public void Trayify_v033_baseline_source_changes_only_deliberately(string relativePath, string expectedHash)
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"Baseline source file is missing: {relativePath}");
        using FileStream stream = File.OpenRead(path);
        string actualHash = Convert.ToHexString(SHA256.HashData(stream));

        Assert.Equal(expectedHash, actualHash);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "STOW.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate STOW repository root.");
    }
}

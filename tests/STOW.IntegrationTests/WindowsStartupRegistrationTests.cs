using STOW.Platform.Windows.Runtime;

namespace STOW.IntegrationTests;

public sealed class WindowsStartupRegistrationTests
{
    [Fact]
    public void Recognizes_installed_and_rejects_development_location()
    {
        string local = @"C:\Users\Chris\AppData\Local";
        string profile = @"C:\Users\Chris";

        Assert.Equal(
            StartupExecutableKind.Installed,
            WindowsStartupRegistration.ResolveKind(
                @"C:\Users\Chris\AppData\Local\STOW\STOW.exe", local, profile));

        Assert.Equal(
            StartupExecutableKind.Unsupported,
            WindowsStartupRegistration.ResolveKind(
                @"C:\Users\Chris\STOW\dist-stow\STOW.exe", local, profile));
    }

    [Fact]
    public void Recognizes_package_manager_locations()
    {
        string local = @"C:\Users\Chris\AppData\Local";
        string profile = @"C:\Users\Chris";

        Assert.Equal(
            StartupExecutableKind.PackageManaged,
            WindowsStartupRegistration.ResolveKind(
                Path.Combine(local, "Microsoft", "WinGet", "Packages", "STOW_test", "STOW.exe"), local, profile));

        Assert.Equal(
            StartupExecutableKind.PackageManaged,
            WindowsStartupRegistration.ResolveKind(
                Path.Combine(profile, "scoop", "apps", "stow", "current", "STOW.exe"), local, profile));
    }

    [Fact]
    public void Startup_commands_always_use_background_mode()
    {
        string installed = WindowsStartupRegistration.BuildInstalledCommand(@"C:\Program Files\STOW\STOW.exe");
        Assert.Equal("\"C:\\Program Files\\STOW\\STOW.exe\" --background", installed);

        string package = WindowsStartupRegistration.BuildPackageManagedCommand(@"C:\Windows\System32\cmd.exe");
        Assert.Contains("where STOW.exe", package);
        Assert.Contains("STOW.exe --background", package);
        Assert.Contains("/v STOW", package);
    }
}

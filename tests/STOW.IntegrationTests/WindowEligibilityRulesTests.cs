using STOW.Platform.Windows.Discovery;

namespace STOW.IntegrationTests;

public sealed class WindowEligibilityRulesTests
{
    [Fact]
    public void Ordinary_visible_taskbar_window_is_eligible()
    {
        Assert.True(WindowEligibilityRules.IsTaskbarCandidate(
            appWindow: false, toolWindow: false, hasOwner: false, cloaked: false, title: "Editor"));
    }

    [Theory]
    [InlineData(false, false, true, false, "Owned")]
    [InlineData(false, true, false, false, "Tool")]
    [InlineData(false, false, false, true, "Cloaked")]
    [InlineData(false, false, false, false, "")]
    public void Non_user_facing_windows_are_filtered(bool appWindow, bool toolWindow, bool hasOwner, bool cloaked, string title)
    {
        Assert.False(WindowEligibilityRules.IsTaskbarCandidate(appWindow, toolWindow, hasOwner, cloaked, title));
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("ShellExperienceHost")]
    [InlineData("StartMenuExperienceHost")]
    [InlineData("SearchHost")]
    [InlineData("TextInputHost")]
    public void Windows_shell_processes_are_filtered(string processName)
    {
        Assert.True(WindowEligibilityRules.IsShellProcess(processName, "Visible title"));
    }

    [Fact]
    public void Normal_application_process_is_not_filtered_as_shell()
    {
        Assert.False(WindowEligibilityRules.IsShellProcess("GrokBot", "Grok"));
    }
}

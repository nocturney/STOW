using STOW.Platform.Windows.Compatibility;

namespace STOW.IntegrationTests;

public sealed class RestoreTargetResolverTests
{
    [Fact]
    public void Prefers_the_original_valid_hwnd()
    {
        var runtime = new FakeWindowRuntime(
            new Window(101, 10, "Original", "Chrome_WidgetWin_1"),
            new Window(202, 10, "Grok", "Chrome_WidgetWin_1"));

        nint? target = RestoreTargetResolver.Resolve(
            [101], new HashSet<int> { 10 }, "Grok", "Chrome_WidgetWin_1", runtime);

        Assert.Equal((nint)101, target);
    }

    [Fact]
    public void Electron_recreated_hwnd_is_resolved_from_tracked_pid()
    {
        var runtime = new FakeWindowRuntime(
            new Window(202, 42, "Grok", "Chrome_WidgetWin_1"),
            new Window(303, 77, "Other", "Chrome_WidgetWin_1"));

        nint? target = RestoreTargetResolver.Resolve(
            [101], new HashSet<int> { 42 }, "Grok", "Chrome_WidgetWin_1", runtime);

        Assert.Equal((nint)202, target);
    }

    [Fact]
    public void Uses_first_nonempty_class_match_as_fallback()
    {
        var runtime = new FakeWindowRuntime(
            new Window(210, 42, "Replacement", "Chrome_WidgetWin_1"),
            new Window(211, 42, "", "Chrome_WidgetWin_1"));

        nint? target = RestoreTargetResolver.Resolve(
            Array.Empty<nint>(), new HashSet<int> { 42 }, "Old title", "Chrome_WidgetWin_1", runtime);

        Assert.Equal((nint)210, target);
    }

    [Fact]
    public void Never_restores_a_window_from_an_untracked_pid()
    {
        var runtime = new FakeWindowRuntime(
            new Window(300, 7, "Grok", "Chrome_WidgetWin_1"));

        nint? target = RestoreTargetResolver.Resolve(
            Array.Empty<nint>(), new HashSet<int> { 42 }, "Grok", "Chrome_WidgetWin_1", runtime);

        Assert.Null(target);
    }

    [Fact]
    public void Hidden_restore_path_has_no_visibility_filter_by_contract()
    {
        Assert.DoesNotContain(
            typeof(IWindowRuntime).GetMethods(),
            method => method.Name.Contains("Visible", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record Window(nint Handle, int Pid, string Title, string ClassName);

    private sealed class FakeWindowRuntime : IWindowRuntime
    {
        private readonly IReadOnlyList<Window> windows;

        public FakeWindowRuntime(params Window[] windows)
        {
            this.windows = windows;
        }

        public bool IsWindow(nint handle) => windows.Any(window => window.Handle == handle);

        public IEnumerable<nint> EnumerateTopLevelWindows() => windows.Select(window => window.Handle);

        public int GetProcessId(nint handle) => Get(handle).Pid;

        public string GetTitle(nint handle) => Get(handle).Title;

        public string GetClassName(nint handle) => Get(handle).ClassName;

        private Window Get(nint handle) => windows.Single(window => window.Handle == handle);
    }
}

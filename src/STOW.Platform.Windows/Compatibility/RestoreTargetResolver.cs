namespace STOW.Platform.Windows.Compatibility;

public interface IWindowRuntime
{
    bool IsWindow(nint handle);
    IEnumerable<nint> EnumerateTopLevelWindows();
    int GetProcessId(nint handle);
    string GetTitle(nint handle);
    string GetClassName(nint handle);
}

public static class RestoreTargetResolver
{
    public static nint? Resolve(
        IEnumerable<nint>? rememberedHandles,
        IReadOnlySet<int>? trackedPids,
        string? titleHint,
        string? classHint,
        IWindowRuntime runtime)
    {
        if (rememberedHandles is not null)
        {
            foreach (nint handle in rememberedHandles)
            {
                if (runtime.IsWindow(handle))
                    return handle;
            }
        }

        if (trackedPids is null || trackedPids.Count == 0)
            return null;

        nint? fallback = null;

        foreach (nint handle in runtime.EnumerateTopLevelWindows())
        {
            if (!trackedPids.Contains(runtime.GetProcessId(handle)))
                continue;

            string title = runtime.GetTitle(handle);
            string className = runtime.GetClassName(handle);

            bool classMatch = string.IsNullOrEmpty(classHint) ||
                              string.Equals(classHint, className, StringComparison.Ordinal);
            bool titleMatch = !string.IsNullOrEmpty(titleHint) &&
                              string.Equals(titleHint, title, StringComparison.OrdinalIgnoreCase);

            if (titleMatch && classMatch)
                return handle;

            if (fallback is null && classMatch && title.Length > 0)
                fallback = handle;
        }

        return fallback;
    }
}

using Xunit;

namespace STOW.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveWin32FactAttribute : FactAttribute
{
    public LiveWin32FactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STOW_LIVE_WIN32_PARITY"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Requires an interactive Windows desktop. Run scripts/run-live-parity.ps1.";
        }
    }
}

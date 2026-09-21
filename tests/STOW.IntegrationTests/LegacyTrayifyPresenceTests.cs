using STOW.Platform.Windows.Runtime;

namespace STOW.IntegrationTests;

public sealed class LegacyTrayifyPresenceTests
{
    [Fact]
    public void Detects_named_legacy_mutex_without_taking_ownership()
    {
        string name = @"Local\STOW-Test-" + Guid.NewGuid().ToString("N");
        var guard = new LegacyTrayifyPresence(name);

        Assert.False(guard.IsRunning());

        using var mutex = new Mutex(initiallyOwned: true, name, out bool created);
        Assert.True(created);
        Assert.True(guard.IsRunning());

        mutex.ReleaseMutex();
    }
}

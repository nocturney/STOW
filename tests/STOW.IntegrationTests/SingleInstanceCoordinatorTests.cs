using STOW.Infrastructure.Runtime;

namespace STOW.IntegrationTests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void Second_instance_signals_the_owner_instead_of_acquiring()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string mutex = @"Local\STOWTestMutex-" + suffix;
        string signal = @"Local\STOWTestShow-" + suffix;

        using var owner = new SingleInstanceCoordinator(mutex, signal);
        using var second = new SingleInstanceCoordinator(mutex, signal);

        Assert.True(owner.TryAcquire());
        Assert.False(second.TryAcquire());
        Assert.True(second.SignalExisting());
        Assert.True(owner.ConsumeShowRequest());
        Assert.False(owner.ConsumeShowRequest());
    }

    [Fact]
    public void Ownership_can_be_reacquired_after_owner_disposes()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string mutex = @"Local\STOWTestMutex-" + suffix;
        string signal = @"Local\STOWTestShow-" + suffix;

        var first = new SingleInstanceCoordinator(mutex, signal);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var replacement = new SingleInstanceCoordinator(mutex, signal);
        Assert.True(replacement.TryAcquire());
    }
}

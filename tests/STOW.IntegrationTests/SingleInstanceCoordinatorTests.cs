using STOW.Infrastructure.Runtime;

namespace STOW.IntegrationTests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void Second_instance_can_signal_show_and_safe_exit_requests()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string mutex = @"Local\STOWTestMutex-" + suffix;
        string show = @"Local\STOWTestShow-" + suffix;
        string exit = @"Local\STOWTestExit-" + suffix;

        using var owner = new SingleInstanceCoordinator(mutex, show, exit);
        using var second = new SingleInstanceCoordinator(mutex, show, exit);

        Assert.True(owner.TryAcquire());
        Assert.False(second.TryAcquire());
        Assert.True(second.SignalExisting());
        Assert.True(owner.ConsumeShowRequest());
        Assert.False(owner.ConsumeShowRequest());
        Assert.True(second.SignalExitExisting());
        Assert.True(owner.ConsumeExitRequest());
        Assert.False(owner.ConsumeExitRequest());
    }

    [Fact]
    public void Ownership_can_be_reacquired_after_owner_disposes()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string mutex = @"Local\STOWTestMutex-" + suffix;
        string show = @"Local\STOWTestShow-" + suffix;
        string exit = @"Local\STOWTestExit-" + suffix;

        var first = new SingleInstanceCoordinator(mutex, show, exit);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var replacement = new SingleInstanceCoordinator(mutex, show, exit);
        Assert.True(replacement.TryAcquire());
    }
}

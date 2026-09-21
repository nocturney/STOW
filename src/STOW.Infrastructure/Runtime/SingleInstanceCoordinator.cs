using System.Runtime.Versioning;

namespace STOW.Infrastructure.Runtime;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceCoordinator : IDisposable
{
    public const string DefaultMutexName = @"Local\STOWSingleInstance";
    public const string DefaultShowEventName = @"Local\STOWShowManager";

    private readonly string mutexName;
    private readonly string showEventName;
    private Mutex? mutex;
    private EventWaitHandle? showEvent;
    private bool ownsMutex;

    public SingleInstanceCoordinator(
        string mutexName = DefaultMutexName,
        string showEventName = DefaultShowEventName)
    {
        this.mutexName = mutexName;
        this.showEventName = showEventName;
    }

    public bool TryAcquire()
    {
        if (mutex is not null)
            return ownsMutex;

        mutex = new Mutex(true, mutexName, out bool createdNew);
        ownsMutex = createdNew;
        if (createdNew)
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName, out _);
        return createdNew;
    }

    public bool SignalExisting()
    {
        try
        {
            using EventWaitHandle existing = EventWaitHandle.OpenExisting(showEventName);
            return existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool ConsumeShowRequest()
    {
        return ownsMutex && showEvent?.WaitOne(0) == true;
    }

    public void Dispose()
    {
        showEvent?.Dispose();
        showEvent = null;

        if (ownsMutex && mutex is not null)
        {
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }

        mutex?.Dispose();
        mutex = null;
        ownsMutex = false;
    }
}

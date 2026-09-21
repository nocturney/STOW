using System.Runtime.Versioning;

namespace STOW.Infrastructure.Runtime;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceCoordinator : IDisposable
{
    public const string DefaultMutexName = @"Local\STOWSingleInstance";
    public const string DefaultShowEventName = @"Local\STOWShowManager";
    public const string DefaultExitEventName = @"Local\STOWRequestExit";

    private readonly string mutexName;
    private readonly string showEventName;
    private readonly string exitEventName;
    private Mutex? mutex;
    private EventWaitHandle? showEvent;
    private EventWaitHandle? exitEvent;
    private bool ownsMutex;

    public SingleInstanceCoordinator(
        string mutexName = DefaultMutexName,
        string showEventName = DefaultShowEventName,
        string exitEventName = DefaultExitEventName)
    {
        this.mutexName = mutexName;
        this.showEventName = showEventName;
        this.exitEventName = exitEventName;
    }

    public bool TryAcquire()
    {
        if (mutex is not null)
            return ownsMutex;

        mutex = new Mutex(true, mutexName, out bool createdNew);
        ownsMutex = createdNew;
        if (createdNew)
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName, out _);
            exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, exitEventName, out _);
        }
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

    public bool SignalExitExisting()
    {
        try
        {
            using EventWaitHandle existing = EventWaitHandle.OpenExisting(exitEventName);
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

    public bool ConsumeExitRequest()
    {
        return ownsMutex && exitEvent?.WaitOne(0) == true;
    }

    public void Dispose()
    {
        showEvent?.Dispose();
        showEvent = null;
        exitEvent?.Dispose();
        exitEvent = null;

        if (ownsMutex && mutex is not null)
        {
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }

        mutex?.Dispose();
        mutex = null;
        ownsMutex = false;
    }
}

namespace STOW.Platform.Windows.Runtime;

public sealed class LegacyTrayifyPresence
{
    public const string DefaultMutexName = @"Local\TrayifySingleInstance";
    private readonly string mutexName;

    public LegacyTrayifyPresence(string mutexName = DefaultMutexName)
    {
        this.mutexName = mutexName;
    }

    public bool IsRunning()
    {
        try
        {
            using Mutex mutex = Mutex.OpenExisting(mutexName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}

namespace STOW.Engine.Contracts;

public enum EngineCommandStatus
{
    Succeeded,
    AlreadyExists,
    NotFound,
    RestoreTargetUnavailable,
    Failed
}

public sealed record EngineCommandResult(
    EngineCommandStatus Status,
    string? Message = null)
{
    public bool Succeeded => Status == EngineCommandStatus.Succeeded;
}

public interface ITrayEngine
{
    EngineSnapshot GetSnapshot();

    EngineCommandResult AddManagedApp(DiscoveredAppSnapshot app, bool enabled = true);

    EngineCommandResult RemoveManagedApp(string appKey);

    EngineCommandResult SetEnabled(string appKey, bool enabled);

    EngineCommandResult Restore(string appKey);
}

public interface ITrayEngineRuntime : ITrayEngine, IDisposable
{
    bool IsRunning { get; }

    void Start();

    EngineCommandResult Shutdown();
}

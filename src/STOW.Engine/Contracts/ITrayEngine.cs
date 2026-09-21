namespace STOW.Engine.Contracts;

public enum EngineCommandStatus
{
    Succeeded,
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

    EngineCommandResult SetEnabled(string appKey, bool enabled);

    EngineCommandResult Restore(string appKey);
}

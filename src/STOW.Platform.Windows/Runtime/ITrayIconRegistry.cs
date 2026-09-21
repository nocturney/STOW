using STOW.Engine.Contracts;

namespace STOW.Platform.Windows.Runtime;

internal interface ITrayIconRegistry : IDisposable
{
    void Ensure(ManagedAppDefinition app, Func<EngineCommandResult> restore, Func<EngineCommandResult> disable);
    void Remove(string appKey);
    void RemoveAll();
}

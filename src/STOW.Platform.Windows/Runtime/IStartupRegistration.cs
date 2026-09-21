namespace STOW.Platform.Windows.Runtime;

internal interface IStartupRegistration
{
    void Update(bool shouldStartWithWindows);
}

internal sealed class NoOpStartupRegistration : IStartupRegistration
{
    public void Update(bool shouldStartWithWindows) { }
}

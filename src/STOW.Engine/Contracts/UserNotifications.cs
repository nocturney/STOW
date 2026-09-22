namespace STOW.Engine.Contracts;

public enum UserNotificationKind
{
    Information,
    Warning,
    Error
}

public interface IUserNotificationSink
{
    void Show(
        string title,
        string message,
        UserNotificationKind kind = UserNotificationKind.Information);
}

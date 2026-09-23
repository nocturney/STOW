using System.Drawing;
using STOW.Engine.Contracts;

namespace STOW.Platform.Windows.Runtime;

public sealed class MainTrayIconHost : IUserNotificationSink, IDisposable
{
    private readonly System.Windows.Forms.ContextMenuStrip menu;
    private readonly System.Windows.Forms.NotifyIcon notifyIcon;

    public MainTrayIconHost(Action open, Action exit)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(exit);

        menu = new System.Windows.Forms.ContextMenuStrip();
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open STOW");
        openItem.Click += (_, _) => open();
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit STOW");
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(openItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "STOW",
            Icon = ResolveApplicationIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => open();
        notifyIcon.BalloonTipClicked += (_, _) => open();
    }

    public void Show(
        string title,
        string message,
        UserNotificationKind kind = UserNotificationKind.Information)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            System.Windows.Forms.ToolTipIcon icon = kind switch
            {
                UserNotificationKind.Warning => System.Windows.Forms.ToolTipIcon.Warning,
                UserNotificationKind.Error => System.Windows.Forms.ToolTipIcon.Error,
                _ => System.Windows.Forms.ToolTipIcon.Info
            };

            notifyIcon.ShowBalloonTip(
                5000,
                string.IsNullOrWhiteSpace(title) ? "STOW" : title.Trim(),
                message.Trim(),
                icon);
        }
        catch
        {
            // Notifications are best-effort and must never affect STOW runtime safety.
        }
    }

    private static Icon ResolveApplicationIcon()
    {
        try
        {
            string? executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                Icon? extracted = Icon.ExtractAssociatedIcon(executablePath);
                if (extracted is not null)
                    return extracted;
            }
        }
        catch { }

        // Defensive fallback only. Release builds embed the reviewed STOW application icon.
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menu.Dispose();
    }
}

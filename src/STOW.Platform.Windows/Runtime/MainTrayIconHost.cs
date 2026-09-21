using System.Drawing;

namespace STOW.Platform.Windows.Runtime;

public sealed class MainTrayIconHost : IDisposable
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

        // Temporary fallback until the final approved STOW .ico is wired into the build.
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menu.Dispose();
    }
}

using System.Drawing;
using System.Windows.Forms;
using STOW.Engine.Contracts;

namespace STOW.Platform.Windows.Runtime;

internal sealed class WinFormsTrayIconRegistry : ITrayIconRegistry
{
    private readonly Dictionary<string, NotifyIcon> icons = new(StringComparer.OrdinalIgnoreCase);

    public void Ensure(ManagedAppDefinition app, Func<EngineCommandResult> restore, Func<EngineCommandResult> disable)
    {
        if (icons.ContainsKey(app.Key))
            return;

        var icon = new NotifyIcon
        {
            Text = app.Name.Length > 60 ? app.Name[..60] : app.Name,
            Icon = GetIconFor(app.ExecutablePath),
            Visible = true
        };

        var menu = new ContextMenuStrip();
        var restoreItem = new ToolStripMenuItem("Restore");
        restoreItem.Click += (_, _) => RunCommand(app, restore, "restore");
        var disableItem = new ToolStripMenuItem("Disable minimize-to-tray");
        disableItem.Click += (_, _) => RunCommand(app, disable, "disable minimize-to-tray for");
        menu.Items.Add(restoreItem);
        menu.Items.Add(disableItem);
        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (_, _) => RunCommand(app, restore, "restore");
        icons[app.Key] = icon;
    }

    public void Remove(string appKey)
    {
        if (!icons.Remove(appKey, out NotifyIcon? icon))
            return;

        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
    }

    public void RemoveAll()
    {
        foreach (string key in icons.Keys.ToArray())
            Remove(key);
    }

    public void Dispose() => RemoveAll();

    private static void RunCommand(ManagedAppDefinition app, Func<EngineCommandResult> command, string verb)
    {
        EngineCommandResult result = command();
        if (result.Succeeded)
            return;

        string detail = string.IsNullOrWhiteSpace(result.Message)
            ? "The app is still running and remains managed by STOW."
            : result.Message;
        MessageBox.Show(
            $"STOW could not {verb} {app.Name}.\n\n{detail}",
            "STOW Restore",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private static Icon GetIconFor(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return Icon.ExtractAssociatedIcon(path) ?? SystemIcons.Application;
        }
        catch { }

        return SystemIcons.Application;
    }
}

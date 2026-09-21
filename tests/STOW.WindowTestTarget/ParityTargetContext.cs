namespace STOW.WindowTestTarget;

internal sealed class ParityTargetContext : ApplicationContext
{
    private const string WindowTitle = "STOW Electron Parity Target";
    private readonly string controlPath;
    private readonly string statePath;
    private readonly System.Windows.Forms.Timer timer;
    private Form currentForm;
    private string lastCommand = string.Empty;
    private bool exiting;

    public ParityTargetContext(string controlPath, string statePath)
    {
        this.controlPath = controlPath;
        this.statePath = statePath;
        currentForm = CreateForm();
        currentForm.Show();
        WriteState(currentForm.Handle);

        timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) => PollCommand();
        timer.Start();
    }

    private static Form CreateForm() => new()
    {
        Text = WindowTitle,
        Width = 720,
        Height = 420,
        ShowInTaskbar = true,
        StartPosition = FormStartPosition.Manual,
        Location = new Point(120, 120)
    };

    private void PollCommand()
    {
        string command;
        try
        {
            command = File.Exists(controlPath) ? File.ReadAllText(controlPath).Trim() : string.Empty;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrEmpty(command) || command == lastCommand)
            return;

        lastCommand = command;
        if (command == "recreate-hidden")
        {
            currentForm.Hide();
            currentForm.Dispose();
            currentForm = CreateForm();
            currentForm.Show();
            nint handle = currentForm.Handle;
            currentForm.Hide();
            WriteState(handle);
        }
        else if (command == "exit")
        {
            exiting = true;
            timer.Stop();
            currentForm.Close();
            currentForm.Dispose();
            ExitThread();
        }
    }

    private void WriteState(nint handle)
    {
        string directory = Path.GetDirectoryName(statePath) ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        string temp = statePath + ".tmp";
        File.WriteAllText(temp, $"{Environment.ProcessId}|{handle.ToInt64()}");
        File.Move(temp, statePath, overwrite: true);
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        timer.Dispose();
        if (!exiting)
        {
            try { currentForm.Close(); } catch { }
            try { currentForm.Dispose(); } catch { }
        }
        base.ExitThreadCore();
    }
}

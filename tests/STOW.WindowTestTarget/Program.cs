namespace STOW.WindowTestTarget;

public static class ParityTargetMarker
{
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? controlPath = ValueAfter(args, "--control");
        string? statePath = ValueAfter(args, "--state");

        if (string.IsNullOrWhiteSpace(controlPath) || string.IsNullOrWhiteSpace(statePath))
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new ParityTargetContext(controlPath, statePath));
    }

    private static string? ValueAfter(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}

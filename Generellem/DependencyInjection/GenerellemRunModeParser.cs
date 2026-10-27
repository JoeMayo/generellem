namespace Generellem.DependencyInjection;

/// <summary>
/// Reads <c>--mode</c> from the console command line and from configuration text.
/// </summary>
public static class GenerellemRunModeParser
{
    public static bool TryParse(string? text, out GenerellemRunMode mode)
    {
        mode = GenerellemRunMode.Both;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        if (char.IsDigit(trimmed[0]))
            return false;

        return Enum.TryParse(trimmed, ignoreCase: true, out mode) && Enum.IsDefined(mode);
    }

    /// <summary>
    /// Reads <c>--mode &lt;value&gt;</c> or <c>--mode=&lt;value&gt;</c>.
    /// The last mode flag wins.
    /// </summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="mode">The parsed mode, or <see cref="GenerellemRunMode.Both"/> when the flag is present but invalid.</param>
    /// <param name="parsed">False when a mode flag is present but the value is not Both, Ingest, or Query.</param>
    /// <returns>True when a mode flag is present.</returns>
    public static bool TryReadArgs(string[] args, out GenerellemRunMode mode, out bool parsed)
    {
        mode = GenerellemRunMode.Both;
        parsed = false;
        bool found = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? value = null;

            if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                {
                    parsed = false;
                    mode = GenerellemRunMode.Both;
                    continue;
                }

                value = args[++i];
            }
            else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                value = arg["--mode=".Length..];
            }
            else
            {
                continue;
            }

            parsed = TryParse(value, out mode);
            if (!parsed)
                mode = GenerellemRunMode.Both;
        }

        return found;
    }
}

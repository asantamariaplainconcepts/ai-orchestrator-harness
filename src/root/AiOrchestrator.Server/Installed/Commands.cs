using System.Diagnostics;
using System.Text;

namespace AiOrchestrator.Server.Installed;

/// <summary>Runs a command and answers its exit code with what it printed, both streams in one text.</summary>
delegate CommandResult RunCommand(string file, params string[] arguments);

/// <param name="ExitCode">The command's exit code; 127 when it could not be started at all, as a shell says.</param>
/// <param name="Output">Standard output, then standard error.</param>
sealed record CommandResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

static class Commands
{
    /// <summary>The real <see cref="RunCommand"/>: the process, waited for, its output read whole.</summary>
    public static CommandResult Run(string file, params string[] arguments)
    {
        var start = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)!;
            // Both streams at once: reading one to its end while the other fills its pipe deadlocks.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            var text = new StringBuilder(output.GetAwaiter().GetResult()).Append(error.GetAwaiter().GetResult());
            return new CommandResult(process.ExitCode, text.ToString().Trim());
        }
        catch (System.ComponentModel.Win32Exception failure)
        {
            return new CommandResult(127, $"{file}: {failure.Message}");
        }
    }
}

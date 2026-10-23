using System.Diagnostics;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Folders;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// Hands a folder or a file to the editor (#514, #515).
/// <para>
/// Everything here launches <b>detached</b> — spawned, not waited on, output not redirected — so
/// the editor outlives this process. Redirecting its streams would keep a pipe open that nobody
/// reads, and waiting for exit would tie an HTTP request to how long somebody leaves VS Code open.
/// </para>
/// <para>
/// It runs on the <b>server</b>, which is the same machine as the user in the habitat this product
/// is built for and is not in any other. That is a real limitation of the browser architecture
/// (ADR-0001), and <see cref="DesktopFailure.Unsupported"/> exists to say so rather than to fail
/// mysteriously.
/// </para>
/// </summary>
sealed class DesktopIntegration(IConfiguration configuration) : IDesktopIntegration
{
    /// <summary>The editor command, if the operator named one. `code` otherwise.</summary>
    const string EditorKey = "Desktop:Editor";


    public Task<DesktopResult> OpenInEditor(
        string path,
        string? requested = null,
        CancellationToken cancellationToken = default
    )
    {
        var target = Validate(path);
        if (target.Reason is { } refusal)
        {
            return Task.FromResult(DesktopResult.Failed(refusal));
        }

        // What the caller named beats what the operator configured, which beats the fallback. The
        // caller is the person clicking a menu item, and a preference they just expressed about
        // this click is a better answer than a file they may never have edited.
        var command = requested?.Trim();
        command = string.IsNullOrWhiteSpace(command) ? configuration[EditorKey]?.Trim() : command;
        command = string.IsNullOrWhiteSpace(command) ? "code" : command;

        // Two shapes, and whitespace does not tell them apart: an unquoted POSIX path can contain
        // spaces. Existence on disk is the test — a configured `/Applications/My Editor/bin/ed`
        // is one executable, while `code --wait` is a command line.
        if (File.Exists(command))
        {
            return Task.FromResult(Spawn(command, [.. EditorArguments(command, target.Path!)]));
        }

        if (command.Contains(' ', StringComparison.Ordinal))
        {
            // A compound line goes through a shell, because that is what the operator wrote.
            return Task.FromResult(
                Spawn("/bin/sh", ["-c", $"{command} {Quote(target.Path!)}"])
            );
        }

        // A bare name has to be resolved here rather than left to Process.Start. Setting PATH on
        // the child's environment does not change how this process looks the binary up, so an
        // editor that only exists on the login shell's PATH was never found.
        if (ResolveOnPath(command) is { } executable)
        {
            return Task.FromResult(Spawn(executable, [.. EditorArguments(command, target.Path!)]));
        }

        // Nothing on the PATH. On macOS the editor is normally installed as an application bundle
        // and its command line tool is a separate, optional step most people never take — so the
        // bundle is the more reliable target, and it is how Reveal already works.
        if (OperatingSystem.IsMacOS() && MacApplication(command) is { } application)
        {
            // `-n` forces a new instance for editors that would otherwise reuse the last window,
            // which is the same worktree confusion EditorArguments guards against.
            string[] arguments = Path
                .GetFileNameWithoutExtension(command)
                .Equals("cursor", StringComparison.OrdinalIgnoreCase)
                ? ["-n", "-a", application, target.Path!]
                : ["-a", application, target.Path!];

            return Task.FromResult(Spawn("open", arguments));
        }

        return Task.FromResult(Spawn(command, [.. EditorArguments(command, target.Path!)]));
    }

    /// <summary>
    /// The macOS application behind an editor's command line name, for the common case where the
    /// app is installed and its <c>PATH</c> shim is not. Unknown names get nothing rather than a
    /// guess — <c>open -a</c> on a name that does not exist fails the same way, only later.
    /// </summary>
    static string? MacApplication(string command) =>
        Path.GetFileNameWithoutExtension(command).ToLowerInvariant() switch
        {
            "code" => "Visual Studio Code",
            "code-insiders" => "Visual Studio Code - Insiders",
            "cursor" => "Cursor",
            "windsurf" => "Windsurf",
            "zed" => "Zed",
            "subl" => "Sublime Text",
            // The JetBrains shims are installed from inside the IDE and most people never do it,
            // so for these the bundle is the usual case rather than the fallback.
            "rider" => "Rider",
            "idea" => "IntelliJ IDEA",
            "webstorm" => "WebStorm",
            "pycharm" => "PyCharm",
            _ => null,
        };

    /// <summary>
    /// Looks a bare command up the way a shell would, over the login shell's <c>PATH</c> when one
    /// could be read and this process's otherwise.
    /// </summary>
    static string? ResolveOnPath(string command)
    {
        var search = LoginShellEnvironment.Path() ?? Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        foreach (var directory in search.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(LocalPath.Expand(directory.Trim()), command);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Cursor routes a bare folder launch through its last active workbench, which silently opens a
    /// <b>different worktree</b> — the failure looks like this product passing the wrong path.
    /// </summary>
    static IEnumerable<string> EditorArguments(string command, string path)
    {
        if (Path.GetFileNameWithoutExtension(command).Equals("cursor", StringComparison.OrdinalIgnoreCase))
        {
            yield return "--new-window";
        }

        yield return path;
    }

    /// <summary>
    /// Checked on every call, not once at startup. A worktree can be removed while the screen
    /// still lists it, and "it is gone" and "nothing answered" need different sentences.
    /// </summary>
    static (string? Path, DesktopFailure? Reason) Validate(string path)
    {
        var expanded = LocalPath.Expand(path);

        if (!Path.IsPathRooted(expanded))
        {
            return (null, DesktopFailure.NotAbsolute);
        }

        if (!Directory.Exists(expanded) && !File.Exists(expanded))
        {
            return (null, DesktopFailure.NotFound);
        }

        return (expanded, null);
    }

    static DesktopResult Spawn(string command, string[] arguments)
    {
        var start = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            // Deliberately not redirected: an unread pipe fills and blocks the child, and this
            // process is not going to read an editor's stdout.
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // A GUI app started from here inherits this process's environment, which may not have the
        // login shell's PATH — the same trap the agent runner solves.
        if (LoginShellEnvironment.Path() is { } resolved)
        {
            start.Environment["PATH"] = resolved;
        }

        try
        {
            using var process = Process.Start(start);

            // Not waited on and not disposed-with-kill: the editor should outlive this call. The
            // `using` disposes the handle, not the process.
            return process is null
                ? DesktopResult.Failed(DesktopFailure.LaunchFailed)
                : DesktopResult.Ok;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // No such binary, or the OS refused. Both read the same to the person: nothing opened.
            return DesktopResult.Failed(DesktopFailure.LaunchFailed);
        }
    }

    static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'" : value;

}

using CliWrap;

namespace AiOrchestrator.Infrastructure;

/// <summary>
/// Recovers the <c>PATH</c> the user's login shell would have set.
/// <para>
/// <b>The single biggest trap in this whole product.</b> A process launched from a GUI — Finder, a
/// Dock icon, launchd — does not inherit the login shell's environment. An agent installed under
/// <c>~/.volta/bin</c>, <c>~/.local/bin</c> or any version manager is then simply not found, and
/// the error says "command not found" rather than anything true. It works in your terminal and
/// fails in the app, which is the most expensive class of bug there is.
/// </para>
/// <para>
/// Started from a terminal — <c>aspire run</c>, <c>dotnet run</c> — the process already inherits
/// the right <c>PATH</c> and this changes nothing. It earns its place the moment anything launches
/// the app any other way.
/// </para>
/// <para>
/// <b>The shell is asked, not read.</b> Startup files compute PATH as often as they assign it —
/// <c>source</c>, <c>eval "$(brew shellenv)"</c>, nvm — and only running them gets that right, in
/// the order and with the files the user's own shell picks. This is what VS Code and the
/// <c>shell-env</c> package do.
/// </para>
/// </summary>
static class LoginShellEnvironment
{
    /// <summary>
    /// Where a command actually is, walked over the recovered <see cref="Path"/> — so a health
    /// check, a spawn, and a question put to a vendor's CLI all agree about what exists. Null when
    /// it is on no directory we can see.
    /// <para>
    /// An absolute or relative path is taken as given and only checked, because a caller that named
    /// a file meant that file.
    /// </para>
    /// </summary>
    public static string? Resolve(string command)
    {
        if (command.Contains('/', StringComparison.Ordinal))
        {
            return File.Exists(command) ? command : null;
        }

        var path = Path() ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (
            var directory in path.Split(
                System.IO.Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            var candidate = System.IO.Path.Combine(directory, command);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// <see cref="Path"/> as the environment a spawn is given on top of this process's own: empty
    /// when the inherited <c>PATH</c> should stand.
    /// </summary>
    public static Dictionary<string, string?> PathOverride() =>
        Path() is { } path ? new() { ["PATH"] = path } : [];

    /// <summary>
    /// The <c>PATH</c> to give a spawned process, or null when nothing could be recovered and the
    /// inherited one should stand. The shell is asked once, on first use, and the answer kept for
    /// the life of the process: it costs about 1.3 s with a real <c>.zshrc</c> (measured on the
    /// developer's machine), which is too much to pay per spawn.
    /// </summary>
    public static string? Path() => Recovered.Value;

    static readonly Lazy<string?> Recovered = new(() =>
        // Windows has no login-shell equivalent: the environment a GUI process inherits is the one
        // the registry describes, which is the same one a terminal gets.
        OperatingSystem.IsWindows() ? null : Merge(Ask(Shell(), TimeSpan.FromSeconds(10)))
    );

    /// <summary>
    /// The user's login shell as the user database records it, and only then <c>$SHELL</c>.
    /// <para>
    /// <b>#475.</b> <c>$SHELL</c> is whatever the parent passed down, and a parent is not always the
    /// person: the installer opens the app under <c>sudo</c>, and the app measured
    /// <c>SHELL=/bin/sh</c> and <c>USER=root</c> while running as its user. <c>/bin/sh -i -l</c>
    /// reads no <c>~/.zshrc</c>, so the PATH came back without <c>/opt/homebrew/bin</c> — no
    /// <c>gh</c>, an empty inbox, and every agent "not installed". The record is the account's, so
    /// it does not depend on who opened the process. <see cref="Environment.UserName"/> is read from
    /// the effective uid, not from <c>$USER</c>.
    /// </para>
    /// </summary>
    static string Shell() =>
        RecordedShell(Environment.UserName)
        ?? (Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } shell ? shell : null)
        ?? (OperatingSystem.IsMacOS() ? "/bin/zsh" : "/bin/sh");

    /// <summary>The shell the user database names for <paramref name="user"/>, when it names one that exists.</summary>
    internal static string? RecordedShell(string user)
    {
        try
        {
            var named = OperatingSystem.IsMacOS()
                ? FromDirectory(
                    ProcessRun
                        .Capture(
                            Cli.Wrap("/usr/bin/dscl").WithArguments([".", "-read", $"/Users/{user}", "UserShell"]),
                            TimeSpan.FromSeconds(5),
                            CancellationToken.None,
                            maxCharacters: 10_000
                        )
                        .GetAwaiter()
                        .GetResult()
                        .StandardOutput
                )
                : File.Exists("/etc/passwd")
                    ? FromPasswd(File.ReadAllText("/etc/passwd"), user)
                    : null;

            return named is not null && File.Exists(named) ? named : null;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary><c>dscl . -read /Users/x UserShell</c> answers <c>UserShell: /bin/zsh</c>.</summary>
    internal static string? FromDirectory(string output) =>
        output
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("UserShell:", StringComparison.Ordinal))
            .Select(line => line["UserShell:".Length..].Trim())
            .FirstOrDefault(value => value.StartsWith('/'));

    /// <summary>The seventh field of the user's <c>/etc/passwd</c> line.</summary>
    internal static string? FromPasswd(string passwd, string user) =>
        passwd
            .Split('\n')
            .Select(line => line.Split(':'))
            .Where(fields => fields.Length >= 7 && fields[0] == user)
            .Select(fields => fields[6].Trim())
            .FirstOrDefault(value => value.StartsWith('/'));

    /// <summary>
    /// The <c>PATH</c> the given shell ends up with after its startup files, or null when it did
    /// not say within <paramref name="timeout"/>.
    /// <para>
    /// <b>Interactive as well as login</b> (<c>-i -l -c</c>, what VS Code runs): a login shell alone
    /// skips <c>.zshrc</c>, and on the developer's own machine that is where <c>~/.local/bin</c>,
    /// <c>~/.bun/bin</c> and <c>~/.aspire/bin</c> are added. The price of <c>-i</c> is the risk of
    /// the shell taking the terminal's foreground: measured under a real pty, neither zsh nor bash
    /// moved the foreground process group with <c>-c</c>, and stdin here is empty rather than a
    /// TTY, so job control is off (bash says so on stderr, which nobody reads).
    /// </para>
    /// <para>
    /// <b>Between markers</b>, because an interactive startup prints whatever it likes — a banner,
    /// a motd, a version manager's notice — and only the bytes between the two copies of a marker
    /// that cannot occur by accident are the answer. The exit code is not consulted for the same
    /// reason: a startup file that fails on its last line still printed the PATH.
    /// </para>
    /// </summary>
    internal static string? Ask(string shell, TimeSpan timeout)
    {
        var marker = $"_AIO_PATH_{Guid.NewGuid():N}_";

        // fish has no "$PATH" string: its PATH is a list, and quoting it joins with spaces.
        var script =
            System.IO.Path.GetFileName(shell) == "fish"
                ? $"printf '%s%s%s' '{marker}' (string join : $PATH) '{marker}'"
                : $"printf '%s%s%s' '{marker}' \"$PATH\" '{marker}'";

        var run = ProcessRun
            .Capture(
                Cli.Wrap(shell).WithArguments(["-i", "-l", "-c", script]),
                timeout,
                CancellationToken.None,
                maxCharacters: 1_000_000
            )
            .GetAwaiter()
            .GetResult();

        var output = run.StandardOutput;
        var open = output.IndexOf(marker, StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }

        var start = open + marker.Length;
        var close = output.IndexOf(marker, start, StringComparison.Ordinal);
        if (close < 0)
        {
            return null;
        }

        var path = output[start..close].Trim();
        return path.Length > 0 ? path : null;
    }

    /// <summary>
    /// The login shell's directories first, then whatever this process already has — under
    /// <c>aspire run</c> that is the right answer, and dropping it would break the case that works.
    /// Null when there is nothing to add, so the inherited <c>PATH</c> stands untouched.
    /// </summary>
    static string? Merge(string? recovered)
    {
        if (recovered is null)
        {
            return null;
        }

        var inherited = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        // Order is the whole meaning of PATH, so the first occurrence of each directory wins and
        // later duplicates are dropped rather than the other way round.
        return string.Join(
            ':',
            $"{recovered}:{inherited}".Split(':', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)
        );
    }
}

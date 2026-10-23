using System.Xml.Linq;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiOrchestrator.Server.Installed;

/// <summary>The operating systems the service is written for, and the one it is not.</summary>
enum ServiceHost
{
    MacOS,
    Linux,
    Windows,
}

/// <summary>
/// <c>aio</c> as a service of the person's session (#703, ADR-0010): a LaunchAgent on macOS and a
/// <c>systemd --user</c> unit on Linux, which start the installed tool at login with no terminal.
/// <c>aio service install|uninstall|status</c> and Settings' <em>Start at login</em> are this class.
/// <para>
/// <b>It restarts only what failed.</b> launchd's <c>KeepAlive</c> is <c>SuccessfulExit: false</c> and
/// systemd's <c>Restart=on-failure</c>: a crash or a <c>kill -9</c> is started again, a clean exit is
/// not. A clean <c>KeepAlive</c> would undo <em>Stop console</em>, and would restart an old version
/// that a newer one asked to stop, which would then ask the newer one to stop. An update's restart
/// exits with <see cref="SelfUpdate.RestartExitCode"/> for that reason.
/// </para>
/// </summary>
sealed class LoginService(
    ServiceHost host,
    string home,
    string label,
    string? command,
    IReadOnlyDictionary<string, string> environment,
    RunCommand run
)
{
    /// <summary>The service's name in launchd and systemd unless <see cref="LabelVariable"/> names another.</summary>
    public const string DefaultLabel = "aio";

    /// <summary>
    /// Another label, for a test install beside the person's own: launchd's domain is the user's,
    /// whatever <c>HOME</c> says, so a second service is told apart by its name.
    /// </summary>
    public const string LabelVariable = "AIO_SERVICE_LABEL";

    /// <summary>Set to the label in the service's own environment: how a process knows launchd or systemd started it.</summary>
    public const string ServiceVariable = "AIO_SERVICE";

    public string Label => label;

    /// <summary>Why this process cannot register the service, or null when it can.</summary>
    public string? Refusal =>
        host == ServiceHost.Windows ? "Starting at login is not supported on Windows yet."
        : command is null ? "Only the installed tool starts at login. Install it with dotnet tool install -g aio, then run aio service install."
        : null;

    /// <summary>Whether launchd and systemd can take it at all; Windows cannot yet.</summary>
    public bool Supported => host != ServiceHost.Windows;

    /// <summary>The file that registers it: the LaunchAgent's plist or the systemd unit.</summary>
    public string Definition =>
        host == ServiceHost.Linux
            ? Path.Combine(home, ".config", "systemd", "user", $"{label}.service")
            : Path.Combine(home, "Library", "LaunchAgents", $"{label}.plist");

    /// <summary>Registered is the file being there: it is what launchd and systemd read at the next login.</summary>
    public bool Registered => File.Exists(Definition);

    /// <summary>The log the app already writes, so a crash before it opens its own still lands there.</summary>
    string Log => Path.Combine(home, ".aiorchestrator", "server.log");

    /// <summary>
    /// The service for this process: the tool's own command, this home, and the environment the
    /// service needs that launchd and systemd do not give (#703).
    /// </summary>
    public static LoginService ForThisProcess()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // The service's own process reads its label from AIO_SERVICE, which its file sets: a test
        // label given at install time is not in launchd's environment.
        var label = Environment.GetEnvironmentVariable(LabelVariable) is { Length: > 0 } named ? named
            : Environment.GetEnvironmentVariable(ServiceVariable) is { Length: > 0 } running ? running
            : DefaultLabel;

        var environment = new Dictionary<string, string>
        {
            // The home it was registered from: the person's own, or a test's, whose data stays there.
            ["HOME"] = home,
            [ServiceVariable] = label,
            // Started at login nobody is looking, and after an update's restart the page is already open.
            [Installation.BrowserVariable] = "none",
        };

        // The tool's command is an apphost that looks for the runtime in DOTNET_ROOT or the default
        // install folder; launchd's environment has neither when .NET came from elsewhere.
        if (Path.GetDirectoryName(typeof(object).Assembly.Location) is { } runtime
            && Directory.GetParent(runtime)?.Parent?.Parent is { } root)
        {
            environment["DOTNET_ROOT"] = root.FullName;
        }

        // An address the person chose wins over the preferred port, in the service as by hand.
        if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { Length: > 0 } urls)
        {
            environment["ASPNETCORE_URLS"] = urls;
        }

        var platform = OperatingSystem.IsMacOS() ? ServiceHost.MacOS
            : OperatingSystem.IsWindows() ? ServiceHost.Windows
            : ServiceHost.Linux;

        return new LoginService(platform, home, label, Installation.ToolCommand, environment, Commands.Run);
    }

    /// <summary>
    /// Writes the file and loads it, which starts the app now as well as at every login. A service
    /// already loaded is left as it is: loading it again would stop the process it runs.
    /// </summary>
    public CommandResult Install()
    {
        if (Refusal is { } refusal)
        {
            return new CommandResult(2, refusal);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Definition)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Log)!);

        if (host == ServiceHost.Linux)
        {
            File.WriteAllText(Definition, Unit());
            run("systemctl", "--user", "daemon-reload");
            return run("systemctl", "--user", "enable", "--now", $"{label}.service");
        }

        File.WriteAllText(Definition, Plist());
        var domain = Domain();

        return run("launchctl", "print", $"{domain}/{label}").Succeeded
            ? new CommandResult(0, "")
            : run("launchctl", "bootstrap", domain, Definition);
    }

    /// <summary>
    /// Unloads it and deletes its file. With <paramref name="stop"/> false, which is Settings run by
    /// the service itself, only the file goes: unloading would end the process that is answering,
    /// and without the file nothing starts it at the next login.
    /// </summary>
    public CommandResult Uninstall(bool stop)
    {
        if (Refusal is { } refusal)
        {
            return new CommandResult(2, refusal);
        }

        if (host == ServiceHost.Linux)
        {
            var disabled = stop
                ? run("systemctl", "--user", "disable", "--now", $"{label}.service")
                : run("systemctl", "--user", "disable", $"{label}.service");
            Forget();
            run("systemctl", "--user", "daemon-reload");
            return disabled;
        }

        if (stop)
        {
            // Not loaded is an error to launchctl and nothing to this: the file goes either way.
            run("launchctl", "bootout", $"{Domain()}/{label}");
        }

        Forget();
        return new CommandResult(0, "");
    }

    /// <summary>Deletes the file; a missing folder is a file already gone, which File.Delete throws on.</summary>
    void Forget()
    {
        if (Registered)
        {
            File.Delete(Definition);
        }
    }

    /// <summary>
    /// <c>aio service install|uninstall|status</c>. <paramref name="answering"/> is the version that
    /// answers on the app's address, or null when nothing does.
    /// </summary>
    public int Answer(string verb, Func<string?> answering, string origin, TextWriter output, TextWriter error)
    {
        if (verb is not ("install" or "uninstall" or "status"))
        {
            error.WriteLine($"aio service: '{verb}' is not a verb it takes. Use install, uninstall or status.");
            return 2;
        }

        if (Refusal is { } refusal)
        {
            error.WriteLine($"aio service: {refusal}");
            return 2;
        }

        switch (verb)
        {
            case "install":
                var installed = Install();
                if (!installed.Succeeded)
                {
                    error.WriteLine($"aio service: registering {label} failed: {installed.Output}");
                    return 1;
                }

                output.WriteLine($"aio starts at login: {Definition}");
                return 0;

            case "uninstall":
                // Unloaded even without its file: Settings run by the service removes only the file.
                var registered = Registered;
                Uninstall(stop: true);
                output.WriteLine(
                    registered
                        ? $"aio no longer starts at login, and its service stopped: {Definition} is gone."
                        : $"aio was not registered to start at login; whatever was still loaded as {label} is stopped."
                );
                return 0;

            default:
                output.WriteLine(Registered ? $"Registered to start at login: {Definition}" : "Not registered to start at login.");
                output.WriteLine(answering() is { } version ? $"aio {version} answers at {origin}." : $"Nothing answers at {origin}.");
                return 0;
        }
    }

    /// <summary>The LaunchAgent: run the tool's command at load, again when it fails, its output to the app's log.</summary>
    public string Plist()
    {
        var variables = new XElement("dict");
        foreach (var (name, value) in environment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            variables.Add(new XElement("key", name), new XElement("string", value));
        }

        var plist = new XDocument(
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement(
                "plist",
                new XAttribute("version", "1.0"),
                new XElement(
                    "dict",
                    new XElement("key", "Label"),
                    new XElement("string", label),
                    new XElement("key", "ProgramArguments"),
                    new XElement("array", new XElement("string", command)),
                    new XElement("key", "RunAtLoad"),
                    new XElement("true"),
                    new XElement("key", "KeepAlive"),
                    new XElement("dict", new XElement("key", "SuccessfulExit"), new XElement("false")),
                    new XElement("key", "EnvironmentVariables"),
                    variables,
                    new XElement("key", "StandardOutPath"),
                    new XElement("string", Log),
                    new XElement("key", "StandardErrorPath"),
                    new XElement("string", Log)
                )
            )
        );

        return """<?xml version="1.0" encoding="UTF-8"?>""" + "\n" + plist + "\n";
    }

    /// <summary>The systemd user unit: the same command, restarted on failure, started with the session.</summary>
    public string Unit()
    {
        var lines = new List<string>
        {
            "[Unit]",
            "Description=AI Orchestrator (aio)",
            "",
            "[Service]",
            $"ExecStart=\"{command}\"",
            "Restart=on-failure",
        };

        lines.AddRange(
            environment.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"Environment=\"{pair.Key}={pair.Value}\"")
        );
        lines.Add($"StandardOutput=append:{Log}");
        lines.Add($"StandardError=append:{Log}");
        lines.AddRange(["", "[Install]", "WantedBy=default.target", ""]);

        return string.Join('\n', lines);
    }

    /// <summary>launchd's domain for the person's graphical session, <c>gui/&lt;uid&gt;</c>.</summary>
    string Domain() => $"gui/{run("id", "-u").Output.Trim()}";
}

/// <param name="How">How this process was started: <c>tool</c>, <c>dnx</c> or <c>checkout</c>. Only <c>tool</c> registers.</param>
/// <param name="Supported">False on Windows, which has no service yet.</param>
/// <param name="Registered">Whether the LaunchAgent or the unit is there, so the next login starts the app.</param>
sealed record StartAtLogin(string How, bool Supported, bool Registered);

/// <param name="Registered">True registers the service, false removes it.</param>
sealed record StartAtLoginChange(bool Registered);

static class LoginServiceEndpoints
{
    public static WebApplication MapStartAtLogin(this WebApplication app)
    {
        var group = app.MapGroup("/api/app/service").WithTags("Updates");

        group
            .MapGet("", () => TypedResults.Ok(State(LoginService.ForThisProcess())))
            .WithName("ReadStartAtLogin");

        group
            .MapPut(
                "",
                Results<Ok<StartAtLogin>, ProblemHttpResult> (StartAtLoginChange change, HttpContext context) =>
                {
                    if (LocalCaller.IsForeignPage(context))
                    {
                        return TypedResults.Problem("Only this app's own page changes this.", statusCode: StatusCodes.Status403Forbidden);
                    }

                    var service = LoginService.ForThisProcess();
                    if (Installation.How != "tool" || service.Refusal is not null)
                    {
                        return TypedResults.Problem(service.Refusal ?? "Only the installed tool starts at login.", statusCode: StatusCodes.Status409Conflict);
                    }

                    // Turned off by the service itself, the process keeps answering: see Uninstall.
                    var result = change.Registered
                        ? service.Install()
                        : service.Uninstall(stop: Environment.GetEnvironmentVariable(LoginService.ServiceVariable) != service.Label);

                    return result.Succeeded
                        ? TypedResults.Ok(State(service))
                        : TypedResults.Problem(result.Output, statusCode: StatusCodes.Status409Conflict);
                }
            )
            .WithName("SetStartAtLogin");

        return app;
    }

    static StartAtLogin State(LoginService service) => new(Installation.How, service.Supported, service.Registered);
}

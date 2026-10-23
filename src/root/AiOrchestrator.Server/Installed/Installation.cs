using System.Diagnostics;
using System.Reflection;
using AiOrchestrator.Infrastructure;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace AiOrchestrator.Server.Installed;

/// <summary>
/// What the Server does differently when it is the installed app, the .NET tool <c>aio</c> (#678,
/// ADR-0009). There is one process (ADR-0008): this one serves the frontend
/// from its own folder and opens the default browser at its own address. A checkout started by
/// <c>aspire start</c> or <c>dotnet run</c> is not installed, and nothing here changes it.
/// </summary>
static class Installation
{
    /// <summary>
    /// The port the installed app asks for first. A PWA is installed per origin, so the same port
    /// launch after launch is what keeps the installed window, its icon and its storage. It is a
    /// preference and not a promise: when somebody else holds it, the app takes any free port.
    /// </summary>
    public const int PreferredPort = 48117;

    /// <summary>
    /// Whether this process runs from the .NET tool package, by <c>dnx aio</c> or an installed
    /// <c>aio</c>. <c>dotnet pack</c> writes <c>DotnetToolSettings.xml</c> beside the entry assembly
    /// and nothing else does, so the file is the answer.
    /// </summary>
    public static bool IsTool { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "DotnetToolSettings.xml"));

    /// <summary>
    /// How this process was started, which decides the command that updates it (#679): <c>tool</c>
    /// for an installed <c>aio</c>, whose files sit under the tools folder's <c>.store</c>;
    /// <c>dnx</c> for <c>dnx aio</c>, which runs the package from the NuGet cache; <c>checkout</c>
    /// for everything that is not the tool.
    /// </summary>
    public static string How { get; } =
        !IsTool ? "checkout"
        : AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}.store{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ? "tool"
        : "dnx";

    /// <summary>
    /// The folder the tool is installed in, the one that holds its command and <c>.store</c>, or
    /// null when this process is not an installed tool: <c>~/.dotnet/tools</c> for <c>-g</c>, or the
    /// <c>--tool-path</c> it was installed with.
    /// </summary>
    public static string? ToolRoot { get; } =
        How == "tool"
            ? AppContext.BaseDirectory[..AppContext.BaseDirectory.IndexOf($"{Path.DirectorySeparatorChar}.store{Path.DirectorySeparatorChar}", StringComparison.Ordinal)]
            : null;

    /// <summary>The installed tool's command, the shim that runs whichever version is installed now.</summary>
    public static string? ToolCommand =>
        ToolRoot is { } root ? Path.Combine(root, OperatingSystem.IsWindows() ? "aio.exe" : "aio") : null;

    /// <summary><c>none</c> starts the app without opening a browser: set by the service and by an update's restart.</summary>
    public const string BrowserVariable = "AIO_BROWSER";

    /// <summary>
    /// What the tool answers when it is given arguments, or null to start the app. <c>aio</c> is
    /// also the harness's command, <c>.aio/aio</c>: an agent that types
    /// <c>aio issue view 1</c> with the tool on its PATH must not start a second app, so it is told
    /// where its command is and gets 2, the exit code of a usage error.
    /// </summary>
    public static int? Answer(string[] args, TextWriter output, TextWriter error)
    {
        switch (args)
        {
            case []:
                return null;

            case ["--version"]:
                output.WriteLine(Version);
                return 0;

            case ["--help" or "-h"]:
                output.WriteLine($"aio {Version}, AI Orchestrator.");
                output.WriteLine($"With no arguments it starts the app at http://localhost:{PreferredPort}/ and opens the browser there.");
                output.WriteLine("aio service install|uninstall|status starts it at login, stops doing so, or says whether it does.");
                output.WriteLine("Options: --version, --help.");
                return 0;

            // #703. The one verb: the service that starts it at login.
            case ["service", var verb]:
                var origin = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { Length: > 0 } urls
                    ? urls.Split(';')[0].TrimEnd('/')
                    : $"http://127.0.0.1:{PreferredPort}";
                return LoginService.ForThisProcess().Answer(verb, () => Takeover.VersionAt(origin), origin, output, error);

            default:
                error.WriteLine($"aio: '{string.Join(' ', args)}' is not something this app does; it takes no arguments but service install|uninstall|status.");
                error.WriteLine("The harness command is .aio/aio, run from the repository: .aio/aio issue|pr|done ...");
                return 2;
        }
    }

    /// <summary>The package's version, the same <c>/api/version</c> reports.</summary>
    public static string Version =>
        typeof(Installation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>The installed app's content root: the folder the binary is in, never the working directory.</summary>
    public static string? ContentRoot => IsTool ? AppContext.BaseDirectory : null;

    /// <summary>
    /// Prepares this process before the host is built. Returns the exit code when this process has
    /// nothing left to do: the same version already answers on the preferred port and the browser
    /// was opened there, or another version answers and would not stop (#703).
    /// </summary>
    public static int? Prepare()
    {
        if (!IsTool)
        {
            return null;
        }

        WriteOutputToLog();

        // #476. Started outside a login shell (a launcher, the Dock), the process gets launchd's
        // PATH and finds no `gh`, `git` from Homebrew or agent CLI. The adapters ask the login shell
        // for theirs; the rest of the process (the gh secret resolver) needs it too.
        if (LoginShell.Path() is { } path)
        {
            Environment.SetEnvironmentVariable("PATH", path);
        }

        var preferred = $"http://127.0.0.1:{PreferredPort}";

        if (Explicit())
        {
            return null;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        switch (Takeover.Settle(preferred, Version, http, Console.Out, OpenBrowser, Takeover.Patience))
        {
            case Arrival.Leave:
                return 0;

            case Arrival.Stuck:
                return 1;

            case Arrival.ListenElsewhere:
                Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
                return null;

            default:
                Environment.SetEnvironmentVariable("ASPNETCORE_URLS", preferred);
                return null;
        }
    }

    /// <summary>Opens the default browser at the address the server bound once it is listening.</summary>
    public static void OpenBrowserWhenStarted(WebApplication app)
    {
        if (!IsTool)
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();

            if (address is not null)
            {
                OpenBrowser(address.TrimEnd('/') + "/");
            }
        });
    }

    /// <summary>
    /// The URL in the browser the operating system calls default. <c>UseShellExecute</c> is
    /// <c>open</c> on macOS, <c>xdg-open</c> on Linux and the shell on Windows.
    /// </summary>
    static void OpenBrowser(string url)
    {
        // The service at login, and the restart after an update, whose page is already open (#703).
        if (Environment.GetEnvironmentVariable(BrowserVariable) == "none")
        {
            Console.WriteLine($"Listening at {url}");
            return;
        }

        try
        {
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No browser to hand it to. The address is in the log, and the server keeps serving.
            Console.WriteLine($"Could not open a browser; the console is at {url}");
        }
    }

    /// <summary>Whether the person chose the address themselves, which always wins over the preferred port.</summary>
    static bool Explicit() =>
        Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { Length: > 0 }
        || Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") is { Length: > 0 };

    /// <summary>
    /// The installed app has no terminal, so what it prints goes to a file a person can open:
    /// <c>~/.aiorchestrator/server.log</c>, beside the database.
    /// </summary>
    static void WriteOutputToLog()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiorchestrator"
        );
        Directory.CreateDirectory(directory);

        // Shared for writing: a second launch writes its one line beside the running server's
        // output before it hands over to the browser. Opened exclusively, it threw and the second
        // launch died before opening anything (measured on the packed app).
        StreamWriter log;
        try
        {
            log = new StreamWriter(
                new FileStream(
                    Path.Combine(directory, "server.log"),
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete
                )
            )
            {
                AutoFlush = true,
            };
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return;
        }

        log.WriteLine($"{DateTimeOffset.UtcNow:u} AI Orchestrator {typeof(Installation).Assembly.GetName().Version} pid {Environment.ProcessId}");

        var synchronized = TextWriter.Synchronized(log);
        Console.SetOut(synchronized);
        Console.SetError(synchronized);
    }
}

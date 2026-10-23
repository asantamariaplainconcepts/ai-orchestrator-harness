using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiOrchestrator.Server.Installed;

/// <summary>
/// <em>Update and restart</em> (#703, ADR-0010): <c>dotnet tool update</c> run by the app it updates,
/// then a restart into the new version. On Unix the running process keeps the files it opened while
/// the update writes a new folder under <c>.store</c>, so the update does not need it stopped first.
/// <para>
/// Started by the service, the restart is an exit with <see cref="RestartExitCode"/>, which launchd
/// and systemd answer by starting the tool's command, now the new version. Started by hand, the new
/// version is launched detached first; it finds this one on the port, asks it to stop and takes it.
/// </para>
/// </summary>
static class SelfUpdate
{
    /// <summary>EX_TEMPFAIL: not a success, so launchd's <c>SuccessfulExit: false</c> and systemd's <c>on-failure</c> restart it.</summary>
    public const int RestartExitCode = 75;

    /// <summary>What <c>Program</c> returns: 0, or <see cref="RestartExitCode"/> once a restart was asked for.</summary>
    public static int ExitCode { get; private set; }

    /// <summary>
    /// The arguments of <c>dotnet</c> that update this tool where it is installed: <c>-g</c> for the
    /// global tools folder, <c>--tool-path</c> for any other, which <c>-g</c> would not touch.
    /// </summary>
    public static string[] UpdateArguments(string toolRoot, string home) =>
        Path.GetFullPath(toolRoot).TrimEnd(Path.DirectorySeparatorChar)
            == Path.GetFullPath(Path.Combine(home, ".dotnet", "tools")).TrimEnd(Path.DirectorySeparatorChar)
            ? ["tool", "update", "-g", "aio"]
            : ["tool", "update", "--tool-path", toolRoot, "aio"];

    /// <summary>
    /// Stops this process for the new version to take its place, <paramref name="delay"/> after the
    /// answer leaves, as <em>Stop console</em> does.
    /// </summary>
    public static void Restart(IHostApplicationLifetime lifetime, bool startedByService, TimeSpan delay)
    {
        if (startedByService)
        {
            ExitCode = RestartExitCode;
        }
        else
        {
            LaunchDetached(Installation.ToolCommand!);
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            lifetime.StopApplication();
        });
    }

    /// <summary>
    /// The tool's command, outside this process's group and terminal, with no browser of its own:
    /// the page that pressed the button is already open and reloads when the new version answers.
    /// </summary>
    static void LaunchDetached(string command)
    {
        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(command) { UseShellExecute = true }
            : new ProcessStartInfo("/bin/sh", ["-c", "nohup \"$0\" >/dev/null 2>&1 &", command]) { UseShellExecute = false };

        start.Environment[Installation.BrowserVariable] = "none";
        using var _ = Process.Start(start);
    }

    public static RouteGroupBuilder MapInstall(this RouteGroupBuilder group)
    {
        group
            .MapPost(
                "/install",
                async Task<Results<Ok<UpdateInstall>, ProblemHttpResult>> (HttpContext context, IHostApplicationLifetime lifetime) =>
                {
                    if (LocalCaller.IsForeignPage(context))
                    {
                        return TypedResults.Problem("Only this app's own page installs an update.", statusCode: StatusCodes.Status403Forbidden);
                    }

                    if (Installation.How != "tool" || Installation.ToolRoot is not { } root)
                    {
                        return TypedResults.Problem(
                            "Only the installed tool updates itself. Started with dnx, the next dnx aio runs the new version; a checkout updates with git pull.",
                            statusCode: StatusCodes.Status409Conflict
                        );
                    }

                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var arguments = UpdateArguments(root, home);
                    var result = await Task.Run(() => Commands.Run("dotnet", arguments));
                    var output = $"$ dotnet {string.Join(' ', arguments)}\n{result.Output}";

                    if (result.Succeeded)
                    {
                        Restart(
                            lifetime,
                            startedByService: Environment.GetEnvironmentVariable(LoginService.ServiceVariable) is { Length: > 0 },
                            TimeSpan.FromMilliseconds(500)
                        );
                    }

                    return TypedResults.Ok(new UpdateInstall(result.Succeeded, output));
                }
            )
            .WithName("InstallUpdate");

        return group;
    }
}

/// <param name="Succeeded">True: the update installed and the app restarts into it.</param>
/// <param name="Output">The command and what it printed, for Settings to show as it was.</param>
sealed record UpdateInstall(bool Succeeded, string Output);

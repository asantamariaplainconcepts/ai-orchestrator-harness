using System.Diagnostics;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Pbis.Features.Vendor.Review;

namespace AiOrchestrator.Modules.Pbis.FunctionalTests;

/// <summary>
/// #719. The two ends of the evidence comment, crossed: what this checkout's
/// <c>.aio/steps/evidence</c> posts for an app's routes is what <c>EvidenceComments</c> reads as
/// captures. Each end had its own tests and the Review tab still showed none of aio-dogfood#43's eight,
/// because the script wrote a route where the reader expected a word.
/// <para>
/// Git and node are real. <c>gh</c>, <c>npx</c> and <c>curl</c> are scripts on PATH; the <c>gh</c>
/// appends each <c>--attach</c> to the body as GitHub does, <c>![&lt;alt&gt;](&lt;asset&gt;)</c> after a
/// blank line, which is the body aio-dogfood#43 carries.
/// </para>
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class EvidenceContractTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-evidence-contract-").FullName;

    string Work => Path.Combine(_root, "work");

    string Fake => Path.Combine(_root, "fake");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void The_alt_text_the_script_writes_for_a_route_is_read_as_a_capture()
    {
        Directory.CreateDirectory(Fake);
        Run(_root, "git", "init", "--quiet", "--bare", "origin.git");
        Run(_root, "git", "clone", "--quiet", "origin.git", "work");
        Run(Work, "git", "commit", "--quiet", "--allow-empty", "-m", "Initial revision");
        Run(Work, "git", "push", "--quiet", "origin", "HEAD:refs/heads/main");
        Run(Work, "git", "fetch", "--quiet");
        Run(Work, "git", "switch", "--quiet", "-c", "change/43");
        Directory.CreateDirectory(Path.Combine(Work, ".aio"));
        File.WriteAllText(
            Path.Combine(Work, ".aio", "config.json"),
            """{"evidence": {"paths": ["web/**"], "start": {"command": "sleep 30", "url": "http://localhost:8080"}, "routes": ["/", "/?day=tomorrow"]}}"""
        );
        Directory.CreateDirectory(Path.Combine(Work, "web"));
        File.WriteAllText(Path.Combine(Work, "web", "app.ts"), "export {};\n");
        Run(Work, "git", "add", "-A");
        Run(Work, "git", "commit", "--quiet", "-m", "Touch the app");
        var head = Run(Work, "git", "rev-parse", "HEAD");

        Executable("curl", "exit 0");
        Executable("npx", """[ "$3" = screenshot ] && printf png > "${@: -1}"; exit 0""");
        Executable("gh", """
            case "$1 $2" in
              "pr view") echo main; exit 0 ;;
              "issue comment")
                n=0
                while [ $# -gt 0 ]; do
                  case "$1" in
                    --body-file) cat "$2" > "$FAKE/body" ;;
                    --attach) n=$((n + 1)); printf '\n\n![%s](https://github.com/user-attachments/assets/%08d-0000-4000-8000-000000000000)' "${2#*#}" "$n" >> "$FAKE/attached" ;;
                  esac
                  shift
                done
                cat "$FAKE/attached" >> "$FAKE/body"
                echo "https://github.com/owner/repo/issues/43#issuecomment-1"; exit 0 ;;
            esac
            exit 1
            """);

        var said = Run(Work, "node", Path.Combine(SelfCheckout.Root()!, ".aio", "steps", "evidence"), "43");

        said.ShouldContain("\"published\":true");
        var batch = EvidenceComments.Newest([new PbiComment("1", "aio-bot", DateTimeOffset.UtcNow, File.ReadAllText(Path.Combine(Fake, "body")))]);
        batch.HeadSha.ShouldBe(head);
        var day = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        batch.Captures.Select(capture => $"{capture.Screen} {capture.Width}x{capture.Height} {capture.Theme}").ShouldBe([
            "/ 1440x900 light",
            "/ 1440x900 dark",
            "/ 390x844 light",
            "/ 390x844 dark",
            $"/?day={day} 1440x900 light",
            $"/?day={day} 1440x900 dark",
            $"/?day={day} 390x844 light",
            $"/?day={day} 390x844 dark",
        ]);
    }

    void Executable(string name, string body)
    {
        var path = Path.Combine(Fake, name);
        File.WriteAllText(path, $"#!/usr/bin/env bash\n{body}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    string Run(string directory, string command, params string[] arguments)
    {
        var start = new ProcessStartInfo(command)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["FAKE"] = Fake;
        start.Environment["PATH"] = $"{Fake}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "test";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"{command} {string.Join(' ', arguments)}: {errors.Result}");
        return output.Result.Trim();
    }
}

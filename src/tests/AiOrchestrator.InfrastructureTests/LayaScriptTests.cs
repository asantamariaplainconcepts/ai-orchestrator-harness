using System.Diagnostics;
using System.Security.Cryptography;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #757. <c>.aio/scorers/laya/laya</c> from this repository (#759), copied into two checkouts, against a
/// temporary <c>HOME</c> and fakes on <c>PATH</c>: a <c>python3</c> whose <c>venv</c> and
/// <c>pip install</c> only make files and record each install, a <c>laya-serve</c> that sleeps and
/// leaves its pid where the fake <c>curl</c> reads it as healthy. Nothing here reaches the real
/// <c>~/.aiorchestrator</c> or the server on 8317.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LayaScriptTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-laya-").FullName;

    string Home => Path.Combine(_root, "home");

    string Fakes => Path.Combine(_root, "fakes");

    string Laya => Path.Combine(Home, ".aiorchestrator", "laya");

    string Installs => Path.Combine(Home, "pip-installs");

    public LayaScriptTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Fakes);

        // Every name find_python tries, so a real python3.12 on the runner is never the one picked.
        foreach (var name in new[] { "python3", "python3.10", "python3.11", "python3.12" })
        {
            Fake(name, """
                case "$1 $2" in
                  "-c "*) exit 0 ;;
                  "-m venv") mkdir -p "$3/bin" && cp "$LAYA_FAKES/python3" "$3/bin/python" ;;
                  "-m pip") sleep 1; echo "$(dirname "$0")" >>"$HOME/pip-installs"
                            cp "$LAYA_FAKES/laya-serve" "$(dirname "$0")/laya-serve" ;;
                  *) exit 1 ;;
                esac
                """);
        }

        Fake("laya-serve", """
            echo $$ >"$HOME/serving"
            exec sleep 600
            """);
        Fake("curl", """
            pid=$(cat "$HOME/serving" 2>/dev/null) && kill -0 "$pid" 2>/dev/null
            """);

        Checkout("a");
        Checkout("b");
    }

    public void Dispose()
    {
        if (File.Exists(Path.Combine(Home, "serving"))
            && int.TryParse(File.ReadAllText(Path.Combine(Home, "serving")).Trim(), out var pid))
        {
            try { Process.GetProcessById(pid).Kill(); } catch (ArgumentException) { } catch (InvalidOperationException) { }
        }

        Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// The first checkout builds the environment in the machine's cache, the second stops the
    /// server the first started and starts it again from the same venv, and pip ran once.
    /// </summary>
    [Fact]
    public void Two_checkouts_share_one_environment_and_one_server()
    {
        var pin = Pin("a");

        Ensure("a");
        var started = File.ReadAllText(Path.Combine(Home, "serving")).Trim();
        File.ReadAllText(Path.Combine(Laya, pin, "laya-serve.pid")).Trim().ShouldBe(started);

        var (code, stdout, _) = Run("b", "stop");
        code.ShouldBe(0);
        stdout.ShouldContain($"stopped pid {started}");

        Ensure("b");

        File.ReadAllLines(Installs).ShouldBe([Path.Combine(Laya, pin, "venv", "bin")]);
        Directory.GetDirectories(Laya).Select(Path.GetFileName).ShouldBe([pin]);
        NothingWrittenUnderHarness();
        Run("b", "stop").Code.ShouldBe(0);
    }

    /// <summary>A pin edit builds a second directory beside the first and leaves the first as it was.</summary>
    [Fact]
    public void A_changed_pin_builds_a_second_environment_and_keeps_the_first()
    {
        Ensure("a");
        Run("a", "stop").Code.ShouldBe(0);
        var first = Pin("a");
        var before = Listing(Path.Combine(Laya, first, "venv"));

        File.AppendAllText(Path.Combine(Scorer("b"), "laya-requirements.txt"), "numpy==2.1.0\n");
        Ensure("b");

        Pin("b").ShouldNotBe(first);
        Directory.GetDirectories(Laya).Select(Path.GetFileName).ShouldBe([first, Pin("b")], ignoreOrder: true);
        Listing(Path.Combine(Laya, first, "venv")).ShouldBe(before);
        File.ReadAllLines(Installs).Length.ShouldBe(2);
        NothingWrittenUnderHarness();
        Run("b", "stop").Code.ShouldBe(0);
    }

    /// <summary>Two ensures at the same moment: one takes the lock and builds, the other waits and uses it.</summary>
    [Fact]
    public async Task Two_ensures_at_once_build_once()
    {
        var both = await Task.WhenAll(Task.Run(() => Run("a", "ensure")), Task.Run(() => Run("b", "ensure")));

        both.ShouldAllBe(result => result.Code == 0);
        File.ReadAllLines(Installs).Length.ShouldBe(1);
        Directory.Exists(Path.Combine(Laya, Pin("a"), ".lock")).ShouldBeFalse();
        NothingWrittenUnderHarness();
        Run("a", "stop").Code.ShouldBe(0);
    }

    void Ensure(string checkout)
    {
        var (code, stdout, stderr) = Run(checkout, "ensure");
        code.ShouldBe(0, $"{stdout}\n{stderr}");
        stdout.ShouldContain("laya: healthy at http://127.0.0.1:8317");
    }

    void NothingWrittenUnderHarness()
    {
        foreach (var checkout in new[] { "a", "b" })
        {
            Directory.GetFileSystemEntries(Path.Combine(_root, checkout, ".aio"))
                .Select(Path.GetFileName)
                .ShouldBe(["scorers"]);
            Directory.GetFileSystemEntries(Path.Combine(_root, checkout, ".aio", "scorers"))
                .Select(Path.GetFileName)
                .ShouldBe(["laya"]);
            Directory.GetFileSystemEntries(Scorer(checkout))
                .Select(Path.GetFileName)
                .ShouldBe(["laya", "laya-requirements.txt"], ignoreOrder: true);
        }
    }

    /// <summary>The first 12 hex characters of the SHA-256 of the checkout's requirements, as the script names its directory.</summary>
    string Pin(string checkout) =>
        Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(Path.Combine(Scorer(checkout), "laya-requirements.txt")))
        )[..12];

    static string[] Listing(string directory) =>
        [.. Directory.GetFileSystemEntries(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

    /// <summary>laya's folder in the checkout, where the package puts it.</summary>
    string Scorer(string checkout) => Path.Combine(_root, checkout, ".aio", "scorers", "laya");

    void Checkout(string name)
    {
        Directory.CreateDirectory(Scorer(name));
        foreach (var file in new[] { "laya", "laya-requirements.txt" })
        {
            File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "scorers", "laya", file), Path.Combine(Scorer(name), file));
        }
    }

    void Fake(string name, string body)
    {
        var path = Path.Combine(Fakes, name);
        File.WriteAllText(path, $"#!/bin/bash\n{body}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    (int Code, string Stdout, string Stderr) Run(string checkout, string verb)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = Path.Combine(_root, checkout),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(Scorer(checkout), "laya"));
        start.ArgumentList.Add(verb);
        start.Environment["HOME"] = Home;
        start.Environment["PATH"] = $"{Fakes}:/usr/bin:/bin";
        start.Environment["LAYA_FAKES"] = Fakes;

        using var process = Process.Start(start)!;
        var output = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        process.WaitForExit(TimeSpan.FromSeconds(60)).ShouldBeTrue($"laya {verb} did not finish in 60 s");
        return (process.ExitCode, output.Result[0], output.Result[1]);
    }
}

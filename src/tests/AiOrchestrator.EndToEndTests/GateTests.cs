using System.Diagnostics;
using System.Text.Json;
using AiOrchestrator.AppHost.CISteps;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The gate's own code (<c>CISteps/</c> and <c>.aio/gate</c>) against checkouts of the
/// test's own, with a bare <c>origin</c> whose <c>master</c> is the trunk. Running <c>aspire do ci</c>
/// inside the gate is the one thing these do not do: each test runs the step that keeps the promise.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GateTests : IDisposable
{
    readonly Checkout _checkout = new();

    public void Dispose() => _checkout.Dispose();

    /// <summary>
    /// The build step against a project that inherits this repository's <c>Directory.Build.props</c>
    /// and declares an unused variable: CS0219 is a warning, the props make it an error, and the step
    /// throws, which is what turns <c>aspire do ci</c> red.
    /// </summary>
    [Fact]
    public async Task A_warning_fails_the_build_step()
    {
        var project = Directory.CreateDirectory(Path.Combine(_checkout.Root, "warns")).FullName;
        File.WriteAllText(
            Path.Combine(project, "Directory.Build.props"),
            $"<Project><Import Project=\"{Path.Combine(Repository.Root, "src", "Directory.Build.props")}\" /></Project>\n"
        );
        File.WriteAllText(Path.Combine(project, "Warns.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        File.WriteAllText(
            Path.Combine(project, "Thing.cs"),
            "namespace Warns;\n\npublic static class Thing\n{\n    public static int One()\n    {\n        var unused = 2;\n        return 1;\n    }\n}\n"
        );
        var logger = new LinesLogger();

        var failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            StepCommand.Run("dotnet", ["build", Path.Combine(project, "Warns.csproj"), "--disable-build-servers"], project, logger, CancellationToken.None)
        );

        failure.Message.ShouldContain("exited 1");
        logger.Lines.ShouldContain(line => line.Contains("error CS0219", StringComparison.Ordinal));
    }

    /// <summary>
    /// The summary the test step writes, from the verdict lines <c>dotnet test</c> prints, read by the
    /// loop's gate after an <c>aspire</c> that leaves it: the why names the assemblies and the tests.
    /// </summary>
    [Fact]
    public void The_gate_names_the_assemblies_and_the_tests_the_test_step_counted()
    {
        string[] printed =
        [
            "Passed!  - Failed:     0, Passed:   212, Skipped:     0, Total:   212, Duration: 41 s - AiOrchestrator.Modules.Loop.FunctionalTests.dll (net10.0)",
            "Build succeeded.",
            "Passed!  - Failed:     0, Passed:    38, Skipped:     1, Total:    39, Duration: 2 s - AiOrchestrator.ArchitectureTests.dll (net10.0)",
        ];
        var summary = new TestSummary(
            [.. TestStep.ParseVerdicts(printed).Select(verdict =>
                new TestAssembly(verdict.Project, verdict.Passed, verdict.Failed, verdict.PassedTests, verdict.Skipped, []))],
            DateTimeOffset.UtcNow
        );
        File.WriteAllText(Path.Combine(_checkout.Root, "summary.json"), JsonSerializer.Serialize(summary, AppHost.CISteps.Json.Options));
        _checkout.Write("src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj", "<Project />\n");
        _checkout.Write(".gitignore", "src/artifacts/\n");
        _checkout.Commit("Carry an AppHost");
        var fake = Directory.CreateDirectory(Path.Combine(_checkout.Root, "fake")).FullName;
        File.WriteAllText(
            Path.Combine(fake, "aspire"),
            $"#!/usr/bin/env bash\nmkdir -p src/artifacts\ncp '{Path.Combine(_checkout.Root, "summary.json")}' src/artifacts/ci-summary.json\necho '✓ 14/14 steps succeeded • Total time: 2m 44s'\n"
        );
        File.SetUnixFileMode(Path.Combine(fake, "aspire"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var (code, stdout, stderr) = Checkout.Process(
            _checkout.Work,
            Path.Combine(Repository.Root, ".aio", "gate"),
            [],
            new Dictionary<string, string>
            {
                ["PATH"] = $"{fake}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}",
                ["TMPDIR"] = _checkout.Root,
            }
        );

        code.ShouldBe(0, stdout + stderr);
        using var line = JsonDocument.Parse(stdout.Trim().Split('\n')[^1]);
        line.RootElement.GetProperty("outcome").GetString().ShouldBe("green");
        line.RootElement.GetProperty("why").GetString()!.ShouldStartWith(
            "aspire do ci passed: 14/14 steps succeeded, 2 test assemblies, 250 tests passed, 0 failed. Log: "
        );
    }
}

/// <summary>
/// The mutation step with the real <c>dotnet</c> restoring a two-project solution, and Stryker played
/// by a script: the copy has no tool manifest, so <c>dotnet dotnet-stryker</c> runs the
/// <c>dotnet-dotnet-stryker</c> it finds on <c>PATH</c>. What is tested is the step's choice of
/// project, copy and report. <c>PATH</c> belongs to the process, so this collection runs alone,
/// after every other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MutationCollection
{
    public const string Name = "mutation-step";
}

[Collection(MutationCollection.Name)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class MutationStepTests : IDisposable
{
    readonly Checkout _checkout = new();
    readonly string? _path = Environment.GetEnvironmentVariable("PATH");
    readonly string? _since = Environment.GetEnvironmentVariable("MUTATION_SINCE");
    readonly string? _nodeReuse = Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE");
    readonly string _fake;

    public MutationStepTests()
    {
        _checkout.Write("src/Tiny.slnx", "<Solution>\n  <Project Path=\"Lib/Lib.csproj\" />\n  <Project Path=\"Lib.Tests/Lib.Tests.csproj\" />\n</Solution>\n");
        _checkout.Write("src/Directory.Build.props", "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        _checkout.Write("src/Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        _checkout.Write("src/Lib/Thing.cs", "namespace Lib;\n\npublic static class Thing\n{\n    public static int Add(int a, int b) => a + b;\n}\n");
        _checkout.Write(
            "src/Lib.Tests/Lib.Tests.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Lib\\Lib.csproj\" />\n  </ItemGroup>\n</Project>\n"
        );
        _checkout.Commit("The trunk");
        var since = _checkout.Git("rev-parse", "HEAD");
        _checkout.Write("src/Lib/Thing.cs", "namespace Lib;\n\npublic static class Thing\n{\n    public static int Add(int a, int b) => a - b + b + a - a;\n}\n");

        _fake = Directory.CreateDirectory(Path.Combine(_checkout.Root, "fake")).FullName;
        var report = """{"files": {"/copy/src/Lib/Thing.cs": {"mutants": [{"status": "Killed", "location": {"start": {"line": 5}}, "mutatorName": "Arithmetic"}, {"status": "Survived", "location": {"start": {"line": 5}}, "mutatorName": "Arithmetic", "replacement": "a + b"}]}}}""";
        var stryker = Path.Combine(_fake, "dotnet-dotnet-stryker");
        File.WriteAllText(
            stryker,
            $$"""
            #!/usr/bin/env bash
            echo "$PWD" > '{{_fake}}/stryker-ran-in'
            [ -f '{{_fake}}/fails' ] && exit 1
            while [ $# -gt 0 ]; do [ "$1" = --output ] && out=$2; shift; done
            mkdir -p "$out/reports"
            echo '{{report}}' > "$out/reports/mutation-report.json"
            """
        );
        File.SetUnixFileMode(stryker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Environment.SetEnvironmentVariable("PATH", $"{_fake}{Path.PathSeparator}{_path}");
        Environment.SetEnvironmentVariable("MUTATION_SINCE", since);
        // An MSBuild node the restore leaves behind holds the step's output pipe open, and the step
        // waits on it until the budget runs out.
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _path);
        Environment.SetEnvironmentVariable("MUTATION_SINCE", _since);
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", _nodeReuse);
        _checkout.Dispose();
    }

    string Output => Path.Combine(_checkout.Work, "src", "artifacts", "mutation");

    [Fact]
    public async Task A_changed_project_with_tests_is_mutated_in_a_copy_and_reported_beside_the_summary()
    {
        var logger = new LinesLogger();

        await MutationStep.Run(Path.Combine(_checkout.Work, "src", "Tiny.slnx"), logger, CancellationToken.None);

        var ranIn = File.ReadAllText(Path.Combine(_fake, "stryker-ran-in")).Trim();
        ranIn.ShouldStartWith(Path.Combine(MutationStep.RealPath(Path.GetTempPath()), "aio-mutation-"));
        ranIn.ShouldEndWith(Path.Combine("Lib", "src"));
        Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(ranIn))).ShouldBeFalse("the copies are removed");
        logger.Lines.ShouldContain(line => line.StartsWith("Lib: mutation score 50.0% (1 of 2 mutants detected)", StringComparison.Ordinal));
        File.Exists(Path.Combine(Output, "Lib", "reports", "mutation-report.json")).ShouldBeTrue();
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "summary.json")));
        var project = summary.RootElement.GetProperty("projects").EnumerateArray().Single();
        project.GetProperty("project").GetString().ShouldBe("Lib");
        project.GetProperty("measured").GetBoolean().ShouldBeTrue();
        project.GetProperty("score").GetDouble().ShouldBe(50.0);
        project.GetProperty("survivors").EnumerateArray().Single().GetProperty("file").GetString().ShouldBe("src/Lib/Thing.cs");
    }

    [Fact]
    public async Task Stryker_exiting_non_zero_is_recorded_as_not_measured_and_the_step_passes()
    {
        File.WriteAllText(Path.Combine(_fake, "fails"), string.Empty);
        var logger = new LinesLogger();

        await MutationStep.Run(Path.Combine(_checkout.Work, "src", "Tiny.slnx"), logger, CancellationToken.None);

        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "summary.json")));
        var project = summary.RootElement.GetProperty("projects").EnumerateArray().Single();
        project.GetProperty("project").GetString().ShouldBe("Lib");
        project.GetProperty("measured").GetBoolean().ShouldBeFalse();
        project.GetProperty("reason").GetString().ShouldBe("Stryker exited 1");
        logger.Lines.ShouldContain("Lib: mutation not measured — Stryker exited 1");
    }
}

/// <summary>A clone of a bare origin under a temporary directory, with git as the tests' own identity.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
sealed class Checkout : IDisposable
{
    public string Root { get; } = MutationStep.RealPath(Directory.CreateTempSubdirectory("aio-gate-").FullName);

    public string Work => Path.Combine(Root, "work");

    public Checkout()
    {
        GitIn(Root, "init", "--quiet", "--bare", "-b", "master", "origin.git");
        GitIn(Root, "clone", "--quiet", "origin.git", "work");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    public void Write(string relative, string text)
    {
        var path = Path.Combine(Work, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void Commit(string message)
    {
        Git("add", "-A");
        Git("commit", "--quiet", "-m", message);
    }

    public string Git(params string[] arguments) => GitIn(Work, arguments);

    static string GitIn(string directory, params string[] arguments)
    {
        var (code, stdout, stderr) = Process(directory, "git", arguments);
        code.ShouldBe(0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout.Trim();
    }

    public static (int Code, string Stdout, string Stderr) Process(
        string directory,
        string file,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? variables = null
    )
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "Gate test";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "gate@example.test";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        foreach (var (name, value) in variables ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{file} did not finish in 60 s");
        }

        return (process.ExitCode, stdout.Result, stderr.Result);
    }
}

/// <summary>This checkout, found from the test binary by its <c>.aio</c>.</summary>
static class Repository
{
    public static string Root { get; } = Find();

    static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".aio")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("No .aio directory above the test binary.");
    }
}

/// <summary>Every line a step logged, formatted as the step's log prints it.</summary>
sealed class LinesLogger : ILogger
{
    readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lines)
        {
            _lines.Add(formatter(state, exception));
        }
    }
}

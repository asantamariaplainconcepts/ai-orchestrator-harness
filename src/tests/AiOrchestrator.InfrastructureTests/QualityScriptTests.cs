using System.Diagnostics;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #746. <c>.aio/steps/quality</c>, run from this repository against a temporary checkout with a bare
/// <c>origin</c>: a change to one method on <c>change/7</c> against <c>main</c>, with the reports the gate
/// would have left written by the test. Git and the package's checks are real; <c>gh</c>, the
/// <c>npx</c> that would run jscpd and the <c>dotnet</c> that would build and run the old tests are scripts
/// on PATH that answer from files and write down the comment. The issue's Dónde cae names <c>src/</c>.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class QualityScriptTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-quality-").FullName;

    string Work => Path.Combine(_root, "work");

    string Fake => Path.Combine(_root, "fake");

    const string Quality = """
        {
          "coverage": { "report": "src/artifacts/coverage/Cobertura.xml", "min": 70 },
          "crap": { "report": "src/artifacts/coverage/crap.tsv", "max": 30 },
          "mutation": { "report": "src/artifacts/mutation/summary.json" },
          "duplication": { "paths": ["src"], "max": 0 },
          "tests": { "max": 0 },
          "touched": { "max": 0 },
          "outside": { "max": 0 },
          "old-tests": { "max": 0 }
        }
        """;

    const string Issue = """
        ## Qué tiene que pasar

        Thing decide otra cosa.

        ## Dónde cae

        - `src/`
        """;

    const string Tests = """
        namespace Tiny;

        public sealed class ThingTests
        {
            [Fact]
            public void Decides() { }

            [Fact]
            public void Falls_back_to_zero() { }
        }
        """;

    public QualityScriptTests()
    {
        Directory.CreateDirectory(Fake);
        Git(_root, "init", "--quiet", "--bare", "origin.git");
        Git(_root, "clone", "--quiet", "origin.git", "work");
        Write(".gitignore", "src/artifacts/\n");
        Write(".aio/config.json", "{ \"quality\": " + Quality + " }");
        Write("src/Thing.cs", Method("return a;"));
        Write("tests/ThingTests.cs", Tests);
        Write("tests/Tiny.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        Commit("The trunk");
        Git(Work, "push", "--quiet", "origin", "HEAD:refs/heads/main");
        Git(Work, "fetch", "--quiet");
        Git(Work, "switch", "--quiet", "-c", "change/7");

        Executable("gh", """
            #!/usr/bin/env bash
            case "$1 $2" in
              "pr view") echo main; exit 0 ;;
              "issue view") cat "$FAKE/issue"; exit 0 ;;
              "issue comment")
                while [ $# -gt 0 ]; do [ "$1" = --body-file ] && cp "$2" "$FAKE/body"; shift; done
                echo "https://github.com/owner/repo/issues/7#issuecomment-746"; exit 0 ;;
            esac
            echo "fake gh: $*" >&2; exit 1
            """);
        // jscpd's report: $FAKE/jscpd-head.json for the checkout, which has .git, and $FAKE/jscpd-base.json
        // for the base's tree; none is no clone.
        Executable("npx", """
            #!/usr/bin/env bash
            echo "npx $* in $PWD" >> "$FAKE/calls"
            while [ $# -gt 0 ]; do [ "$1" = --output ] && out="$2"; shift; done
            mkdir -p "$out"
            side=$([ -e .git ] && echo head || echo base)
            cat "$FAKE/jscpd-$side.json" 2>/dev/null > "$out/jscpd-report.json" || echo '{"duplicates":[]}' > "$out/jscpd-report.json"
            """);
        // The old tests build, and run two tests; with $FAKE/failing, ThingTests.Decides fails.
        Executable("dotnet", """
            #!/usr/bin/env bash
            echo "dotnet $*" >> "$FAKE/dotnet-calls"
            [ "$1" = test ] || exit 0
            if [ -e "$FAKE/failing" ]; then
              echo "  Failed ThingTests.Decides [3 ms]"; echo "Total tests: 2"; exit 1
            fi
            echo "  Passed ThingTests.Decides [3 ms]"; echo "Total tests: 2"
            """);
        File.WriteAllText(Path.Combine(Fake, "issue"), Issue);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// A change that rewrites a method into one of CRAP 42, adds four lines a test runs one of, adds a
    /// file that copies a 12-line block and deletes a test: each row says so, and is over its line.
    /// The comment opens with the marker and the head; the line is <c>reported</c>, exit 0.
    /// </summary>
    [Fact]
    public void A_hot_method_uncovered_lines_a_copied_block_and_a_lost_test_are_each_over_their_line()
    {
        Write("src/Thing.cs", Method("if (a > 0)\n        {\n            return a;\n        }\n        return b > 0 ? b : 0;"));
        Write("src/Copy.cs", "namespace Tiny;\n" + string.Concat(Enumerable.Range(1, 12).Select(line => $"// copied line {line}\n")));
        Write("tests/ThingTests.cs", Tests.Replace("    [Fact]\n    public void Falls_back_to_zero() { }\n", "", StringComparison.Ordinal));
        Commit("Rewrite the method, copy a block, drop a test");
        var head = Git(Work, "rev-parse", "HEAD");
        var trunk = Git(Work, "rev-parse", "origin/main");
        Reports(head, crap: "42.0\t6\t25.0\tsrc/Thing.cs\t6\t11\tTiny.Thing::Decide(System.Int32,System.Int32)", hits: [1, 0, 0, 0]);
        Write("src/artifacts/mutation/summary.json", $$"""
            {"measured": true, "reason": null, "since": "{{trunk}}", "budgetMinutes": 10, "projects": [
              {"project": "Tiny", "measured": true, "reason": null, "score": 55.0, "detected": 11, "valid": 20, "counts": {}, "survivors": [], "report": null}]}
            """);
        File.WriteAllText(Path.Combine(Fake, "jscpd-head.json"), """
            {"duplicates": [{"format": "csharp", "lines": 12, "tokens": 80,
              "firstFile": {"name": "src/Copy.cs", "start": 2, "end": 13},
              "secondFile": {"name": "src/Original.cs", "start": 40, "end": 51}}]}
            """);

        var (code, line) = Run("7");

        code.ShouldBe(0, line.ToString());
        line.GetProperty("outcome").GetString().ShouldBe("reported");
        line.GetProperty("published").GetBoolean().ShouldBeTrue();
        line.GetProperty("comment").GetString().ShouldBe("https://github.com/owner/repo/issues/7#issuecomment-746");
        line.GetProperty("why").GetString().ShouldBe($"Published the quality of {head[..7]} in https://github.com/owner/repo/issues/7#issuecomment-746: 6 over, 0 not measured.");

        var body = Body();
        body.ShouldStartWith($"<!-- aio:quality {head} -->\nQuality of `{head[..7]}` against `{trunk[..7]}` for #7: 6 over, 0 not measured.");
        // Eight checks, under the header and its separator.
        body.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal)).Skip(2).Count().ShouldBe(8);
        Row(body, "Coverage of changed lines").ShouldBe(["25.0%: 1 of 4 changed lines covered", "70%", "over"]);
        Row(body, "CRAP of touched methods").ShouldBe(["highest 42.0 of 1 method, 1 over 30", "30", "over"]);
        Row(body, "Mutation in touched files").ShouldBe(["55.0%: 11 of 20 mutants killed", "reported only", "ok"]);
        Row(body, "New duplicated lines").ShouldBe(["12 lines in 1 file: src/Copy.cs 12", "0", "over"]);
        Row(body, "Tests lost or skipped").ShouldBe(["1 lost: ThingTests.Falls_back_to_zero; 0 skipped", "0", "over"]);
        // The test file stood at the base and sits outside src/, the only path the issue declares.
        Row(body, "Tests touched").ShouldBe(["1: tests/ThingTests.cs (M)", "0", "over"]);
        Row(body, "Files outside the issue's paths").ShouldBe(["1 file: tests/ThingTests.cs", "0", "over"]);
        Row(body, "New code on old tests only").ShouldBe(["no old test fails against the new code", "0", "ok"]);
        // jscpd ran on the head, in the checkout, and on the base, in a tree of its own.
        File.ReadAllLines(Path.Combine(Fake, "calls")).Select(call => call.EndsWith("/work", StringComparison.Ordinal)).ShouldBe([true, false]);
        File.ReadAllText(Path.Combine(Fake, "calls")).ShouldContain("--yes jscpd@4.0.5 --min-lines 10 --min-tokens 70");
    }

    /// <summary>No Cobertura.xml where quality in config.json says: the row is not measured and says where it looked.</summary>
    [Fact]
    public void Without_a_Cobertura_report_coverage_is_not_measured_and_says_where_it_looked()
    {
        Write("src/Thing.cs", Method("return b;"));
        Commit("Touch the method");
        var head = Git(Work, "rev-parse", "HEAD");
        Reports(head, crap: "2.0\t1\t100.0\tsrc/Thing.cs\t6\t8\tTiny.Thing::Decide(System.Int32,System.Int32)", hits: null);

        var (code, line) = Run("7");

        code.ShouldBe(0);
        line.GetProperty("why").GetString()!.ShouldEndWith(": 0 over, 2 not measured.");
        var body = Body();
        Row(body, "Coverage of changed lines").ShouldBe(["no Cobertura.xml at src/artifacts/coverage/Cobertura.xml", "70%", "not measured"]);
        Row(body, "CRAP of touched methods").ShouldBe(["highest 2.0 of 1 method, 0 over 30", "30", "ok"]);
        Row(body, "Mutation in touched files").ShouldBe(["no summary.json at src/artifacts/mutation/summary.json", "reported only", "not measured"]);
        Row(body, "Tests lost or skipped").ShouldBe(["none lost or skipped", "0", "ok"]);
    }

    /// <summary>A crap.tsv another commit left: CRAP and the coverage beside it say which commit they measured.</summary>
    [Fact]
    public void A_crap_report_of_another_commit_says_which_commit_it_measured()
    {
        Write("src/Thing.cs", Method("return b;"));
        Commit("Touch the method");
        var head = Git(Work, "rev-parse", "HEAD");
        const string Other = "1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d";
        Reports(Other, crap: "42.0\t6\t0.0\tsrc/Thing.cs\t6\t8\tTiny.Thing::Decide(System.Int32,System.Int32)", hits: [1, 1, 1]);

        Run("7").Code.ShouldBe(0);

        var body = Body();
        Row(body, "CRAP of touched methods").ShouldBe([$"src/artifacts/coverage/crap.tsv was measured on 1a2b3c4, not {head[..7]}", "30", "not measured"]);
        Row(body, "Coverage of changed lines").ShouldBe(
            [$"src/artifacts/coverage/Cobertura.xml was measured on 1a2b3c4, not on the head {head[..7]}", "70%", "not measured"]
        );
    }

    /// <summary>
    /// A change that edits only <c>src/Thing.cs</c>, inside the issue's Dónde cae: no test touched, none
    /// to run in its base form, so nothing is built, and no file outside.
    /// </summary>
    [Fact]
    public void A_change_to_source_only_touches_no_test_and_stays_inside_the_issue()
    {
        Write("src/Thing.cs", Method("return b;"));
        Commit("Touch the method");

        Run("7").Code.ShouldBe(0);

        var body = Body();
        Row(body, "Tests touched").ShouldBe(["no test that stood at the base touched", "0", "ok"]);
        Row(body, "New code on old tests only").ShouldBe(["no old test fails against the new code", "0", "ok"]);
        Row(body, "Files outside the issue's paths").ShouldBe(["every file inside the issue's paths", "0", "ok"]);
        File.Exists(Path.Combine(Fake, "dotnet-calls")).ShouldBeFalse();
    }

    /// <summary>
    /// A change that edits the test that stood at the base, outside the issue's <c>src/</c>, and whose base
    /// form fails <c>ThingTests.Decides</c> against the head: each of the three rows names it, over its line.
    /// </summary>
    [Fact]
    public void An_old_test_edited_to_agree_with_new_code_is_touched_outside_and_fails_in_its_base_form()
    {
        Write("src/Thing.cs", Method("return b;"));
        Write("tests/ThingTests.cs", Tests.Replace("public void Decides() { }", "public void Decides() { var b = 1; }", StringComparison.Ordinal));
        Commit("Change what Thing decides, and the test with it");
        File.WriteAllText(Path.Combine(Fake, "failing"), "");

        Run("7").Code.ShouldBe(0);

        var body = Body();
        Row(body, "Tests touched").ShouldBe(["1: tests/ThingTests.cs (M)", "0", "over"]);
        Row(body, "New code on old tests only").ShouldBe(["1 test failing in its base form: ThingTests.Decides", "0", "over"]);
        Row(body, "Files outside the issue's paths").ShouldBe(["1 file: tests/ThingTests.cs", "0", "over"]);
        // The old test was built and run, once each, filtered to its class.
        var calls = File.ReadAllLines(Path.Combine(Fake, "dotnet-calls"));
        calls.Select(call => call.Split(' ')[1]).ShouldBe(["build", "test"]);
        calls[1].ShouldContain("--filter FullyQualifiedName~.ThingTests.");
    }

    /// <summary>An issue whose body has no Dónde cae: the outside row is not measured and says why.</summary>
    [Fact]
    public void Without_a_Donde_cae_files_outside_the_issue_are_not_measured()
    {
        File.WriteAllText(Path.Combine(Fake, "issue"), "## Qué tiene que pasar\n\nThing decide otra cosa.\n");
        Write("src/Thing.cs", Method("return b;"));
        Commit("Touch the method");

        Run("7").Code.ShouldBe(0);

        Row(Body(), "Files outside the issue's paths").ShouldBe(["the issue declares no file-set (CHECK_DECLARED)", "0", "not measured"]);
    }

    /// <summary>
    /// The reports the gate leaves: a crap.tsv stamped with <paramref name="commit"/> holding one method,
    /// and, unless <paramref name="hits"/> is null, a Cobertura.xml whose Thing.cs lines from 7 on, the method's body, were hit
    /// that many times, at the absolute path ReportGenerator writes.
    /// </summary>
    void Reports(string commit, string crap, int[]? hits)
    {
        Write("src/artifacts/coverage/crap.tsv", $"# commit\t{commit}\n# crap\tcomplexity\tcoverage\tfile\tfirst\tlast\tmethod\n{crap}\n");
        if (hits is null)
        {
            return;
        }

        var lines = string.Concat(hits.Select((hit, index) => $"<line number=\"{7 + index}\" hits=\"{hit}\" branch=\"false\" />"));
        Write("src/artifacts/coverage/Cobertura.xml", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage><sources><source>{Work}/src/</source></sources><packages><package name="Tiny"><classes>
            <class name="Tiny.Thing" filename="{Path.Combine(Work, "src", "Thing.cs")}"><methods><method name="Decide"><lines>{lines}</lines></method></methods><lines>{lines}</lines></class>
            </classes></package></packages></coverage>
            """);
    }

    /// <summary>A class whose method's body starts on line 7 and is <paramref name="body"/>.</summary>
    static string Method(string body) =>
        $"namespace Tiny;\n\npublic static class Thing\n{{\n    public static int Decide(int a, int b)\n    {{\n        {body}\n    }}\n}}\n";

    string Body() => File.ReadAllText(Path.Combine(Fake, "body"));

    /// <summary>The measured, line and result cells of the table row of <paramref name="check"/>.</summary>
    static string[] Row(string body, string check) =>
        body.Split('\n').Single(line => line.StartsWith($"| {check} |", StringComparison.Ordinal))
            .Split('|', StringSplitOptions.TrimEntries)[2..5];

    void Write(string relative, string text)
    {
        var path = Path.Combine(Work, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    void Commit(string message)
    {
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", message);
    }

    void Executable(string name, string text)
    {
        var path = Path.Combine(Fake, name);
        File.WriteAllText(path, text);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    (int Code, JsonElement Line) Run(string issue)
    {
        var script = HarnessScripts.Find(ThisRepository.Root(), "quality") ?? throw new FileNotFoundException("This checkout has no quality.");
        var (code, stdout, stderr) = Process(Work, script, [issue]);
        var last = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        last.ShouldNotBeNull($"quality printed no line: {stderr}");
        using var document = JsonDocument.Parse(last);
        return (code, document.RootElement.Clone());
    }

    string Git(string directory, params string[] arguments)
    {
        var (code, stdout, stderr) = Process(directory, "git", arguments);
        code.ShouldBe(0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout.Trim();
    }

    (int Code, string Stdout, string Stderr) Process(string directory, string file, IReadOnlyList<string> arguments)
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

        start.Environment["PATH"] = $"{Fake}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
        start.Environment["FAKE"] = Fake;
        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "Quality script test";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "quality@example.test";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";

        using var process = System.Diagnostics.Process.Start(start)!;
        var output = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        process.WaitForExit(TimeSpan.FromSeconds(60)).ShouldBeTrue($"{file} did not finish in 60 s");
        return (process.ExitCode, output.Result[0], output.Result[1]);
    }
}

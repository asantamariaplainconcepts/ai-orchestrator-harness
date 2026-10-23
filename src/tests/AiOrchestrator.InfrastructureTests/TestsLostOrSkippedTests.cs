using System.Diagnostics;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// #754. <c>.aio/checks/tests-lost-or-skipped.sh</c>, run from this repository against a
/// temporary checkout of two commits, with <c>CHECK_BASE</c> on the first. Git and the check are real.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TestsLostOrSkippedTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-tests-lost-").FullName;

    /// <summary>aio-dogfood's AioDogfood.Tests/WeatherForecastTests.cs at 06fa146, its bodies cut to one line.</summary>
    const string Forecasts = """
        using System.Net.Http.Json;
        using Microsoft.AspNetCore.Mvc.Testing;

        namespace AioDogfood.Tests;

        public class WeatherForecastTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
        {
            record Forecast(DateOnly Date, int TemperatureC, int TemperatureF, string? Summary);

            [Fact]
            public async Task The_forecast_has_five_days_from_tomorrow()
            {
                var days = await factory.CreateClient().GetFromJsonAsync<Forecast[]>("/api/weatherforecast");
            }

            [Fact]
            public async Task Fahrenheit_follows_celsius()
            {
                var days = await factory.CreateClient().GetFromJsonAsync<Forecast[]>("/api/weatherforecast");
            }

            [Theory]
            [InlineData("0")]
            [InlineData("abc")]
            public async Task Days_out_of_range_or_not_a_number_is_a_bad_request(string days)
            {
                var response = await factory.CreateClient().GetAsync($"/api/weatherforecast?days={days}");
            }
        }
        """;

    public TestsLostOrSkippedTests() => Git("init", "--quiet");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// aio-dogfood#65: a second nested record declared after the first. Every test is still there, so
    /// none is lost; before #754 all three were, renamed from <c>Forecast.</c> to <c>Detail.</c>.
    /// </summary>
    [Fact]
    public void A_record_declared_inside_the_test_class_loses_no_test()
    {
        var head = Forecasts
            .Replace("string? Summary);", "string? Summary, string Alert);\n\n    record Detail(DateOnly Date, int TemperatureC, string? Summary, string Alert);", StringComparison.Ordinal);

        var (code, output) = Check(("Tests/WeatherForecastTests.cs", Forecasts), ("Tests/WeatherForecastTests.cs", head));

        output.ShouldBeEmpty();
        code.ShouldBe(0);
    }

    /// <summary>A block namespace and a positional record nested above the tests: a deleted test is named by its class.</summary>
    [Fact]
    public void In_a_block_namespace_with_a_nested_record_a_deleted_test_is_named_by_its_class()
    {
        const string Base = """
            namespace Tiny
            {
                public sealed class ThingTests
                {
                    record Probe(int Value);

                    [Fact]
                    public void Decides() { }

                    [Fact]
                    public void Falls_back_to_zero()
                    {
                        var probe = new Probe(0);
                    }
                }

                record Unrelated(string Name);
            }
            """;
        var head = Base.Replace("        [Fact]\n        public void Decides() { }\n\n", "", StringComparison.Ordinal);

        var (code, output) = Check(("tests/ThingTests.cs", Base), ("tests/ThingTests.cs", head));

        output.ShouldBe("lost test: ThingTests.Decides\n");
        code.ShouldBe(1);
    }

    /// <summary>A file-scoped namespace and a <c>Skip =</c> added to a test: the skip is named, nothing is lost.</summary>
    [Fact]
    public void A_skip_added_to_a_test_is_reported_as_skipped()
    {
        const string Base = """
            namespace Tiny;

            public sealed class ThingTests
            {
                [Fact]
                public void Decides() { }
            }
            """;
        var head = Base.Replace("[Fact]", "[Fact(Skip = \"flaky\")]", StringComparison.Ordinal);

        var (code, output) = Check(("tests/ThingTests.cs", Base), ("tests/ThingTests.cs", head));

        output.ShouldBe("skip added in tests/ThingTests.cs: [Fact(Skip = \"flaky\")]\n");
        code.ShouldBe(1);
    }

    /// <summary>Commits <paramref name="before"/>, then <paramref name="after"/>, and runs the check on the second against the first.</summary>
    (int Code, string Output) Check((string Path, string Text) before, (string Path, string Text) after)
    {
        Commit(before.Path, before.Text);
        var baseSha = Git("rev-parse", "HEAD");
        Commit(after.Path, after.Text);

        var script = Path.Combine(ThisRepository.Root(), ".aio", "checks", "tests-lost-or-skipped.sh");
        var (code, stdout, stderr) = Run("bash", [script], ("CHECK_BASE", baseSha));
        stderr.ShouldBeEmpty();
        return (code, stdout);
    }

    void Commit(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        Git("add", "-A");
        Git("commit", "--quiet", "-m", relative);
    }

    string Git(params string[] arguments)
    {
        var (code, stdout, stderr) = Run("git", arguments);
        code.ShouldBe(0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout.Trim();
    }

    (int Code, string Stdout, string Stderr) Run(string file, IReadOnlyList<string> arguments, params (string Name, string Value)[] environment)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_AUTHOR_NAME"] = start.Environment["GIT_COMMITTER_NAME"] = "Tests lost test";
        start.Environment["GIT_AUTHOR_EMAIL"] = start.Environment["GIT_COMMITTER_EMAIL"] = "tests-lost@example.test";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var output = Task.WhenAll(process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
        process.WaitForExit(TimeSpan.FromSeconds(60)).ShouldBeTrue($"{file} did not finish in 60 s");
        return (process.ExitCode, output.Result[0], output.Result[1]);
    }
}

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #519. <c>.aio/steps/diff-risk</c>, the score of <c>merge-review</c>: one JSON line with
/// <c>outcome</c>, <c>band</c> and <c>why</c>: <c>high</c> whenever the model is not sure, <c>unknown</c> when there is no score to give (#596). The script runs for
/// real, in a git repository of its own with a branch to score; what stands in for the world is a
/// <c>gh</c> on its PATH that prints an issue, and a server on loopback that answers in laya's shape.
/// A diff of at most 20 lines is <c>low</c> before any model is asked (#551), so the tests of the
/// model's part score a change of <see cref="Large"/> lines. #732: a diff that touches a path of the
/// base's <c>protected</c> in <c>.aio/config.json</c> is <c>high</c> before any model is asked.
/// </summary>
public sealed class DiffRiskTests : IDisposable
{
    static readonly string[] Facts = ["new_dependency", "deletes_or_rewrites", "untested", "beyond_issue"];
    const int Large = 25;

    readonly string _directory = Path.Combine(Path.GetTempPath(), $"diff-risk-{Guid.NewGuid():N}");
    readonly string _repository;
    readonly string _bin;
    string _base;

    /// <summary>How many requests the loopback model server has answered, its health check included.</summary>
    int _asked;

    public DiffRiskTests()
    {
        _repository = Path.Combine(_directory, "repository");
        _bin = Path.Combine(_directory, "bin");
        Directory.CreateDirectory(_repository);
        Directory.CreateDirectory(_bin);

        Git("init", "-q");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "first\n");
        Git("add", ".");
        Git("-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "first");
        _base = Git("rev-parse", "HEAD").Trim();

        var gh = Path.Combine(_bin, "gh");
        File.WriteAllText(gh, "#!/bin/sh\necho '{\"title\":\"Add a line\",\"body\":\"The README needs a second line.\"}'\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(gh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void No_model_server_is_unknown_and_says_it_could_not_score()
    {
        Change("README.md", Large);
        var answer = DiffRisk($"http://127.0.0.1:{FreePort()}");

        answer["outcome"]!.GetValue<string>().ShouldBe("unknown");
        answer["why"]!.GetValue<string>().ShouldStartWith("diff-risk: no se pudo puntuar: no laya server");
    }

    [Fact]
    public void A_confident_low_with_no_fact_fired_is_low()
    {
        Change("README.md", Large);
        var answer = WithModel(Answers("low", 0.9, fact: 0.1));

        answer["band"]!.GetValue<string>().ShouldBe("low", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldContain("low@0.9");
    }

    [Fact]
    public void A_low_the_model_is_not_sure_of_is_high()
    {
        Change("README.md", Large);
        var answer = WithModel(Answers("low", 0.6, fact: 0.1));

        answer["band"]!.GetValue<string>().ShouldBe("high");
        answer["why"]!.GetValue<string>().ShouldContain("not sure (0.6 < 0.7)");
    }

    [Fact]
    public void A_fact_that_fires_makes_a_confident_low_high()
    {
        Change("README.md", Large);
        var answer = WithModel(Answers("low", 0.9, fact: 0.1, untested: 0.8));

        answer["band"]!.GetValue<string>().ShouldBe("high");
        answer["why"]!.GetValue<string>().ShouldContain("fired: untested=0.8");
    }

    [Fact]
    public void A_fact_the_model_is_not_sure_of_is_high()
    {
        Change("README.md", Large);
        var answer = WithModel(Answers("low", 0.9, fact: 0.1, factConfidence: 0.4));

        answer["band"]!.GetValue<string>().ShouldBe("high");
        answer["why"]!.GetValue<string>().ShouldContain("not sure about: new_dependency@0.4");
    }

    [Fact]
    public void An_answer_outside_the_contract_is_unknown()
    {
        Change("README.md", Large);
        var answer = WithModel("""{"answers":{"band":{"choice":"low"}}}""");

        answer["outcome"]!.GetValue<string>().ShouldBe("unknown");
        answer["why"]!.GetValue<string>().ShouldContain("not laya's typed contract");
    }

    [Fact]
    public void A_one_line_change_is_low_without_a_model()
    {
        Change("README.md", 1);

        var answer = DiffRisk($"http://127.0.0.1:{FreePort()}");

        answer["outcome"]!.GetValue<string>().ShouldBe("low", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldStartWith("small diff: 1 changed line in 1 file");
        // #732: a repository without the list protects nothing, and the answer says so once.
        answer["why"]!.GetValue<string>().ShouldEndWith("; no protected paths in .aio/config.json at the base, so no path is protected");
    }

    [Fact]
    public void Twenty_lines_are_small_and_twenty_one_ask_the_model()
    {
        Change("README.md", 20);
        DiffRisk($"http://127.0.0.1:{FreePort()}")["outcome"]!.GetValue<string>().ShouldBe("low");

        Change("NOTES.md", 1);
        var answer = DiffRisk($"http://127.0.0.1:{FreePort()}");

        answer["outcome"]!.GetValue<string>().ShouldBe("unknown", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldContain("no laya server");
    }

    /// <summary>
    /// #732. One line in a protected path holds for a person, whatever the size, and the model is
    /// never asked: the server here would answer a confident low, and is not called at all.
    /// </summary>
    [Theory]
    [InlineData("src/Persistence/Migrations/20261011092940_ScriptOutput.cs")]
    [InlineData(".aio/loop.yaml")]
    [InlineData("AGENTS.md")]
    public void A_one_line_change_to_a_protected_path_is_high_and_asks_no_model(string path)
    {
        Protect("AGENTS.md", ".aio/*", "src/*/Migrations/*");
        Change(path, 1);
        Change("NOTES.md", Large);

        var answer = WithModel(Answers("low", 0.9, fact: 0.1));

        answer["outcome"]!.GetValue<string>().ShouldBe("high", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldBe($"protected: {path}");
        _asked.ShouldBe(0);
    }

    [Fact]
    public void A_small_change_to_no_protected_path_is_low_and_says_none_is()
    {
        Protect(".aio/*");
        Change("README.md", 1);

        var answer = DiffRisk($"http://127.0.0.1:{FreePort()}");

        answer["outcome"]!.GetValue<string>().ShouldBe("low", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldBe("small diff: 1 changed line in 1 file; none protected");
    }

    /// <summary>
    /// #732. The list is read from the base: a change that protects the path it touches is judged
    /// by the list without that line, and one that unprotects the path it touches by the list with it.
    /// </summary>
    [Fact]
    public void A_change_to_the_list_is_judged_by_the_list_of_the_base()
    {
        Protect("secrets/*");
        WriteList("secrets/*", "docs/*");
        Change("docs/guide.md", 1);

        var protecting = DiffRisk($"http://127.0.0.1:{FreePort()}");

        protecting["outcome"]!.GetValue<string>().ShouldBe("low", protecting.ToJsonString());

        Git("reset", "--hard", "-q", _base);
        WriteList();
        Change("secrets/key.txt", 1);

        var unprotecting = DiffRisk($"http://127.0.0.1:{FreePort()}");

        unprotecting["outcome"]!.GetValue<string>().ShouldBe("high", unprotecting.ToJsonString());
        unprotecting["why"]!.GetValue<string>().ShouldBe("protected: secrets/key.txt");
    }

    /// <summary>
    /// #732. This repository's own list keeps what the pattern diff-risk had before protected: the
    /// contracts, the dependency manifests, the ports, auth and secrets, and the harness.
    /// </summary>
    [Theory]
    [InlineData("src/Directory.Packages.props")]
    [InlineData("src/frontend/shared/http/contracts.ts")]
    [InlineData("src/frontend/shared/http/openapi.json")]
    [InlineData("src/frontend/package.json")]
    [InlineData("src/frontend/pnpm-lock.yaml")]
    [InlineData("src/root/AiOrchestrator.Server/AiOrchestrator.Server.csproj")]
    [InlineData("src/shared/AiOrchestrator.ServiceDefaults/Secrets/GitHubCliSecretResolver.cs")]
    [InlineData("src/shared/AiOrchestrator.Infrastructure/Ports.cs")]
    [InlineData(".github/workflows/release.yml")]
    [InlineData("scripts/release")]
    [InlineData("scripts/keep-committer")]
    [InlineData("src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Harness/Package/package.props")]
    [InlineData(".aio/steps/merge")]
    public void A_one_line_change_to_what_this_repository_protects_is_high(string path)
    {
        Protect(ThisRepositorysList());
        Change(path, 1);

        var answer = DiffRisk($"http://127.0.0.1:{FreePort()}");

        answer["outcome"]!.GetValue<string>().ShouldBe("high", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldBe($"protected: {path}");
    }

    /// <summary>
    /// #779. A base from before harness 37 has its list in <c>.harness/protected-paths</c>, one pattern per
    /// line: it still protects, so the change that moves the list to <c>.aio/config.json</c> is judged by it.
    /// </summary>
    [Fact]
    public void A_base_with_the_list_in_the_old_file_still_protects()
    {
        var file = Path.Combine(_repository, ".harness", "protected-paths");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file, ["# Paths a person always reviews.", "secrets/*"]);
        Git("add", ".");
        Git("-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "protect");
        _base = Git("rev-parse", "HEAD").Trim();
        Change("secrets/key.txt", 1);

        var answer = DiffRisk($"http://127.0.0.1:{FreePort()}");

        answer["outcome"]!.GetValue<string>().ShouldBe("high", answer.ToJsonString());
        answer["why"]!.GetValue<string>().ShouldBe("protected: secrets/key.txt");
    }

    /// <summary>This repository's own <c>protected</c>, as <c>.aio/config.json</c> carries it, without its <c>//</c> lines.</summary>
    static string[] ThisRepositorysList()
    {
        var text = string.Join('\n', File.ReadAllLines(Path.Combine(Root(), ".aio", "config.json")).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        using var config = System.Text.Json.JsonDocument.Parse(text);
        return [.. config.RootElement.GetProperty("protected").EnumerateArray().Select(pattern => pattern.GetString()!)];
    }

    /// <summary>Writes <c>.aio/config.json</c> with <paramref name="patterns"/> as <c>protected</c>, without committing it.</summary>
    void WriteList(params string[] patterns)
    {
        var file = Path.Combine(_repository, ".aio", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, $"// Paths a person always reviews.\n{{\n  \"protected\": [{string.Join(", ", patterns.Select(pattern => $"\"{pattern}\""))}]\n}}\n");
    }

    /// <summary>Commits <c>.aio/config.json</c> with <paramref name="patterns"/> as <c>protected</c>, and makes that commit the base.</summary>
    void Protect(params string[] patterns)
    {
        WriteList(patterns);
        Git("add", ".");
        Git("-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "protect");
        _base = Git("rev-parse", "HEAD").Trim();
    }

    /// <summary>Commits <paramref name="lines"/> new lines to <paramref name="path"/> on the scored branch.</summary>
    void Change(string path, int lines)
    {
        var file = Path.Combine(_repository, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.AppendAllLines(file, Enumerable.Range(1, lines).Select(line => $"line {line}"));
        Git("add", ".");
        Git("-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", $"{lines} lines to {path}");
    }

    /// <summary>The body laya returns, with every fact at one probability unless named.</summary>
    static string Answers(string choice, double bandConfidence, double fact, double? untested = null, double factConfidence = 0.9)
    {
        var answers = new JsonObject();
        foreach (var name in Facts)
        {
            var p = name == "untested" && untested is { } u ? u : fact;
            answers[name] = new JsonObject { ["type"] = "noul", ["noul"] = p, ["answer_confidence"] = factConfidence };
        }
        answers["band"] = new JsonObject { ["type"] = "choice", ["choice"] = choice, ["answer_confidence"] = bandConfidence };
        return new JsonObject { ["answers"] = answers }.ToJsonString();
    }

    /// <summary>Runs the script against a loopback server that answers <paramref name="body"/>.</summary>
    JsonNode WithModel(string body)
    {
        var url = $"http://127.0.0.1:{FreePort()}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        var serving = Task.Run(() =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                Interlocked.Increment(ref _asked);
                var bytes = Encoding.UTF8.GetBytes(context.Request.Url!.AbsolutePath == "/health" ? "{}" : body);
                context.Response.ContentType = "application/json";
                context.Response.OutputStream.Write(bytes);
                context.Response.Close();
            }
        });

        try
        {
            return DiffRisk(url.TrimEnd('/'));
        }
        finally
        {
            listener.Stop();
            serving.Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Runs diff-risk on the branch, and checks it answered exactly one JSON line.</summary>
    JsonNode DiffRisk(string laya)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = _repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(Root(), ".aio", "steps", "diff-risk"));
        start.ArgumentList.Add("7");
        start.ArgumentList.Add(_base);
        start.Environment["LAYA_URL"] = laya;
        start.Environment["PATH"] = $"{_bin}:{Environment.GetEnvironmentVariable("PATH")}";

        using var process = Process.Start(start)!;
        var said = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, said);
        var lines = said.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(1, said);
        var answer = JsonNode.Parse(lines[0])!.AsObject();
        answer.Select(pair => pair.Key).ShouldBe(["outcome", "band", "why"]);
        // #593: the engine reads outcome; band repeats it for a person.
        answer["outcome"]!.GetValue<string>().ShouldBe(answer["band"]!.GetValue<string>());
        return answer;
    }

    string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var said = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);
        return said;
    }

    static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    static string Root() =>
        SelfCheckout.Root() ?? throw new DirectoryNotFoundException("These tests run this checkout's own .aio/aio, and there is none.");
}

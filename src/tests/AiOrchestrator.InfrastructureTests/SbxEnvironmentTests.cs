using System.Text;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Environments;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.Infrastructure.Sessions;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The sbx environment (#614) against a stand-in <c>sbx</c>: a shell script that logs every call and
/// does what the real one does to the host — a clone wired back as <c>sandbox-&lt;name&gt;</c> on
/// create, the clone switched by <c>exec</c>, the launch run by <c>exec -it</c> with its <c>-w</c> and
/// <c>-e</c>, the remote gone on <c>rm</c>. The worktree, git, the PTY, <c>aio</c> and the recording
/// are real. The real sbx is checked by a run of the product, recorded in the pull request.
/// </summary>
public sealed class SbxEnvironmentTests : IDisposable
{
    const string Both = """
        SCOPE      TYPE      NAME        SECRET
        (global)   service   github      (stored)
        (global)   service   anthropic   (oauth configured)
        """;

    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    readonly GitCli _git = new();
    string _root = Directory.CreateTempSubdirectory("aio-sbx-").FullName;

    string Checkout => Path.Combine(_root, "checkout");
    string RunDirectory => Path.Combine(_root, "runs", "1");
    string Log => Path.Combine(_root, "sbx.log");
    string Clone => Path.Combine(_root, "clone");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task A_run_gets_a_sandbox_per_pbi_starts_inside_it_and_retiring_brings_the_branch_back_and_removes_it()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        var environment = Sbx(Both, api: new Uri("http://localhost:5080"));
        var place = await environment.PrepareAsync(new PlaceRequest(Checkout, "change/1", "HEAD", RunDirectory), CancellationToken.None);
        const string Sandbox = "aio-checkout-change-1";

        place.Environment.ShouldBe("sbx");
        place.WorkingDirectory.ShouldBe(Path.Combine(Checkout, ".worktrees", "change-1"));
        var calls = Calls();
        calls.ShouldContain(
            $"create -q --name {Sandbox} --clone --deny-network gist.github.com --deny-network raw.githubusercontent.com "
                + $"--deny-network *.amazonaws.com --deny-network *.blob.core.windows.net claude {Checkout} {RunDirectory}"
        );
        // #634: the control plane's port too, which sbx's proxy names localhost on the host.
        calls.ShouldContain($"policy allow network --sandbox {Sandbox} api.nuget.org,*.nuget.org,builds.dotnet.microsoft.com,dotnetcli.azureedge.net,localhost:5080");
        calls.ShouldContain(call => call.StartsWith($"exec -w {Checkout} {Sandbox} sh -c ", StringComparison.Ordinal) && call.EndsWith(" /run/sandbox/source change/1", StringComparison.Ordinal));
        (await Git(Clone, "branch", "--show-current")).Trim().ShouldBe("change/1", "the clone is switched to the PBI's branch");

        // The provider's launch, in the sandbox's own mode, wrapped in sbx exec -it.
        var provider = new ClaudeCodeProvider(StandIn(), Path.Combine(_root, ".claude.json"), Path.Combine(_root, "projects"));
        var request = new AgentRequest(
            "/aio:implement 1",
            null,
            place,
            Path.Combine(RunDirectory, "done.json"),
            Path.Combine(RunDirectory, "session.cast"),
            RunId: Guid.Parse("0199e2a4-1111-7000-8000-000000000741"),
            Permissions: environment.Permissions!.Value,
            Repository: "owner/checkout",
            Api: new Uri("http://localhost:5080")
        );
        var launch = environment.Enter(place, provider.Launch(request));

        launch.FileName.ShouldBe(Path.Combine(_root, "bin", "sbx"));
        launch.Environment.ShouldBeEmpty("nothing of this process reaches the sandbox but what -e passes");
        launch.Arguments.Take(4).ShouldBe(["exec", "-it", "-w", Checkout]);
        launch.Arguments.ShouldContain($"AIO_DONE_FILE={request.EndRecordPath}");
        launch.Arguments.ShouldContain("AIO_API=http://host.docker.internal:5080", "inside the sandbox the host's loopback is host.docker.internal");
        launch.Arguments.ShouldContain("AIO_REPOSITORY=owner/checkout");
        // #741. The run's committer crosses into the sandbox like every other variable of the launch.
        launch.Arguments.ShouldContain("GIT_COMMITTER_NAME=aio run 0199e2a4-1111-7000-8000-000000000741");
        launch.Arguments.ShouldContain("GIT_COMMITTER_EMAIL=noreply@aio.invalid");
        launch.Arguments.Where((_, index) => index > 0 && launch.Arguments[index - 1] == "-e").ShouldNotContain(variable => variable.StartsWith("PATH=", StringComparison.Ordinal));
        var inside = launch.Arguments.SkipWhile(argument => argument != Sandbox).ToList();
        inside.Take(5).ShouldBe([Sandbox, "sh", "-c", "PATH=\"$0/.aio:$0/.harness/aio:$0/.harness/scripts:$PATH\" exec \"$@\"", Checkout]);
        inside.Skip(5).Take(3).ShouldBe(["claude", "--permission-mode", "bypassPermissions"]);
        inside.Last().ShouldBe("/aio:implement 1");

        await using (var session = new PtySession(TimeProvider.System))
        {
            await session.StartAsync(launch, CancellationToken.None);
            var seen = await ReadToEnd(session);

            seen.ShouldContain($"cwd:{Checkout}");
            seen.ShouldContain("The run ends with 'approved'.");
        }

        // The end record is on this machine where AIO_DONE_FILE said, and the PTY recorded the session.
        File.ReadAllText(request.EndRecordPath).ShouldContain("\"outcome\":\"approved\"");
        File.ReadAllText(request.RecordingPath!).ShouldContain("approved");

        // The agent's commit in the sandbox, which retiring brings into the worktree before it goes.
        await File.WriteAllTextAsync(Path.Combine(Clone, "work.txt"), "done in the sandbox\n");
        await Git(Clone, "add", "work.txt");
        await Git(Clone, "commit", "--quiet", "-m", "work in the sandbox");
        var committed = (await Git(Clone, "rev-parse", "HEAD")).Trim();

        await environment.RetireAsync(place, CancellationToken.None);

        (await Git(Checkout, "rev-parse", "change/1")).Trim().ShouldBe(committed);
        Directory.Exists(place.WorkingDirectory).ShouldBeFalse();
        calls = Calls();
        calls.ShouldContain($"rm --force {Sandbox}");
        calls.ShouldContain($"secret rm github --sandbox {Sandbox} -f");
        calls.ShouldContain($"secret rm anthropic --sandbox {Sandbox} -f");
        (await Git(Checkout, "remote")).ShouldNotContain($"sandbox-{Sandbox}");
    }

    [Theory]
    [InlineData("http://localhost:5080", "http://host.docker.internal:5080")]
    [InlineData("http://127.0.0.1:5080/", "http://host.docker.internal:5080")]
    [InlineData("https://aio.example.com", "https://aio.example.com")]
    public void A_loopback_aio_api_names_the_host_from_inside_and_any_other_is_passed_as_it_is(string outside, string inside) =>
        SbxEnvironment.FromInside(outside).ShouldBe(inside);

    [Fact]
    public async Task A_sandbox_left_with_this_runs_directory_is_reused_and_one_without_it_is_made_again()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        var environment = Sbx(Both);
        var first = await environment.PrepareAsync(new PlaceRequest(Checkout, "change/1", "HEAD", RunDirectory), CancellationToken.None);
        File.Delete(Log);

        await environment.PrepareAsync(new PlaceRequest(Checkout, "change/1", "HEAD", RunDirectory), CancellationToken.None);
        Calls().ShouldNotContain(call => call.StartsWith("create", StringComparison.Ordinal));

        var other = Path.Combine(_root, "runs", "2");
        await environment.PrepareAsync(new PlaceRequest(Checkout, "change/1", "HEAD", other), CancellationToken.None);
        var calls = Calls();
        calls.ShouldContain("rm --force aio-checkout-change-1");
        calls.ShouldContain(call => call.StartsWith("create", StringComparison.Ordinal) && call.EndsWith(other, StringComparison.Ordinal));

        await environment.RetireAsync(first, CancellationToken.None);
    }

    [Fact]
    public async Task Without_sbx_a_sign_in_or_a_claude_credential_it_is_not_offered_and_a_run_waits_with_the_fix()
    {
        var request = new PlaceRequest("/repos/widgets", "change/7", "HEAD");

        var missing = new SbxEnvironment(new WorktreeEnvironment(), SbxSettings.Default with { Cli = Path.Combine(_root, "no-sbx") });
        var absent = await missing.ReadinessAsync(CancellationToken.None);
        absent.Offered.ShouldBeFalse();
        absent.Note.ShouldNotBeNull().ShouldStartWith("sbx is not on the PATH of the login shell");
        (await missing.WaitsAsync(request, CancellationToken.None)).ShouldNotBeNull().ShouldStartWith("sbx is not on the PATH");

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var signedOut = Sbx(Both, failSecrets: true);
        var refused = await signedOut.ReadinessAsync(CancellationToken.None);
        refused.Offered.ShouldBeFalse();
        refused.Remedy.ShouldBe("sbx login");

        var noClaude = Sbx("""
            SCOPE      TYPE      NAME        SECRET
            (global)   service   github      (stored)
            """);
        var unsigned = await noClaude.ReadinessAsync(CancellationToken.None);
        unsigned.Offered.ShouldBeFalse();
        unsigned.Remedy.ShouldBe("sbx run claude");
        (await noClaude.WaitsAsync(request, CancellationToken.None)).ShouldNotBeNull().ShouldContain("/login");

        // A failed check makes nothing: no worktree, no sandbox.
        await Should.ThrowAsync<InvalidOperationException>(() => noClaude.PrepareAsync(request, CancellationToken.None));
        Calls().ShouldNotContain(call => call.StartsWith("create", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GitHub_comes_from_the_sandboxs_own_token_or_the_global_one_with_a_caution_or_the_run_waits()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var request = new PlaceRequest("/repos/widgets", "change/7", "HEAD");
        const string Name = "aio-widgets-change-7";

        var global = Sbx(Both);
        var caution = await global.ReadinessAsync(CancellationToken.None);
        caution.Offered.ShouldBeTrue();
        caution.Note.ShouldNotBeNull().ShouldContain("reaches all your repositories");
        caution.Remedy.ShouldNotBeNull().ShouldStartWith("sbx secret set github --sandbox");
        (await global.WaitsAsync(request, CancellationToken.None)).ShouldBeNull();

        var scoped = Sbx($"""
            SCOPE      TYPE      NAME        SECRET
            (global)   service   anthropic   (oauth configured)
            {Name}   service   github      (stored)
            """);
        (await scoped.WaitsAsync(request, CancellationToken.None)).ShouldBeNull();
        (await scoped.WaitsAsync(request with { Branch = "change/8" }, CancellationToken.None)).ShouldNotBeNull().ShouldContain("no GitHub token for aio-widgets-change-8");

        var none = Sbx("""
            SCOPE      TYPE      NAME        SECRET
            (global)   service   anthropic   (oauth configured)
            """);
        (await none.ReadinessAsync(CancellationToken.None)).Remedy.ShouldBe("gh auth token | sbx secret set github");
    }

    [Fact]
    public void An_agent_in_a_sandbox_runs_in_yolo_unless_settings_lower_it()
    {
        SbxSettings.From(null).Permissions.ShouldBe(AgentPermissions.Yolo);
        Settings([]).Permissions.ShouldBe(AgentPermissions.Yolo);
        Settings(new() { ["Environments:Sbx:Permissions"] = "auto" }).Permissions.ShouldBe(AgentPermissions.Auto);
        Settings(new() { ["Environments:Sbx:Permissions"] = "allow-list" }).Permissions.ShouldBe(AgentPermissions.AllowList);

        var lowered = new SbxEnvironment(new WorktreeEnvironment(), Settings(new() { ["Environments:Sbx:Permissions"] = "auto" }));
        lowered.Permissions.ShouldBe(AgentPermissions.Auto);
        new SbxEnvironment(new WorktreeEnvironment(), SbxSettings.Default).Permissions.ShouldBe(AgentPermissions.Yolo);
        lowered.Sandboxed.ShouldBeTrue();

        var hosts = Settings(new() { ["Environments:Sbx:Allow:0"] = "pypi.org", ["Environments:Sbx:Deny:0"] = "example.com" });
        hosts.Allow.ShouldBe(["pypi.org"]);
        hosts.Deny.ShouldBe(["example.com"]);
    }

    [Theory]
    [InlineData("/repos/widgets", "change/7", "aio-widgets-change-7")]
    [InlineData("/repos/AI Orchestrator/", "change/614", "aio-ai-orchestrator-change-614")]
    [InlineData("/repos/a-very-long-repository-name-that-goes-on-and-on", "change/123456789", "aio-a-very-long-repository-name-that-goes-on-and-on-")]
    public void A_sandbox_is_named_after_its_checkout_and_branch_in_the_characters_sbx_takes(string repository, string branch, string expected)
    {
        var name = SbxEnvironment.SandboxName(repository, branch);

        name.ShouldStartWith(expected.TrimEnd('-'));
        name.Length.ShouldBeLessThanOrEqualTo(60);
        name.ShouldMatch("^[a-z0-9.-]+$");
    }

    static SbxSettings Settings(Dictionary<string, string?> values) =>
        SbxSettings.From(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    SbxEnvironment Sbx(string secrets, bool failSecrets = false, Uri? api = null)
    {
        File.WriteAllText(Path.Combine(_root, "secrets.txt"), secrets + "\n");
        var path = Path.Combine(_root, "bin", "sbx");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            #!/bin/bash
            root=$(cd "$(dirname "$0")/.." && pwd -P)
            printf '%s\n' "$*" >> "$root/sbx.log"
            case "$1" in
              secret)
                [ "$2" = ls ] || exit 0
                FAIL_SECRETS
                cat "$root/secrets.txt" ;;
              ls)
                if [ -f "$root/sandbox.json" ]; then cat "$root/sandbox.json"; else echo '{"sandboxes":[]}'; fi ;;
              create)
                shift; name=""
                while [ $# -gt 0 ]; do
                  case "$1" in
                    --name|--deny-network) [ "$1" = --name ] && name=$2; shift 2 ;;
                    claude) shift; break ;;
                    *) shift ;;
                  esac
                done
                rm -rf "$root/clone"
                git clone -q "$1" "$root/clone"
                git -C "$root/clone" config user.name "Sandbox"; git -C "$root/clone" config user.email "sandbox@localhost"
                git -C "$1" remote add "sandbox-$name" "$root/clone"
                printf '%s\n' "$1" > "$root/repository"
                printf '{"sandboxes":[{"name":"%s","status":"running","workspaces":["%s","%s"]}]}\n' "$name" "$1" "$2" > "$root/sandbox.json" ;;
              exec)
                shift; tty=""; workdir=""
                while [ $# -gt 0 ]; do
                  case "$1" in
                    -it) tty=1; shift ;;
                    -w) workdir=$2; shift 2 ;;
                    -e) export "$2"; shift 2 ;;
                    *) break ;;
                  esac
                done
                shift
                if [ -n "$tty" ]; then cd "$workdir"; export PATH="$root/bin:$PATH"; exec "$@"; fi
                # The switch: the same script, in the clone, with the checkout standing in for the source.
                cd "$root/clone"; script=$3; shift 4; source=$(cat "$root/repository")
                exec sh -c "$script" "$source" "$@" ;;
              rm)
                name="${@: -1}"
                [ -f "$root/repository" ] && git -C "$(cat "$root/repository")" remote remove "sandbox-$name" 2>/dev/null
                rm -f "$root/sandbox.json" ;;
            esac
            exit 0
            """.Replace("FAIL_SECRETS", failSecrets ? "echo 'not signed in' >&2; exit 1" : ":", StringComparison.Ordinal)
        );
        Executable(path);

        return new SbxEnvironment(new WorktreeEnvironment(), SbxSettings.Default with { Cli = path }, () => api);
    }

    /// <summary>A <c>claude</c> that says where it runs and ends the run with a bare <c>aio done</c>.</summary>
    string StandIn()
    {
        var path = Path.Combine(_root, "bin", "claude");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\necho \"cwd:$(pwd -P)\"\naio done approved --reason \"ran in the sandbox\"\n");
        Executable(path);
        return path;
    }

    static void Executable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
        }
    }

    IReadOnlyList<string> Calls() => File.Exists(Log) ? File.ReadAllLines(Log) : [];

    async Task Initialize()
    {
        await Git(_root, "init", "--quiet", "--initial-branch=master", Checkout);
        // macOS's temp directory is /var -> /private/var, and git names the real one.
        _root = Path.GetDirectoryName((await Git(Checkout, "rev-parse", "--show-toplevel")).Trim())!;
        await Git(Checkout, "config", "user.name", "Sbx tests");
        await Git(Checkout, "config", "user.email", "sbx-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");

        var aio = Path.Combine(Checkout, ".aio", "aio");
        Directory.CreateDirectory(Path.GetDirectoryName(aio)!);
        File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "aio"), aio);
        Executable(aio);

        await File.WriteAllTextAsync(Path.Combine(Checkout, ".gitignore"), ".worktrees/\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");
    }

    static async Task<string> ReadToEnd(ISession session)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        var bytes = new List<byte>();

        await foreach (var chunk in session.ReadAsync(deadline.Token))
        {
            bytes.AddRange(chunk.ToArray());
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }

    async Task<string> Git(string directory, params string[] arguments)
    {
        var result = await _git.Run(directory, arguments);
        result.Succeeded.ShouldBeTrue(result.StandardError);
        return result.StandardOutput;
    }
}

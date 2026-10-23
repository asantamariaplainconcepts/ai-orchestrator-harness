using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.Sessions;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// <see cref="PtySession"/> against a real pseudo-terminal: <c>/bin/sh</c> under <c>forkpty</c> on
/// macOS and Linux, PowerShell under ConPTY on Windows. Each test reads the recording back as
/// asciicast v2 and checks it holds what the reader saw.
/// </summary>
public sealed class PtySessionTests : IDisposable
{
    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    readonly string _directory = Directory.CreateTempSubdirectory("aio-pty-session-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    string Recording => Path.Combine(_directory, "run", "session.cast");

    [Fact]
    public async Task Printf_with_control_sequences_is_recorded_as_the_reader_saw_it()
    {
        await using var session = await Start(
            unix: @"printf '\033[1;31mred\033[0m plain\r\n\033[2Kgone\rkept\r\n'",
            windows: "[Console]::Write([char]27 + '[1;31mred' + [char]27 + '[0m plain')"
        );

        var seen = await ReadToEnd(session);

        seen.ShouldContain("\u001b[1;31mred\u001b[0m plain");
        var cast = Cast.Read(Recording);
        cast.Header.GetProperty("version").GetInt32().ShouldBe(2);
        cast.Header.GetProperty("width").GetInt32().ShouldBe(120);
        cast.Header.GetProperty("height").GetInt32().ShouldBe(30);
        cast.Output.ShouldBe(seen);
        cast.Events.Select(e => e.Time).ShouldBeInOrder();
    }

    [Fact]
    public async Task What_is_written_reaches_the_process_as_keystrokes()
    {
        await using var session = await Start(
            unix: "read line; echo \"got:$line\"",
            windows: "$line = Read-Host; Write-Output \"got:$line\""
        );

        await session.WriteAsync(Encoding.UTF8.GetBytes("hello\r"), CancellationToken.None);

        (await ReadToEnd(session)).ShouldContain("got:hello");
    }

    [Fact]
    public async Task A_resize_reaches_the_process_and_is_recorded_as_an_r_event()
    {
        await using var session = await Start(
            unix: "read x; stty size",
            windows: "$null = Read-Host; $s = $Host.UI.RawUI.WindowSize; Write-Output ('{0} {1}' -f $s.Height, $s.Width)"
        );

        await session.ResizeAsync(1, 1, CancellationToken.None);
        await session.ResizeAsync(100, 40, CancellationToken.None);
        await session.WriteAsync("\r"u8.ToArray(), CancellationToken.None);

        (await ReadToEnd(session)).ShouldContain("40 100");
        Cast.Read(Recording).Events.Where(e => e.Code == "r").Select(e => e.Data).ShouldBe(["100x40"]);
    }

    [Fact]
    public async Task Kill_ends_the_process_and_leaves_a_closed_recording_every_line_of_which_parses()
    {
        await using var session = await Start(unix: "echo started; sleep 30", windows: "Write-Output started; Start-Sleep 30");
        var reading = ReadToEnd(session);

        await WaitFor(() => File.ReadAllText(Recording).Contains("started", StringComparison.Ordinal));
        await session.KillAsync(CancellationToken.None).WaitAsync(Deadline);
        await session.KillAsync(CancellationToken.None);

        (await reading).ShouldContain("started");

        // Opening it with no sharing proves nothing holds it any longer.
        using (new FileStream(Recording, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }

        File.ReadAllText(Recording).ShouldEndWith("\n");
        Cast.Read(Recording).Output.ShouldContain("started");
    }

    [Fact]
    public async Task A_session_with_no_recording_path_writes_no_file()
    {
        await using var session = await Start(unix: "echo quiet", windows: "Write-Output quiet", recording: null);

        (await ReadToEnd(session)).ShouldContain("quiet");
        Directory.EnumerateFileSystemEntries(_directory).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_session_starts_once()
    {
        await using var session = await Start(unix: "true", windows: "exit 0");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            session.StartAsync(Options("true", "exit 0", null), CancellationToken.None)
        );
    }

    [Fact]
    public async Task Killing_a_session_that_never_started_does_nothing()
    {
        var session = new PtySession(TimeProvider.System);

        await session.KillAsync(CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            session.WriteAsync("x"u8.ToArray(), CancellationToken.None)
        );
    }

    [Fact]
    public void A_bare_name_is_resolved_on_the_path_the_process_is_given()
    {
        var bin = Directory.CreateDirectory(Path.Combine(_directory, "bin")).FullName;
        var tool = Path.Combine(bin, OperatingSystem.IsWindows() ? "tool.exe" : "tool");
        File.WriteAllText(tool, "");

        PtySession.Resolve("tool", $"/nowhere{Path.PathSeparator}{bin}").ShouldBe(tool);
        PtySession.Resolve("absent", bin).ShouldBe("absent");
        PtySession.Resolve(tool, "").ShouldBe(tool);
    }

    async Task<PtySession> Start(string unix, string windows, string? recording = "default")
    {
        var session = new PtySession(TimeProvider.System);
        await session.StartAsync(
            Options(unix, windows, recording is null ? null : Recording),
            CancellationToken.None
        );
        return session;
    }

    SessionOptions Options(string unix, string windows, string? recording) =>
        OperatingSystem.IsWindows()
            ? new SessionOptions(
                "powershell.exe",
                ["-NoProfile", "-NonInteractive", "-Command", windows],
                _directory,
                new Dictionary<string, string>(),
                120,
                30,
                recording
            )
            : new SessionOptions(
                "/bin/sh",
                ["-c", unix],
                _directory,
                new Dictionary<string, string>(),
                120,
                30,
                recording
            );

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

    static async Task WaitFor(Func<bool> condition)
    {
        var until = DateTime.UtcNow + Deadline;

        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(until);
            await Task.Delay(50);
        }
    }
}

/// <summary>An asciicast v2 file read back strictly: every line must be JSON of the right shape.</summary>
sealed record Cast(JsonElement Header, IReadOnlyList<Cast.Event> Events)
{
    public sealed record Event(double Time, string Code, string Data);

    public string Output => string.Concat(Events.Where(e => e.Code == "o").Select(e => e.Data));

    public static Cast Read(string path)
    {
        string[] lines;

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        var header = JsonDocument.Parse(lines[0]).RootElement.Clone();
        header.ValueKind.ShouldBe(JsonValueKind.Object);

        var events = lines
            .Skip(1)
            .Select(line =>
            {
                var array = JsonDocument.Parse(line).RootElement;
                array.GetArrayLength().ShouldBe(3);
                return new Event(array[0].GetDouble(), array[1].GetString()!, array[2].GetString()!);
            })
            .ToList();

        return new Cast(header, events);
    }
}

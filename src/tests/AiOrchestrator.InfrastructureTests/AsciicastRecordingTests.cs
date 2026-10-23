using AiOrchestrator.Infrastructure.Sessions;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The recording's format, byte for byte, with a clock the test moves: the header asciinema reads,
/// one event per line with its seconds since the start, and text decoded across read boundaries.
/// </summary>
public sealed class AsciicastRecordingTests : IDisposable
{
    readonly string _directory = Directory.CreateTempSubdirectory("aio-asciicast-").FullName;
    readonly SteppedClock _clock = new();

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    string Path => System.IO.Path.Combine(_directory, "nested", "run.cast");

    [Fact]
    public void The_file_is_a_v2_header_then_one_event_per_line()
    {
        using (var recording = AsciicastRecording.Create(Path, 80, 24, _clock))
        {
            _clock.Advance(TimeSpan.FromMilliseconds(1500));
            recording.Output("\u001b[1mhi\r\n"u8);
            _clock.Advance(TimeSpan.FromSeconds(1));
            recording.Resize(100, 40);
        }

        File.ReadAllText(Path)
            .ShouldBe(
                """
                {"version":2,"width":80,"height":24,"timestamp":1767225600,"env":{"TERM":"xterm-256color"}}
                [1.500000, "o", "\u001B[1mhi\r\n"]
                [2.500000, "r", "100x40"]

                """.ReplaceLineEndings("\n")
            );
    }

    [Fact]
    public void A_character_split_across_two_reads_is_written_once_whole()
    {
        var euro = "€"u8.ToArray();

        using (var recording = AsciicastRecording.Create(Path, 80, 24, _clock))
        {
            recording.Output(euro.AsSpan(0, 1));
            recording.Output(euro.AsSpan(1));
        }

        var cast = Cast.Read(Path);
        cast.Events.Count.ShouldBe(1);
        cast.Output.ShouldBe("€");
    }

    [Fact]
    public void A_sequence_cut_short_at_exit_is_kept_and_nothing_is_written_after_closing()
    {
        var recording = AsciicastRecording.Create(Path, 80, 24, _clock);
        recording.Output("ok"u8);
        recording.Output("€"u8.ToArray().AsSpan(0, 2));

        recording.Dispose();
        recording.Dispose();
        recording.Output("late"u8);
        recording.Resize(100, 40);

        Cast.Read(Path).Output.ShouldBe("ok�");
    }

    [Fact]
    public void Each_event_is_on_disk_before_the_recording_closes()
    {
        using var recording = AsciicastRecording.Create(Path, 80, 24, _clock);

        recording.Output("live"u8);

        Cast.Read(Path).Output.ShouldBe("live");
    }

    sealed class SteppedClock : TimeProvider
    {
        long _ticks;

        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}

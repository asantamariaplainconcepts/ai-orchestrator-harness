using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AiOrchestrator.Infrastructure.Sessions;

/// <summary>
/// One session's recording in asciinema's asciicast v2: a JSON header line, then one JSON array per
/// event — <c>[seconds, "o", text]</c> for output and <c>[seconds, "r", "COLSxROWS"]</c> for a
/// resize. The format is asciinema's, so <c>asciinema play</c> and <c>asciinema-player</c> replay it
/// without a line of ours (ARCHITECTURE.md#what-we-did-not-invent).
/// <para>
/// <b>Flushed line by line.</b> Every event reaches the file before the next is accepted, so a
/// session killed in the middle of a run, or a server that dies with it, leaves a file whose every
/// line parses. A buffered writer would leave the last few kilobytes in memory and a truncated line
/// on disk.
/// </para>
/// <para>
/// The format carries text, not bytes, so output is decoded as UTF-8 with one decoder for the whole
/// session: a character split across two reads is written once, whole, rather than as two
/// replacement characters. asciinema's own recorder does the same.
/// </para>
/// </summary>
sealed class AsciicastRecording : IDisposable
{
    readonly StreamWriter _writer;
    readonly TimeProvider _clock;
    readonly long _started;
    readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    readonly Lock _gate = new();
    bool _closed;

    AsciicastRecording(StreamWriter writer, TimeProvider clock)
    {
        _writer = writer;
        _clock = clock;
        _started = clock.GetTimestamp();
    }

    /// <summary>Creates the file, replacing any earlier one at the path, and writes its header.</summary>
    public static AsciicastRecording Create(string path, int columns, int rows, TimeProvider clock)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } directory)
        {
            Directory.CreateDirectory(directory);
        }

        // FileShare.Read so a reader can follow the file while the run is still writing it.
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };

        var header = JsonSerializer.Serialize(
            new
            {
                version = 2,
                width = columns,
                height = rows,
                timestamp = clock.GetUtcNow().ToUnixTimeSeconds(),
                env = new { TERM = PtySession.Terminal },
            }
        );

        writer.WriteLine(header);
        return new AsciicastRecording(writer, clock);
    }

    /// <summary>Records bytes the PTY wrote.</summary>
    public void Output(ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            var characters = new char[_decoder.GetCharCount(bytes, flush: false)];
            var written = _decoder.GetChars(bytes, characters, flush: false);

            if (written > 0)
            {
                Event("o", new string(characters, 0, written));
            }
        }
    }

    /// <summary>Records the terminal taking a new size.</summary>
    public void Resize(int columns, int rows)
    {
        lock (_gate)
        {
            if (!_closed)
            {
                Event("r", $"{columns}x{rows}");
            }
        }
    }

    /// <summary>
    /// Writes what the decoder still holds — a sequence the process cut short at exit — and closes
    /// the file. Idempotent.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            var characters = new char[_decoder.GetCharCount([], flush: true)];
            var written = _decoder.GetChars([], characters, flush: true);

            if (written > 0)
            {
                Event("o", new string(characters, 0, written));
            }

            _closed = true;
            _writer.Dispose();
        }
    }

    void Event(string code, string data)
    {
        var seconds = _clock.GetElapsedTime(_started).TotalSeconds;

        _writer.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{seconds:0.000000}, {JsonSerializer.Serialize(code)}, {JsonSerializer.Serialize(data)}]"
            )
        );
    }
}

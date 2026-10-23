using System.Text;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// The last characters a run's session printed, escapes included, for its CLI's adapter to read when
/// the session falls silent (#706). Decoded as it arrives, so a character split between two chunks
/// is read whole. The run's reader appends and the watch reads, so both go through one lock.
/// The same characters keep the <see cref="TerminalScreen"/> of the session's size, whose last lines
/// a run ended for its silence quotes (#764).
/// </summary>
sealed class ScreenTail(int columns, int rows)
{
    /// <summary>A screen of 120 by 30 several times over, escapes and all.</summary>
    const int Keep = 16 * 1024;

    readonly Lock _gate = new();
    readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    readonly StringBuilder _text = new();
    readonly TerminalScreen _screen = new(columns, rows);

    public void Append(ReadOnlySpan<byte> chunk)
    {
        lock (_gate)
        {
            var chars = new char[_decoder.GetCharCount(chunk, flush: false)];
            _decoder.GetChars(chunk, chars, flush: false);
            _text.Append(chars);
            _screen.Write(chars);
            if (_text.Length > Keep)
            {
                _text.Remove(0, _text.Length - Keep);
            }
        }
    }

    public string Text()
    {
        lock (_gate)
        {
            return _text.ToString();
        }
    }

    public IReadOnlyList<string> LastLines(int count)
    {
        lock (_gate)
        {
            return _screen.LastLines(count);
        }
    }
}

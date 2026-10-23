using System.Globalization;
using System.Text;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// What a terminal of the session's size shows, rebuilt from what the session prints (#764), so a run
/// ended for its silence can say what was on its screen. Claude Code draws with cursor moves rather
/// than lines: on 15 October the last lines of the stream were spinner fragments, while the screen
/// held the agent asking a person to allow a commit.
/// <para>
/// Only what places text is followed: printing, carriage return, line feed, backspace, the cursor
/// moves (<c>A B C D G H f d</c>, save and restore) and erasing in the line and the display, which is
/// every sequence two recorded runs of Claude Code used to place text. Colours, modes and titles are
/// dropped, a wide character takes one cell, and a resize from a viewer is not followed.
/// </para>
/// </summary>
sealed class TerminalScreen
{
    enum Read { Text, Escape, Charset, Control, Command }

    readonly int _columns;
    readonly int _rows;
    readonly List<char[]> _lines = [];
    readonly StringBuilder _parameters = new();
    Read _read;
    int _row;
    int _column;
    (int Row, int Column) _saved;

    public TerminalScreen(int columns, int rows)
    {
        _columns = Math.Max(1, columns);
        _rows = Math.Max(1, rows);
        for (var line = 0; line < _rows; line++)
        {
            _lines.Add(Blank());
        }
    }

    public void Write(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            Write(character);
        }
    }

    /// <summary>The last <paramref name="count"/> lines with a letter or a digit, so rules and an empty prompt are left out.</summary>
    public IReadOnlyList<string> LastLines(int count) =>
        [.. _lines.Select(line => new string(line).Trim()).Where(line => line.Any(char.IsLetterOrDigit)).TakeLast(count)];

    void Write(char character)
    {
        switch (_read)
        {
            case Read.Escape:
                Escaped(character);
                return;
            case Read.Charset:
                _read = Read.Text;
                return;
            case Read.Control:
                if (character is >= '@' and <= '~')
                {
                    Control(character);
                    _read = Read.Text;
                }
                else
                {
                    _parameters.Append(character);
                }

                return;
            case Read.Command:
                // A title or a notification ends at BEL or at ESC \, whose backslash ends the escape.
                if (character == '\a')
                {
                    _read = Read.Text;
                }
                else if (character == '\u001b')
                {
                    _read = Read.Escape;
                }

                return;
        }

        switch (character)
        {
            case '\u001b':
                _read = Read.Escape;
                break;
            case '\r':
                _column = 0;
                break;
            case '\n':
                LineFeed();
                break;
            case '\b':
                _column = Math.Max(0, _column - 1);
                break;
            case < ' ' or '\u007f':
                break;
            default:
                if (_column >= _columns)
                {
                    _column = 0;
                    LineFeed();
                }

                _lines[_row][_column++] = character;
                break;
        }
    }

    void Escaped(char character)
    {
        _read = Read.Text;
        switch (character)
        {
            case '[':
                _parameters.Clear();
                _read = Read.Control;
                break;
            case ']':
                _read = Read.Command;
                break;
            case '(' or ')':
                _read = Read.Charset;
                break;
            case '7':
                _saved = (_row, _column);
                break;
            case '8':
                (_row, _column) = _saved;
                break;
        }
    }

    void Control(char final)
    {
        var raw = _parameters.ToString();
        if (raw.Length > 0 && raw[0] is '?' or '<' or '>' or '=')
        {
            return;
        }

        var numbers = raw.Split(';');
        int Number(int at, int fallback) =>
            at < numbers.Length && int.TryParse(numbers[at], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : fallback;

        switch (final)
        {
            case 'A':
                _row = Math.Max(0, _row - Number(0, 1));
                break;
            case 'B':
                _row = Math.Min(_rows - 1, _row + Number(0, 1));
                break;
            case 'C':
                _column = Math.Min(_columns - 1, _column + Number(0, 1));
                break;
            case 'D':
                _column = Math.Max(0, Math.Min(_column, _columns - 1) - Number(0, 1));
                break;
            case 'G':
                _column = Math.Min(_columns - 1, Number(0, 1) - 1);
                break;
            case 'd':
                _row = Math.Min(_rows - 1, Number(0, 1) - 1);
                break;
            case 'H' or 'f':
                _row = Math.Min(_rows - 1, Number(0, 1) - 1);
                _column = Math.Min(_columns - 1, Number(1, 1) - 1);
                break;
            case 'K':
                EraseInLine(Number(0, 0));
                break;
            case 'J':
                EraseInDisplay(Number(0, 0));
                break;
        }
    }

    void EraseInLine(int mode)
    {
        var column = Math.Min(_column, _columns - 1);
        var (from, to) = mode switch
        {
            0 => (column, _columns),
            1 => (0, column + 1),
            _ => (0, _columns),
        };
        Array.Fill(_lines[_row], ' ', from, to - from);
    }

    void EraseInDisplay(int mode)
    {
        EraseInLine(mode);
        var (from, to) = mode switch
        {
            0 => (_row + 1, _rows),
            1 => (0, _row),
            _ => (0, _rows),
        };
        for (var line = from; line < to; line++)
        {
            _lines[line] = Blank();
        }
    }

    void LineFeed()
    {
        if (_row < _rows - 1)
        {
            _row++;
            return;
        }

        _lines.RemoveAt(0);
        _lines.Add(Blank());
    }

    char[] Blank()
    {
        var line = new char[_columns];
        Array.Fill(line, ' ');
        return line;
    }
}

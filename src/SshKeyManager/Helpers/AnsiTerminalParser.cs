using System.Globalization;
using System.Text;
using System.Windows.Media;

namespace SshKeyManager.Helpers;

/// <summary>
/// Stateful ANSI/VT parser for SSH shell output.
/// Supported: SGR colors (30–37, 90–97, 40–47, 100–107), bold, reset, CR/LF, BEL.
/// Stripped (not rendered): OSC (e.g. window title), DEC private modes, bracketed paste,
/// cursor movement/erase/clear-screen CSI, charset designations, other C1/control sequences.
/// v1 limitation: no real cursor addressing, alternate screen, or scroll-region emulation —
/// those sequences are consumed so they do not appear as garbage.
/// </summary>
public sealed class AnsiTerminalParser
{
    private readonly StringBuilder _carry = new();
    private AnsiSgrState _state;

    public void Reset()
    {
        _carry.Clear();
        _state = default;
    }

    /// <summary>
    /// Parses the next chunk of raw terminal text into styled runs.
    /// Incomplete escape sequences are held until the next call.
    /// </summary>
    public IReadOnlyList<AnsiTextRun> Parse(string? input)
    {
        if (string.IsNullOrEmpty(input) && _carry.Length == 0)
        {
            return Array.Empty<AnsiTextRun>();
        }

        var source = _carry.Length == 0 ? input! : _carry + (input ?? string.Empty);
        _carry.Clear();

        var runs = new List<AnsiTextRun>();
        var textBuffer = new StringBuilder();
        var i = 0;

        while (i < source.Length)
        {
            var ch = source[i];

            if (ch == '\u001b')
            {
                FlushText(textBuffer, runs);
                if (!TryConsumeEscape(source, ref i))
                {
                    _carry.Append(source.AsSpan(i));
                    break;
                }

                continue;
            }

            if (ch == '\a')
            {
                i++;
                continue;
            }

            if (ch == '\r')
            {
                FlushText(textBuffer, runs);
                i++;
                if (i < source.Length && source[i] == '\n')
                {
                    i++;
                }

                textBuffer.Append('\n');
                continue;
            }

            if (ch is '\b' or '\f' or '\v' or '\0')
            {
                i++;
                continue;
            }

            textBuffer.Append(ch);
            i++;
        }

        FlushText(textBuffer, runs);
        return runs;
    }

    /// <summary>
    /// Removes ANSI/VT escape sequences and returns plain text suitable for logs.
    /// </summary>
    public static string Strip(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var parser = new AnsiTerminalParser();
        var runs = parser.Parse(input);
        // Drop any incomplete trailing escape so it never leaks into logs.
        parser._carry.Clear();

        if (runs.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var run in runs)
        {
            sb.Append(run.Text);
        }

        return sb.ToString();
    }

    private void FlushText(StringBuilder textBuffer, List<AnsiTextRun> runs)
    {
        if (textBuffer.Length == 0)
        {
            return;
        }

        runs.Add(new AnsiTextRun(textBuffer.ToString(), _state.ToStyle()));
        textBuffer.Clear();
    }

    private bool TryConsumeEscape(string source, ref int i)
    {
        // Incomplete: lone ESC at end.
        if (i + 1 >= source.Length)
        {
            return false;
        }

        var next = source[i + 1];

        // CSI: ESC [
        if (next == '[')
        {
            return TryConsumeCsi(source, ref i);
        }

        // OSC: ESC ]
        if (next == ']')
        {
            return TryConsumeOsc(source, ref i);
        }

        // DCS / PM / APC: ESC P / ESC ^ / ESC _
        if (next is 'P' or '^' or '_')
        {
            return TryConsumeStringTerminated(source, ref i, startOffset: 2);
        }

        // Charset designation ESC ( B, ESC ) 0, ESC * ..., ESC + ...
        if (next is '(' or ')' or '*' or '+')
        {
            if (i + 2 >= source.Length)
            {
                return false;
            }

            i += 3;
            return true;
        }

        // Dual-character sequences: ESC c (RIS), ESC 7/8, ESC D/E/H/M, etc. — consume ESC + char.
        if ((next is >= '@' and <= '_')
            || (next is >= 'a' and <= 'z')
            || next is '7' or '8' or '=' or '>')
        {
            i += 2;
            return true;
        }

        // Unknown ESC: drop ESC only so following text is not lost.
        i++;
        return true;
    }

    private bool TryConsumeCsi(string source, ref int i)
    {
        // ESC [ [?] params... final
        var pos = i + 2;
        if (pos > source.Length)
        {
            return false;
        }

        if (pos < source.Length && source[pos] == '?')
        {
            pos++;
        }

        while (pos < source.Length)
        {
            var c = source[pos];
            if (c is >= '0' and <= '9' or ';' or ':' or '<' or '=' or '>' or '?')
            {
                pos++;
                continue;
            }

            // Intermediate bytes
            if (c is >= ' ' and <= '/')
            {
                pos++;
                continue;
            }

            // Final byte
            if (c is >= '@' and <= '~')
            {
                var sequence = source.AsSpan(i + 2, pos - (i + 2));
                if (c == 'm' && !sequence.StartsWith("?", StringComparison.Ordinal))
                {
                    ApplySgr(sequence);
                }

                i = pos + 1;
                return true;
            }

            // Invalid — abort consume, skip ESC
            i++;
            return true;
        }

        return false;
    }

    private static bool TryConsumeOsc(string source, ref int i)
    {
        // ESC ] ... BEL  or  ESC ] ... ESC \
        var pos = i + 2;
        while (pos < source.Length)
        {
            var c = source[pos];
            if (c == '\a')
            {
                i = pos + 1;
                return true;
            }

            if (c == '\u001b')
            {
                if (pos + 1 >= source.Length)
                {
                    return false;
                }

                if (source[pos + 1] == '\\')
                {
                    i = pos + 2;
                    return true;
                }

                // Nested ESC — treat as end of OSC to avoid runaway.
                i = pos;
                return true;
            }

            if (c == '\u009c')
            {
                i = pos + 1;
                return true;
            }

            pos++;
        }

        return false;
    }

    private static bool TryConsumeStringTerminated(string source, ref int i, int startOffset)
    {
        var pos = i + startOffset;
        while (pos < source.Length)
        {
            var c = source[pos];
            if (c == '\u001b')
            {
                if (pos + 1 >= source.Length)
                {
                    return false;
                }

                if (source[pos + 1] == '\\')
                {
                    i = pos + 2;
                    return true;
                }

                i = pos;
                return true;
            }

            if (c is '\a' or '\u009c')
            {
                i = pos + 1;
                return true;
            }

            pos++;
        }

        return false;
    }

    private void ApplySgr(ReadOnlySpan<char> parameterText)
    {
        if (parameterText.IsEmpty)
        {
            _state = default;
            return;
        }

        var start = 0;
        while (start <= parameterText.Length)
        {
            var sep = parameterText[start..].IndexOf(';');
            ReadOnlySpan<char> token;
            if (sep < 0)
            {
                token = parameterText[start..];
                start = parameterText.Length + 1;
            }
            else
            {
                token = parameterText.Slice(start, sep);
                start += sep + 1;
            }

            if (token.IsEmpty)
            {
                _state = default;
                continue;
            }

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            switch (code)
            {
                case 0:
                    _state = default;
                    break;
                case 1:
                    _state.Bold = true;
                    if (_state.Foreground is { } fg)
                    {
                        _state.Foreground = AnsiPalette.BrightenIfNormal(fg);
                    }

                    break;
                case 2:
                    _state.Dim = true;
                    break;
                case 3:
                    _state.Italic = true;
                    break;
                case 4:
                    _state.Underline = true;
                    break;
                case 7:
                    _state.Reverse = true;
                    break;
                case 22:
                    _state.Bold = false;
                    _state.Dim = false;
                    break;
                case 23:
                    _state.Italic = false;
                    break;
                case 24:
                    _state.Underline = false;
                    break;
                case 27:
                    _state.Reverse = false;
                    break;
                case 39:
                    _state.Foreground = null;
                    break;
                case 49:
                    _state.Background = null;
                    break;
                case >= 30 and <= 37:
                    _state.Foreground = AnsiPalette.GetForeground(code - 30, bright: _state.Bold);
                    break;
                case >= 40 and <= 47:
                    _state.Background = AnsiPalette.GetBackground(code - 40, bright: false);
                    break;
                case >= 90 and <= 97:
                    _state.Foreground = AnsiPalette.GetForeground(code - 90, bright: true);
                    break;
                case >= 100 and <= 107:
                    _state.Background = AnsiPalette.GetBackground(code - 100, bright: true);
                    break;
                case 38:
                case 48:
                    // Extended colors: 38;5;n or 38;2;r;g;b.
                    ConsumeExtendedColor(parameterText, ref start, setForeground: code == 38);
                    break;
            }
        }
    }

    private void ConsumeExtendedColor(
        ReadOnlySpan<char> parameterText,
        ref int start,
        bool setForeground)
    {
        if (start > parameterText.Length)
        {
            return;
        }

        var modeToken = ReadNextParam(parameterText, ref start);
        if (!int.TryParse(modeToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode))
        {
            return;
        }

        if (mode == 5)
        {
            var indexToken = ReadNextParam(parameterText, ref start);
            if (int.TryParse(indexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                var color = AnsiPalette.GetIndexed(index);
                if (setForeground)
                {
                    _state.Foreground = color;
                }
                else
                {
                    _state.Background = color;
                }
            }

            return;
        }

        if (mode == 2)
        {
            var rTok = ReadNextParam(parameterText, ref start);
            var gTok = ReadNextParam(parameterText, ref start);
            var bTok = ReadNextParam(parameterText, ref start);
            if (int.TryParse(rTok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                && int.TryParse(gTok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
                && int.TryParse(bTok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            {
                var color = Color.FromRgb(ClampByte(r), ClampByte(g), ClampByte(b));
                if (setForeground)
                {
                    _state.Foreground = color;
                }
                else
                {
                    _state.Background = color;
                }
            }
        }
    }

    private static ReadOnlySpan<char> ReadNextParam(ReadOnlySpan<char> parameterText, ref int start)
    {
        if (start >= parameterText.Length)
        {
            return ReadOnlySpan<char>.Empty;
        }

        var sep = parameterText[start..].IndexOf(';');
        if (sep < 0)
        {
            var token = parameterText[start..];
            start = parameterText.Length + 1;
            return token;
        }

        var slice = parameterText.Slice(start, sep);
        start += sep + 1;
        return slice;
    }

    private static byte ClampByte(int value) =>
        (byte)Math.Clamp(value, 0, 255);
}

public readonly struct AnsiTextStyle : IEquatable<AnsiTextStyle>
{
    public AnsiTextStyle(Color? foreground, Color? background, bool bold, bool italic, bool underline)
    {
        Foreground = foreground;
        Background = background;
        Bold = bold;
        Italic = italic;
        Underline = underline;
    }

    public Color? Foreground { get; }
    public Color? Background { get; }
    public bool Bold { get; }
    public bool Italic { get; }
    public bool Underline { get; }

    public bool Equals(AnsiTextStyle other) =>
        Foreground == other.Foreground
        && Background == other.Background
        && Bold == other.Bold
        && Italic == other.Italic
        && Underline == other.Underline;

    public override bool Equals(object? obj) => obj is AnsiTextStyle other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Foreground, Background, Bold, Italic, Underline);
}

public sealed class AnsiTextRun
{
    public AnsiTextRun(string text, AnsiTextStyle style)
    {
        Text = text ?? string.Empty;
        Style = style;
    }

    public string Text { get; }

    public AnsiTextStyle Style { get; }
}

internal struct AnsiSgrState
{
    public Color? Foreground;
    public Color? Background;
    public bool Bold;
    public bool Dim;
    public bool Italic;
    public bool Underline;
    public bool Reverse;

    public AnsiTextStyle ToStyle()
    {
        var fg = Foreground;
        var bg = Background;
        if (Reverse)
        {
            (fg, bg) = (bg ?? Colors.Black, fg ?? Colors.White);
        }

        return new AnsiTextStyle(fg, bg, Bold && !Dim, Italic, Underline);
    }
}

internal static class AnsiPalette
{
    private static readonly Color[] Normal =
    [
        Color.FromRgb(0x00, 0x00, 0x00),
        Color.FromRgb(0xCD, 0x00, 0x00),
        Color.FromRgb(0x00, 0xCD, 0x00),
        Color.FromRgb(0xCD, 0xCD, 0x00),
        Color.FromRgb(0x00, 0x00, 0xEE),
        Color.FromRgb(0xCD, 0x00, 0xCD),
        Color.FromRgb(0x00, 0xCD, 0xCD),
        Color.FromRgb(0xE5, 0xE5, 0xE5)
    ];

    private static readonly Color[] Bright =
    [
        Color.FromRgb(0x7F, 0x7F, 0x7F),
        Color.FromRgb(0xFF, 0x00, 0x00),
        Color.FromRgb(0x00, 0xFF, 0x00),
        Color.FromRgb(0xFF, 0xFF, 0x00),
        Color.FromRgb(0x5C, 0x5C, 0xFF),
        Color.FromRgb(0xFF, 0x00, 0xFF),
        Color.FromRgb(0x00, 0xFF, 0xFF),
        Color.FromRgb(0xFF, 0xFF, 0xFF)
    ];

    public static Color GetForeground(int index, bool bright) =>
        (bright ? Bright : Normal)[Math.Clamp(index, 0, 7)];

    public static Color GetBackground(int index, bool bright) =>
        GetForeground(index, bright);

    public static Color BrightenIfNormal(Color color)
    {
        for (var i = 0; i < Normal.Length; i++)
        {
            if (Normal[i] == color)
            {
                return Bright[i];
            }
        }

        return color;
    }

    public static Color GetIndexed(int index)
    {
        index = Math.Clamp(index, 0, 255);
        if (index < 8)
        {
            return Normal[index];
        }

        if (index < 16)
        {
            return Bright[index - 8];
        }

        if (index < 232)
        {
            var n = index - 16;
            var r = n / 36;
            var g = (n % 36) / 6;
            var b = n % 6;
            return Color.FromRgb(Cube(r), Cube(g), Cube(b));
        }

        var gray = (byte)(8 + (index - 232) * 10);
        return Color.FromRgb(gray, gray, gray);
    }

    private static byte Cube(int level) =>
        level == 0 ? (byte)0 : (byte)(55 + level * 40);
}

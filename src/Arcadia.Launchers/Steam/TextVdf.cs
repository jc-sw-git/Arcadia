using System.Text;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Reads Steam's text VDF / KeyValues (<c>libraryfolders.vdf</c>, <c>appmanifest_*.acf</c>,
/// <c>localconfig.vdf</c>): <c>"key" "value"</c> pairs and <c>"key" { … }</c> blocks. Values are
/// <see cref="string"/> or nested <see cref="VdfMap"/>; lookups ignore case like Steam does.
/// </summary>
public static class TextVdf
{
    /// <summary>Parses a whole file. The root holds the top-level keys, usually exactly one block.</summary>
    /// <exception cref="FormatException">The text is not valid VDF (unbalanced braces, key without value, open quote, nested too deep).</exception>
    public static VdfMap Parse(string text)
    {
        var reader = new Reader(text);
        var root = reader.ReadMap(depth: 0);
        return root;
    }

    private ref struct Reader(string text)
    {
        private readonly string _text = text;
        private int _position;

        public VdfMap ReadMap(int depth)
        {
            if (depth > VdfMap.MaxDepth)
            {
                throw new FormatException($"VDF nested deeper than {VdfMap.MaxDepth} levels.");
            }

            var isRoot = depth == 0;
            var map = new VdfMap();
            while (true)
            {
                var token = Next();
                if (token is null)
                {
                    return isRoot ? map : throw new FormatException("Unexpected end of file inside a block.");
                }

                if (token.Value.Kind == TokenKind.Close)
                {
                    return isRoot ? throw new FormatException("Unexpected '}'.") : map;
                }

                if (token.Value.Kind != TokenKind.String)
                {
                    throw new FormatException("Expected a key.");
                }

                var key = token.Value.Text;
                var value = Next() ?? throw new FormatException($"Key '{key}' has no value.");
                switch (value.Kind)
                {
                    case TokenKind.Open:
                        map.Add(new(key, ReadMap(depth + 1)));
                        break;
                    case TokenKind.String:
                        map.Add(new(key, value.Text));
                        break;
                    default:
                        throw new FormatException($"Key '{key}' has no value.");
                }
            }
        }

        private Token? Next()
        {
            while (true)
            {
                SkipWhitespace();
                if (_position >= _text.Length)
                {
                    return null;
                }

                var c = _text[_position];
                if (c == '/' && Peek(1) == '/')
                {
                    SkipLine();
                    continue;
                }

                // Platform conditions such as [$WIN32] after a value; they don't change the data we read.
                if (c == '[')
                {
                    var end = _text.IndexOf(']', _position);
                    _position = end < 0 ? _text.Length : end + 1;
                    continue;
                }

                switch (c)
                {
                    case '{':
                        _position++;
                        return new Token(TokenKind.Open, string.Empty);
                    case '}':
                        _position++;
                        return new Token(TokenKind.Close, string.Empty);
                    case '"':
                        return new Token(TokenKind.String, ReadQuoted());
                    default:
                        return new Token(TokenKind.String, ReadUnquoted());
                }
            }
        }

        private string ReadQuoted()
        {
            _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var c = _text[_position++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c == '\\' && _position < _text.Length)
                {
                    var escaped = _text[_position++];
                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        _ => escaped,
                    });
                    continue;
                }

                builder.Append(c);
            }

            throw new FormatException("Unterminated quoted string.");
        }

        private string ReadUnquoted()
        {
            var start = _position;
            while (_position < _text.Length && !char.IsWhiteSpace(_text[_position]) && _text[_position] is not ('{' or '}' or '"'))
            {
                _position++;
            }

            return _text[start.._position];
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        private void SkipLine()
        {
            var end = _text.IndexOf('\n', _position);
            _position = end < 0 ? _text.Length : end + 1;
        }

        private readonly char Peek(int offset) =>
            _position + offset < _text.Length ? _text[_position + offset] : '\0';
    }

    private enum TokenKind
    {
        String,
        Open,
        Close,
    }

    private readonly record struct Token(TokenKind Kind, string Text);
}

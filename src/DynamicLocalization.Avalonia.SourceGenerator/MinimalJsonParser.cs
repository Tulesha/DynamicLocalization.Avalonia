using System.Text;

namespace DynamicLocalization.Avalonia.SourceGenerator;

/// <summary>One "localization.json" row: a key plus its per-locale translations, in file order.</summary>
internal readonly struct JsonLocalizationEntry
{
    public JsonLocalizationEntry(string key, IReadOnlyList<KeyValuePair<string, string>> values)
    {
        Key = key;
        Values = values;
    }

    public string Key { get; }

    public IReadOnlyList<KeyValuePair<string, string>> Values { get; }
}

/// <summary>
///     Hand-rolled parser for the flat "localization.json" shape (an array of flat string-only
///     objects). A source generator project intentionally avoids taking a dependency on
///     System.Text.Json to sidestep analyzer/host version conflicts.
/// </summary>
internal static class MinimalJsonParser
{
    public static List<JsonLocalizationEntry> ParseEntries(string json)
    {
        var parser = new Parser(json);
        return parser.ParseTopLevelArray();
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _pos;

        public Parser(string text)
        {
            _text = text;
            _pos = 0;
        }

        public List<JsonLocalizationEntry> ParseTopLevelArray()
        {
            SkipWhitespace();
            Expect('[');
            var result = new List<JsonLocalizationEntry>();

            SkipWhitespace();
            if (Peek() == ']')
            {
                _pos++;
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                result.Add(ParseEntryObject());
                SkipWhitespace();
                var c = Next();
                if (c == ',')
                {
                    continue;
                }

                if (c == ']')
                {
                    break;
                }

                throw new FormatException($"Unexpected character '{c}' at position {_pos - 1}; expected ',' or ']'.");
            }

            return result;
        }

        private JsonLocalizationEntry ParseEntryObject()
        {
            Expect('{');
            string? key = null;
            var values = new List<KeyValuePair<string, string>>();

            SkipWhitespace();
            if (Peek() == '}')
            {
                _pos++;
            }
            else
            {
                while (true)
                {
                    SkipWhitespace();
                    var propertyName = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    var propertyValue = ParseValue();

                    if (string.Equals(propertyName, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        key = propertyValue;
                    }
                    else if (propertyValue is not null)
                    {
                        values.Add(new KeyValuePair<string, string>(propertyName, propertyValue));
                    }

                    SkipWhitespace();
                    var c = Next();
                    if (c == ',')
                    {
                        continue;
                    }

                    if (c == '}')
                    {
                        break;
                    }

                    throw new FormatException(
                        $"Unexpected character '{c}' at position {_pos - 1}; expected ',' or '}}'.");
                }
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new FormatException("A localization entry is missing a non-empty 'Key' property.");
            }

            return new JsonLocalizationEntry(key!, values);
        }

        private string? ParseValue()
        {
            var c = Peek();
            if (c == '"')
            {
                return ParseString();
            }

            if (MatchLiteral("null"))
            {
                return null;
            }

            if (MatchLiteral("true"))
            {
                return "true";
            }

            if (MatchLiteral("false"))
            {
                return "false";
            }

            throw new FormatException($"Unexpected value at position {_pos}; only string properties are supported.");
        }

        private bool MatchLiteral(string literal)
        {
            if (_pos + literal.Length <= _text.Length &&
                string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) == 0)
            {
                _pos += literal.Length;
                return true;
            }

            return false;
        }

        private string ParseString()
        {
            Expect('"');
            var sb = new StringBuilder();

            while (true)
            {
                if (_pos >= _text.Length)
                {
                    throw new FormatException("Unterminated string literal.");
                }

                var c = _text[_pos++];
                if (c == '"')
                {
                    break;
                }

                if (c == '\\')
                {
                    if (_pos >= _text.Length)
                    {
                        throw new FormatException("Unterminated escape sequence.");
                    }

                    var esc = _text[_pos++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length)
                            {
                                throw new FormatException("Invalid unicode escape sequence.");
                            }

                            var hex = _text.Substring(_pos, 4);
                            _pos += 4;
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            break;
                        default:
                            throw new FormatException($"Invalid escape sequence '\\{esc}'.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
        }

        private char Peek()
        {
            if (_pos >= _text.Length)
            {
                throw new FormatException("Unexpected end of JSON.");
            }

            return _text[_pos];
        }

        private char Next()
        {
            if (_pos >= _text.Length)
            {
                throw new FormatException("Unexpected end of JSON.");
            }

            return _text[_pos++];
        }

        private void Expect(char expected)
        {
            var c = Next();
            if (c != expected)
            {
                throw new FormatException($"Expected '{expected}' but found '{c}' at position {_pos - 1}.");
            }
        }
    }
}
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Minimal, dependency-free JSON parser for Claude Code's NDJSON stream events.
    /// Deserializes into plain object graphs: Dictionary&lt;string, object&gt;,
    /// List&lt;object&gt;, string, double, bool, and null. Unity's built-in JsonUtility
    /// cannot handle the dynamic, deeply-nested, mixed-type shapes the CLI emits, so we
    /// parse them ourselves. Based on the public-domain MiniJSON pattern.
    /// </summary>
    public static class ClaudeJson
    {
        /// <summary>Parse a single JSON document. Returns null on empty/invalid input.</summary>
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var p = new Parser(json);
            return p.ParseValue();
        }

        // ---- Tolerant accessors (never throw on missing / wrong-typed fields) ----

        public static object Get(object o, string key)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(key, out var v)) return v;
            return null;
        }

        public static string GetString(object o, string key) => Get(o, key) as string;

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string s)
            {
                _s = s;
                _i = 0;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (_i >= _s.Length) return null;
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't':
                    case 'f': return ParseBool();
                    case 'n': return ParseNull();
                    default: return ParseNumber();
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                _i++; // consume '{'
                while (true)
                {
                    SkipWhitespace();
                    if (_i >= _s.Length) break;
                    char c = _s[_i];
                    if (c == '}') { _i++; break; }
                    if (c == ',') { _i++; continue; }
                    if (c != '"') break; // malformed; bail out gracefully
                    string key = ParseString();
                    SkipWhitespace();
                    if (_i < _s.Length && _s[_i] == ':') _i++;
                    dict[key] = ParseValue();
                }
                return dict;
            }

            List<object> ParseArray()
            {
                var list = new List<object>();
                _i++; // consume '['
                while (true)
                {
                    SkipWhitespace();
                    if (_i >= _s.Length) break;
                    char c = _s[_i];
                    if (c == ']') { _i++; break; }
                    if (c == ',') { _i++; continue; }
                    list.Add(ParseValue());
                }
                return list;
            }

            string ParseString()
            {
                var sb = new StringBuilder();
                _i++; // consume opening quote
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) break;
                        char e = _s[_i++];
                        switch (e)
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
                                if (_i + 4 <= _s.Length)
                                {
                                    var hex = _s.Substring(_i, 4);
                                    if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                        sb.Append((char)code);
                                    _i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            object ParseBool()
            {
                if (_i + 4 <= _s.Length && _s.Substring(_i, 4) == "true") { _i += 4; return true; }
                if (_i + 5 <= _s.Length && _s.Substring(_i, 5) == "false") { _i += 5; return false; }
                _i++;
                return null;
            }

            object ParseNull()
            {
                if (_i + 4 <= _s.Length && _s.Substring(_i, 4) == "null") _i += 4;
                else _i++;
                return null;
            }

            object ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || (c >= '0' && c <= '9')) _i++;
                    else break;
                }
                if (_i == start) { _i++; return null; } // no progress on junk char; skip it
                string num = _s.Substring(start, _i - start);
                if (double.TryParse(num, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
                return null;
            }

            void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }
        }
    }
}

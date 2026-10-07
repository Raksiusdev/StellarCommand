using System.Collections.Generic;
using System.Text;

namespace StellarCommand.SaveParser
{
    /// <summary>
    /// Parses Paradox Development Studio's custom script format used in Stellaris gamestate files.
    /// Format: key=value pairs, nested blocks with { }, quoted strings, and # line comments.
    /// </summary>
    public static class PdxScriptParser
    {
        public class PdxNode
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public List<PdxNode> Children { get; } = new List<PdxNode>();
            public bool IsBlock => Children.Count > 0;

            public PdxNode GetChild(string key)
            {
                foreach (var c in Children)
                    if (c.Key == key) return c;
                return null;
            }

            public List<PdxNode> GetChildren(string key)
            {
                var result = new List<PdxNode>();
                foreach (var c in Children)
                    if (c.Key == key) result.Add(c);
                return result;
            }

            public string GetValue(string key, string fallback = "")
            {
                var child = GetChild(key);
                return child?.Value ?? fallback;
            }

            public float GetFloat(string key, float fallback = 0f)
            {
                var val = GetValue(key);
                return float.TryParse(val, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float result) ? result : fallback;
            }

            public int GetInt(string key, int fallback = 0)
            {
                var val = GetValue(key);
                return int.TryParse(val, out int result) ? result : fallback;
            }
        }

        public static PdxNode Parse(string text)
        {
            var root = new PdxNode { Key = "root" };
            int pos = 0;
            ParseBlock(text, ref pos, root);
            return root;
        }

        private static void ParseBlock(string text, ref int pos, PdxNode parent)
        {
            int len = text.Length;

            while (pos < len)
            {
                SkipWhitespaceAndComments(text, ref pos);
                if (pos >= len) break;

                char c = text[pos];

                if (c == '}') { pos++; return; }

                // Anonymous block "{ ... }" (list items, variables, owned_fleets...). Its entries are
                // merged into the parent; parsing recursively consumes the matching '}'.
                if (c == '{')
                {
                    pos++;
                    ParseBlock(text, ref pos, parent);
                    continue;
                }

                string key = ReadToken(text, ref pos);
                if (string.IsNullOrEmpty(key)) { pos++; continue; }

                SkipWhitespaceAndComments(text, ref pos);
                if (pos >= len) break;

                // handle entries without = (bare values inside lists)
                if (text[pos] != '=')
                {
                    var bareNode = new PdxNode { Key = key, Value = key };
                    parent.Children.Add(bareNode);
                    continue;
                }

                pos++; // consume '='
                SkipWhitespaceAndComments(text, ref pos);
                if (pos >= len) break;

                if (text[pos] == '{')
                {
                    pos++; // consume '{'
                    var blockNode = new PdxNode { Key = key };
                    ParseBlock(text, ref pos, blockNode);
                    parent.Children.Add(blockNode);
                }
                else
                {
                    string value = ReadToken(text, ref pos);
                    parent.Children.Add(new PdxNode { Key = key, Value = value });
                }
            }
        }

        private static void SkipWhitespaceAndComments(string text, ref int pos)
        {
            int len = text.Length;
            while (pos < len)
            {
                char c = text[pos];
                if (c == '#')
                {
                    while (pos < len && text[pos] != '\n') pos++;
                }
                else if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    pos++;
                }
                else break;
            }
        }

        private static string ReadToken(string text, ref int pos)
        {
            int len = text.Length;
            if (pos >= len) return "";

            if (text[pos] == '"')
            {
                pos++; // skip opening quote
                var sb = new StringBuilder();
                while (pos < len && text[pos] != '"')
                {
                    if (text[pos] == '\\' && pos + 1 < len) pos++; // skip escape char
                    sb.Append(text[pos++]);
                }
                if (pos < len) pos++; // skip closing quote
                return sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                while (pos < len)
                {
                    char c = text[pos];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' ||
                        c == '=' || c == '{' || c == '}' || c == '#')
                        break;
                    sb.Append(c);
                    pos++;
                }
                return sb.ToString();
            }
        }
    }
}

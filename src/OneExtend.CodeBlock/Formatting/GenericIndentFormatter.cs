using System;
using System.Collections.Generic;
using System.Text;

namespace OneExtend.CodeBlock.Formatting
{
    /// <summary>
    /// Structure-aware re-indenter for brace languages (JS/TS/TSX/Java/CSS) and
    /// tag languages (HTML). First pass inserts line breaks at structural
    /// boundaries (outside strings/comments); second pass recomputes leading
    /// indentation from nesting depth. Line content itself is preserved.
    /// </summary>
    public sealed class GenericIndentFormatter : ICodeFormatter
    {
        public enum Mode { Brace, Tag }

        private readonly Mode _mode;

        public GenericIndentFormatter(Mode mode) { _mode = mode; }

        public string Id => _mode == Mode.Brace ? "generic-brace" : "generic-tag";

        public string Format(string source, FormatOptions options)
        {
            if (string.IsNullOrEmpty(source))
                return source;
            var indentWidth = Math.Max(1, options.IndentWidth);
            var structured = _mode == Mode.Brace
                ? StructureBraces(source, indentWidth)
                : StructureTags(source);
            return ApplyIndents(structured, indentWidth, _mode);
        }

        // ---- pass 1: structural line breaks -------------------------------------

        private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');

        private string StructureBraces(string source, int indentWidth)
        {
            var s = Normalize(source).Replace("\t", new string(' ', indentWidth));
            var sb = new StringBuilder(s.Length + 64);
            var inLineComment = false;
            var inBlockComment = false;
            var quote = '\0';
            var escaped = false;
            var parenDepth = 0;

            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                var next = i + 1 < s.Length ? s[i + 1] : '\0';

                if (inLineComment)
                {
                    sb.Append(c);
                    if (c == '\n')
                        inLineComment = false;
                    continue;
                }
                if (inBlockComment)
                {
                    sb.Append(c);
                    if (c == '*' && next == '/')
                    {
                        sb.Append(next);
                        i++;
                        inBlockComment = false;
                    }
                    continue;
                }
                if (quote != '\0')
                {
                    sb.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == quote) quote = '\0';
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    inLineComment = true;
                    sb.Append(c);
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    inBlockComment = true;
                    sb.Append(c);
                    continue;
                }
                if (c == '\'' || c == '"' || c == '`')
                {
                    quote = c;
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '(':
                        parenDepth++;
                        sb.Append(c);
                        break;
                    case ')':
                        parenDepth = Math.Max(0, parenDepth - 1);
                        sb.Append(c);
                        break;
                    case '{':
                        if (sb.Length > 0)
                        {
                            var last = sb[sb.Length - 1];
                            if (last != ' ' && last != '\n' && last != '\t' && last != '(')
                                sb.Append(' ');
                        }
                        sb.Append(c).Append('\n');
                        break;
                    case '}':
                        TrimTrailingSpaces(sb);
                        if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                            sb.Append('\n');
                        sb.Append(c).Append('\n');
                        break;
                    case ';':
                        sb.Append(c);
                        if (parenDepth == 0)
                            sb.Append('\n');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private string StructureTags(string source)
        {
            var s = Normalize(source);
            var sb = new StringBuilder(s.Length + 64);
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '<')
                {
                    var close = FindTagEnd(s, i);
                    if (close < 0)
                    {
                        sb.Append(s.Substring(i));
                        break;
                    }
                    // keep comments/doctypes on their own line
                    var tag = s.Substring(i, close - i + 1);
                    sb.Append(tag).Append('\n');
                    i = close + 1;
                }
                else
                {
                    var next = s.IndexOf('<', i);
                    if (next < 0)
                    {
                        sb.Append(s.Substring(i));
                        break;
                    }
                    var text = s.Substring(i, next - i);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var trimmed = text.Trim();
                        sb.Append(trimmed).Append('\n');
                    }
                    i = next;
                }
            }
            return sb.ToString();
        }

        private static int FindTagEnd(string s, int start)
        {
            var quote = '\0';
            for (var i = start; i < s.Length; i++)
            {
                var c = s[i];
                if (quote != '\0')
                {
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'') { quote = c; continue; }
                if (c == '>') return i;
            }
            return -1;
        }

        private static void TrimTrailingSpaces(StringBuilder sb)
        {
            var j = sb.Length - 1;
            while (j >= 0 && (sb[j] == ' ' || sb[j] == '\t'))
                j--;
            sb.Length = j + 1;
        }

        // ---- pass 2: indentation ------------------------------------------------

        private string ApplyIndents(string structured, int indentWidth, Mode mode)
        {
            var lines = structured.Split('\n');
            var sb = new StringBuilder(structured.Length + 64);
            var depth = 0;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    sb.Append('\n');
                    continue;
                }

                if (mode == Mode.Brace)
                {
                    var leadingClosers = CountLeading(line, '}');
                    var indentDepth = Math.Max(0, depth - leadingClosers);
                    sb.Append(new string(' ', indentDepth * indentWidth)).Append(line).Append('\n');
                    depth = Math.Max(0, depth + CountChar(line, '{') - CountChar(line, '}'));
                }
                else
                {
                    var leadingClosers = line.StartsWith("</", StringComparison.Ordinal) ? 1 : 0;
                    var indentDepth = Math.Max(0, depth - leadingClosers);
                    sb.Append(new string(' ', indentDepth * indentWidth)).Append(line).Append('\n');
                    var opens = 0;
                    var closes = 0;
                    var k = 0;
                    while (k < line.Length)
                    {
                        if (line[k] == '<')
                        {
                            if (k + 1 < line.Length && line[k + 1] == '/') closes++;
                            else if (k + 1 < line.Length && (char.IsLetter(line[k + 1]))) opens++;
                        }
                        k++;
                    }
                    // self-closing tags net out
                    if (line.EndsWith("/>", StringComparison.Ordinal) && opens > 0)
                        opens--;
                    depth = Math.Max(0, depth + opens - closes);
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static int CountLeading(string s, char c)
        {
            var n = 0;
            while (n < s.Length && s[n] == c) n++;
            return n;
        }

        private static int CountChar(string s, char c)
        {
            var n = 0;
            foreach (var ch in s)
                if (ch == c) n++;
            return n;
        }
    }
}

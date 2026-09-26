using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneExtend.CodeBlock.Model;

namespace OneExtend.CodeBlock.Formatting
{
    /// <summary>
    /// Structured SQL layout engine (FR-904/905/906): clauses on their own
    /// right-aligned lines, JOIN segments, AND/OR alignment, column lists,
    /// CASE blocks and subqueries indented, keyword casing. Works on the token
    /// stream of any SQL-family grammar (ANSI / Oracle / T-SQL / MySQL).
    /// </summary>
    public sealed class SqlFormatter : ICodeFormatter
    {
        private static readonly string[] MultiWordClauses = { "GROUP BY", "ORDER BY", "UNION ALL", "UNION", "INSERT INTO", "FOR UPDATE", "DELETE FROM" };
        private static readonly HashSet<string> SingleClauses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "HAVING", "LIMIT", "OFFSET", "FETCH", "RETURNING",
            "VALUES", "SET", "WITH", "INTERSECT", "EXCEPT", "UPDATE"
        };
        private static readonly HashSet<string> JoinQualifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER"
        };

        private readonly Tokenizer _tokenizer;

        public SqlFormatter(Tokenizer tokenizer) { _tokenizer = tokenizer; }

        public string Id => "sql-structured";

        public string Format(string source, FormatOptions options)
        {
            if (string.IsNullOrEmpty(source))
                return source;
            var tokens = TokenFilters.Clean(_tokenizer.Tokenize(TokenFilters.Normalize(source)));
            var statements = SplitStatements(tokens);
            var sb = new StringBuilder();
            for (var i = 0; i < statements.Count; i++)
            {
                if (i > 0)
                    sb.Append('\n');
                var lines = LayoutStatement(statements[i], options, 0);
                foreach (var line in lines)
                {
                    sb.Append(line);
                    if (i < statements.Count - 1 || line != lines.Last())
                        sb.Append('\n');
                }
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Lays out one statement (tokens include the trailing ';' when present).</summary>
        public List<string> LayoutStatement(IReadOnlyList<Token> statement, FormatOptions o, int baseIndent)
        {
            var tokens = TokenFilters.Clean(statement.ToList());
            var hasTerminator = tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenType.Punctuation
                                && tokens[tokens.Count - 1].Text == ";";
            if (hasTerminator)
                tokens.RemoveAt(tokens.Count - 1);

            var lines = new List<string>();
            var i = 0;
            while (i < tokens.Count && tokens[i].Type == TokenType.Comment)
            {
                foreach (var l in tokens[i].Text.Split('\n'))
                    lines.Add(Indent(baseIndent) + l.TrimEnd());
                i++;
            }
            var body = tokens.Skip(i).ToList();
            if (body.Count > 0)
            {
                var firstWord = WordOf(body[0]);
                List<string> bodyLines;
                switch (firstWord)
                {
                    case "SELECT":
                    case "WITH":
                        bodyLines = LayoutSelect(body, o);
                        break;
                    case "INSERT":
                        bodyLines = LayoutInsert(body, o);
                        break;
                    case "UPDATE":
                        bodyLines = LayoutUpdate(body, o);
                        break;
                    case "DELETE":
                        bodyLines = LayoutDelete(body, o);
                        break;
                    default:
                        bodyLines = new List<string> { RenderExpr(body, o) };
                        break;
                }
                foreach (var l in bodyLines)
                    lines.Add(string.IsNullOrEmpty(l) ? l : Indent(baseIndent) + l);
            }

            if (hasTerminator && lines.Count > 0)
                lines[lines.Count - 1] += ";";
            return lines;
        }

        // ---- statement splitters -------------------------------------------------

        private static List<List<Token>> SplitStatements(List<Token> tokens)
        {
            var result = new List<List<Token>>();
            var current = new List<Token>();
            var depth = 0;
            foreach (var t in tokens)
            {
                if (t.Type == TokenType.Punctuation)
                {
                    if (t.Text == "(") depth++;
                    else if (t.Text == ")") depth = Math.Max(0, depth - 1);
                    else if (t.Text == ";" && depth == 0)
                    {
                        current.Add(t);
                        result.Add(current);
                        current = new List<Token>();
                        continue;
                    }
                }
                current.Add(t);
            }
            if (current.Count > 0)
                result.Add(current);
            return result;
        }

        // ---- clause partitioning ---------------------------------------------------

        private sealed class Segment
        {
            /// <summary>Uppercase form used for ordinal comparisons.</summary>
            public string Display;
            /// <summary>Original casing used for rendering.</summary>
            public string RawDisplay;
            public List<Token> Expr = new List<Token>();
            public bool IsJoin;
        }

        private static List<Segment> Partition(List<Token> body)
        {
            var segments = new List<Segment>();
            Segment current = null;
            var depth = 0;
            var i = 0;
            while (i < body.Count)
            {
                if (depth == 0)
                {
                    var clause = TryClauseDisplay(body, ref i);
                    if (clause != null)
                    {
                        current = new Segment { Display = clause.ToUpperInvariant(), RawDisplay = clause };
                        segments.Add(current);
                        continue;
                    }
                    var join = TryJoinDisplay(body, ref i, segments, current);
                    if (join != null)
                    {
                        current = join;
                        segments.Add(current);
                        continue;
                    }
                }
                var tok = body[i];
                if (tok.Type == TokenType.Punctuation && tok.Text == "(") depth++;
                else if (tok.Type == TokenType.Punctuation && tok.Text == ")") depth = Math.Max(0, depth - 1);
                if (current == null)
                {
                    current = new Segment { Display = string.Empty };
                    segments.Add(current);
                }
                current.Expr.Add(tok);
                i++;
            }
            return segments;
        }

        private static string TryClauseDisplay(List<Token> body, ref int i)
        {
            var first = body[i];
            if (first.Type != TokenType.Keyword && first.Type != TokenType.ControlKeyword)
                return null;
            var word = WordOf(first);

            foreach (var mw in MultiWordClauses)
            {
                var parts = mw.Split(' ');
                if (!string.Equals(word, parts[0], StringComparison.OrdinalIgnoreCase))
                    continue;
                if (parts.Length == 2 && i + 1 < body.Count &&
                    body[i + 1].Type == TokenType.Keyword &&
                    string.Equals(WordOf(body[i + 1]), parts[1], StringComparison.OrdinalIgnoreCase))
                {
                    i += 2;
                    return body[i - 2].Text + " " + body[i - 1].Text;
                }
                if (parts.Length == 1)
                {
                    i += 1;
                    return body[i - 1].Text;
                }
            }
            if (SingleClauses.Contains(word))
            {
                i += 1;
                return body[i - 1].Text;
            }
            return null;
        }

        private static Segment TryJoinDisplay(List<Token> body, ref int i, List<Segment> segments, Segment current)
        {
            if (current == null || (!string.Equals(current.Display, "FROM", StringComparison.OrdinalIgnoreCase) && !current.IsJoin))
                return null;
            var word = WordOf(body[i]);
            var qualifiers = new List<string>();
            var j = i;
            while (j < body.Count && body[j].Type == TokenType.Keyword && JoinQualifiers.Contains(WordOf(body[j])))
            {
                qualifiers.Add(body[j].Text);
                j++;
            }
            if (j < body.Count && body[j].Type == TokenType.Keyword && string.Equals(WordOf(body[j]), "JOIN", StringComparison.OrdinalIgnoreCase))
            {
                var raw = string.Join(" ", qualifiers.Concat(new[] { body[j].Text }));
                // qualifier tokens were never added to the FROM expression (they are
                // still ahead of i), so nothing to remove - just consume them here.
                i = j + 1;
                return new Segment { Display = raw.ToUpperInvariant(), RawDisplay = raw, IsJoin = true };
            }
            return null;
        }

        // ---- per-statement layouts -------------------------------------------------

        private List<string> LayoutSelect(List<Token> body, FormatOptions o)
        {
            var segments = Partition(body);
            var lines = new List<string>();
            if (segments.Count == 0)
                return lines;

            var maxW = segments.Count > 1
                ? segments.Skip(1).Max(s => s.Display.Length)
                : 0;

            // first clause: SELECT (or WITH): columns on aligned lines
            var first = segments[0];
            var header = string.IsNullOrEmpty(first.Display) ? string.Empty : ClauseText(first.RawDisplay ?? first.Display, o);
            var exprs = SplitOnCommas(first.Expr);
            for (var c = 0; c < exprs.Count; c++)
            {
                var prefix = c == 0
                    ? header + " "
                    : new string(' ', string.IsNullOrEmpty(header) ? 0 : header.Length + 1);
                var rendered = RenderExpr(exprs[c], o);
                AppendExprLines(lines, prefix, rendered, o.CommaPosition, c < exprs.Count - 1);
            }

            for (var s = 1; s < segments.Count; s++)
            {
                var seg = segments[s];
                var clausePad = new string(' ', Math.Max(0, maxW - seg.Display.Length)) + ClauseText(seg.RawDisplay ?? seg.Display, o);

                if (seg.IsJoin)
                {
                    lines.Add(clausePad + " " + RenderExpr(seg.Expr, o));
                    continue;
                }
                switch (seg.Display)
                {
                    case "WHERE":
                        AppendWhere(lines, clausePad, seg.Expr, o);
                        break;
                    case "GROUP BY":
                    case "ORDER BY":
                    case "HAVING":
                        AppendAlignedList(lines, clausePad, seg.Expr, maxW + 1, o);
                        break;
                    default:
                        // FROM / UNION / WITH tails etc.
                        var e = RenderExpr(seg.Expr, o);
                        if (e.Length == 0)
                            lines.Add(clausePad.TrimEnd());
                        else
                            lines.Add(clausePad + " " + e);
                        break;
                }
            }
            return lines;
        }

        private List<string> LayoutInsert(List<Token> body, FormatOptions o)
        {
            var segments = Partition(body);
            var lines = new List<string>();
            foreach (var seg in segments)
            {
                var insertClause = ClauseText(seg.RawDisplay ?? seg.Display, o);
                if (lines.Count == 0)
                {
                    lines.Add(insertClause + " " + RenderExpr(seg.Expr, o));
                }
                else if (seg.Display == "VALUES")
                {
                    var rows = SplitOnCommas(seg.Expr);
                    for (var r = 0; r < rows.Count; r++)
                    {
                        var prefix = r == 0 ? insertClause + " " : new string(' ', insertClause.Length + 1);
                        lines.Add(prefix + RenderExpr(rows[r], o));
                    }
                }
                else
                {
                    lines.Add(insertClause + " " + RenderExpr(seg.Expr, o));
                }
            }
            return lines;
        }

        private List<string> LayoutUpdate(List<Token> body, FormatOptions o)
        {
            var segments = Partition(body);
            var lines = new List<string>();
            var maxW = segments.Count > 1 ? segments.Skip(1).Max(s => s.Display.Length) : 0;
            for (var s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                if (s == 0)
                {
                    lines.Add(ClauseText(seg.RawDisplay ?? seg.Display, o) + " " + RenderExpr(seg.Expr, o));
                    continue;
                }
                var clausePad = new string(' ', Math.Max(0, maxW - seg.Display.Length)) + ClauseText(seg.RawDisplay ?? seg.Display, o);
                if (seg.Display == "SET")
                    AppendAlignedList(lines, clausePad, seg.Expr, maxW + 1, o);
                else if (seg.Display == "WHERE")
                    AppendWhere(lines, clausePad, seg.Expr, o);
                else
                    lines.Add(clausePad + " " + RenderExpr(seg.Expr, o));
            }
            return lines;
        }

        private List<string> LayoutDelete(List<Token> body, FormatOptions o)
        {
            var segments = Partition(body);
            var lines = new List<string>();
            foreach (var seg in segments)
            {
                if (seg.Display == "WHERE" && lines.Count > 0)
                {
                    var maxW = segments.Skip(1).DefaultIfEmpty(seg).Max(x => x.Display.Length);
                    AppendWhere(lines, new string(' ', Math.Max(0, maxW - seg.Display.Length)) + ClauseText(seg.RawDisplay ?? seg.Display, o), seg.Expr, o);
                }
                else
                {
                    var clause = ClauseText(seg.RawDisplay ?? seg.Display, o);
                    lines.Add((string.IsNullOrEmpty(clause) ? string.Empty : clause + " ") + RenderExpr(seg.Expr, o));
                }
            }
            return lines;
        }

        // ---- clause renderers --------------------------------------------------------

        private void AppendWhere(List<string> lines, string clausePad, List<Token> expr, FormatOptions o)
        {
            var conds = SplitOnAndOr(expr);
            for (var c = 0; c < conds.Count; c++)
            {
                var rendered = RenderExpr(conds[c].Tokens, o);
                var firstLines = rendered.Split('\n');
                if (c == 0)
                {
                    lines.Add(clausePad + " " + firstLines[0].TrimStart());
                    for (var k = 1; k < firstLines.Length; k++)
                        lines.Add(firstLines[k]);
                }
                else if (o.AlignAndOr)
                {
                    var conj = conds[c].Conjunction ?? "AND";
                    lines.Add("   " + conj + " " + firstLines[0].TrimStart());
                    for (var k = 1; k < firstLines.Length; k++)
                        lines.Add("     " + firstLines[k].TrimStart());
                }
                else
                {
                    var lastIndex = lines.Count - 1;
                    lines[lastIndex] = lines[lastIndex] + " " + (conds[c].Conjunction ?? "AND") + " " + rendered.Replace("\n", " ");
                }
            }
        }

        private void AppendAlignedList(List<string> lines, string clausePad, List<Token> expr, int alignCol, FormatOptions o)
        {
            var items = SplitOnCommas(expr);
            for (var c = 0; c < items.Count; c++)
            {
                var prefix = c == 0 ? clausePad + " " : new string(' ', alignCol);
                AppendExprLines(lines, prefix, RenderExpr(items[c], o), o.CommaPosition, c < items.Count - 1);
            }
        }

        /// <summary>Adds a (possibly multi-line) rendered expression, placing commas per option.</summary>
        private static void AppendExprLines(List<string> lines, string prefix, string rendered, CommaPosition commaPos, bool moreFollow)
        {
            var parts = rendered.Split('\n');
            lines.Add(prefix + parts[0].TrimStart());
            for (var k = 1; k < parts.Length; k++)
                lines.Add(parts[k]);
            if (moreFollow)
            {
                if (commaPos == CommaPosition.Trailing)
                    lines[lines.Count - 1] += ",";
                else
                    lines[lines.Count - 1] = lines[lines.Count - 1] + ",";
            }
        }

        // ---- expression renderer -------------------------------------------------------

        /// <summary>
        /// Renders tokens with spacing rules; CASE blocks and parenthesized
        /// subqueries become multi-line with relative indentation baked in.
        /// </summary>
        internal string RenderExpr(List<Token> toks, FormatOptions o)
        {
            var sb = new StringBuilder();
            var buffer = new List<Token>();
            var i = 0;
            while (i < toks.Count)
            {
                var t = toks[i];
                var word = WordOf(t);

                if (t.Type == TokenType.ControlKeyword && string.Equals(word, "CASE", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(JoinTokens(buffer, o));
                    buffer.Clear();
                    sb.Append("CASE");
                    i++;
                    var caseDepth = 1;
                    var line = new StringBuilder();
                    while (i < toks.Count && caseDepth > 0)
                    {
                        var ct = toks[i];
                        var cw = WordOf(ct);
                        if (ct.Type == TokenType.ControlKeyword && string.Equals(cw, "CASE", StringComparison.OrdinalIgnoreCase))
                        {
                            caseDepth++;
                            line.Append(' ').Append(Display(ct, o));
                        }
                        else if (ct.Type == TokenType.ControlKeyword && string.Equals(cw, "END", StringComparison.OrdinalIgnoreCase))
                        {
                            caseDepth--;
                            if (caseDepth == 0)
                            {
                                if (line.Length > 0)
                                    sb.Append("\n  ").Append(line.ToString().Trim());
                                sb.Append("\nEND");
                                i++; // consume the END token itself
                                break;
                            }
                            line.Append(' ').Append(Display(ct, o));
                        }
                        else if (caseDepth == 1 && (ct.Type == TokenType.ControlKeyword || ct.Type == TokenType.Keyword) &&
                                 (string.Equals(cw, "WHEN", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(cw, "ELSE", StringComparison.OrdinalIgnoreCase)))
                        {
                            // WHEN and ELSE both start their own line
                            if (line.Length > 0)
                                sb.Append("\n  ").Append(line.ToString().Trim());
                            line.Clear();
                            line.Append(Display(ct, o));
                        }
                        else
                        {
                            line.Append(' ').Append(Display(ct, o));
                        }
                        i++;
                    }
                    continue;
                }

                if (t.Type == TokenType.Punctuation && t.Text == "(")
                {
                    var group = ReadGroup(toks, i);
                    if (GroupContainsSelect(group))
                    {
                        sb.Append(JoinTokens(buffer, o));
                        buffer.Clear();
                        if (sb.Length > 0 && sb[sb.Length - 1] != '(' && sb[sb.Length - 1] != '.')
                            sb.Append(' ');
                        sb.Append('(').Append('\n');
                        var inner = LayoutStatement(group, o, 4);
                        foreach (var l in inner)
                            sb.Append(l).Append('\n');
                        sb.Append(')');
                        i = i + group.Count + 2;
                        continue;
                    }
                }

                buffer.Add(t);
                i++;
            }
            sb.Append(JoinTokens(buffer, o));
            return sb.ToString().TrimEnd();
        }

        private static List<Token> ReadGroup(List<Token> toks, int openIndex)
        {
            var depth = 0;
            var result = new List<Token>();
            for (var i = openIndex + 1; i < toks.Count; i++)
            {
                var t = toks[i];
                if (t.Type == TokenType.Punctuation && t.Text == "(") depth++;
                else if (t.Type == TokenType.Punctuation && t.Text == ")")
                {
                    if (depth == 0) return result;
                    depth--;
                }
                result.Add(t);
            }
            return result;
        }

        private static bool GroupContainsSelect(List<Token> group)
        {
            var depth = 0;
            foreach (var t in group)
            {
                if (t.Type == TokenType.Punctuation && t.Text == "(") depth++;
                else if (t.Type == TokenType.Punctuation && t.Text == ")") depth--;
                else if (depth == 0 && t.Type == TokenType.Keyword &&
                         string.Equals(WordOf(t), "SELECT", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ---- token joining / spacing -----------------------------------------------------

        internal string JoinTokens(List<Token> toks, FormatOptions o)
        {
            var sb = new StringBuilder();
            Token prev = null;
            foreach (var t in toks)
            {
                if (prev != null)
                {
                    var needSpace = NeedsSpace(prev, t);
                    // unary minus directly after '(' or ',': "(-1", not "(- 1"
                    if (needSpace &&
                        prev.Type == TokenType.Operator && prev.Text == "-" &&
                        sb.Length > 0 && sb[sb.Length - 1] == '-')
                    {
                        var k = sb.Length - 2;
                        while (k >= 0 && sb[k] == ' ') k--;
                        if (k >= 0 && (sb[k] == '(' || sb[k] == ','))
                            needSpace = false;
                    }
                    if (needSpace)
                        sb.Append(' ');
                }
                sb.Append(Display(t, o));
                prev = t;
            }
            return sb.ToString();
        }

        private static bool NeedsSpace(Token a, Token b)
        {
            var bt = b.Text;
            if (bt == "," || bt == ";" || bt == ")" || bt == ".")
                return false;
            var at = a.Text;
            if (at == "(" || at == ".")
                return false;
            if (bt == "(")
            {
                if (a.Type == TokenType.Identifier || a.Type == TokenType.Function || a.Type == TokenType.BuiltinPackage)
                    return false;
                return true;
            }
            if (b.Type == TokenType.Operator || a.Type == TokenType.Operator)
                return true;
            return true;
        }

        internal static string Display(Token t, FormatOptions o)
        {
            switch (t.Type)
            {
                case TokenType.Keyword:
                case TokenType.ControlKeyword:
                case TokenType.Type:
                case TokenType.Function:
                    return ApplyCase(t.Text, o.KeywordCase);
                case TokenType.BuiltinPackage:
                    return t.Text.ToUpperInvariant();
                default:
                    return t.Text;
            }
        }

        /// <summary>Applies keyword casing to a (possibly multi-word) clause display.</summary>
        private static string ClauseText(string raw, FormatOptions o)
        {
            if (string.IsNullOrEmpty(raw) || o.KeywordCase == KeywordCase.Preserve)
                return raw;
            return string.Join(" ", raw.Split(' ').Select(w => ApplyCase(w, o.KeywordCase)));
        }

        public static string ApplyCase(string word, KeywordCase c)
        {
            switch (c)
            {
                case KeywordCase.Upper: return word.ToUpperInvariant();
                case KeywordCase.Lower: return word.ToLowerInvariant();
                case KeywordCase.Capitalize:
                    return word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
                default: return word;
            }
        }

        // ---- misc helpers -----------------------------------------------------------------

        // Returns the comparison word for clause/keyword dispatch (uppercased;
        // every call site compares case-insensitively).
        private static string WordOf(Token t) => t.Text.ToUpperInvariant();

        private static List<List<Token>> SplitOnCommas(List<Token> expr)
        {
            var result = new List<List<Token>> { new List<Token>() };
            var depth = 0;
            foreach (var t in expr)
            {
                if (t.Type == TokenType.Punctuation && t.Text == "(") depth++;
                else if (t.Type == TokenType.Punctuation && t.Text == ")") depth--;
                if (t.Type == TokenType.Punctuation && t.Text == "," && depth == 0)
                {
                    result.Add(new List<Token>());
                    continue;
                }
                result[result.Count - 1].Add(t);
            }
            return result;
        }

        private sealed class ConditionGroup
        {
            public string Conjunction;
            public List<Token> Tokens = new List<Token>();
        }

        private static List<ConditionGroup> SplitOnAndOr(List<Token> expr)
        {
            var result = new List<ConditionGroup> { new ConditionGroup() };
            var depth = 0;
            for (var i = 0; i < expr.Count; i++)
            {
                var t = expr[i];
                if (t.Type == TokenType.Punctuation && t.Text == "(") depth++;
                else if (t.Type == TokenType.Punctuation && t.Text == ")") depth--;
                if (depth == 0 && (t.Type == TokenType.Keyword) &&
                    (string.Equals(WordOf(t), "AND", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(WordOf(t), "OR", StringComparison.OrdinalIgnoreCase)))
                {
                    var g = new ConditionGroup { Conjunction = WordOf(t).ToUpperInvariant() };
                    result.Add(g);
                    continue;
                }
                result[result.Count - 1].Tokens.Add(t);
            }
            return result;
        }

        internal static string Indent(int width) => new string(' ', Math.Max(0, width));
    }

    /// <summary>Shared token-stream helpers.</summary>
    internal static class TokenFilters
    {
        public static string Normalize(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');

        public static List<Token> Clean(List<Token> tokens)
        {
            var result = new List<Token>(tokens.Count);
            foreach (var t in tokens)
            {
                if (t.Type == TokenType.Plain && t.Text.Trim().Length == 0)
                    continue;
                result.Add(t);
            }
            return result;
        }
    }
}

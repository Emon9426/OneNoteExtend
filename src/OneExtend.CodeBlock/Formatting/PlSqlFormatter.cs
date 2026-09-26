using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneExtend.CodeBlock.Model;

namespace OneExtend.CodeBlock.Formatting
{
    /// <summary>
    /// PL/SQL block-structure beautifier (FR-903): DECLARE/BEGIN/EXCEPTION/END
    /// nesting, IF/ELSIF/ELSE, LOOP and CASE stacks, exception WHEN alignment,
    /// and embedded SQL statements laid out by <see cref="SqlFormatter"/>.
    /// </summary>
    public sealed class PlSqlFormatter : ICodeFormatter
    {
        private const int Step = 3;

        private static readonly HashSet<string> AlwaysBreakBefore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DECLARE", "BEGIN", "EXCEPTION", "ELSIF", "ELSE", "END", "WHEN",
            "PROCEDURE", "FUNCTION", "PACKAGE", "TRIGGER"
        };

        private readonly Tokenizer _tokenizer;
        private readonly SqlFormatter _sql;

        public PlSqlFormatter(Tokenizer tokenizer)
        {
            _tokenizer = tokenizer;
            _sql = new SqlFormatter(tokenizer);
        }

        public string Id => "plsql-structured";

        public string Format(string source, FormatOptions options)
        {
            if (string.IsNullOrEmpty(source))
                return source;
            return string.Join("\n", FormatToLines(source, options)).TrimEnd();
        }

        // ---- pass 1: decide line boundaries ------------------------------------

        private static List<List<Token>> BreakLines(List<Token> tokens)
        {
            var lines = new List<List<Token>> { new List<Token>() };
            var parenSelectStack = new Stack<bool>();
            Token prevSignificant = null;

            bool LineHasText() => lines[lines.Count - 1].Any(t => t.Text.Trim().Length > 0);
            void Break()
            {
                if (LineHasText())
                    lines.Add(new List<Token>());
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                var word = KeywordWord(t);

                // subquery parenthesesis: break right after '(' and before its ')'
                if (t.Type == TokenType.Punctuation && t.Text == "(")
                {
                    var group = ReadGroup(tokens, i);
                    var hasSelect = group.Count > 0 && group.Any(x =>
                        x.Type == TokenType.Keyword &&
                        string.Equals(x.Text, "SELECT", StringComparison.OrdinalIgnoreCase));
                    parenSelectStack.Push(hasSelect);
                    lines[lines.Count - 1].Add(t);
                    prevSignificant = t;
                    if (hasSelect)
                        Break();
                    continue;
                }
                if (t.Type == TokenType.Punctuation && t.Text == ")")
                {
                    var hadSelect = parenSelectStack.Count > 0 && parenSelectStack.Pop();
                    if (hadSelect)
                        Break();
                    lines[lines.Count - 1].Add(t);
                    prevSignificant = t;
                    continue;
                }

                if (word != null && LineHasText())
                {
                    var prevWord = KeywordWord(prevSignificant);
                    var shouldBreak =
                        AlwaysBreakBefore.Contains(word) ||
                        (word == "IF" && prevWord != "END" && IsStatementStart(prevSignificant)) ||
                        ((word == "FOR" || word == "WHILE") && IsStatementStart(prevSignificant)) ||
                        (word == "CASE" && IsStatementStart(prevSignificant));
                    if (shouldBreak)
                        Break();
                }

                lines[lines.Count - 1].Add(t);
                if (t.Text.Trim().Length > 0)
                    prevSignificant = t;

                // break after statement terminators, unit headers and block openers
                if (t.Type == TokenType.Punctuation && t.Text == ";")
                    Break();
                if (word == "BEGIN" || word == "DECLARE" || word == "THEN" || word == "ELSE")
                    Break();
                if (word == "LOOP")
                {
                    // keep "END LOOP;" together: no break when the terminator follows
                    var nextTok = i + 1 < tokens.Count ? tokens[i + 1] : null;
                    if (!(nextTok != null && nextTok.Type == TokenType.Punctuation && nextTok.Text == ";"))
                        Break();
                }
                if ((word == "IS" || word == "AS") && i + 1 < tokens.Count)
                {
                    var next = tokens[i + 1];
                    var nw = KeywordWord(next);
                    if (nw == "BEGIN" || nw == "DECLARE" || nw == "PROCEDURE" ||
                        nw == "FUNCTION" || nw == "SELECT" || nw == "CURSOR")
                        Break();
                }
            }
            return lines.Where(l => l.Any(t => t.Text.Trim().Length > 0)).ToList();
        }

        private static bool IsStatementStart(Token prev)
        {
            if (prev == null)
                return true;
            if (prev.Type == TokenType.Punctuation && prev.Text == ";")
                return true;
            var w = KeywordWord(prev);
            return w == "THEN" || w == "ELSE" || w == "BEGIN" || w == "LOOP";
        }

        private static string KeywordWord(Token t)
        {
            if (t == null)
                return null;
            if (t.Type == TokenType.Keyword || t.Type == TokenType.ControlKeyword)
                return t.Text.ToUpperInvariant();
            return null;
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

        // ---- pass 2: indents and rendering --------------------------------------

        private sealed class Frame
        {
            public string Kind;
            public int Depth;
            public bool ExceptionPhase;
        }

        private List<string> RenderAll(List<List<Token>> lines, FormatOptions o)
        {
            var output = new List<string>();
            var stack = new List<Frame>();
            int TopDepthPlusStep() => (stack.Count == 0 ? 0 : stack[stack.Count - 1].Depth + Step);
            Frame Top() => stack.Count == 0 ? null : stack[stack.Count - 1];

            foreach (var line in lines)
            {
                var significant = line.Where(t => t.Text.Trim().Length > 0).ToList();
                if (significant.Count == 0)
                {
                    output.Add(string.Empty);
                    continue;
                }

                var firstWord = KeywordWord(significant[0]);
                var secondWord = significant.Count > 1 ? KeywordWord(significant[1]) : null;
                var lastWord = KeywordWord(significant[significant.Count - 1]);
                var indent = TopDepthPlusStep();

                switch (firstWord)
                {
                    case "DECLARE":
                        indent = 0;
                        stack.Add(new Frame { Kind = "decl", Depth = 0 });
                        break;
                    case "BEGIN":
                        if (Top()?.Kind == "decl")
                            stack.RemoveAt(stack.Count - 1);
                        indent = Top() == null ? 0 : Top().Depth; // BEGIN aligns with its unit header
                        stack.Add(new Frame { Kind = "block", Depth = indent });
                        break;
                    case "PROCEDURE":
                    case "FUNCTION":
                    case "PACKAGE":
                    case "TRIGGER":
                        indent = TopDepthPlusStep();
                        if (lastWord == "IS" || lastWord == "AS")
                            stack.Add(new Frame { Kind = "unit", Depth = indent });
                        break;
                    case "IF":
                        indent = TopDepthPlusStep();
                        stack.Add(new Frame { Kind = "if", Depth = indent });
                        break;
                    case "ELSIF":
                    case "ELSE":
                        indent = Top()?.Kind == "if" ? Top().Depth : TopDepthPlusStep();
                        break;
                    case "FOR":
                    case "WHILE":
                    case "LOOP":
                        indent = TopDepthPlusStep();
                        stack.Add(new Frame { Kind = "loop", Depth = indent });
                        break;
                    case "CASE":
                        indent = TopDepthPlusStep();
                        stack.Add(new Frame { Kind = "case", Depth = indent });
                        break;
                    case "WHEN":
                        {
                            // close a previous WHEN branch, then open one under EXCEPTION / CASE
                            if (Top()?.Kind == "when")
                                stack.RemoveAt(stack.Count - 1);
                            var top = Top();
                            if (top != null && (top.ExceptionPhase || top.Kind == "case"))
                            {
                                indent = top.Depth + 2;
                                stack.Add(new Frame { Kind = "when", Depth = indent });
                            }
                            else
                            {
                                indent = TopDepthPlusStep();
                            }
                            break;
                        }
                    case "EXCEPTION":
                        {
                            var top = Top();
                            if (top != null && top.Kind == "block")
                            {
                                indent = top.Depth;
                                top.ExceptionPhase = true;
                            }
                            else
                            {
                                indent = TopDepthPlusStep();
                            }
                            break;
                        }
                    case "END":
                        {
                            var target = secondWord == "LOOP" ? "loop"
                                       : secondWord == "IF" ? "if"
                                       : secondWord == "CASE" ? "case"
                                       : null;
                            if (stack.Count > 0)
                            {
                                Frame popped = null;
                                while (stack.Count > 0)
                                {
                                    var f = stack[stack.Count - 1];
                                    stack.RemoveAt(stack.Count - 1);
                                    popped = f;
                                    if (target == null)
                                    {
                                        if (f.Kind == "block" || f.Kind == "unit" || f.Kind == "decl")
                                            break;
                                    }
                                    else if (f.Kind == target)
                                    {
                                        break;
                                    }
                                }
                                indent = popped?.Depth ?? 0;
                            }
                            else
                            {
                                indent = 0;
                            }
                            break;
                        }
                    default:
                        indent = TopDepthPlusStep();
                        break;
                }

                // a line starting with ')' closes a subquery group: align with its
                // opener (after the switch so default cannot override it)
                if (significant[0].Type == TokenType.Punctuation && significant[0].Text == ")" && stack.Count > 0)
                {
                    indent = Top().Depth;
                }

                output.AddRange(RenderOnePhysicalLine(significant, o, indent, stack));
            }
            return output;
        }

        private List<string> RenderOnePhysicalLine(List<Token> significant, FormatOptions o, int indent, List<Frame> stack)
        {
            var pad = SqlFormatter.Indent(indent);
            var firstWord = KeywordWord(significant[0]);

            var isSqlStatement =
                firstWord == "SELECT" || firstWord == "INSERT" || firstWord == "UPDATE" ||
                firstWord == "DELETE" || firstWord == "MERGE";

            // FOR x IN ( <subquery> ) LOOP: hand the subquery to the SQL layouter
            if (firstWord == "FOR")
            {
                var open = significant.FindIndex(t => t.Type == TokenType.Punctuation && t.Text == "(");
                if (open >= 0)
                {
                    var group = ReadGroup(significant, open);
                    var hasSelect = group.Any(x =>
                        x.Type == TokenType.Keyword &&
                        string.Equals(x.Text, "SELECT", StringComparison.OrdinalIgnoreCase));
                    if (hasSelect)
                    {
                        var prefix = significant.Take(open).ToList();
                        var suffixStart = open + group.Count + 2;
                        var suffix = significant.Skip(suffixStart).ToList();
                        var result = new List<string>
                        {
                            pad + _sql.JoinTokens(prefix, o) + " ("
                        };
                        var innerTerm = group.Count > 0 && group[group.Count - 1].Type == TokenType.Punctuation
                                        && group[group.Count - 1].Text == ";";
                        var inner = group;
                        if (innerTerm)
                            inner = group.Take(group.Count - 1).ToList();
                        result.AddRange(_sql.LayoutStatement(inner, o, indent + Step));
                        result.Add(pad + ") " + _sql.JoinTokens(suffix, o));
                        return result;
                    }
                }
            }

            if (isSqlStatement)
                return _sql.LayoutStatement(significant, o, indent).ToList();

            return new List<string> { pad + _sql.JoinTokens(significant, o) };
        }

        // Format() entry uses RenderAll; keep the public surface small.
        internal List<string> FormatToLines(string source, FormatOptions options)
        {
            var tokens = TokenFilters.Clean(_tokenizer.Tokenize(TokenFilters.Normalize(source)));
            var lines = BreakLines(tokens);
            return RenderAll(lines, options);
        }
    }
}

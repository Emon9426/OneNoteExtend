using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.Model;

namespace OneExtend.CodeBlock
{
    /// <summary>
    /// Grammar-driven tokenizer. Token rules are combined into one alternation
    /// regex in declaration order (first match wins). An `identifier` rule is
    /// special: the matched word is classified through the grammar's keyword lists.
    /// </summary>
    public sealed class Tokenizer
    {
        private readonly GrammarPack _grammar;
        private readonly TokenRule[] _rules;
        private readonly Regex _combined;

        public Tokenizer(GrammarPack grammar)
        {
            _grammar = grammar;
            _rules = grammar.Tokens.ToArray();

            var pattern = new StringBuilder();
            for (var i = 0; i < _rules.Length; i++)
            {
                if (i > 0)
                    pattern.Append('|');
                pattern.Append("(?<g").Append(i).Append('>');
                pattern.Append(_rules[i].Pattern).Append(')');
            }

            var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
            if (grammar.CaseInsensitive)
                options |= RegexOptions.IgnoreCase;
            _combined = new Regex(pattern.ToString(), options);
        }

        public List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(source))
                return tokens;

            var pos = 0;
            foreach (Match m in _combined.Matches(source))
            {
                if (!m.Success)
                    continue;
                if (m.Index > pos)
                    tokens.Add(new Token(TokenType.Plain, source.Substring(pos, m.Index - pos)));

                var ruleIndex = FindMatchedGroup(m);
                if (ruleIndex >= 0)
                {
                    var ruleType = _rules[ruleIndex].Type;
                    var parsed = TokenTypeMap.Parse(ruleType);
                    if (parsed == TokenType.Identifier)
                        parsed = ClassifyIdentifier(m.Value);
                    tokens.Add(new Token(parsed, m.Value));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Plain, m.Value));
                }

                pos = m.Index + m.Length;
            }

            if (pos < source.Length)
                tokens.Add(new Token(TokenType.Plain, source.Substring(pos)));
            return tokens;
        }

        /// <summary>Splits tokens containing newlines into per-line token lists.</summary>
        public static List<List<Token>> SplitLines(IReadOnlyList<Token> tokens)
        {
            var lines = new List<List<Token>> { new List<Token>() };
            foreach (var token in tokens)
            {
                var parts = token.Text.Split('\n');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                        lines.Add(new List<Token>());
                    if (parts[i].Length > 0)
                        lines[lines.Count - 1].Add(new Token(token.Type, parts[i]));
                }
            }
            return lines;
        }

        private TokenType ClassifyIdentifier(string word)
        {
            if (_grammar.IsWordIn("control", word))
                return TokenType.ControlKeyword;
            if (_grammar.IsWordIn("keyword", word))
                return TokenType.Keyword;
            if (_grammar.IsWordIn("type", word))
                return TokenType.Type;
            if (_grammar.IsWordIn("function", word))
                return TokenType.Function;
            return TokenType.Identifier;
        }

        private int FindMatchedGroup(Match m)
        {
            for (var i = 0; i < _rules.Length; i++)
            {
                var g = m.Groups["g" + i];
                if (g.Success)
                    return i;
            }
            return -1;
        }
    }
}

using System.Linq;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Model;
using Xunit;

namespace OneExtend.Tests
{
    public class TokenizerTests
    {
        [Fact]
        public void Plsql_ClassifiesKeywordsCaseInsensitively()
        {
            var tokenizer = TestHost.TokenizerFor("plsql");
            var tokens = tokenizer.Tokenize("select x FROM t");

            Assert.Equal(TokenType.Keyword, tokens.First(t => t.Text.Equals("select", System.StringComparison.OrdinalIgnoreCase)).Type);
            Assert.Equal(TokenType.Keyword, tokens.First(t => t.Text == "FROM").Type);
        }

        [Fact]
        public void Plsql_BindVariableAndSubstitution()
        {
            var tokenizer = TestHost.TokenizerFor("plsql");
            var tokens = tokenizer.Tokenize("WHERE a > :min_amount AND name = &dept.");

            Assert.Contains(tokens, t => t.Type == TokenType.BindVariable && t.Text == ":min_amount");
            Assert.Contains(tokens, t => t.Type == TokenType.Substitution && t.Text.StartsWith("&"));
        }

        [Fact]
        public void Plsql_BuiltinPackagesAndAttributes()
        {
            var tokenizer = TestHost.TokenizerFor("plsql");
            var tokens = tokenizer.Tokenize("DBMS_OUTPUT.PUT_LINE(v_count); v_row t_orders%ROWTYPE;");

            Assert.Contains(tokens, t => t.Type == TokenType.BuiltinPackage && t.Text == "DBMS_OUTPUT");
            Assert.Contains(tokens, t => t.Type == TokenType.Attribute && t.Text == "%ROWTYPE");
        }

        [Fact]
        public void Plsql_OracleAlternativeQuotedString()
        {
            var tokenizer = TestHost.TokenizerFor("plsql");
            var tokens = tokenizer.Tokenize("x := q'[It's fine]'");

            Assert.Contains(tokens, t => t.Type == TokenType.String && t.Text == "q'[It's fine]'");
        }

        [Fact]
        public void Plsql_CommentsAndStrings()
        {
            var tokenizer = TestHost.TokenizerFor("plsql");
            var tokens = tokenizer.Tokenize("-- line comment\n/* block\ncomment */\ns = 'it''s'");

            Assert.Contains(tokens, t => t.Type == TokenType.Comment && t.Text.StartsWith("--"));
            Assert.Contains(tokens, t => t.Type == TokenType.Comment && t.Text.StartsWith("/*"));
            Assert.Contains(tokens, t => t.Type == TokenType.String && t.Text == "'it''s'");
        }

        [Fact]
        public void JavaScript_StringsTemplateAndArrow()
        {
            var tokenizer = TestHost.TokenizerFor("javascript");
            var tokens = tokenizer.Tokenize("const s = `hi ${name}`; const f = (a) => 'ok'; // done");

            Assert.Contains(tokens, t => t.Type == TokenType.String && t.Text.StartsWith("`"));
            Assert.Contains(tokens, t => t.Type == TokenType.Comment && t.Text == "// done");
            Assert.Contains(tokens, t => t.Type == TokenType.Operator && t.Text == "=>");
            Assert.Contains(tokens, t => t.Text == "const" && t.Type == TokenType.Keyword);
        }

        [Fact]
        public void SplitLines_CutsTokensContainingNewlines()
        {
            var tokenizer = TestHost.TokenizerFor("plsql");
            var tokens = tokenizer.Tokenize("a -- one\ntwo\nb");
            var lines = Tokenizer.SplitLines(tokens);

            Assert.Equal(3, lines.Count);
        }

        [Fact]
        public void Html_TagsAndAttributes()
        {
            var tokenizer = TestHost.TokenizerFor("html");
            var tokens = tokenizer.Tokenize("<input class=\"a\" required>");

            Assert.Contains(tokens, t => t.Type == TokenType.Tag && t.Text == "<input");
            Assert.Contains(tokens, t => t.Type == TokenType.Attribute && t.Text == "class");
            Assert.Contains(tokens, t => t.Type == TokenType.String && t.Text == "\"a\"");
        }
    }
}

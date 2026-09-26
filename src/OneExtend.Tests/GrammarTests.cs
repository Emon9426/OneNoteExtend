using System.IO;
using System.Linq;
using OneExtend.CodeBlock.Grammar;
using Xunit;

namespace OneExtend.Tests
{
    public class GrammarTests
    {
        [Fact]
        public void LoadsAllPacksFromRepository()
        {
            var ids = TestHost.Languages.All.Select(p => p.Id).ToList();

            Assert.Contains("plsql", ids);
            Assert.Contains("sql-ansi", ids);
            Assert.Contains("tsql", ids);
            Assert.Contains("mysql", ids);
            Assert.Contains("javascript", ids);
            Assert.Contains("typescript", ids);
            Assert.Contains("tsx", ids);
            Assert.Contains("html", ids);
            Assert.Contains("css", ids);
            Assert.Contains("java", ids);
            Assert.True(ids.Count >= 10, $"expected >=10 packs, got {ids.Count}");
        }

        [Fact]
        public void Plsql_InheritsAnsiKeywords()
        {
            var plsql = TestHost.Languages.Get("plsql");

            // SELECT is ANSI, inherited into the Oracle pack
            Assert.True(plsql.IsWordIn("keyword", "SELECT"));
            Assert.True(plsql.IsWordIn("keyword", "FROM"));
            // Oracle-specific word
            Assert.True(plsql.IsWordIn("keyword", "DECLARE"));
            Assert.Equal("plsql-structured", plsql.Formatter);
        }

        [Fact]
        public void Tsql_InheritsAnsiAndAddsOwn()
        {
            var tsql = TestHost.Languages.Get("tsql");

            Assert.True(tsql.IsWordIn("keyword", "SELECT"));      // inherited
            Assert.True(tsql.IsWordIn("keyword", "TOP"));         // own
            Assert.True(tsql.IsWordIn("function", "GETDATE"));
        }

        [Fact]
        public void Loader_RoundTripsPackJson()
        {
            var dir = Path.Combine(TestHost.DataRoot, "grammars");
            var file = Path.Combine(dir, "css.grammar.json");

            var pack = GrammarLoader.LoadFile(file);
            Assert.Equal("css", pack.Id);
            Assert.True(pack.Tokens.Count >= 5);
        }

        [Fact]
        public void DetectLanguage_PicksOracleForOracleSnippet()
        {
            var id = TestHost.Languages.DetectLanguage(
                "BEGIN\n  DBMS_OUTPUT.PUT_LINE(SYSDATE);\nEXCEPTION WHEN OTHERS THEN NULL; END;");

            Assert.Equal("plsql", id);
        }

        [Fact]
        public void DetectLanguage_PicksJavaScriptAndHtml()
        {
            Assert.Equal("javascript", TestHost.Languages.DetectLanguage(
                "const x = 1;\nasync function f() { await fetch('/a'); return module.exports; }"));
            Assert.Equal("html", TestHost.Languages.DetectLanguage(
                "<div class=\"x\">\n  <a href=\"#\">link</a>\n</div>"));
        }
    }
}

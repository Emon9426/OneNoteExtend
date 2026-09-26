using System;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Formatting;
using OneExtend.CodeBlock.OneNoteXml;
using Xunit;

namespace OneExtend.Tests
{
    /// <summary>FR-602 acceptance: render → extract must return the formatted source.</summary>
    public class RoundTripTests
    {
        private static string RoundTrip(string source, string langId, Action<CodeBlockOptions> tweak = null)
        {
            var language = TestHost.Languages.Get(langId);
            var options = new CodeBlockOptions
            {
                Title = "t",
                LanguageId = langId,
                Beautify = false
            };
            tweak?.Invoke(options);
            var renderer = new CodeBlockRenderer(language, TestHost.Themes.GetDefault());
            var rendered = renderer.Render(source, options);
            var extracted = SourceExtractor.FromFragment(rendered.FragmentXml);
            return extracted.Source;
        }

        [Fact]
        public void Plsql_SourceWithSpecialChars_Survives()
        {
            var source = "SELECT a < b AND c > d FROM t;\n-- <tag> & 'quote'\ns := 'a<b & c>d';";

            Assert.Equal(source, RoundTrip(source, "plsql"));
        }

        [Fact]
        public void SourceWithBlankLines_BlankLinesComeBack()
        {
            var source = "line one\n\nline three";

            Assert.Equal(source, RoundTrip(source, "javascript"));
        }

        [Fact]
        public void InlineMode_AlsoRoundTrips()
        {
            var source = "SELECT *\nFROM t";

            var extracted = RoundTrip(source, "plsql", o => o.LineNumbers.Mode = LineNumberMode.Inline);
            Assert.Equal(source, extracted);
        }

        [Fact]
        public void MetaHash_MatchesFormattedSource()
        {
            var language = TestHost.Languages.Get("plsql");
            var options = new CodeBlockOptions { Title = "t", LanguageId = "plsql", Beautify = false };
            var renderer = new CodeBlockRenderer(language, TestHost.Themes.GetDefault());
            var rendered = renderer.Render("SELECT 1 FROM t", options);
            var extracted = SourceExtractor.FromFragment(rendered.FragmentXml);

            Assert.Equal(CodeBlockMeta.HashSource(rendered.FormattedSource), extracted.Meta.SourceHash);
            Assert.Equal("plsql", extracted.Meta.Lang);
            Assert.False(extracted.Meta.Beautified);
        }

        [Fact]
        public void Title_IsRecoverable()
        {
            var language = TestHost.Languages.Get("plsql");
            var options = new CodeBlockOptions { Title = "get_orders.sql", LanguageId = "plsql", Beautify = false };
            var renderer = new CodeBlockRenderer(language, TestHost.Themes.GetDefault());
            var rendered = renderer.Render("SELECT 1", options);
            var extracted = SourceExtractor.FromFragment(rendered.FragmentXml);

            Assert.Equal("get_orders.sql", extracted.Title);
        }

        [Fact]
        public void BeautifiedBlock_RoundTripsFormattedText()
        {
            var language = TestHost.Languages.Get("plsql");
            var options = new CodeBlockOptions
            {
                Title = "t",
                LanguageId = "plsql",
                Beautify = true,
                Format = new FormatOptions { KeywordCase = KeywordCase.Upper }
            };
            var renderer = new CodeBlockRenderer(language, TestHost.Themes.GetDefault());
            var rendered = renderer.Render("select a from t", options);
            var extracted = SourceExtractor.FromFragment(rendered.FragmentXml);

            Assert.Equal(rendered.FormattedSource, extracted.Source);
            Assert.Contains("SELECT", extracted.Source);
        }
    }
}

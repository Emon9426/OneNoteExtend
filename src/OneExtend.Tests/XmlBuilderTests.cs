using System.Collections.Generic;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Model;
using OneExtend.CodeBlock.OneNoteXml;
using OneExtend.CodeBlock.Theme;
using Xunit;

namespace OneExtend.Tests
{
    public class XmlBuilderTests
    {
        private static readonly List<List<Token>> Lines = new List<List<Token>>
        {
            new List<Token>
            {
                new Token(TokenType.Keyword, "SELECT"),
                new Token(TokenType.Plain, " a"),
            },
            new List<Token>
            {
                new Token(TokenType.Comment, "-- note <b> & more"),
            },
            new List<Token>()
        };

        private static CodeBlockOptions Options() => new CodeBlockOptions
        {
            Title = "demo.sql",
            LanguageId = "plsql",
            Beautify = false,
            PluginVersion = "0.1.0"
        };

        [Fact]
        public void ColumnMode_ProducesMetaTitleTableAndSpans()
        {
            var xml = OneNoteXmlBuilder.Build(Lines, TestHost.Themes.GetDefault(), Options(), "PL/SQL (Oracle)");

            Assert.Contains("<one:OE xmlns:one=", xml);
            Assert.Contains("<one:Meta name=\"OneExtend:CodeBlock\" content=\"lang=plsql;v=0.1.0;ln=col:1:1;fmt=off;h=", xml);
            Assert.Contains("<one:Table bordersVisible=\"false\">", xml);
            Assert.Contains("<one:Column width=\"48.0\"/>", xml);
            Assert.Contains("shadingColor=\"#F3F4F6\"", xml);   // line-number cell
            Assert.Contains("shadingColor=\"#FBFCFD\"", xml);   // code cell
            Assert.Contains("┌ demo.sql · PL/SQL (Oracle)", xml);
            // keyword run colored (span attributes use single quotes), code text escaped
            Assert.Contains("color:#0000FF'>SELECT</span>", xml);
            Assert.Contains("-- note &lt;b&gt; &amp; more", xml);
            // empty line becomes nbsp
            Assert.Contains("&nbsp;", xml);
            // number cell lines exist with the bar separator
            Assert.Contains("'>1 │ </span>", xml);
        }

        [Fact]
        public void InlineMode_NoTable_PrefixInEachLine()
        {
            var options = Options();
            options.LineNumbers.Mode = LineNumberMode.Inline;

            var xml = OneNoteXmlBuilder.Build(Lines, TestHost.Themes.GetDefault(), options, "PL/SQL (Oracle)");

            Assert.DoesNotContain("<one:Table", xml);
            Assert.Contains("<one:OEChildren>", xml);
            Assert.Contains("1 │ ", xml);
        }

        [Fact]
        public void LineNumbersOff_SingleColumnTable()
        {
            var options = Options();
            options.LineNumbers.Enabled = false;

            var xml = OneNoteXmlBuilder.Build(Lines, TestHost.Themes.GetDefault(), options, "PL/SQL (Oracle)");

            Assert.DoesNotContain("width=\"48.0\"", xml);
            // exactly one Column and one Cell
            Assert.Single(GetOccurrences(xml, "<one:Column "));
            Assert.Single(GetOccurrences(xml, "<one:Cell "));
        }

        [Fact]
        public void NumberTexts_StartStepAndPadding()
        {
            var opts = new LineNumberOptions { Start = 8, Step = 3 };
            var texts = OneNoteXmlBuilder.NumberTexts(opts, 4);

            Assert.Equal(" 8 │ ", texts[0]);
            Assert.Equal("11 │ ", texts[1]);
            Assert.Equal("14 │ ", texts[2]);
            Assert.Equal("17 │ ", texts[3]);
        }

        [Fact]
        public void Meta_RoundTrips()
        {
            var info = new CodeBlockMetaInfo
            {
                Lang = "tsx",
                Version = "1.2.3",
                LineNumberMode = "inl",
                LineStart = 5,
                LineStep = 2,
                Beautified = true,
                SourceHash = "abcd1234"
            };

            var parsed = CodeBlockMeta.Parse(CodeBlockMeta.Serialize(info));

            Assert.Equal("tsx", parsed.Lang);
            Assert.Equal("1.2.3", parsed.Version);
            Assert.Equal("inl", parsed.LineNumberMode);
            Assert.Equal(5, parsed.LineStart);
            Assert.Equal(2, parsed.LineStep);
            Assert.True(parsed.Beautified);
            Assert.Equal("abcd1234", parsed.SourceHash);
        }

        [Fact]
        public void DarkTheme_ColorsApplied()
        {
            var xml = OneNoteXmlBuilder.Build(Lines, TestHost.Themes.Get("dark"), Options(), "PL/SQL (Oracle)");

            Assert.Contains("#161B22", xml);
            Assert.Contains("color:#FF7B72", xml);   // dark keyword color
        }

        private static System.Collections.Generic.List<int> GetOccurrences(string s, string fragment)
        {
            var result = new System.Collections.Generic.List<int>();
            var idx = 0;
            while ((idx = s.IndexOf(fragment, idx, System.StringComparison.Ordinal)) >= 0)
            {
                result.Add(idx);
                idx += fragment.Length;
            }
            return result;
        }
    }
}

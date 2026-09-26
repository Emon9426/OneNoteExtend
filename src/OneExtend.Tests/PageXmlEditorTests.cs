using System;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.OneNoteXml;
using Xunit;

namespace OneExtend.Tests
{
    public class PageXmlEditorTests
    {
        private const string PagePrefix =
            "<?xml version=\"1.0\"?><one:Page xmlns:one=\"http://schemas.microsoft.com/office/onenote/2013/onenote\" ID=\"{1}\" name=\"Test\">";

        private static string MakePage(string inner) => PagePrefix.Replace("{1}", "page-1") + inner + "</one:Page>";

        private static string MakeFragment(string langId = "plsql", string source = "SELECT 1 FROM t")
        {
            var renderer = new CodeBlockRenderer(
                TestHost.Languages.Get(langId),
                TestHost.Themes.GetDefault());
            return renderer.Render(source, new CodeBlockOptions
            {
                Title = "demo",
                LanguageId = langId,
                Beautify = false
            }).FragmentXml;
        }

        private static string MakePageWithOurBlockSelected()
        {
            var fragment = MakeFragment();
            // emulate the user's cursor inside the block's table (a T node with selected attr)
            var selected = fragment.Replace("<one:T><![CDATA[", "<one:T selected=\"all\"><![CDATA[", StringComparison.Ordinal);
            return MakePage("<one:OEChildren><one:OE ID=\"o1\"><one:T><![CDATA[plain paragraph]]></one:T></one:OE>" +
                            selected + "</one:OEChildren>");
        }

        [Fact]
        public void InsertFragment_PlacesBlockAfterSelectedElement()
        {
            var page = MakePage(
                "<one:OEChildren>" +
                "<one:OE ID=\"o1\"><one:T><![CDATA[first]]></one:T></one:OE>" +
                "<one:OE ID=\"o2\"><one:T selected=\"all\"><![CDATA[second]]></one:T></one:OE>" +
                "</one:OEChildren>");

            var updated = PageXmlEditor.InsertFragment(page, MakeFragment());

            var posFragment = updated.IndexOf("OneExtend:CodeBlock", StringComparison.Ordinal);
            var posO2 = updated.IndexOf("o2", StringComparison.Ordinal);
            var posO1 = updated.IndexOf("o1", StringComparison.Ordinal);
            Assert.True(posFragment > posO2, "block must come after the selected OE");
            Assert.True(posO2 > posO1, "page order preserved");
        }

        [Fact]
        public void InsertFragment_NoSelection_AppendsToEnd()
        {
            var page = MakePage(
                "<one:OEChildren>" +
                "<one:OE ID=\"o1\"><one:T><![CDATA[first]]></one:T></one:OE>" +
                "</one:OEChildren>");

            var updated = PageXmlEditor.InsertFragment(page, MakeFragment());

            Assert.Contains("OneExtend:CodeBlock", updated);
            var posO1 = updated.IndexOf("o1", StringComparison.Ordinal);
            var posFragment = updated.IndexOf("OneExtend:CodeBlock", StringComparison.Ordinal);
            Assert.True(posFragment > posO1);
        }

        [Fact]
        public void FindSelectedCodeBlockFragment_FindsBlockAndParsesBack()
        {
            var page = MakePageWithOurBlockSelected();

            var fragment = PageXmlEditor.FindSelectedCodeBlockFragment(page);

            Assert.NotNull(fragment);
            var extracted = SourceExtractor.FromFragment(fragment);
            Assert.Equal("SELECT 1 FROM t", extracted.Source);
            Assert.Equal("plsql", extracted.Meta.Lang);
        }

        [Fact]
        public void FindSelectedCodeBlockFragment_ReturnsNullOutsideBlocks()
        {
            var page = MakePage(
                "<one:OEChildren><one:OE><one:T selected=\"all\"><![CDATA[plain]]></one:T></one:OE></one:OEChildren>");

            Assert.Null(PageXmlEditor.FindSelectedCodeBlockFragment(page));
        }

        [Fact]
        public void ReplaceSelectedCodeBlock_SwapsContentInPlace()
        {
            var page = MakePageWithOurBlockSelected();
            var oldHash = SourceExtractor.FromFragment(PageXmlEditor.FindSelectedCodeBlockFragment(page)).Meta.SourceHash;

            var replacement = MakeFragment(source: "SELECT 2 FROM other");
            var updated = PageXmlEditor.ReplaceSelectedCodeBlock(page, replacement);

            Assert.NotNull(updated);
            Assert.DoesNotContain(oldHash, updated);
            // the rebuilt block carries the new source (no selected marker survives a
            // programmatic rebuild - the cursor is OneNote's business, not ours)
            Assert.Contains("OneExtend:CodeBlock", updated);
            Assert.Contains(">SELECT</span>", updated);
            Assert.Contains("> other</span>", updated);
        }
    }
}

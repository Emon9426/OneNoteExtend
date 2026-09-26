using OneExtend.CodeBlock.Formatting;
using Xunit;

namespace OneExtend.Tests
{
    public class GenericIndentTests
    {
        private static string[] FormatJs(string source, int width = 4)
        {
            var f = new GenericIndentFormatter(GenericIndentFormatter.Mode.Brace);
            return f.Format(source, new FormatOptions { IndentWidth = width }).Split('\n');
        }

        [Fact]
        public void JavaScript_BraceStructureIsRebuilt()
        {
            var lines = FormatJs("function f(){var x=1;if(x){x=2}else{x=3}return x}");

            // K&R style: opening brace stays at end of line, closing brace on its own line
            Assert.Equal("function f() {", lines[0]);
            Assert.Equal("    var x=1;", lines[1]);
            Assert.Equal("    if(x) {", lines[2]);
            Assert.Equal("        x=2", lines[3]);
            Assert.Equal("    }", lines[4]);
            Assert.Equal("    else {", lines[5]);
            Assert.Equal("        x=3", lines[6]);
            Assert.Equal("    }", lines[7]);
            Assert.Equal("    return x", lines[8]);
            Assert.Equal("}", lines[lines.Length - 1]);
        }

        [Fact]
        public void JavaScript_BracesInsideStringsAreIgnored()
        {
            var lines = FormatJs("var s = \"{ not code }\"; var t = 1;");

            // the string content must stay on one line and not introduce structure
            Assert.Equal(2, lines.Length);
            Assert.Contains("\"{ not code }\"", lines[0]);
        }

        [Fact]
        public void JavaScript_ForLoopSemicolonsNotSplit()
        {
            var lines = FormatJs("for(var i=0;i<3;i++){sum+=i}");

            // the two semicolons inside for(...) must stay inline
            Assert.StartsWith("for(var i=0;i<3;i++)", lines[0]);
            Assert.Equal(3, lines.Length); // header / body / closing brace
        }

        [Fact]
        public void Css_DeclarationsAndRules()
        {
            var f = new GenericIndentFormatter(GenericIndentFormatter.Mode.Brace);
            var lines = f.Format(
                ".card{color:red;padding:4px}@media print{.card{display:none}}",
                new FormatOptions { IndentWidth = 2 }).Split('\n');

            Assert.Equal(".card {", lines[0]);
            Assert.Equal("  color:red;", lines[1]);
            Assert.Equal("  padding:4px", lines[2]);
            Assert.Equal("}", lines[3]);
            Assert.Equal("@media print {", lines[4]);
            Assert.Equal("  .card {", lines[5]);
            Assert.Equal("    display:none", lines[6]);
            Assert.Equal("  }", lines[7]);
            Assert.Equal("}", lines[8]);
        }

        [Fact]
        public void Html_TagsOnePerLineWithIndent()
        {
            var f = new GenericIndentFormatter(GenericIndentFormatter.Mode.Tag);
            var lines = f.Format(
                "<div class=\"a\"><p>hello</p><span>x</span></div>",
                new FormatOptions { IndentWidth = 2 }).Split('\n');

            Assert.Equal("<div class=\"a\">", lines[0]);
            Assert.Equal("  <p>", lines[1]);
            Assert.Equal("    hello", lines[2]);   // inside div > p
            Assert.Equal("  </p>", lines[3]);
            Assert.Equal("  <span>", lines[4]);
            Assert.Equal("    x", lines[5]);
            Assert.Equal("  </span>", lines[6]);
            Assert.Equal("</div>", lines[7]);
        }
    }
}

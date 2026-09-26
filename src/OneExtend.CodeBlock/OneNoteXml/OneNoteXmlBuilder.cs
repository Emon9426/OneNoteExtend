using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneExtend.CodeBlock.Model;
using OneExtend.CodeBlock.Theme;

namespace OneExtend.CodeBlock.OneNoteXml
{
    /// <summary>A run of same-colored text ready for span rendering (shared by XML + WPF preview).</summary>
    public struct ColoredRun
    {
        public string Color;
        public string Text;
        public ColoredRun(string color, string text) { Color = color; Text = text; }
    }

    /// <summary>
    /// Produces the OneNote page-XML fragment for a code block: a one:OE with
    /// identity metadata, a plain-text title line, and either a two-column table
    /// (line numbers | code) or inline-prefixed paragraphs.
    /// </summary>
    public static class OneNoteXmlBuilder
    {
        private const string NsDeclaration = "xmlns:one=\"http://schemas.microsoft.com/office/onenote/2013/onenote\"";

        public static string Build(
            IReadOnlyList<List<Token>> tokenLines,
            CodeTheme theme,
            CodeBlockOptions options,
            string languageName)
        {
            if (tokenLines == null)
                throw new ArgumentNullException(nameof(tokenLines));
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var formattedSource = string.Join("\n", tokenLines.Select(TokenizeLineText));
            var meta = new CodeBlockMetaInfo
            {
                Lang = options.LanguageId,
                Version = options.PluginVersion,
                LineNumberMode = options.LineNumbers.Mode == LineNumberMode.Inline ? "inl" : "col",
                LineStart = options.LineNumbers.Start,
                LineStep = options.LineNumbers.Step,
                Beautified = options.Beautify,
                SourceHash = CodeBlockMeta.HashSource(formattedSource)
            };

            var fontFamily = string.IsNullOrEmpty(options.FontFamily) ? theme.FontFamily : options.FontFamily;
            var fontSize = string.IsNullOrEmpty(options.FontSize) ? theme.FontSize : options.FontSize;

            var sb = new StringBuilder(tokenLines.Count * 96 + 256);
            sb.Append("<one:OE ").Append(NsDeclaration).Append('>');
            sb.Append("<one:Meta name=\"").Append(CodeBlockMeta.MetaName)
              .Append("\" content=\"").Append(EscapeAttribute(CodeBlockMeta.Serialize(meta))).Append("\"/>");

            // Title line (plain OneNote text: stays editable by the user)
            var titleText = BuildTitle(options.Title, languageName);
            sb.Append("<one:T><![CDATA[<span style='font-family:").Append(fontFamily)
              .Append(";font-size:").Append(fontSize)
              .Append(";font-weight:bold;color:#3C424B'>").Append(EscapeHtml(titleText))
              .Append("</span>]]></one:T>");

            if (options.LineNumbers.Enabled && options.LineNumbers.Mode == LineNumberMode.Inline)
                BuildInline(sb, tokenLines, theme, options, fontFamily, fontSize);
            else
                BuildTable(sb, tokenLines, theme, options, fontFamily, fontSize);

            sb.Append("</one:OE>");
            return sb.ToString();
        }

        private static void BuildTable(
            StringBuilder sb,
            IReadOnlyList<List<Token>> tokenLines,
            CodeTheme theme,
            CodeBlockOptions options,
            string fontFamily,
            string fontSize)
        {
            var showNumbers = options.LineNumbers.Enabled;
            sb.Append("<one:Table bordersVisible=\"false\">");
            sb.Append("<one:Columns>");
            if (showNumbers)
                sb.Append("<one:Column width=\"48.0\"/>");
            sb.Append("<one:Column width=\"640.0\"/>");
            sb.Append("</one:Columns>");
            sb.Append("<one:Row>");

            if (showNumbers)
            {
                sb.Append("<one:Cell shadingColor=\"").Append(theme.LineNumberBackground).Append("\">");
                sb.Append("<one:OEChildren>");
                foreach (var numberText in NumberTexts(options.LineNumbers, tokenLines.Count))
                {
                    sb.Append("<one:OE><one:T><![CDATA[<span style='font-family:").Append(fontFamily)
                      .Append(";font-size:").Append(fontSize)
                      .Append(";color:").Append(LineNumberColor(theme))
                      .Append("'>").Append(numberText)
                      .Append("</span>]]></one:T></one:OE>");
                }
                sb.Append("</one:OEChildren></one:Cell>");
            }

            sb.Append("<one:Cell shadingColor=\"").Append(theme.Background).Append("\">");
            sb.Append("<one:OEChildren>");
            foreach (var line in tokenLines)
            {
                sb.Append("<one:OE><one:T><![CDATA[");
                AppendCodeSpans(sb, BuildRuns(line, theme), fontFamily, fontSize);
                sb.Append("]]></one:T></one:OE>");
            }
            sb.Append("</one:OEChildren></one:Cell>");

            sb.Append("</one:Row></one:Table>");
        }

        private static void BuildInline(
            StringBuilder sb,
            IReadOnlyList<List<Token>> tokenLines,
            CodeTheme theme,
            CodeBlockOptions options,
            string fontFamily,
            string fontSize)
        {
            sb.Append("<one:OEChildren>");
            var numbers = NumberTexts(options.LineNumbers, tokenLines.Count);
            for (var i = 0; i < tokenLines.Count; i++)
            {
                sb.Append("<one:OE><one:T><![CDATA[");
                sb.Append("<span style='font-family:").Append(fontFamily)
                  .Append(";font-size:").Append(fontSize)
                  .Append(";color:").Append(LineNumberColor(theme)).Append("'>")
                  .Append(numbers[i]).Append("</span>");
                AppendCodeSpans(sb, BuildRuns(tokenLines[i], theme), fontFamily, fontSize);
                sb.Append("]]></one:T></one:OE>");
            }
            sb.Append("</one:OEChildren>");
        }

        private static string LineNumberColor(CodeTheme theme)
        {
            return theme.Colors != null && theme.Colors.TryGetValue("lineNumber", out var c) && !string.IsNullOrEmpty(c)
                ? c
                : "#8B949E";
        }

        /// <summary>Line number cell texts: right-padded numbers with separator suffix.</summary>
        public static IReadOnlyList<string> NumberTexts(LineNumberOptions options, int lineCount)
        {
            var result = new List<string>(lineCount);
            int last = options.Start + Math.Max(0, lineCount - 1) * options.Step;
            var digits = options.FixedDigits > 0
                ? options.FixedDigits
                : Math.Max(1, last.ToString().Length);
            var sep = options.Separator == LineNumberSeparator.Bar ? " │ "
                    : options.Separator == LineNumberSeparator.Dot ? " · "
                    : "  ";
            for (var i = 0; i < lineCount; i++)
            {
                var n = options.Start + i * options.Step;
                result.Add(n.ToString().PadLeft(digits) + sep);
            }
            return result;
        }

        /// <summary>Merges adjacent tokens of the same color into renderable runs.</summary>
        public static List<ColoredRun> BuildRuns(List<Token> lineTokens, CodeTheme theme)
        {
            var runs = new List<ColoredRun>();
            foreach (var t in lineTokens)
            {
                var color = theme.ColorOf(t.Type);
                if (runs.Count > 0 && runs[runs.Count - 1].Color == color)
                {
                    var last = runs[runs.Count - 1];
                    runs[runs.Count - 1] = new ColoredRun(last.Color, last.Text + t.Text);
                }
                else
                {
                    runs.Add(new ColoredRun(color, t.Text));
                }
            }
            return runs;
        }

        private static void AppendCodeSpans(StringBuilder sb, List<ColoredRun> runs, string fontFamily, string fontSize)
        {
            if (runs.Count == 0 || runs.All(r => r.Text.Trim().Length == 0))
            {
                sb.Append("<span style='font-family:").Append(fontFamily)
                  .Append(";font-size:").Append(fontSize).Append("'>&nbsp;</span>");
                return;
            }
            foreach (var run in runs)
            {
                sb.Append("<span style='font-family:").Append(fontFamily)
                  .Append(";font-size:").Append(fontSize)
                  .Append(";color:").Append(run.Color).Append("'>")
                  .Append(EscapeHtml(run.Text))
                  .Append("</span>");
            }
        }

        internal static string BuildTitle(string title, string languageName)
        {
            var t = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            if (string.IsNullOrEmpty(t))
                return "┌ " + languageName;
            if (string.IsNullOrEmpty(languageName))
                return "┌ " + t;
            return "┌ " + t + " · " + languageName;
        }

        internal static string TokenizeLineText(List<Token> line) =>
            string.Concat(line.Select(t => t.Text));

        internal static string EscapeHtml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string EscapeAttribute(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}

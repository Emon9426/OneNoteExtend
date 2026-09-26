using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace OneExtend.CodeBlock.OneNoteXml
{
    public sealed class ExtractedCodeBlock
    {
        public string Source { get; set; }
        public CodeBlockMetaInfo Meta { get; set; }
        public string Title { get; set; }
    }

    /// <summary>
    /// Reverses <see cref="OneNoteXmlBuilder"/>: parses an inserted block's XML
    /// fragment back into plain source code (FR-602 round-trip editing).
    /// </summary>
    public static class SourceExtractor
    {
        private static readonly Regex SpanTag = new Regex(@"</?span[^>]*>", RegexOptions.Compiled);
        private static readonly Regex InlineNumberPrefix = new Regex(@"^\s*\d+\s*(?:[│·]\s*)?", RegexOptions.Compiled);

        public static ExtractedCodeBlock FromFragment(string fragmentXml)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(fragmentXml);
            }
            catch (XmlException)
            {
                return null;
            }

            var root = doc.DocumentElement;
            if (root == null || root.LocalName != "OE")
                return null;

            var result = new ExtractedCodeBlock();

            var metaNode = SelectFirst(root, "one:Meta[@name='" + CodeBlockMeta.MetaName + "']");
            result.Meta = metaNode != null
                ? CodeBlockMeta.Parse(metaNode.Attributes?["content"]?.Value)
                : new CodeBlockMetaInfo();

            var titleNode = SelectFirst(root, "one:T");
            result.Title = TitleFromHeader(titleNode?.InnerText);

            var codeLines = new List<string>();

            var table = SelectFirst(root, "one:Table");
            XmlNodeList codeTs;
            if (table != null)
            {
                var cells = table.SelectNodes(".//one:Cell", Ns(root));
                XmlNode codeCell = null;
                if (cells != null && cells.Count >= 2)
                    codeCell = cells[cells.Count - 1];
                else if (cells != null && cells.Count == 1)
                    codeCell = cells[0];
                codeTs = codeCell?.SelectNodes(".//one:T", Ns(root));
            }
            else
            {
                codeTs = root.SelectNodes("one:OEChildren/one:OE/one:T", Ns(root));
            }

            if (codeTs != null)
            {
                var stripInlineNumbers = result.Meta.LineNumberMode == "inl";
                foreach (XmlNode t in codeTs)
                {
                    var line = HtmlToPlainText(t.InnerText);
                    if (stripInlineNumbers)
                        line = InlineNumberPrefix.Replace(line, string.Empty);
                    codeLines.Add(line);
                }
            }

            result.Source = string.Join("\n", codeLines);
            return result;
        }

        /// <summary>Strips span tags and HTML entities from one:T CDATA content.</summary>
        public static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            var text = SpanTag.Replace(html, string.Empty);
            text = text
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&nbsp;", " ")
                .Replace("\u00A0", " ")
                .Replace("&amp;", "&");
            if (text.Trim().Length == 0)
                return string.Empty;
            return text.TrimEnd();
        }

        internal static string TitleFromHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
                return string.Empty;
            var text = HtmlToPlainText(header).Trim();
            if (text.StartsWith("┌", StringComparison.Ordinal))
                text = text.Substring(1).TrimStart();
            var idx = text.LastIndexOf(" · ", StringComparison.Ordinal);
            return idx > 0 ? text.Substring(0, idx) : text;
        }

        private static XmlNamespaceManager Ns(XmlElement root)
        {
            var nsmgr = new XmlNamespaceManager(new NameTable());
            nsmgr.AddNamespace("one", root.GetNamespaceOfPrefix("one") ??
                                      "http://schemas.microsoft.com/office/onenote/2013/onenote");
            return nsmgr;
        }

        private static XmlNode SelectFirst(XmlElement root, string xpath)
        {
            return root.SelectSingleNode(xpath, Ns(root));
        }
    }
}

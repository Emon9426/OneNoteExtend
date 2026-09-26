using System;
using System.Xml;

namespace OneExtend.CodeBlock.OneNoteXml
{
    /// <summary>
    /// Pure XML surgery on OneNote page documents: insert a fragment after the
    /// current selection (or append at the end), and locate / replace an
    /// OneExtend code block containing the selection. No COM involved, fully testable.
    /// </summary>
    public static class PageXmlEditor
    {
        private const string OneNs = "http://schemas.microsoft.com/office/onenote/2013/onenote";

        public static string InsertFragment(string pageXml, string fragmentXml)
        {
            var doc = LoadPage(pageXml);
            var imported = ImportFragment(doc, fragmentXml);

            var anchor = FindSelectedOutlineElement(doc);
            if (anchor != null && anchor.ParentNode != null)
            {
                anchor.ParentNode.InsertAfter(imported, anchor);
            }
            else
            {
                var container = FindLastOutlineChildren(doc);
                container.AppendChild(imported);
            }
            return doc.OuterXml;
        }

        /// <summary>Returns the outer XML of the OneExtend block containing the selection, or null.</summary>
        public static string FindSelectedCodeBlockFragment(string pageXml)
        {
            var doc = LoadPage(pageXml);
            var blockNode = FindSelectedCodeBlockNode(doc);
            return blockNode?.OuterXml;
        }

        /// <summary>Replaces the selected code block with a new fragment; returns updated XML or null when no block was found.</summary>
        public static string ReplaceSelectedCodeBlock(string pageXml, string newFragmentXml)
        {
            var doc = LoadPage(pageXml);
            var blockNode = FindSelectedCodeBlockNode(doc);
            if (blockNode == null || blockNode.ParentNode == null)
                return null;
            var imported = ImportFragment(doc, newFragmentXml);
            blockNode.ParentNode.ReplaceChild(imported, blockNode);
            return doc.OuterXml;
        }

        // ---- internals -----------------------------------------------------------

        private static XmlDocument LoadPage(string pageXml)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(pageXml);
            return doc;
        }

        private static XmlNamespaceManager Ns(XmlDocument doc)
        {
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            var uri = doc.DocumentElement != null &&
                      doc.DocumentElement.NamespaceURI.StartsWith("http://schemas.microsoft.com/office/onenote", StringComparison.OrdinalIgnoreCase)
                ? doc.DocumentElement.NamespaceURI
                : OneNs;
            nsmgr.AddNamespace("one", uri);
            return nsmgr;
        }

        private static XmlNode ImportFragment(XmlDocument doc, string fragmentXml)
        {
            var fragDoc = new XmlDocument();
            fragDoc.LoadXml(fragmentXml);
            return doc.ImportNode(fragDoc.DocumentElement, true);
        }

        /// <summary>The nearest ancestor one:OE of the first element marked selected, if any.</summary>
        private static XmlElement FindSelectedOutlineElement(XmlDocument doc)
        {
            var selected = doc.SelectSingleNode("//*[@selected]", Ns(doc)) as XmlElement;
            if (selected == null)
                return null;
            for (var node = selected; node != null; node = node.ParentNode as XmlElement)
            {
                if (node.LocalName == "OE")
                    return node;
            }
            return null;
        }

        /// <summary>The one:OE that (a) is an ancestor-or-self of the selection and (b) carries our Meta.</summary>
        private static XmlElement FindSelectedCodeBlockNode(XmlDocument doc)
        {
            var selected = doc.SelectSingleNode("//*[@selected]", Ns(doc)) as XmlElement;
            if (selected == null)
                return null;
            for (var node = selected; node != null; node = node.ParentNode as XmlElement)
            {
                if (node.LocalName != "OE")
                    continue;
                var metas = node.SelectNodes("one:Meta[@name='" + CodeBlockMeta.MetaName + "']", Ns(doc));
                if (metas != null && metas.Count > 0)
                    return node;
            }
            return null;
        }

        private static XmlNode FindLastOutlineChildren(XmlDocument doc)
        {
            var nsmgr = Ns(doc);
            var children = doc.SelectNodes("//one:OE/one:OEChildren", nsmgr);
            if (children != null && children.Count > 0)
                return children[children.Count - 1];

            // page with no outline yet: create one under the page element
            var page = doc.DocumentElement;
            var outline = doc.CreateElement("one", "OE", nsmgr.LookupNamespace("one"));
            var outlineChildren = doc.CreateElement("one", "OEChildren", nsmgr.LookupNamespace("one"));
            outline.AppendChild(outlineChildren);
            page.AppendChild(outline);
            return outlineChildren;
        }
    }
}

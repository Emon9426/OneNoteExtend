using System;
using System.Windows;
using OneExtend.CodeBlock.OneNoteXml;
using OneExtend.Core.Commands;
using OneExtend.Infrastructure.Logging;
using OneExtend.OneNote;

namespace OneExtend.Addin.Commands
{
    /// <summary>Copies the source of the block under the cursor as plain text (FR-603).</summary>
    public sealed class CopyCodeBlockCommand : ICommand
    {
        private readonly ServiceRegistry _services;

        public CopyCodeBlockCommand(ServiceRegistry services)
        {
            _services = services;
        }

        public CommandMetadata Metadata => new CommandMetadata(
            "codeblock.copySource",
            "复制为纯文本 Copy as Plain Text",
            "代码块 Code Block",
            "Ctrl+Shift+C",
            "⧉");

        public void Execute(ICommandContext context)
        {
            var logger = _services.Resolve<ILogger>() ?? NullLogger.Instance;
            try
            {
                using (var session = new ReflectionOneNoteSession())
                {
                    var pageId = session.GetActivePageId();
                    if (string.IsNullOrEmpty(pageId))
                        return;
                    var pageXml = session.GetPageContent(pageId);
                    var fragment = PageXmlEditor.FindSelectedCodeBlockFragment(pageXml);
                    if (fragment == null)
                    {
                        MessageBox.Show("请先将光标放在一个 OneExtend 代码块内。", "OneExtend",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    var extracted = SourceExtractor.FromFragment(fragment);
                    if (extracted == null)
                        return;
                    Clipboard.SetText(extracted.Source);
                }
                logger.Info("Code block copied as plain text.");
            }
            catch (Exception ex)
            {
                logger.Error("CopyCodeBlockCommand failed", ex);
            }
        }
    }
}

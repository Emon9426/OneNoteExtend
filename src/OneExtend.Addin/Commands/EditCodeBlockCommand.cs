using System;
using System.Windows;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Formatting;
using OneExtend.CodeBlock.OneNoteXml;
using OneExtend.CodeBlock.UI;
using OneExtend.Core.Commands;
using OneExtend.Infrastructure.Logging;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.Theme;
using OneExtend.Infrastructure.Settings;
using OneExtend.OneNote;

namespace OneExtend.Addin.Commands
{
    /// <summary>Re-opens the block under the cursor for editing and rebuilds it in place (FR-602).</summary>
    public sealed class EditCodeBlockCommand : ICommand
    {
        private readonly ServiceRegistry _services;

        public EditCodeBlockCommand(ServiceRegistry services)
        {
            _services = services;
        }

        public CommandMetadata Metadata => new CommandMetadata(
            "codeblock.edit",
            "编辑代码块 Edit Code Block",
            "代码块 Code Block",
            "Ctrl+Alt+E",
            "✎");

        public void Execute(ICommandContext context)
        {
            var logger = _services.Resolve<ILogger>() ?? NullLogger.Instance;
            try
            {
                var languages = _services.Resolve<LanguageRegistry>();
                var themes = _services.Resolve<ThemeLoader>();
                var settings = _services.Resolve<AppSettings>() ?? new AppSettings();

                using (var session = new ReflectionOneNoteSession())
                {
                    var pageId = session.GetActivePageId();
                    if (string.IsNullOrEmpty(pageId))
                    {
                        MessageBox.Show("未找到活动页面。", "OneExtend", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var pageXml = session.GetPageContent(pageId);
                    var fragment = PageXmlEditor.FindSelectedCodeBlockFragment(pageXml);
                    if (fragment == null)
                    {
                        MessageBox.Show("请先将光标放在一个 OneExtend 代码块内再执行编辑。", "OneExtend",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var extracted = SourceExtractor.FromFragment(fragment);
                    if (extracted == null)
                    {
                        MessageBox.Show("代码块结构无法解析（可能由更新版本的插件创建）。", "OneExtend",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var langId = extracted.Meta?.Lang;
                    var prefillOptions = new CodeBlockOptions
                    {
                        LanguageId = langId,
                        ThemeId = settings.Appearance?.Theme ?? "light",
                        Beautify = extracted.Meta?.Beautified != false,
                        LineNumbers = new LineNumberOptions
                        {
                            Enabled = true,
                            Start = extracted.Meta?.LineStart ?? 1,
                            Step = extracted.Meta?.LineStep ?? 1,
                            Mode = extracted.Meta?.LineNumberMode == "inl" ? LineNumberMode.Inline : LineNumberMode.Column
                        },
                        Format = new FormatOptions
                        {
                            IndentWidth = settings.Formatting?.IndentWidth ?? 4,
                            KeywordCase = settings.Formatting?.KeywordCase ?? KeywordCase.Preserve
                        }
                    };

                    var dialog = new InsertCodeBlockWindow(
                        languages, themes, settings,
                        prefillSource: extracted.Source,
                        prefillLanguageId: langId,
                        prefillOptions: prefillOptions);
                    if (dialog.ShowDialog() != true || dialog.Result == null)
                        return;

                    var result = dialog.Result;
                    if (!languages.TryGet(result.LanguageId, out var pack))
                    {
                        MessageBox.Show($"未知语言：{result.LanguageId}", "OneExtend",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var renderer = new CodeBlockRenderer(pack, themes.Get(result.Options.ThemeId));
                    var rendered = renderer.Render(result.Source, result.Options);
                    var updated = PageXmlEditor.ReplaceSelectedCodeBlock(pageXml, rendered.FragmentXml);
                    if (updated == null)
                    {
                        MessageBox.Show("未能定位原代码块（页面可能已变化），请改用插入。", "OneExtend",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    session.UpdatePageContent(updated);
                }
                logger.Info("Code block edited in place.");
            }
            catch (Exception ex)
            {
                logger.Error("EditCodeBlockCommand failed", ex);
                MessageBox.Show("编辑代码块时出错：\n" + ex.Message, "OneExtend",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

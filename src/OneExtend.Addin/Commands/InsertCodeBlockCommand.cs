using System;
using System.Windows;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.OneNoteXml;
using OneExtend.CodeBlock.Theme;
using OneExtend.CodeBlock.UI;
using OneExtend.Core.Commands;
using OneExtend.Infrastructure.Logging;
using OneExtend.Infrastructure.Settings;
using OneExtend.OneNote;

namespace OneExtend.Addin.Commands
{
    /// <summary>Opens the insert dialog and writes the rendered block at the cursor (FR-101..106).</summary>
    public sealed class InsertCodeBlockCommand : ICommand
    {
        private readonly ServiceRegistry _services;

        public InsertCodeBlockCommand(ServiceRegistry services)
        {
            _services = services;
        }

        public CommandMetadata Metadata => new CommandMetadata(
            "codeblock.insert",
            "插入代码块 Insert Code Block",
            "代码块 Code Block",
            "Ctrl+Alt+C",
            "</>");

        public void Execute(ICommandContext context)
        {
            var logger = _services.Resolve<ILogger>() ?? NullLogger.Instance;
            try
            {
                var languages = _services.Resolve<LanguageRegistry>();
                var themes = _services.Resolve<ThemeLoader>();
                var settingsStore = _services.Resolve<SettingsStore>();
                var settings = _services.Resolve<AppSettings>() ?? new AppSettings();

                string prefill = null;
                try
                {
                    if (Clipboard.ContainsText() && !string.IsNullOrWhiteSpace(Clipboard.GetText()))
                        prefill = Clipboard.GetText();
                }
                catch
                {
                    // clipboard may be locked by another process; dialog works without it
                }

                var dialog = new InsertCodeBlockWindow(languages, themes, settings, prefill);
                if (dialog.ShowDialog() != true || dialog.Result == null)
                    return;

                var result = dialog.Result;
                if (!languages.TryGet(result.LanguageId, out var pack))
                    pack = languages.Get("plsql");

                var theme = themes.Get(result.Options.ThemeId);
                var renderer = new CodeBlockRenderer(pack, theme);
                var rendered = renderer.Render(result.Source, result.Options);

                using (var session = new ReflectionOneNoteSession())
                {
                    var pageId = session.GetActivePageId();
                    if (string.IsNullOrEmpty(pageId))
                    {
                        MessageBox.Show("未找到活动页面，请先打开一个 OneNote 页面。", "OneExtend",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var pageXml = session.GetPageContent(pageId);
                    var updated = PageXmlEditor.InsertFragment(pageXml, rendered.FragmentXml);
                    session.UpdatePageContent(updated);
                }

                // remember the language as recent
                try
                {
                    settings.RecentLanguages.RemoveAll(r => string.Equals(r, result.LanguageId, StringComparison.OrdinalIgnoreCase));
                    settings.RecentLanguages.Insert(0, result.LanguageId);
                    if (settings.RecentLanguages.Count > 5)
                        settings.RecentLanguages.RemoveRange(5, settings.RecentLanguages.Count - 5);
                    settingsStore?.Save(settings);
                }
                catch (Exception ex)
                {
                    logger.Warn("Failed to persist recent languages: " + ex.Message);
                }

                logger.Info($"Inserted code block: lang={result.LanguageId}, lines={rendered.FormattedSource.Split('\n').Length}");
            }
            catch (Exception ex)
            {
                logger.Error("InsertCodeBlockCommand failed", ex);
                MessageBox.Show("插入代码块时出错：\n" + ex.Message, "OneExtend",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

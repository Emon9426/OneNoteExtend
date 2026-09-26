using System.Collections.Generic;
using OneExtend.CodeBlock.Formatting;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.Model;
using OneExtend.CodeBlock.OneNoteXml;
using OneExtend.CodeBlock.Theme;

namespace OneExtend.CodeBlock
{
    public sealed class RenderedCodeBlock
    {
        public string FormattedSource { get; set; }
        public string FragmentXml { get; set; }
        public IReadOnlyList<string> PreviewLines { get; set; }
    }

    /// <summary>
    /// Facade over the whole pipeline: format → tokenize → theme → OneNote XML
    /// (or preview lines). The same engine feeds the dialog preview and the
    /// page insertion, guaranteeing WYSIWYG.
    /// </summary>
    public sealed class CodeBlockRenderer
    {
        private readonly FormatterRegistry _formatters = new FormatterRegistry();

        public CodeTheme Theme { get; }
        public GrammarPack Language { get; }

        public CodeBlockRenderer(GrammarPack language, CodeTheme theme)
        {
            Language = language;
            Theme = theme;
        }

        public RenderedCodeBlock Render(string source, CodeBlockOptions options)
        {
            var formatted = Formatting.TokenFilters.Normalize(source ?? string.Empty);
            if (options.Beautify)
            {
                var formatter = _formatters.Get(Language);
                if (formatter != null)
                    formatted = formatter.Format(formatted, options.Format);
            }

            var tokenizer = new Tokenizer(Language);
            var tokens = tokenizer.Tokenize(formatted);
            var lines = Tokenizer.SplitLines(tokens);

            var fragment = OneNoteXmlBuilder.Build(lines, Theme, options, Language.Display);

            var preview = new List<string>(lines.Count);
            var numbers = OneNoteXmlBuilder.NumberTexts(options.LineNumbers, lines.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                var lineText = OneNoteXmlBuilder.TokenizeLineText(lines[i]);
                preview.Add(options.LineNumbers.Enabled ? numbers[i] + lineText : lineText);
            }

            return new RenderedCodeBlock
            {
                FormattedSource = formatted,
                FragmentXml = fragment,
                PreviewLines = preview
            };
        }

        /// <summary>Token/color runs per line, for the WPF preview control.</summary>
        public List<List<ColoredRun>> BuildPreviewRuns(string source, CodeBlockOptions options)
        {
            var formatted = source;
            if (options.Beautify)
            {
                var formatter = _formatters.Get(Language);
                if (formatter != null)
                    formatted = formatter.Format(Formatting.TokenFilters.Normalize(formatted), options.Format);
            }
            var tokenizer = new Tokenizer(Language);
            var lines = Tokenizer.SplitLines(tokenizer.Tokenize(formatted));
            var result = new List<List<ColoredRun>>(lines.Count);
            foreach (var line in lines)
                result.Add(OneNoteXmlBuilder.BuildRuns(line, Theme));
            return result;
        }
    }
}

using OneExtend.CodeBlock.Formatting;

namespace OneExtend.CodeBlock.OneNoteXml
{
    public enum LineNumberMode { Column, Inline }
    public enum LineNumberSeparator { None, Bar, Dot }

    public sealed class LineNumberOptions
    {
        public bool Enabled { get; set; } = true;
        public int Start { get; set; } = 1;
        public int Step { get; set; } = 1;
        public LineNumberMode Mode { get; set; } = LineNumberMode.Column;
        public LineNumberSeparator Separator { get; set; } = LineNumberSeparator.Bar;
        /// <summary>0 = auto width from the largest rendered number.</summary>
        public int FixedDigits { get; set; } = 0;

        public LineNumberOptions Clone() => new LineNumberOptions
        {
            Enabled = Enabled,
            Start = Start,
            Step = Step,
            Mode = Mode,
            Separator = Separator,
            FixedDigits = FixedDigits
        };
    }

    /// <summary>Everything the renderer needs to know about one code block insertion.</summary>
    public sealed class CodeBlockOptions
    {
        public string Title { get; set; }
        public string LanguageId { get; set; }
        public string ThemeId { get; set; } = "light";
        public bool Beautify { get; set; } = true;
        /// <summary>Optional per-block font override; null falls back to the theme font.</summary>
        public string FontFamily { get; set; }
        public string FontSize { get; set; }
        public FormatOptions Format { get; set; } = new FormatOptions();
        public LineNumberOptions LineNumbers { get; set; } = new LineNumberOptions();
        public string PluginVersion { get; set; } = "0.1.0";
    }
}

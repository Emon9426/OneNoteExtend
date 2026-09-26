namespace OneExtend.CodeBlock.Formatting
{
    public enum KeywordCase { Preserve, Upper, Lower, Capitalize }
    public enum CommaPosition { Trailing, Leading }

    public sealed class FormatOptions
    {
        public int IndentWidth { get; set; } = 4;
        public KeywordCase KeywordCase { get; set; } = KeywordCase.Preserve;
        public CommaPosition CommaPosition { get; set; } = CommaPosition.Trailing;
        public bool ClauseNewline { get; set; } = true;
        public bool AlignAndOr { get; set; } = true;

        public FormatOptions Clone() => new FormatOptions
        {
            IndentWidth = IndentWidth,
            KeywordCase = KeywordCase,
            CommaPosition = CommaPosition,
            ClauseNewline = ClauseNewline,
            AlignAndOr = AlignAndOr
        };
    }
}

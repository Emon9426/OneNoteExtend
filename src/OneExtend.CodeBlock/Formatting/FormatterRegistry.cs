using OneExtend.CodeBlock.Grammar;

namespace OneExtend.CodeBlock.Formatting
{
    public interface ICodeFormatter
    {
        string Id { get; }
        string Format(string source, FormatOptions options);
    }

    /// <summary>Resolves a grammar's `formatter` id to a formatter instance.</summary>
    public sealed class FormatterRegistry
    {
        public ICodeFormatter Get(GrammarPack grammar)
        {
            var id = grammar?.Formatter ?? "none";
            switch (id)
            {
                case "sql-structured":
                    return new SqlFormatter(new Tokenizer(grammar));
                case "plsql-structured":
                    return new PlSqlFormatter(new Tokenizer(grammar));
                case "generic-brace":
                    return new GenericIndentFormatter(GenericIndentFormatter.Mode.Brace);
                case "generic-tag":
                    return new GenericIndentFormatter(GenericIndentFormatter.Mode.Tag);
                default:
                    return null;
            }
        }
    }
}

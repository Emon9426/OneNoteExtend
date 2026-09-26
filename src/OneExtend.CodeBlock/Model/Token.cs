namespace OneExtend.CodeBlock.Model
{
    public enum TokenType
    {
        Plain,
        Keyword,
        ControlKeyword,
        Type,
        String,
        Number,
        Comment,
        Function,
        Variable,
        Tag,
        Attribute,
        Operator,
        Punctuation,
        BindVariable,
        Substitution,
        BuiltinPackage,
        Identifier
    }

    public sealed class Token
    {
        public TokenType Type { get; }
        public string Text { get; }

        public Token(TokenType type, string text)
        {
            Type = type;
            Text = text ?? string.Empty;
        }

        public override string ToString() => $"{Type}:{Text}";
    }

    /// <summary>Maps the string token types used in grammar packs / themes to the enum.</summary>
    public static class TokenTypeMap
    {
        public static TokenType Parse(string ruleType)
        {
            switch ((ruleType ?? string.Empty).ToLowerInvariant())
            {
                case "comment.line":
                case "comment.block":
                case "comment": return TokenType.Comment;
                case "string":
                case "string.q":
                case "string.template": return TokenType.String;
                case "number": return TokenType.Number;
                case "keyword": return TokenType.Keyword;
                case "control": return TokenType.ControlKeyword;
                case "type": return TokenType.Type;
                case "function": return TokenType.Function;
                case "variable": return TokenType.Variable;
                case "tag": return TokenType.Tag;
                case "attribute": return TokenType.Attribute;
                case "operator": return TokenType.Operator;
                case "punctuation": return TokenType.Punctuation;
                case "bindvar": return TokenType.BindVariable;
                case "substitution": return TokenType.Substitution;
                case "builtin.package": return TokenType.BuiltinPackage;
                case "identifier": return TokenType.Identifier;
                default: return TokenType.Plain;
            }
        }

        /// <summary>The key used in theme JSON color maps.</summary>
        public static string ThemeKey(TokenType type)
        {
            switch (type)
            {
                case TokenType.Keyword: return "keyword";
                case TokenType.ControlKeyword: return "controlKeyword";
                case TokenType.Type: return "type";
                case TokenType.String: return "string";
                case TokenType.Number: return "number";
                case TokenType.Comment: return "comment";
                case TokenType.Function: return "function";
                case TokenType.Variable: return "variable";
                case TokenType.Tag: return "tag";
                case TokenType.Attribute: return "attribute";
                case TokenType.Operator: return "operator";
                case TokenType.BindVariable: return "bindVariable";
                case TokenType.Substitution: return "substitution";
                case TokenType.BuiltinPackage: return "builtinPackage";
                case TokenType.Identifier: return "identifier";
                default: return "plain";
            }
        }
    }
}

using System.Collections.Generic;
using OneExtend.CodeBlock.Model;

namespace OneExtend.CodeBlock.Theme
{
    /// <summary>Color scheme for rendering; keys of `colors` are TokenTypeMap.ThemeKey values.</summary>
    public sealed class CodeTheme
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Background { get; set; } = "#FBFCFD";
        public string LineNumberBackground { get; set; } = "#F3F4F6";
        public string TitleBackground { get; set; } = "#EEF0F3";
        public string FontFamily { get; set; } = "Consolas";
        public string FontSize { get; set; } = "10pt";
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>();

        public string ColorOf(TokenType type)
        {
            var key = TokenTypeMap.ThemeKey(type);
            if (Colors != null && Colors.TryGetValue(key, out var color) && !string.IsNullOrEmpty(color))
                return color;
            return "#1F2328";
        }
    }
}

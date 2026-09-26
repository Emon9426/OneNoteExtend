using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OneExtend.CodeBlock.Grammar
{
    public sealed class DetectionConfig
    {
        /// <summary>Regex patterns used for auto language detection (always case-insensitive).</summary>
        public List<string> Patterns { get; set; } = new List<string>();
    }

    public sealed class TokenRule
    {
        public string Type { get; set; }
        public string Pattern { get; set; }
    }

    /// <summary>
    /// Declarative language definition loaded from grammars/*.grammar.json.
    /// New languages ship as data files - no code changes required.
    /// </summary>
    public sealed class GrammarPack
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string BasedOn { get; set; }
        public bool CaseInsensitive { get; set; }
        public string Formatter { get; set; } = "none";
        public List<string> Extensions { get; set; } = new List<string>();
        public DetectionConfig Detection { get; set; }
        public Dictionary<string, List<string>> Keywords { get; set; } =
            new Dictionary<string, List<string>>();
        public List<TokenRule> Tokens { get; set; } = new List<TokenRule>();

        [JsonIgnore]
        public string Display => string.IsNullOrEmpty(Name) ? Id : Name;

        public bool IsWordIn(string listName, string word)
        {
            if (Keywords == null || !Keywords.TryGetValue(listName, out var list) || list == null)
                return false;
            var comparer = CaseInsensitive
                ? System.StringComparer.OrdinalIgnoreCase
                : System.StringComparer.Ordinal;
            foreach (var w in list)
            {
                if (comparer.Equals(w, word))
                    return true;
            }
            return false;
        }
    }
}

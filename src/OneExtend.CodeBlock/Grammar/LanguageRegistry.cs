using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OneExtend.CodeBlock.Grammar
{
    /// <summary>
    /// All loaded languages plus heuristic source-language detection
    /// (each grammar's `detection.patterns` score matches; highest wins).
    /// </summary>
    public sealed class LanguageRegistry
    {
        private readonly Dictionary<string, GrammarPack> _byId;
        private readonly List<GrammarPack> _ordered;

        public LanguageRegistry(IReadOnlyList<GrammarPack> packs)
        {
            _byId = new Dictionary<string, GrammarPack>(packs.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var p in packs)
                _byId[p.Id] = p;
            _ordered = packs.OrderBy(p => p.Display, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static LanguageRegistry LoadDefault(string dataRoot = null)
        {
            var root = dataRoot ?? GrammarLoader.LocateDataRoot();
            var dir = System.IO.Path.Combine(root, "grammars");
            if (!System.IO.Directory.Exists(dir))
                dir = root;
            return new LanguageRegistry(GrammarLoader.LoadDirectory(dir));
        }

        public IReadOnlyList<GrammarPack> All => _ordered;

        public bool TryGet(string id, out GrammarPack pack) => _byId.TryGetValue(id ?? string.Empty, out pack);

        public GrammarPack Get(string id) =>
            TryGet(id, out var pack) ? pack : throw new KeyNotFoundException($"Unknown language '{id}'.");

        /// <summary>Returns the best-matching language id for the snippet, or null.</summary>
        public string DetectLanguage(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            string best = null;
            int bestScore = 0;
            foreach (var pack in _ordered)
            {
                if (pack.Detection?.Patterns == null || pack.Detection.Patterns.Count == 0)
                    continue;
                var score = 0;
                foreach (var pattern in pack.Detection.Patterns)
                {
                    int count;
                    try
                    {
                        count = Regex.Matches(source, pattern, RegexOptions.IgnoreCase).Count;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                    // Cap per-pattern influence so one noisy pattern cannot dominate.
                    score += Math.Min(count, 3);
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pack.Id;
                }
            }
            return best;
        }
    }
}

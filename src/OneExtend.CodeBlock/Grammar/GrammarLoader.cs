using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OneExtend.CodeBlock.Grammar
{
    /// <summary>
    /// Loads grammar packs from JSON files and resolves `basedOn` inheritance:
    /// keyword lists are merged (union), while tokens / detection / formatter
    /// override the parent when the child defines them.
    /// </summary>
    public static class GrammarLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Walks up from the assembly location looking for the repository /
        /// install layout containing a `grammars` directory.
        /// </summary>
        public static string LocateDataRoot(string startDirectory = null)
        {
            var dir = startDirectory ?? AppContext.BaseDirectory;
            for (var i = 0; i < 8 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "grammars")))
                    return dir;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            return AppContext.BaseDirectory;
        }

        public static IReadOnlyList<GrammarPack> LoadDirectory(string directory)
        {
            var raw = new List<GrammarPack>();
            foreach (var file in Directory.EnumerateFiles(directory, "*.grammar.json", SearchOption.TopDirectoryOnly))
            {
                var pack = LoadFile(file);
                if (pack != null)
                    raw.Add(pack);
            }
            return ResolveInheritance(raw);
        }

        public static GrammarPack LoadFile(string file)
        {
            var json = File.ReadAllText(file);
            var pack = JsonSerializer.Deserialize<GrammarPack>(json, JsonOptions);
            if (pack == null || string.IsNullOrWhiteSpace(pack.Id))
                throw new InvalidDataException($"Grammar pack '{file}' is missing an id.");
            if (pack.Tokens == null || pack.Tokens.Count == 0)
                throw new InvalidDataException($"Grammar pack '{pack.Id}' declares no token rules.");
            foreach (var rule in pack.Tokens)
            {
                if (string.IsNullOrWhiteSpace(rule.Pattern) || string.IsNullOrWhiteSpace(rule.Type))
                    throw new InvalidDataException(
                        $"Grammar pack '{pack.Id}' has a token rule without type or pattern.");
            }
            return pack;
        }

        private static IReadOnlyList<GrammarPack> ResolveInheritance(List<GrammarPack> packs)
        {
            var byId = new Dictionary<string, GrammarPack>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in packs)
                byId[p.Id] = p;

            var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pack in packs)
                Resolve(pack, byId, resolved, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            return packs;
        }

        private static void Resolve(
            GrammarPack pack,
            Dictionary<string, GrammarPack> byId,
            HashSet<string> resolved,
            HashSet<string> visiting)
        {
            if (resolved.Contains(pack.Id))
                return;
            if (!string.IsNullOrEmpty(pack.BasedOn))
            {
                if (!visiting.Add(pack.Id))
                    throw new InvalidDataException($"Circular basedOn chain involving '{pack.Id}'.");
                if (byId.TryGetValue(pack.BasedOn, out var parent))
                {
                    Resolve(parent, byId, resolved, visiting);
                    MergeInto(pack, parent);
                }
                visiting.Remove(pack.Id);
            }
            resolved.Add(pack.Id);
        }

        private static void MergeInto(GrammarPack child, GrammarPack parent)
        {
            // Keyword lists merge as union (parent first, child adds/overrides nothing but words).
            foreach (var kv in parent.Keywords)
            {
                if (!child.Keywords.TryGetValue(kv.Key, out var childList))
                {
                    child.Keywords[kv.Key] = new List<string>(kv.Value);
                }
                else
                {
                    var seen = new HashSet<string>(childList, StringComparer.OrdinalIgnoreCase);
                    foreach (var w in kv.Value)
                        if (seen.Add(w))
                            childList.Add(w);
                }
            }

            // Detection patterns merge (parent patterns kept when child has none of its own).
            if (child.Detection == null || child.Detection.Patterns.Count == 0)
            {
                child.Detection = parent.Detection;
            }
            else if (parent.Detection != null)
            {
                var seen = new HashSet<string>(child.Detection.Patterns);
                foreach (var p in parent.Detection.Patterns)
                    if (seen.Add(p))
                        child.Detection.Patterns.Insert(0, p);
            }

            // Parent identity settings apply when the child does not define them.
            if (string.IsNullOrEmpty(child.Formatter) || child.Formatter == "none")
                child.Formatter = parent.Formatter;
            if (!child.CaseInsensitive)
                child.CaseInsensitive = parent.CaseInsensitive;
        }
    }
}

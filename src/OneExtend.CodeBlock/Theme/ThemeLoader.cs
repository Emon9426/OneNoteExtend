using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OneExtend.CodeBlock.Theme
{
    /// <summary>Loads themes/*.json with built-in fallbacks so the renderer always has a scheme.</summary>
    public sealed class ThemeLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private readonly Dictionary<string, CodeTheme> _themes;

        public ThemeLoader(IReadOnlyList<CodeTheme> themes)
        {
            _themes = new Dictionary<string, CodeTheme>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in themes)
                if (!string.IsNullOrEmpty(t.Id))
                    _themes[t.Id] = t;
        }

        public IReadOnlyList<CodeTheme> All => _themes.Values.OrderBy(t => t.Name).ToList();

        public CodeTheme Get(string id) =>
            _themes.TryGetValue(id ?? string.Empty, out var theme) ? theme : GetDefault();

        public CodeTheme GetDefault() =>
            _themes.TryGetValue("light", out var theme) ? theme : BuiltInLight;

        public static ThemeLoader LoadDefault(string dataRoot = null)
        {
            var root = dataRoot ?? Grammar.GrammarLoader.LocateDataRoot();
            var dir = Path.Combine(root, "themes");
            var themes = new List<CodeTheme>();
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var theme = JsonSerializer.Deserialize<CodeTheme>(File.ReadAllText(file), JsonOptions);
                        if (theme != null && !string.IsNullOrEmpty(theme.Id))
                            themes.Add(theme);
                    }
                    catch (JsonException)
                    {
                        // A bad theme file must not take the add-in down; skip it.
                    }
                }
            }
            if (themes.Count == 0)
            {
                themes.Add(BuiltInLight);
                themes.Add(BuiltInDark);
            }
            return new ThemeLoader(themes);
        }

        public static CodeTheme BuiltInLight => new CodeTheme
        {
            Id = "light",
            Name = "浅色 Light",
            Background = "#FBFCFD",
            LineNumberBackground = "#F3F4F6",
            TitleBackground = "#EEF0F3",
            FontFamily = "Consolas",
            FontSize = "10pt",
            Colors = new Dictionary<string, string>
            {
                ["plain"] = "#1F2328",
                ["keyword"] = "#0000FF",
                ["controlKeyword"] = "#AF00DB",
                ["type"] = "#267F99",
                ["string"] = "#A31515",
                ["number"] = "#098658",
                ["comment"] = "#008000",
                ["function"] = "#795E26",
                ["variable"] = "#001080",
                ["tag"] = "#800000",
                ["attribute"] = "#E50000",
                ["operator"] = "#24292F",
                ["bindVariable"] = "#8250DF",
                ["substitution"] = "#8250DF",
                ["builtinPackage"] = "#267F99",
                ["identifier"] = "#1F2328",
                ["lineNumber"] = "#8B949E"
            }
        };

        public static CodeTheme BuiltInDark => new CodeTheme
        {
            Id = "dark",
            Name = "深色 Dark",
            Background = "#161B22",
            LineNumberBackground = "#1C2128",
            TitleBackground = "#21262D",
            FontFamily = "Consolas",
            FontSize = "10pt",
            Colors = new Dictionary<string, string>
            {
                ["plain"] = "#C9D1D9",
                ["keyword"] = "#FF7B72",
                ["controlKeyword"] = "#D2A8FF",
                ["type"] = "#7EE787",
                ["string"] = "#A5D6FF",
                ["number"] = "#79C0FF",
                ["comment"] = "#8B949E",
                ["function"] = "#FFA657",
                ["variable"] = "#79C0FF",
                ["tag"] = "#7EE787",
                ["attribute"] = "#79C0FF",
                ["operator"] = "#C9D1D9",
                ["bindVariable"] = "#D2A8FF",
                ["substitution"] = "#D2A8FF",
                ["builtinPackage"] = "#56D4DD",
                ["identifier"] = "#C9D1D9",
                ["lineNumber"] = "#484F58"
            }
        };
    }
}

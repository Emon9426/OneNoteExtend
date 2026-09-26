using System.Collections.Generic;
using OneExtend.CodeBlock.Formatting;
using OneExtend.CodeBlock.OneNoteXml;

namespace OneExtend.Infrastructure.Settings
{
    /// <summary>Persisted user preferences (see docs/需求文档 §6.1).</summary>
    public sealed class AppSettings
    {
        public int SettingsVersion { get; set; } = 1;

        public GeneralSettings General { get; set; } = new GeneralSettings();
        public AppearanceSettings Appearance { get; set; } = new AppearanceSettings();
        public LineNumberOptions LineNumbers { get; set; } = new LineNumberOptions();
        public FormatOptions Formatting { get; set; } = new FormatOptions();
        public Dictionary<string, string> Shortcuts { get; set; } = new Dictionary<string, string>
        {
            ["insertCodeBlock"] = "Ctrl+Alt+C",
            ["editCodeBlock"] = "Ctrl+Alt+E"
        };
        public Dictionary<string, bool> Modules { get; set; } = new Dictionary<string, bool>
        {
            ["codeblock"] = true
        };

        /// <summary>Most recently used languages, most recent first (bounded).</summary>
        public List<string> RecentLanguages { get; set; } = new List<string>();

        public AppSettings Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
        }
    }

    public sealed class GeneralSettings
    {
        public string DefaultLanguage { get; set; } = "plsql";
        public string UiLanguage { get; set; } = "zh-CN";
    }

    public sealed class AppearanceSettings
    {
        public string Theme { get; set; } = "light";
        public string FontFamily { get; set; } = "Consolas";
        public int FontSizePt { get; set; } = 10;
        public bool FollowSystemDark { get; set; } = true;
    }
}

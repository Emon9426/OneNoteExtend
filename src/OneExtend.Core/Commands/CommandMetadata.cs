using System;

namespace OneExtend.Core.Commands
{
    /// <summary>
    /// Descriptive data for a command. Ribbon entries, shortcuts, context-menu
    /// items and the command palette are all generated from this metadata,
    /// so adding a feature never requires touching the host shell.
    /// </summary>
    public sealed class CommandMetadata
    {
        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public string Shortcut { get; }
        public string Icon { get; }
        public bool ShowInCommandPalette { get; }

        public CommandMetadata(
            string id,
            string title,
            string category = "General",
            string shortcut = null,
            string icon = null,
            bool showInCommandPalette = true)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Command id is required.", nameof(id));
            Id = id;
            Title = title ?? id;
            Category = category ?? "General";
            Shortcut = shortcut;
            Icon = icon;
            ShowInCommandPalette = showInCommandPalette;
        }
    }
}

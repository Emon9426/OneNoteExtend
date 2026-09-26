using System;
using System.Text.RegularExpressions;

namespace OneExtend.CodeBlock.OneNoteXml
{
    public sealed class CodeBlockMetaInfo
    {
        public string Lang { get; set; }
        public string Version { get; set; }
        public string LineNumberMode { get; set; } = "col";
        public int LineStart { get; set; } = 1;
        public int LineStep { get; set; } = 1;
        public bool Beautified { get; set; }
        public string SourceHash { get; set; }
    }

    /// <summary>
    /// The `one:Meta` payload that makes an inserted block self-describing
    /// (round-trip editing, FR-601) and identifies it to future features.
    /// </summary>
    public static class CodeBlockMeta
    {
        public const string MetaName = "OneExtend:CodeBlock";

        public static string Serialize(CodeBlockMetaInfo info)
        {
            return string.Format(
                "lang={0};v={1};ln={2}:{3}:{4};fmt={5};h={6}",
                info.Lang ?? "plain",
                info.Version ?? "0",
                info.LineNumberMode ?? "col",
                info.LineStart,
                info.LineStep,
                info.Beautified ? "on" : "off",
                info.SourceHash ?? string.Empty);
        }

        public static CodeBlockMetaInfo Parse(string content)
        {
            var info = new CodeBlockMetaInfo();
            if (string.IsNullOrEmpty(content))
                return info;
            foreach (var part in content.Split(';'))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length != 2)
                    continue;
                var key = kv[0].Trim();
                var value = kv[1].Trim();
                switch (key)
                {
                    case "lang": info.Lang = value; break;
                    case "v": info.Version = value; break;
                    case "ln":
                        var seg = value.Split(':');
                        if (seg.Length > 0) info.LineNumberMode = seg[0];
                        if (seg.Length > 1 && int.TryParse(seg[1], out var s1)) info.LineStart = s1;
                        if (seg.Length > 2 && int.TryParse(seg[2], out var s2)) info.LineStep = s2;
                        break;
                    case "fmt": info.Beautified = value == "on"; break;
                    case "h": info.SourceHash = value; break;
                }
            }
            return info;
        }

        /// <summary>Short fingerprint of the formatted source (first 8 hex chars of SHA-256).</summary>
        public static string HashSource(string formattedSource)
        {
            if (formattedSource == null)
                return string.Empty;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(formattedSource));
                var sb = new System.Text.StringBuilder();
                for (var i = 0; i < 4; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}

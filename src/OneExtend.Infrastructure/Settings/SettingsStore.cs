using System;
using System.IO;
using System.Text.Json;

namespace OneExtend.Infrastructure.Settings
{
    /// <summary>Loads/saves AppSettings as JSON, always returning a usable instance.</summary>
    public sealed class SettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private readonly string _path;
        private readonly object _gate = new object();

        public SettingsStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public static string DefaultPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OneExtend", "settings.json");

        public AppSettings Load()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(_path))
                    {
                        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
                        if (settings != null)
                            return settings;
                    }
                }
                catch
                {
                    // Corrupt settings fall back to defaults; the next Save repairs the file.
                }
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            lock (_gate)
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
            }
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace OneExtend.Infrastructure.Logging
{
    public interface ILogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception = null);
    }

    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception exception = null) { }
    }

    /// <summary>Append-only daily log file; never throws into the caller.</summary>
    public sealed class FileLogger : ILogger
    {
        private readonly string _directory;
        private readonly object _gate = new object();

        public FileLogger(string directory)
        {
            _directory = directory;
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
                // Logging must never break the add-in.
            }
        }

        public static string DefaultDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OneExtend", "logs");

        public void Info(string message) => Write("INFO ", message);
        public void Warn(string message) => Write("WARN ", message);
        public void Error(string message, Exception exception = null) =>
            Write("ERROR", message + (exception == null ? string.Empty : Environment.NewLine + exception));

        private void Write(string level, string message)
        {
            try
            {
                var file = Path.Combine(_directory, $"oneextend-{DateTime.Now:yyyy-MM-dd}.log");
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                  .Append(" [").Append(level).Append("] ")
                  .AppendLine(message ?? string.Empty);
                lock (_gate)
                {
                    File.AppendAllText(file, sb.ToString());
                }
            }
            catch
            {
                // swallow - see class doc
            }
        }
    }
}

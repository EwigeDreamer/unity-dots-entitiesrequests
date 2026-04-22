using System;
using System.IO;

namespace Utilities
{
    /// <summary>
    /// Simple file logger for source generators.
    /// Logs messages to a text file in the temp directory.
    /// </summary>
    public class FileLogger
    {
        private enum LogLevel { INFO, WARNING, ERROR }

        private const string BaseLogFolder = "Unity/SourceGenerator";
        private readonly string _filePath;

        private static readonly object Lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogger"/> class.
        /// </summary>
        /// <param name="projectName">Name of the project, used as a subfolder for logs.</param>
        /// <param name="logFileName">Optional log file name. Defaults to "Log.txt".</param>
        public FileLogger(string projectName, string logFileName = "Log.txt")
        {
            var directoryName = Path.Combine(BaseLogFolder, projectName);
            _filePath = Path.Combine(Path.GetTempPath(), directoryName, logFileName);
        }

        private void EnsureLogDirectory()
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        public void Info(string message) => Log(LogLevel.INFO, message);
        public void Warning(string message) => Log(LogLevel.WARNING, message);
        public void Error(string message) => Log(LogLevel.ERROR, message);

        private void Log(LogLevel level, string message)
        {
            lock (Lock)
            {
                EnsureLogDirectory();
                File.AppendAllText(_filePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}]: {message}{Environment.NewLine}");
            }
        }
    }
}
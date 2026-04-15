using System;
using System.IO;

namespace EntitiesRequestsSourceGenerator
{
    public class FileLogger
    {
        private enum LogLevel { INFO, WARNING, ERROR }
        
        private const string LogFolderName = "Unity/SourceGenerator/EntitiesRequests";
        private const string LogFileName = "Log.txt";
        
        private readonly string _filePath = Path.Combine(Path.GetTempPath(), LogFolderName, LogFileName);
        
        private static readonly object Lock = new object();

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
                File.AppendAllText(_filePath, $"{DateTime.Now:yyyy-MM-dd hh:mm:ss.fff} [{level}]: {message}{Environment.NewLine}");
            }
        }
    }
}
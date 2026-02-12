using System;
using System.IO;
using System.Threading;

namespace SURS.App.Common
{
    /// <summary>
    /// 文件日志实现
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly object _lockObject = new object();

        public FileLogger()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SURS",
                "Logs"
            );

            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public void LogInfo(string message)
        {
            WriteToFile("info.log", $"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            WriteToFile("warning.log", $"[WARN] {message}");
        }

        public void LogError(string message, Exception? exception = null)
        {
            var logEntry = $"[ERROR] {message}";
            if (exception != null)
            {
                logEntry += $"\n异常类型: {exception.GetType().Name}";
                logEntry += $"\n异常消息: {exception.Message}";
                logEntry += $"\n堆栈跟踪:\n{exception.StackTrace}";
            }
            WriteToFile("error.log", logEntry);
        }

        public void LogDebug(string message)
        {
#if DEBUG
            WriteToFile("debug.log", $"[DEBUG] {message}");
#endif
        }

        private void WriteToFile(string fileName, string content)
        {
            try
            {
                lock (_lockObject)
                {
                    var filePath = Path.Combine(_logDirectory, fileName);
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var logLine = $"[{timestamp}] {content}{Environment.NewLine}";

                    // 限制日志文件大小（10MB）
                    if (File.Exists(filePath) && new FileInfo(filePath).Length > 10 * 1024 * 1024)
                    {
                        var backupPath = filePath.Replace(".log", $".{DateTime.Now:yyyyMMddHHmmss}.log");
                        File.Move(filePath, backupPath);
                    }

                    File.AppendAllText(filePath, logLine);
                }
            }
            catch
            {
                // 日志写入失败时静默处理，避免影响主程序
            }
        }
    }
}


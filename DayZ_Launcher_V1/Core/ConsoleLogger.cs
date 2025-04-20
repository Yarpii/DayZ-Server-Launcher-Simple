using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameServerManager.Core
{
    /// <summary>
    /// Implementation of ILogger that logs messages to the console with timestamps, 
    /// color formatting, and optional file logging.
    /// </summary>
    public class ConsoleLogger : ILogger, IDisposable
    {
        private readonly object _syncLock = new();
        private readonly LogLevel _minimumLevel;
        // These fields can't be readonly since they need to be modified
        private bool _enableFileLogging;
        private string? _logFilePath;
        private StreamWriter? _logFileWriter;
        private bool _isDisposed;
        private int _errorCount;
        private int _warningCount;

        /// <summary>
        /// Gets the total number of error messages logged.
        /// </summary>
        public int ErrorCount => _errorCount;

        /// <summary>
        /// Gets the total number of warning messages logged.
        /// </summary>
        public int WarningCount => _warningCount;

        /// <summary>
        /// Gets or sets whether to include timestamps in log messages.
        /// </summary>
        public bool IncludeTimestamps { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to include log levels in messages.
        /// </summary>
        public bool IncludeLogLevels { get; set; } = true;

        /// <summary>
        /// Gets or sets the date/time format for log timestamps.
        /// </summary>
        public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Initializes a new instance of the ConsoleLogger class.
        /// </summary>
        /// <param name="minimumLevel">Minimum level of messages to log</param>
        /// <param name="enableFileLogging">Whether to also log messages to a file</param>
        /// <param name="logFilePath">Path to the log file, or null to use the default path</param>
        public ConsoleLogger(
            LogLevel minimumLevel = LogLevel.Info,
            bool enableFileLogging = false,
            string? logFilePath = null)
        {
            _minimumLevel = minimumLevel;
            _enableFileLogging = enableFileLogging;

            if (enableFileLogging)
            {
                // Use specified path or create default path
                _logFilePath = logFilePath ?? GetDefaultLogFilePath();
                InitializeFileLogging();
            }
        }

        /// <summary>
        /// Logs an informational message to the console with optional color formatting.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">The console color to use for the message</param>
        public void LogInfo(string message, ConsoleColor color = ConsoleColor.White)
        {
            if (_minimumLevel <= LogLevel.Info)
            {
                LogWithLevel(message, "INFO", color, LogLevel.Info);
            }
        }

        /// <summary>
        /// Logs a debug message to the console.
        /// </summary>
        /// <param name="message">The debug message to log</param>
        public void LogDebug(string message)
        {
            if (_minimumLevel <= LogLevel.Debug)
            {
                LogWithLevel(message, "DEBUG", ConsoleColor.Gray, LogLevel.Debug);
            }
        }

        /// <summary>
        /// Logs a warning message to the console in yellow.
        /// </summary>
        /// <param name="message">The warning message to log</param>
        public void LogWarning(string message)
        {
            if (_minimumLevel <= LogLevel.Warning)
            {
                Interlocked.Increment(ref _warningCount);
                LogWithLevel(message, "WARN", ConsoleColor.Yellow, LogLevel.Warning);
            }
        }

        /// <summary>
        /// Logs an error message to the console in red.
        /// </summary>
        /// <param name="message">The error message to log</param>
        public void LogError(string message)
        {
            if (_minimumLevel <= LogLevel.Error)
            {
                Interlocked.Increment(ref _errorCount);
                LogWithLevel(message, "ERROR", ConsoleColor.Red, LogLevel.Error);
            }
        }

        /// <summary>
        /// Logs a critical error message to the console.
        /// </summary>
        /// <param name="message">The critical error message to log</param>
        public void LogCritical(string message)
        {
            if (_minimumLevel <= LogLevel.Critical)
            {
                Interlocked.Increment(ref _errorCount);

                // Use background color for critical errors to make them stand out
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;

                LogWithLevel(message, "CRITICAL", ConsoleColor.White, LogLevel.Critical);

                Console.ResetColor();
            }
        }

        /// <summary>
        /// Logs a message with the specified log level.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="levelText">The text representation of the log level</param>
        /// <param name="color">The console color to use for the message</param>
        /// <param name="level">The log level of this message</param>
        private void LogWithLevel(string message, string levelText, ConsoleColor color, LogLevel level)
        {
            // Build the complete log message
            string timestamp = IncludeTimestamps ? $"[{DateTime.Now.ToString(TimestampFormat)}] " : "";
            string levelPrefix = IncludeLogLevels ? $"[{levelText}] " : "";
            string fullMessage = $"{timestamp}{levelPrefix}{message}";

            // Lock to prevent interleaved console output from multiple threads
            lock (_syncLock)
            {
                // Only change console colors for console output
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(fullMessage);
                Console.ForegroundColor = originalColor;

                // If file logging is enabled, write to file
                WriteToLogFile(fullMessage, level);
            }
        }

        /// <summary>
        /// Writes a message to the log file if file logging is enabled.
        /// </summary>
        /// <param name="message">The message to write</param>
        /// <param name="level">The log level of this message</param>
        private void WriteToLogFile(string message, LogLevel level)
        {
            if (!_enableFileLogging || _logFileWriter == null)
                return;

            try
            {
                _logFileWriter.WriteLine(message);

                // Flush immediately for critical/error logs or periodically for others
                if (level <= LogLevel.Error)
                {
                    _logFileWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                // Don't use LogError here to avoid potential infinite recursion
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error writing to log file: {ex.Message}");
                Console.ResetColor();

                // Disable file logging to prevent further errors
                _enableFileLogging = false;
            }
        }

        /// <summary>
        /// Initializes file logging by creating the log file and writer.
        /// </summary>
        private void InitializeFileLogging()
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath))
                    return;

                // Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create or append to log file
                var fileStream = new FileStream(
                    _logFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);

                _logFileWriter = new StreamWriter(fileStream, Encoding.UTF8)
                {
                    AutoFlush = false // We'll control flushing manually
                };

                // Add a separator and startup entry
                _logFileWriter.WriteLine();
                _logFileWriter.WriteLine($"=== Log started at {DateTime.Now.ToString(TimestampFormat)} ===");
                _logFileWriter.Flush();
            }
            catch (Exception ex)
            {
                // Don't use LogError here to avoid potential infinite recursion
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to initialize log file: {ex.Message}");
                Console.ResetColor();

                // Disable file logging to prevent further errors
                _enableFileLogging = false;
            }
        }

        /// <summary>
        /// Gets the default path for the log file.
        /// </summary>
        /// <returns>The default log file path</returns>
        private string GetDefaultLogFilePath()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logFolder = Path.Combine(appDataFolder, "GameServerManager", "Logs");

            string date = DateTime.Now.ToString("yyyyMMdd");
            string time = DateTime.Now.ToString("HHmmss");

            return Path.Combine(logFolder, $"server_{date}_{time}.log");
        }

        /// <summary>
        /// Flushes any buffered log messages to ensure they are written.
        /// </summary>
        public void Flush()
        {
            lock (_syncLock)
            {
                _logFileWriter?.Flush();
            }
        }

        /// <summary>
        /// Disposes resources used by the logger.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the logger.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing && _logFileWriter != null)
                {
                    lock (_syncLock)
                    {
                        try
                        {
                            _logFileWriter.WriteLine($"=== Log ended at {DateTime.Now.ToString(TimestampFormat)} ===");
                            _logFileWriter.Flush();
                            _logFileWriter.Dispose();
                        }
                        catch
                        {
                            // Ignore exceptions during disposal
                        }
                    }
                }

                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Defines the available log levels.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Debug information (most verbose).
        /// </summary>
        Debug = 0,

        /// <summary>
        /// Informational messages.
        /// </summary>
        Info = 1,

        /// <summary>
        /// Warning messages.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Error messages.
        /// </summary>
        Error = 3,

        /// <summary>
        /// Critical error messages.
        /// </summary>
        Critical = 4,

        /// <summary>
        /// No logging.
        /// </summary>
        None = 5
    }
}
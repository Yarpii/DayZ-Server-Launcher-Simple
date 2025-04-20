using System;

namespace GameServerManager.Core
{
    /// <summary>
    /// Interface for logging functionality across the application.
    /// </summary>
    public interface ILogger : IDisposable
    {
        /// <summary>
        /// Logs a debug message with detailed information for development purposes.
        /// </summary>
        /// <param name="message">The debug message to log</param>
        void LogDebug(string message);

        /// <summary>
        /// Logs an informational message with optional color formatting.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">The console color to use (for console loggers)</param>
        void LogInfo(string message, ConsoleColor color = ConsoleColor.White);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log</param>
        void LogError(string message);

        /// <summary>
        /// Logs a critical error message for severe issues that require immediate attention.
        /// </summary>
        /// <param name="message">The critical error message to log</param>
        void LogCritical(string message);

        /// <summary>
        /// Gets the total number of error messages logged.
        /// </summary>
        int ErrorCount { get; }

        /// <summary>
        /// Gets the total number of warning messages logged.
        /// </summary>
        int WarningCount { get; }

        /// <summary>
        /// Flushes any buffered log messages to ensure they are written.
        /// </summary>
        void Flush();
    }
}
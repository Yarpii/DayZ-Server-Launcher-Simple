using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServerManager.Core;
using GameServerManager.Server;

namespace GameServerManager.Scheduling
{
    /// <summary>
    /// Handles scheduled server restarts and maintenance operations.
    /// </summary>
    public class RestartScheduler : IDisposable
    {
        private readonly IServerManager _serverManager;
        private readonly ILogger _logger;
        private Timer? _checkTimer;
        private readonly List<ScheduledRestart> _scheduledRestarts = new();
        private readonly object _scheduleLock = new();
        private bool _isDisposed;
        private readonly TimeSpan _checkInterval;
        private CancellationTokenSource? _warningTaskCts;

        /// <summary>
        /// Gets the currently configured server restart schedule.
        /// </summary>
        public IReadOnlyList<ScheduledRestart> Schedule => _scheduledRestarts.AsReadOnly();

        /// <summary>
        /// Gets the time until the next scheduled restart.
        /// </summary>
        public TimeSpan? TimeUntilNextRestart
        {
            get
            {
                lock (_scheduleLock)
                {
                    if (_scheduledRestarts.Count == 0)
                        return null;

                    var nextRestart = _scheduledRestarts.OrderBy(r => r.ScheduledTime).First();
                    return nextRestart.ScheduledTime - DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Event triggered when a restart warning is issued.
        /// </summary>
        public event EventHandler<RestartWarningEventArgs>? RestartWarningIssued;

        /// <summary>
        /// Event triggered when a restart is initiated.
        /// </summary>
        public event EventHandler<RestartEventArgs>? RestartInitiated;

        /// <summary>
        /// Initializes a new instance of the RestartScheduler class.
        /// </summary>
        /// <param name="serverManager">The server manager to control</param>
        /// <param name="config">The server configuration</param>
        /// <param name="logger">The logger for output</param>
        /// <param name="checkIntervalSeconds">How often to check for scheduled restarts (in seconds)</param>
        public RestartScheduler(
            IServerManager serverManager,
            ServerConfig config,
            ILogger logger,
            int checkIntervalSeconds = 30)
        {
            _serverManager = serverManager ?? throw new ArgumentNullException(nameof(serverManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _checkInterval = TimeSpan.FromSeconds(Math.Max(5, checkIntervalSeconds)); // Minimum 5 seconds

            // Subscribe to server status events
            _serverManager.ServerStatusChanged += OnServerStatusChanged;

            // Initial configuration
            LoadScheduleFromConfig(config);
        }

        /// <summary>
        /// Initializes the restart scheduler and starts monitoring for scheduled restarts.
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            lock (_scheduleLock)
            {
                // Stop existing timer if running
                _checkTimer?.Dispose();

                // Log scheduled restarts
                LogSchedule();

                // Start a timer to check for scheduled restarts
                _checkTimer = new Timer(
                    CheckScheduledRestarts,
                    null,
                    TimeSpan.Zero,
                    _checkInterval);
            }
        }

        /// <summary>
        /// Updates the restart scheduler with a new configuration.
        /// </summary>
        /// <param name="config">The new server configuration</param>
        public void UpdateConfiguration(ServerConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            LoadScheduleFromConfig(config);

            // Restart the timer to apply new configuration
            Initialize();
        }

        /// <summary>
        /// Adds a one-time restart to the schedule.
        /// </summary>
        /// <param name="restartTime">The time when the server should restart</param>
        /// <param name="reason">Optional reason for the restart</param>
        /// <returns>The scheduled restart object</returns>
        public ScheduledRestart AddOneTimeRestart(DateTime restartTime, string? reason = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            if (restartTime <= DateTime.Now)
                throw new ArgumentException("Restart time must be in the future", nameof(restartTime));

            lock (_scheduleLock)
            {
                var restart = new ScheduledRestart(
                    restartTime,
                    isRecurring: false,
                    warningMinutes: 5,
                    reason: reason);

                _scheduledRestarts.Add(restart);
                _logger.LogInfo($"One-time restart scheduled for {restartTime:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Yellow);

                return restart;
            }
        }

        /// <summary>
        /// Adds a recurring restart to the schedule.
        /// </summary>
        /// <param name="timeOfDay">The time of day for the restart (HH:mm format)</param>
        /// <param name="warningMinutes">Minutes before restart to issue warnings</param>
        /// <param name="reason">Optional reason for the restart</param>
        /// <returns>The scheduled restart object, or null if the time format was invalid</returns>
        public ScheduledRestart? AddRecurringRestart(string timeOfDay, int warningMinutes = 5, string? reason = null)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            if (!TimeSpan.TryParse(timeOfDay, out TimeSpan restartTime))
            {
                _logger.LogError($"Invalid restart time format: {timeOfDay}");
                return null;
            }

            // Calculate next occurrence
            DateTime now = DateTime.Now;
            DateTime scheduledTime = now.Date.Add(restartTime);

            // If the time has already passed today, schedule for tomorrow
            if (scheduledTime < now)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            lock (_scheduleLock)
            {
                var restart = new ScheduledRestart(
                    scheduledTime,
                    isRecurring: true,
                    warningMinutes: warningMinutes,
                    reason: reason,
                    recurrencePattern: restartTime);

                _scheduledRestarts.Add(restart);
                _logger.LogInfo($"Daily restart scheduled for {timeOfDay}, next at {scheduledTime:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Cyan);

                return restart;
            }
        }

        /// <summary>
        /// Removes a scheduled restart.
        /// </summary>
        /// <param name="restart">The scheduled restart to remove</param>
        /// <returns>True if the restart was found and removed, false otherwise</returns>
        public bool RemoveScheduledRestart(ScheduledRestart restart)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            lock (_scheduleLock)
            {
                bool removed = _scheduledRestarts.Remove(restart);
                if (removed)
                {
                    _logger.LogInfo($"Scheduled restart removed: {restart.ScheduledTime:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Yellow);
                }
                return removed;
            }
        }

        /// <summary>
        /// Removes all scheduled restarts.
        /// </summary>
        public void ClearSchedule()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            lock (_scheduleLock)
            {
                _scheduledRestarts.Clear();
                _logger.LogInfo("Restart schedule cleared", ConsoleColor.Yellow);
            }
        }

        /// <summary>
        /// Immediately initiates a server restart.
        /// </summary>
        /// <param name="reason">Optional reason for the restart</param>
        /// <param name="delaySeconds">Optional delay before restart (seconds)</param>
        public async Task RestartNowAsync(string? reason = null, int delaySeconds = 0)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RestartScheduler));

            _logger.LogWarning($"Manual restart initiated{(reason != null ? $": {reason}" : "")}");

            // Issue warning if there's a delay
            if (delaySeconds > 0)
            {
                _logger.LogWarning($"Server will restart in {delaySeconds} seconds");

                // Cancel any existing warning task
                _warningTaskCts?.Cancel();
                _warningTaskCts = new CancellationTokenSource();

                try
                {
                    // Send initial warning
                    OnRestartWarningIssued(delaySeconds, reason);

                    // Wait for the specified delay
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _warningTaskCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInfo("Restart warning cancelled", ConsoleColor.Yellow);
                    return;
                }
                finally
                {
                    _warningTaskCts = null;
                }
            }

            // Trigger restart event
            OnRestartInitiated(reason);

            // Stop the server
            _serverManager.StopServer();

            _logger.LogInfo("Restart command sent to server", ConsoleColor.Magenta);
        }

        /// <summary>
        /// Loads the restart schedule from a configuration object.
        /// </summary>
        /// <param name="config">The server configuration</param>
        private void LoadScheduleFromConfig(ServerConfig config)
        {
            lock (_scheduleLock)
            {
                // Clear existing schedule
                _scheduledRestarts.Clear();

                // Add scheduled restarts from config
                foreach (string timeStr in config.ScheduledRestartTimes)
                {
                    AddRecurringRestart(timeStr, config.RestartWarningMinutes, "Scheduled maintenance");
                }
            }
        }

        /// <summary>
        /// Logs the current restart schedule.
        /// </summary>
        private void LogSchedule()
        {
            lock (_scheduleLock)
            {
                if (_scheduledRestarts.Count == 0)
                {
                    _logger.LogInfo("No scheduled restarts configured", ConsoleColor.Yellow);
                    return;
                }

                _logger.LogInfo($"Scheduler configured with {_scheduledRestarts.Count} restart(s):", ConsoleColor.White);

                foreach (var restart in _scheduledRestarts.OrderBy(r => r.ScheduledTime))
                {
                    string recurringInfo = restart.IsRecurring ? "recurring" : "one-time";
                    string warningInfo = $"warning at {restart.WarningMinutes} mins";
                    string reasonInfo = !string.IsNullOrEmpty(restart.Reason) ? $"({restart.Reason})" : "";

                    _logger.LogInfo($"  • {restart.ScheduledTime:yyyy-MM-dd HH:mm:ss} - {recurringInfo}, {warningInfo} {reasonInfo}",
                        restart.IsRecurring ? ConsoleColor.Cyan : ConsoleColor.Yellow);
                }
            }
        }

        /// <summary>
        /// Checks if a scheduled restart is due and performs actions accordingly.
        /// </summary>
        /// <param name="state">State object (not used)</param>
        private void CheckScheduledRestarts(object? state)
        {
            if (_isDisposed)
                return;

            try
            {
                DateTime now = DateTime.Now;
                List<ScheduledRestart> restartsToProcess;

                // Create a safe copy of the list to avoid thread issues
                lock (_scheduleLock)
                {
                    restartsToProcess = _scheduledRestarts.ToList();
                }

                foreach (var restart in restartsToProcess)
                {
                    // Check if we're in the warning period
                    TimeSpan timeUntilRestart = restart.ScheduledTime - now;

                    if (timeUntilRestart.TotalMinutes <= restart.WarningMinutes &&
                        timeUntilRestart.TotalMinutes > 0 &&
                        !restart.WarningsIssued)
                    {
                        int minutesRemaining = (int)Math.Ceiling(timeUntilRestart.TotalMinutes);
                        _logger.LogWarning($"⚠️ Server will restart in {minutesRemaining} minutes" +
                            (!string.IsNullOrEmpty(restart.Reason) ? $" - {restart.Reason}" : ""));

                        // Mark warning as issued
                        restart.WarningsIssued = true;

                        // Raise warning event
                        OnRestartWarningIssued(minutesRemaining * 60, restart.Reason);
                    }

                    // Check if it's time for a restart
                    if (now >= restart.ScheduledTime && !restart.IsProcessed)
                    {
                        _logger.LogInfo("⏰ Scheduled restart initiated..." +
                            (!string.IsNullOrEmpty(restart.Reason) ? $" ({restart.Reason})" : ""),
                            ConsoleColor.Magenta);

                        // Mark as processed
                        restart.IsProcessed = true;

                        // Trigger event
                        OnRestartInitiated(restart.Reason);

                        // Stop the server - it will automatically restart
                        _serverManager.StopServer();

                        // Process rest of the schedule after the server restarts
                        break;
                    }
                }

                // Clean up processed restarts and schedule recurring ones for the next day
                CleanupAndReschedule();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in restart scheduler: {ex.Message}");
                _logger.LogError(ex.StackTrace ?? "No stack trace available");
            }
        }

        /// <summary>
        /// Cleans up processed restarts and reschedules recurring ones.
        /// </summary>
        private void CleanupAndReschedule()
        {
            lock (_scheduleLock)
            {
                // Process each restart entry
                for (int i = _scheduledRestarts.Count - 1; i >= 0; i--)
                {
                    var restart = _scheduledRestarts[i];

                    // If processed, handle based on whether it's recurring
                    if (restart.IsProcessed)
                    {
                        if (restart.IsRecurring && restart.RecurrencePattern.HasValue)
                        {
                            // Reschedule for tomorrow
                            var nextOccurrence = DateTime.Now.Date.AddDays(1).Add(restart.RecurrencePattern.Value);

                            _scheduledRestarts[i] = new ScheduledRestart(
                                nextOccurrence,
                                true,
                                restart.WarningMinutes,
                                restart.Reason,
                                restart.RecurrencePattern);

                            _logger.LogInfo($"Recurring restart rescheduled for {nextOccurrence:yyyy-MM-dd HH:mm:ss}",
                                ConsoleColor.Cyan);
                        }
                        else
                        {
                            // Remove one-time restarts
                            _scheduledRestarts.RemoveAt(i);
                        }
                    }
                    // Reset warning flag for restarts that are more than warning period away
                    else if (restart.WarningsIssued &&
                             (restart.ScheduledTime - DateTime.Now).TotalMinutes > restart.WarningMinutes + 1)
                    {
                        restart.WarningsIssued = false;
                    }
                }
            }
        }

        /// <summary>
        /// Handles server status change events.
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The status event arguments</param>
        private void OnServerStatusChanged(object? sender, ServerStatusEventArgs e)
        {
            // If the server is starting, ensure we have a clean schedule state
            if (e.Status == ServerStatus.Starting)
            {
                CleanupAndReschedule();
            }
        }

        /// <summary>
        /// Raises the RestartWarningIssued event.
        /// </summary>
        /// <param name="secondsRemaining">Seconds remaining until restart</param>
        /// <param name="reason">Optional reason for the restart</param>
        private void OnRestartWarningIssued(int secondsRemaining, string? reason)
        {
            RestartWarningIssued?.Invoke(this, new RestartWarningEventArgs(
                TimeSpan.FromSeconds(secondsRemaining),
                reason));
        }

        /// <summary>
        /// Raises the RestartInitiated event.
        /// </summary>
        /// <param name="reason">Optional reason for the restart</param>
        private void OnRestartInitiated(string? reason)
        {
            RestartInitiated?.Invoke(this, new RestartEventArgs(reason));
        }

        /// <summary>
        /// Disposes resources used by the restart scheduler.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the restart scheduler.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Cancel any active warning task
                    _warningTaskCts?.Cancel();
                    _warningTaskCts?.Dispose();

                    // Dispose timer
                    _checkTimer?.Dispose();

                    // Unsubscribe from events
                    if (_serverManager != null)
                    {
                        _serverManager.ServerStatusChanged -= OnServerStatusChanged;
                    }
                }

                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a scheduled server restart.
    /// </summary>
    public class ScheduledRestart
    {
        /// <summary>
        /// Gets the scheduled restart time.
        /// </summary>
        public DateTime ScheduledTime { get; }

        /// <summary>
        /// Gets whether this is a recurring restart.
        /// </summary>
        public bool IsRecurring { get; }

        /// <summary>
        /// Gets the number of minutes before the restart when warnings should be issued.
        /// </summary>
        public int WarningMinutes { get; }

        /// <summary>
        /// Gets the optional reason for the restart.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Gets the recurrence pattern for recurring restarts (time of day).
        /// </summary>
        public TimeSpan? RecurrencePattern { get; }

        /// <summary>
        /// Gets or sets whether warnings have been issued for this restart.
        /// </summary>
        internal bool WarningsIssued { get; set; }

        /// <summary>
        /// Gets or sets whether this restart has been processed.
        /// </summary>
        internal bool IsProcessed { get; set; }

        /// <summary>
        /// Initializes a new instance of the ScheduledRestart class.
        /// </summary>
        /// <param name="scheduledTime">The time when the restart should occur</param>
        /// <param name="isRecurring">Whether this is a recurring restart</param>
        /// <param name="warningMinutes">Minutes before restart to issue warnings</param>
        /// <param name="reason">Optional reason for the restart</param>
        /// <param name="recurrencePattern">Recurrence pattern for recurring restarts</param>
        public ScheduledRestart(
            DateTime scheduledTime,
            bool isRecurring,
            int warningMinutes,
            string? reason = null,
            TimeSpan? recurrencePattern = null)
        {
            ScheduledTime = scheduledTime;
            IsRecurring = isRecurring;
            WarningMinutes = Math.Max(0, warningMinutes);
            Reason = reason;
            RecurrencePattern = recurrencePattern;
            WarningsIssued = false;
            IsProcessed = false;
        }
    }

    /// <summary>
    /// Event arguments for restart warnings.
    /// </summary>
    public class RestartWarningEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the time remaining until the restart.
        /// </summary>
        public TimeSpan TimeRemaining { get; }

        /// <summary>
        /// Gets the optional reason for the restart.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of the RestartWarningEventArgs class.
        /// </summary>
        /// <param name="timeRemaining">Time remaining until the restart</param>
        /// <param name="reason">Optional reason for the restart</param>
        public RestartWarningEventArgs(TimeSpan timeRemaining, string? reason = null)
        {
            TimeRemaining = timeRemaining;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for restart events.
    /// </summary>
    public class RestartEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the optional reason for the restart.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of the RestartEventArgs class.
        /// </summary>
        /// <param name="reason">Optional reason for the restart</param>
        public RestartEventArgs(string? reason = null)
        {
            Reason = reason;
        }
    }
}
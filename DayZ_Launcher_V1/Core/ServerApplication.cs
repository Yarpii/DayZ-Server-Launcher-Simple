using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using GameServerManager.Server;
using GameServerManager.Scheduling;
using GameServerManager.Core;

namespace GameServerManager.Core
{
    /// <summary>
    /// Main application class that coordinates server management functionality.
    /// </summary>
    public class ServerApplication : IDisposable
    {
        private readonly IConfigManager _configManager;
        private readonly IServerManager _serverManager;
        private readonly ILogger _logger;
        private readonly RestartScheduler _restartScheduler;
        private readonly ServerConfig _config;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;

        /// <summary>
        /// Gets the current application version.
        /// </summary>
        public static string Version => "1.1.0";

        /// <summary>
        /// Initializes a new instance of the ServerApplication with explicit dependencies.
        /// </summary>
        /// <param name="configManager">The configuration manager to use</param>
        /// <param name="logger">The logger to use</param>
        /// <param name="configPath">Path to the configuration file</param>
        /// <exception cref="ArgumentNullException">Thrown if required dependencies are null</exception>
        /// <exception cref="ApplicationException">Thrown if configuration loading fails</exception>
        public ServerApplication(IConfigManager configManager, ILogger logger, string configPath)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration
            _config = _configManager.LoadConfig(configPath) ??
                throw new ApplicationException($"Failed to load configuration from {configPath}");

            // Create server manager
            _serverManager = CreateServerManager();

            // Setup restart scheduler
            _restartScheduler = new RestartScheduler(_serverManager, _config, _logger);

            // Initialize cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Subscribe to server status events
            _serverManager.ServerStatusChanged += OnServerStatusChanged;
        }

        /// <summary>
        /// Initializes a new instance of the ServerApplication with default implementations.
        /// </summary>
        /// <param name="configPath">Path to the configuration file</param>
        public ServerApplication(string configPath)
            : this(new JsonConfigManager(new ConsoleLogger()), new ConsoleLogger(), configPath)
        {
        }

        /// <summary>
        /// Factory method to create appropriate server manager based on configuration.
        /// </summary>
        /// <returns>An implementation of IServerManager</returns>
        private IServerManager CreateServerManager()
        {
            // In the future, this could select different server implementations
            // based on config or environment (e.g., different game types)
            return new DayZServerManager(_config, _logger);
        }

        /// <summary>
        /// Runs the server application asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task RunAsync()
        {
            try
            {
                DisplayHeader();

                // Verify requirements
                if (!_serverManager.VerifyRequirements())
                {
                    _logger.LogError("Failed to verify server requirements. Exiting.");
                    return;
                }

                // Initialize restart scheduler
                _restartScheduler.Initialize();

                // Run the server loop in a separate thread
                await Task.Run(() => _serverManager.RunServerLoop(), _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Server operation was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unhandled exception: {ex.Message}");
                _logger.LogError(ex.StackTrace ?? "No stack trace available");

                // Optionally, you could add telemetry/error reporting here
                // ReportError(ex);
            }
        }

        /// <summary>
        /// Runs the server application synchronously.
        /// </summary>
        public void Run()
        {
            RunAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Initiates a graceful shutdown of the server application.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for shutdown in milliseconds</param>
        /// <returns>True if shutdown was successful, false if it timed out</returns>
        public bool Shutdown(int timeout = 30000)
        {
            _logger.LogWarning("Shutdown requested. Stopping server...");

            try
            {
                // Signal cancellation to any ongoing tasks
                _cancellationTokenSource.Cancel();

                // Stop the server
                _serverManager.StopServer();

                // Dispose the restart scheduler
                _restartScheduler.Dispose();

                _logger.LogInfo("Server shutdown complete.", ConsoleColor.Green);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during shutdown: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles server status change events.
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The event arguments</param>
        private void OnServerStatusChanged(object? sender, ServerStatusEventArgs e)
        {
            // Log all status changes with appropriate color
            ConsoleColor color = e.Status switch
            {
                ServerStatus.Running => ConsoleColor.Green,
                ServerStatus.Stopped => ConsoleColor.Yellow,
                ServerStatus.Crashed => ConsoleColor.Red,
                ServerStatus.Restarting => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };

            string statusMessage = $"Server status changed to {e.Status}";
            if (!string.IsNullOrEmpty(e.Message))
                statusMessage += $" - {e.Message}";

            _logger.LogInfo(statusMessage, color);

            // Here you could add additional event handling:
            // - Send notifications (Discord, email, etc.)
            // - Update a status dashboard
            // - Record uptime statistics
        }

        /// <summary>
        /// Displays the application header and configuration details.
        /// </summary>
        private void DisplayHeader()
        {
            // Display application header with version
            _logger.LogInfo("╔══════════════════════════════════════════════════╗", ConsoleColor.White);
            _logger.LogInfo($"║     DayZ Dedicated Server Manager v{Version}     ║", ConsoleColor.White);
            _logger.LogInfo("╚══════════════════════════════════════════════════╝", ConsoleColor.White);

            // Display current date and time
            _logger.LogInfo($"Started on {DateTime.Now:yyyy-MM-dd} at {DateTime.Now:HH:mm:ss}", ConsoleColor.Gray);

            // Display server configuration details
            _serverManager.DisplayConfiguration();
        }

        /// <summary>
        /// Disposes resources used by the application.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the application.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _cancellationTokenSource.Dispose();

                    // Unsubscribe from events
                    if (_serverManager != null)
                    {
                        _serverManager.ServerStatusChanged -= OnServerStatusChanged;
                    }

                    // Dispose the restart scheduler if it implements IDisposable
                    (_restartScheduler as IDisposable)?.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}
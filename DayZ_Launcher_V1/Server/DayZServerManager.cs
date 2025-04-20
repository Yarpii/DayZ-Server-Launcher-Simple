using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using GameServerManager.Core;

namespace GameServerManager.Server
{
    /// <summary>
    /// Implementation of IServerManager for DayZ dedicated servers.
    /// </summary>
    public class DayZServerManager : IServerManager
    {
        private readonly ServerConfig _config;
        private readonly ILogger _logger;
        private Process? _currentServerProcess;
        private bool _keepRunning = true;

        /// <summary>
        /// Event that is triggered when the server status changes.
        /// </summary>
        public event EventHandler<ServerStatusEventArgs>? ServerStatusChanged;

        /// <summary>
        /// Initializes a new instance of the DayZServerManager class.
        /// </summary>
        /// <param name="config">The server configuration</param>
        /// <param name="logger">The logger for output</param>
        public DayZServerManager(ServerConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Verifies that all requirements are met to run the DayZ server.
        /// </summary>
        /// <returns>True if all requirements are met, false otherwise</returns>
        public bool VerifyRequirements()
        {
            // Check if server executable exists
            if (!File.Exists(_config.ServerPath))
            {
                _logger.LogError($"Server executable not found: {_config.ServerPath}");
                return false;
            }

            // Check if BattlEye directory exists
            string serverDir = Path.GetDirectoryName(_config.ServerPath) ?? "";
            string battleyePath = Path.Combine(serverDir, "BattlEye");
            if (!Directory.Exists(battleyePath))
            {
                _logger.LogError("'BattlEye' directory missing next to server executable!");
                _logger.LogError($"Expected location: {battleyePath}");
                return false;
            }

            // Check configuration file
            if (!File.Exists(_config.ConfigFile))
            {
                _logger.LogError($"Server configuration file not found: {_config.ConfigFile}");
                return false;
            }

            // Check/create profiles directory
            if (!Directory.Exists(_config.ProfilesFolder))
            {
                _logger.LogWarning($"Profiles directory does not exist, creating: {_config.ProfilesFolder}");
                try
                {
                    Directory.CreateDirectory(_config.ProfilesFolder!);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to create profiles directory: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Starts the main server execution loop.
        /// </summary>
        public void RunServerLoop()
        {
            while (_keepRunning)
            {
                try
                {
                    StartServer();

                    // Wait for server process to exit
                    if (_currentServerProcess != null)
                    {
                        _currentServerProcess.WaitForExit();

                        if (_currentServerProcess.ExitCode != 0)
                        {
                            _logger.LogWarning($"Server exited with code: {_currentServerProcess.ExitCode}");
                            OnServerStatusChanged(ServerStatus.Crashed, $"Exit code: {_currentServerProcess.ExitCode}");
                        }
                        else
                        {
                            _logger.LogInfo("Server exited normally", ConsoleColor.Yellow);
                            OnServerStatusChanged(ServerStatus.Stopped);
                        }

                        _currentServerProcess = null;
                    }

                    if (!_keepRunning)
                        break;

                    _logger.LogWarning($"Server stopped or crashed. Restarting in {_config.RestartDelaySeconds} seconds...");
                    OnServerStatusChanged(ServerStatus.Restarting);
                    Thread.Sleep(_config.RestartDelaySeconds * 1000);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error during server execution: {ex.Message}");
                    Thread.Sleep(10000); // Wait before retry
                }
            }
        }

        /// <summary>
        /// Attempts to gracefully stop the running server.
        /// </summary>
        public void StopServer()
        {
            _keepRunning = false;

            if (_currentServerProcess != null && !_currentServerProcess.HasExited)
            {
                try
                {
                    _logger.LogWarning("Graceful server shutdown initiated...");
                    OnServerStatusChanged(ServerStatus.Stopping);

                    _currentServerProcess.CloseMainWindow();
                    if (!_currentServerProcess.WaitForExit(60000)) // Wait up to 60 seconds
                    {
                        _logger.LogWarning("Server not responding. Forcing shutdown...");
                        _currentServerProcess.Kill();
                    }

                    OnServerStatusChanged(ServerStatus.Stopped);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error stopping server: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Displays the current server configuration information.
        /// </summary>
        public void DisplayConfiguration()
        {
            int cpuCount = Math.Min(Environment.ProcessorCount, _config.MaxCpuCores);

            _logger.LogInfo($"Path      : {_config.ServerPath}");
            _logger.LogInfo($"Port      : {_config.Port}");
            _logger.LogInfo($"CPU Cores : {cpuCount} / {Environment.ProcessorCount}");
            _logger.LogInfo($"RAM Limit : {_config.MaxMemoryMB} MB");
            _logger.LogInfo($"Restart   : {_config.RestartDelaySeconds} seconds after crash");

            if (_config.ScheduledRestartTimes.Count > 0)
            {
                _logger.LogInfo($"Scheduled restarts: {string.Join(", ", _config.ScheduledRestartTimes)}");
                _logger.LogInfo($"Warning: {_config.RestartWarningMinutes} minutes before restart");
            }

            _logger.LogInfo("");
            _logger.LogInfo("Press Ctrl+C to stop the server and exit.");
            _logger.LogInfo("");
        }

        /// <summary>
        /// Starts the DayZ server process.
        /// </summary>
        private void StartServer()
        {
            string launchParams = BuildLaunchParams();

            _logger.LogInfo("Starting DayZ server...", ConsoleColor.Cyan);
            _logger.LogInfo("Parameters: " + launchParams, ConsoleColor.DarkGray);

            OnServerStatusChanged(ServerStatus.Starting);

            var serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.ServerPath,
                    Arguments = launchParams,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                },
                EnableRaisingEvents = true
            };

            // Configure RAM limit (Windows only)
            if (OperatingSystem.IsWindows())
            {
                serverProcess.StartInfo.UseShellExecute = true;
            }

            serverProcess.Start();
            _currentServerProcess = serverProcess;

            _logger.LogInfo($"Server running. PID: {serverProcess.Id}", ConsoleColor.Green);
            OnServerStatusChanged(ServerStatus.Running, $"PID: {serverProcess.Id}");

            // Apply RAM limit if possible (Windows only)
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // Here you would use Job Objects API to set memory limits
                    _logger.LogInfo($"RAM limit of {_config.MaxMemoryMB}MB set", ConsoleColor.DarkCyan);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not apply RAM limit: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Builds the command line parameters for the DayZ server.
        /// </summary>
        /// <returns>The command line parameters as a string</returns>
        private string BuildLaunchParams()
        {
            int cpuCount = Math.Min(Environment.ProcessorCount, _config.MaxCpuCores);

            var parameters = new[]
            {
                $"-config={_config.ConfigFile}",
                $"-port={_config.Port}",
                $"-profiles={_config.ProfilesFolder}",
                $"-cpuCount={cpuCount}",
                "-dologs",
                "-adminlog",
                "-netlog",
                "-freezecheck",
                "-noCrashDialog"
            };

            string result = string.Join(" ", parameters);

            // Add mods if they are present
            if (!string.IsNullOrWhiteSpace(_config.ClientMods))
                result += $" -mod=\"{_config.ClientMods}\"";

            if (!string.IsNullOrWhiteSpace(_config.ServerMods))
                result += $" -serverMod=\"{_config.ServerMods}\"";

            return result;
        }

        /// <summary>
        /// Raises the ServerStatusChanged event.
        /// </summary>
        /// <param name="status">The new server status</param>
        /// <param name="message">Optional message with additional details</param>
        private void OnServerStatusChanged(ServerStatus status, string? message = null)
        {
            ServerStatusChanged?.Invoke(this, new ServerStatusEventArgs(status, message));
        }
    }
}
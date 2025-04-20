using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;

namespace GameServerManager.Core
{
    /// <summary>
    /// Configuration model for game server settings.
    /// </summary>
    public class ServerConfig
    {
        /// <summary>
        /// Gets or sets the path to the server executable.
        /// </summary>
        [Required(ErrorMessage = "Server executable path is required")]
        public string? ServerPath { get; set; }

        /// <summary>
        /// Gets or sets the network port the server will use.
        /// </summary>
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        public int Port { get; set; } = 2302;

        /// <summary>
        /// Gets or sets the path to the folder containing server profiles.
        /// </summary>
        [Required(ErrorMessage = "Profiles folder path is required")]
        public string? ProfilesFolder { get; set; }

        /// <summary>
        /// Gets or sets the path to the server configuration file.
        /// </summary>
        [Required(ErrorMessage = "Server configuration file path is required")]
        public string? ConfigFile { get; set; }

        /// <summary>
        /// Gets or sets the list of client mods.
        /// </summary>
        [JsonProperty("ClientMods")]
        private string? _clientModsString { get; set; }

        /// <summary>
        /// Gets or sets the list of server mods.
        /// </summary>
        [JsonProperty("ServerMods")]
        private string? _serverModsString { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of CPU cores the server can use.
        /// </summary>
        [Range(1, 128, ErrorMessage = "MaxCpuCores must be between 1 and 128")]
        public int MaxCpuCores { get; set; } = 4;

        /// <summary>
        /// Gets or sets the maximum memory usage in megabytes.
        /// </summary>
        [Range(1024, 65536, ErrorMessage = "MaxMemoryMB must be between 1024 and 65536")]
        public int MaxMemoryMB { get; set; } = 8192;

        /// <summary>
        /// Gets or sets the number of seconds to wait before restarting after a crash.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "RestartDelaySeconds must be between 1 and 3600")]
        public int RestartDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the list of times when the server should restart (in "HH:mm" format).
        /// </summary>
        public List<string> ScheduledRestartTimes { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the minutes before a scheduled restart to issue warnings.
        /// </summary>
        [Range(0, 120, ErrorMessage = "RestartWarningMinutes must be between 0 and 120")]
        public int RestartWarningMinutes { get; set; } = 5;

        /// <summary>
        /// Gets or sets the server name displayed in the server browser.
        /// </summary>
        [StringLength(64, ErrorMessage = "ServerName cannot exceed 64 characters")]
        public string? ServerName { get; set; } = "DayZ Dedicated Server";

        /// <summary>
        /// Gets or sets the server password (if any).
        /// </summary>
        public string? ServerPassword { get; set; }

        /// <summary>
        /// Gets or sets the admin password for remote administration.
        /// </summary>
        public string? AdminPassword { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of players allowed on the server.
        /// </summary>
        [Range(1, 128, ErrorMessage = "MaxPlayers must be between 1 and 128")]
        public int MaxPlayers { get; set; } = 60;

        /// <summary>
        /// Gets or sets whether the server should automatically update before starting.
        /// </summary>
        public bool AutoUpdate { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable server performance metrics logging.
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable verbose logging.
        /// </summary>
        public bool VerboseLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets custom command line parameters to append to the server startup.
        /// </summary>
        public string? CustomParameters { get; set; }

        /// <summary>
        /// Gets or sets priority for the server process.
        /// </summary>
        public ProcessPriority ProcessPriority { get; set; } = ProcessPriority.Normal;

        /// <summary>
        /// Gets the client mods as a list.
        /// </summary>
        [JsonIgnore]
        public List<string> ClientModsList
        {
            get => ParseModsList(_clientModsString);
            set => _clientModsString = string.Join(";", value);
        }

        /// <summary>
        /// Gets the server mods as a list.
        /// </summary>
        [JsonIgnore]
        public List<string> ServerModsList
        {
            get => ParseModsList(_serverModsString);
            set => _serverModsString = string.Join(";", value);
        }

        /// <summary>
        /// Gets the client mods as a semicolon-separated string.
        /// </summary>
        [JsonIgnore]
        public string ClientMods
        {
            get => _clientModsString ?? "";
            set => _clientModsString = value;
        }

        /// <summary>
        /// Gets the server mods as a semicolon-separated string.
        /// </summary>
        [JsonIgnore]
        public string ServerMods
        {
            get => _serverModsString ?? "";
            set => _serverModsString = value;
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        /// <returns>A list of validation errors, or an empty list if validation passed</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // Basic validation
            if (string.IsNullOrEmpty(ServerPath))
            {
                errors.Add("ServerPath cannot be empty");
            }

            if (Port <= 0 || Port > 65535)
            {
                errors.Add($"Port must be between 1 and 65535, got: {Port}");
            }

            if (string.IsNullOrEmpty(ProfilesFolder))
            {
                errors.Add("ProfilesFolder cannot be empty");
            }

            if (string.IsNullOrEmpty(ConfigFile))
            {
                errors.Add("ConfigFile cannot be empty");
            }

            if (MaxCpuCores <= 0)
            {
                errors.Add($"MaxCpuCores must be positive, got: {MaxCpuCores}");
            }

            if (MaxMemoryMB < 1024)
            {
                errors.Add($"MaxMemoryMB should be at least 1024 MB, got: {MaxMemoryMB}");
            }

            if (RestartDelaySeconds < 0)
            {
                errors.Add($"RestartDelaySeconds cannot be negative, got: {RestartDelaySeconds}");
            }

            if (RestartWarningMinutes < 0)
            {
                errors.Add($"RestartWarningMinutes cannot be negative, got: {RestartWarningMinutes}");
            }

            if (MaxPlayers < 1 || MaxPlayers > 128)
            {
                errors.Add($"MaxPlayers must be between 1 and 128, got: {MaxPlayers}");
            }

            // Validate restart times
            if (ScheduledRestartTimes != null)
            {
                foreach (string time in ScheduledRestartTimes)
                {
                    if (!TimeSpan.TryParse(time, out _))
                    {
                        errors.Add($"Invalid time format in ScheduledRestartTimes: {time}");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Creates a deep clone of the configuration.
        /// </summary>
        /// <returns>A new instance with the same values</returns>
        public ServerConfig Clone()
        {
            return new ServerConfig
            {
                ServerPath = ServerPath,
                Port = Port,
                ProfilesFolder = ProfilesFolder,
                ConfigFile = ConfigFile,
                _clientModsString = _clientModsString,
                _serverModsString = _serverModsString,
                MaxCpuCores = MaxCpuCores,
                MaxMemoryMB = MaxMemoryMB,
                RestartDelaySeconds = RestartDelaySeconds,
                ScheduledRestartTimes = new List<string>(ScheduledRestartTimes),
                RestartWarningMinutes = RestartWarningMinutes,
                ServerName = ServerName,
                ServerPassword = ServerPassword,
                AdminPassword = AdminPassword,
                MaxPlayers = MaxPlayers,
                AutoUpdate = AutoUpdate,
                EnablePerformanceMonitoring = EnablePerformanceMonitoring,
                VerboseLogging = VerboseLogging,
                CustomParameters = CustomParameters,
                ProcessPriority = ProcessPriority
            };
        }

        /// <summary>
        /// Parses a semicolon-separated list of mods into a list of strings.
        /// </summary>
        /// <param name="modsString">The semicolon-separated mod string</param>
        /// <returns>A list of mod paths</returns>
        private static List<string> ParseModsList(string? modsString)
        {
            if (string.IsNullOrWhiteSpace(modsString))
            {
                return new List<string>();
            }

            return modsString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(mod => mod.Trim())
                .Where(mod => !string.IsNullOrWhiteSpace(mod))
                .ToList();
        }
    }

    /// <summary>
    /// Defines process priority levels for the server.
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Low process priority.
        /// </summary>
        Low,

        /// <summary>
        /// Below normal process priority.
        /// </summary>
        BelowNormal,

        /// <summary>
        /// Normal process priority.
        /// </summary>
        Normal,

        /// <summary>
        /// Above normal process priority.
        /// </summary>
        AboveNormal,

        /// <summary>
        /// High process priority.
        /// </summary>
        High
    }
}
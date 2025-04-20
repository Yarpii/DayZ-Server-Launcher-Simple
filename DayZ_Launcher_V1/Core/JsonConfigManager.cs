using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using GameServerManager.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;

namespace GameServerManager.Core
{
    /// <summary>
    /// Implementation of IConfigManager that loads and saves configuration from JSON files
    /// with support for validation, backup, and async operations.
    /// </summary>
    public class JsonConfigManager : IConfigManager, IDisposable
    {
        private readonly ILogger _logger;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly string _backupDirectory;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the JsonConfigManager class.
        /// </summary>
        /// <param name="logger">The logger to use for error reporting</param>
        /// <param name="createBackups">Whether to create backups when saving configuration</param>
        /// <param name="backupDirectoryPath">Custom path for config backups, or null for default</param>
        public JsonConfigManager(ILogger logger, bool createBackups = true, string? backupDirectoryPath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure JSON serialization settings
            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None
            };

            // Initialize backup directory if needed
            if (createBackups)
            {
                _backupDirectory = backupDirectoryPath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GameServerManager", "ConfigBackups");

                EnsureBackupDirectoryExists();
            }
            else
            {
                _backupDirectory = string.Empty;
            }
        }

        /// <summary>
        /// Creates a new configuration with default values.
        /// </summary>
        /// <returns>A new ServerConfig instance with default values</returns>
        public ServerConfig CreateDefaultConfig()
        {
            try
            {
                _logger.LogInfo("Creating default configuration");

                return new ServerConfig
                {
                    ServerPath = "C:\\GameServers\\dayZ\\DayZServer\\DayZServer_x64.exe",
                    Port = 2302,
                    ProfilesFolder = "C:\\GameServers\\dayZ\\profile",
                    ConfigFile = "C:\\GameServers\\dayZ\\serverDZ.cfg",
                    ClientMods = "",
                    ServerMods = "",
                    MaxCpuCores = Math.Max(1, Environment.ProcessorCount - 1),
                    MaxMemoryMB = 8192,
                    RestartDelaySeconds = 10,
                    ScheduledRestartTimes = new List<string> { "03:00", "15:00" },
                    RestartWarningMinutes = 5,
                    ServerName = "DayZ Dedicated Server",
                    MaxPlayers = 60,
                    AutoUpdate = true,
                    EnablePerformanceMonitoring = true,
                    VerboseLogging = false,
                    ProcessPriority = ProcessPriority.Normal
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating default configuration: {ex.Message}");

                // Return a minimal valid configuration
                return new ServerConfig
                {
                    Port = 2302,
                    MaxCpuCores = 2,
                    MaxMemoryMB = 4096,
                    RestartDelaySeconds = 10,
                    RestartWarningMinutes = 5
                };
            }
        }

        /// <summary>
        /// Flushes any buffered configuration data to ensure it is written.
        /// </summary>
        public void Flush()
        {
            try
            {
                // If using file-based logging, ensure all logs are written
                _logger.LogDebug("Flushing configuration manager");

                // In this implementation, there's no buffering to flush,
                // but we implement the method to satisfy the interface
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error flushing configuration data: {ex.Message}");
            }
        }



        /// <summary>
        /// Default constructor that creates a console logger and enables backups.
        /// </summary>
        public JsonConfigManager() : this(new ConsoleLogger(), true)
        {
        }

        /// <summary>
        /// Loads server configuration from a JSON file.
        /// </summary>
        /// <param name="path">Path to the JSON configuration file</param>
        /// <returns>The loaded ServerConfig, or null if loading failed</returns>
        public ServerConfig? LoadConfig(string path)
        {
            try
            {
                // Validate path
                if (string.IsNullOrWhiteSpace(path))
                {
                    _logger.LogError("Configuration path cannot be null or empty");
                    return null;
                }

                // Check if file exists
                if (!File.Exists(path))
                {
                    _logger.LogError($"Configuration file not found: {path}");
                    return null;
                }

                // Read and parse file
                string configText = File.ReadAllText(path);
                ServerConfig? config = DeserializeConfig(configText);

                // Validate parsed config
                if (config == null)
                {
                    return null;
                }

                // Log success
                _logger.LogInfo($"Configuration loaded successfully from {path}", ConsoleColor.Green);
                return config;
            }
            catch (IOException ex)
            {
                _logger.LogError($"File error loading configuration: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parsing error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error loading configuration: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads server configuration from a JSON file asynchronously.
        /// </summary>
        /// <param name="path">Path to the JSON configuration file</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the loaded ServerConfig, or null if loading failed</returns>
        public async Task<ServerConfig?> LoadConfigAsync(string path)
        {
            try
            {
                // Validate path
                if (string.IsNullOrWhiteSpace(path))
                {
                    _logger.LogError("Configuration path cannot be null or empty");
                    return null;
                }

                // Check if file exists
                if (!File.Exists(path))
                {
                    _logger.LogError($"Configuration file not found: {path}");
                    return null;
                }

                // Read and parse file
                string configText = await File.ReadAllTextAsync(path);
                ServerConfig? config = DeserializeConfig(configText);

                // Validate parsed config
                if (config == null)
                {
                    return null;
                }

                // Log success
                _logger.LogInfo($"Configuration loaded successfully from {path}", ConsoleColor.Green);
                return config;
            }
            catch (IOException ex)
            {
                _logger.LogError($"File error loading configuration: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parsing error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error loading configuration: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves server configuration to a JSON file.
        /// </summary>
        /// <param name="config">The ServerConfig to save</param>
        /// <param name="path">Path where the configuration should be saved</param>
        /// <returns>True if saving was successful, false otherwise</returns>
        public bool SaveConfig(ServerConfig config, string path)
        {
            try
            {
                // Validate inputs
                if (config == null)
                {
                    _logger.LogError("Cannot save null configuration");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    _logger.LogError("Save path cannot be null or empty");
                    return false;
                }

                // Create backup if enabled and file exists
                if (!string.IsNullOrEmpty(_backupDirectory) && File.Exists(path))
                {
                    CreateBackup(path);
                }

                // Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize and save
                string json = JsonConvert.SerializeObject(config, _serializerSettings);
                File.WriteAllText(path, json);

                _logger.LogInfo($"Configuration saved successfully to {path}", ConsoleColor.Green);
                return true;
            }
            catch (IOException ex)
            {
                _logger.LogError($"File error saving configuration: {ex.Message}");
                return false;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON serialization error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error saving configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves server configuration to a JSON file asynchronously.
        /// </summary>
        /// <param name="config">The ServerConfig to save</param>
        /// <param name="path">Path where the configuration should be saved</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether saving was successful</returns>
        public async Task<bool> SaveConfigAsync(ServerConfig config, string path)
        {
            try
            {
                // Validate inputs
                if (config == null)
                {
                    _logger.LogError("Cannot save null configuration");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    _logger.LogError("Save path cannot be null or empty");
                    return false;
                }

                // Create backup if enabled and file exists
                if (!string.IsNullOrEmpty(_backupDirectory) && File.Exists(path))
                {
                    await CreateBackupAsync(path);
                }

                // Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize and save
                string json = JsonConvert.SerializeObject(config, _serializerSettings);
                await File.WriteAllTextAsync(path, json);

                _logger.LogInfo($"Configuration saved successfully to {path}", ConsoleColor.Green);
                return true;
            }
            catch (IOException ex)
            {
                _logger.LogError($"File error saving configuration: {ex.Message}");
                return false;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON serialization error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error saving configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates a server configuration against schema and business rules.
        /// </summary>
        /// <param name="config">The configuration to validate</param>
        /// <returns>A list of validation errors, or an empty list if validation passed</returns>
        public List<string> ValidateConfig(ServerConfig config)
        {
            var errors = new List<string>();

            if (config == null)
            {
                errors.Add("Configuration cannot be null");
                return errors;
            }

            // Basic validation
            if (string.IsNullOrEmpty(config.ServerPath))
            {
                errors.Add("ServerPath cannot be empty");
            }
            else if (!File.Exists(config.ServerPath))
            {
                errors.Add($"ServerPath does not exist: {config.ServerPath}");
            }

            if (string.IsNullOrEmpty(config.ConfigFile))
            {
                errors.Add("ConfigFile cannot be empty");
            }
            else if (!File.Exists(config.ConfigFile))
            {
                errors.Add($"ConfigFile does not exist: {config.ConfigFile}");
            }

            if (string.IsNullOrEmpty(config.ProfilesFolder))
            {
                errors.Add("ProfilesFolder cannot be empty");
            }

            if (config.Port <= 0 || config.Port > 65535)
            {
                errors.Add($"Port must be between 1 and 65535, got: {config.Port}");
            }

            if (config.MaxCpuCores <= 0)
            {
                errors.Add($"MaxCpuCores must be positive, got: {config.MaxCpuCores}");
            }

            if (config.MaxMemoryMB <= 0)
            {
                errors.Add($"MaxMemoryMB must be positive, got: {config.MaxMemoryMB}");
            }

            if (config.RestartDelaySeconds < 0)
            {
                errors.Add($"RestartDelaySeconds cannot be negative, got: {config.RestartDelaySeconds}");
            }

            if (config.RestartWarningMinutes < 0)
            {
                errors.Add($"RestartWarningMinutes cannot be negative, got: {config.RestartWarningMinutes}");
            }

            // Validate restart times
            if (config.ScheduledRestartTimes != null)
            {
                foreach (string time in config.ScheduledRestartTimes)
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
        /// Creates a backup of the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file to back up</param>
        private void CreateBackup(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                string fileName = Path.GetFileName(filePath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(_backupDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}");

                File.Copy(filePath, backupPath, true);
                _logger.LogInfo($"Backup created: {backupPath}", ConsoleColor.Gray);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to create backup: {ex.Message}");
                // Continue without backup - non-critical error
            }
        }

        /// <summary>
        /// Creates a backup of the specified file asynchronously.
        /// </summary>
        /// <param name="filePath">The path to the file to back up</param>
        private async Task CreateBackupAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                string fileName = Path.GetFileName(filePath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(_backupDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}");

                using (FileStream sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                using (FileStream destinationStream = new FileStream(backupPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }

                _logger.LogInfo($"Backup created: {backupPath}", ConsoleColor.Gray);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to create backup: {ex.Message}");
                // Continue without backup - non-critical error
            }
        }

        /// <summary>
        /// Ensures the backup directory exists.
        /// </summary>
        private void EnsureBackupDirectoryExists()
        {
            try
            {
                if (!string.IsNullOrEmpty(_backupDirectory) && !Directory.Exists(_backupDirectory))
                {
                    Directory.CreateDirectory(_backupDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to create backup directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Deserializes and validates a JSON string to a ServerConfig object.
        /// </summary>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>The deserialized ServerConfig, or null if deserialization failed</returns>
        private ServerConfig? DeserializeConfig(string json)
        {
            try
            {
                // First try to parse as JObject to catch JSON syntax errors
                JObject.Parse(json);

                // Deserialize to our config object
                var config = JsonConvert.DeserializeObject<ServerConfig>(json, _serializerSettings);

                if (config == null)
                {
                    _logger.LogError("Failed to deserialize configuration (null result)");
                    return null;
                }

                // Perform validation
                var validationErrors = ValidateConfig(config);
                if (validationErrors.Count > 0)
                {
                    _logger.LogError("Configuration validation failed:");
                    foreach (var error in validationErrors)
                    {
                        _logger.LogError($"- {error}");
                    }

                    // You could choose to return the config anyway and just log the errors
                    // Here we're returning null to indicate failure
                    return null;
                }

                return config;
            }
            catch (JsonReaderException ex)
            {
                _logger.LogError($"JSON syntax error: {ex.Message}");
                return null;
            }
            catch (JsonSerializationException ex)
            {
                _logger.LogError($"JSON serialization error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Disposes resources used by the config manager.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the config manager.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // No managed resources to dispose currently
                }

                _isDisposed = true;
            }
        }
    }
}
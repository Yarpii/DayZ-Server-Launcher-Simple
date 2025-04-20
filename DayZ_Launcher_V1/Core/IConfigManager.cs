using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameServerManager.Core;

namespace GameServerManager.Core
{
    /// <summary>
    /// Interface for loading, saving, and validating server configuration.
    /// </summary>
    public interface IConfigManager : IDisposable
    {
        /// <summary>
        /// Loads server configuration from the specified path.
        /// </summary>
        /// <param name="path">The path to the configuration file</param>
        /// <returns>The loaded server configuration, or null if loading failed</returns>
        ServerConfig? LoadConfig(string path);

        /// <summary>
        /// Loads server configuration asynchronously from the specified path.
        /// </summary>
        /// <param name="path">The path to the configuration file</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the loaded ServerConfig, or null if loading failed</returns>
        Task<ServerConfig?> LoadConfigAsync(string path);

        /// <summary>
        /// Saves server configuration to the specified path.
        /// </summary>
        /// <param name="config">The server configuration to save</param>
        /// <param name="path">The path where the configuration should be saved</param>
        /// <returns>True if the save was successful, false otherwise</returns>
        bool SaveConfig(ServerConfig config, string path);

        /// <summary>
        /// Saves server configuration asynchronously to the specified path.
        /// </summary>
        /// <param name="config">The server configuration to save</param>
        /// <param name="path">The path where the configuration should be saved</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the save was successful</returns>
        Task<bool> SaveConfigAsync(ServerConfig config, string path);

        /// <summary>
        /// Validates a server configuration against schema and business rules.
        /// </summary>
        /// <param name="config">The configuration to validate</param>
        /// <returns>A list of validation errors, or an empty list if validation passed</returns>
        List<string> ValidateConfig(ServerConfig config);

        /// <summary>
        /// Creates a new configuration with default values.
        /// </summary>
        /// <returns>A new ServerConfig instance with default values</returns>
        ServerConfig CreateDefaultConfig();

        /// <summary>
        /// Flushes any buffered configuration data to ensure it is written.
        /// </summary>
        void Flush();
    }
}
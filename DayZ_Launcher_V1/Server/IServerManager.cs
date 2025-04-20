using System;
using GameServerManager.Server;
using GameServerManager.Core;

namespace GameServerManager.Server
{
    /// <summary>
    /// Interface for managing game server lifecycle.
    /// </summary>
    public interface IServerManager
    {
        /// <summary>
        /// Verifies that all requirements are met to run the server.
        /// </summary>
        /// <returns>True if all requirements are met, false otherwise</returns>
        bool VerifyRequirements();

        /// <summary>
        /// Starts the main server execution loop.
        /// </summary>
        void RunServerLoop();

        /// <summary>
        /// Attempts to gracefully stop the running server.
        /// </summary>
        void StopServer();

        /// <summary>
        /// Displays the current server configuration information.
        /// </summary>
        void DisplayConfiguration();

        /// <summary>
        /// Event that is triggered when the server status changes.
        /// </summary>
        event EventHandler<ServerStatusEventArgs>? ServerStatusChanged;
    }
}
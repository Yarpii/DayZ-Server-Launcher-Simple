using GameServerManager.Server;
using System;

namespace GameServerManager.Server
{
    /// <summary>
    /// Event arguments for server status change events.
    /// </summary>
    public class ServerStatusEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the current status of the server.
        /// </summary>
        public ServerStatus Status { get; }

        /// <summary>
        /// Gets an optional message associated with the status change.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Gets the timestamp when the status change occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the ServerStatusEventArgs class.
        /// </summary>
        /// <param name="status">The new server status</param>
        /// <param name="message">Optional message with additional details</param>
        public ServerStatusEventArgs(ServerStatus status, string? message = null)
        {
            Status = status;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
}
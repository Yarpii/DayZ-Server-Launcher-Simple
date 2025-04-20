namespace GameServerManager.Server
{
    /// <summary>
    /// Enumeration of possible server states.
    /// </summary>
    public enum ServerStatus
    {
        /// <summary>
        /// Server is initializing and about to start.
        /// </summary>
        Starting,

        /// <summary>
        /// Server is currently running.
        /// </summary>
        Running,

        /// <summary>
        /// Server is in the process of shutting down.
        /// </summary>
        Stopping,

        /// <summary>
        /// Server has crashed or exited unexpectedly.
        /// </summary>
        Crashed,

        /// <summary>
        /// Server is preparing to restart.
        /// </summary>
        Restarting,

        /// <summary>
        /// Server has fully stopped and is not running.
        /// </summary>
        Stopped
    }
}
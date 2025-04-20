using System;
using GameServerManager.Core;

namespace GameServerManager
{
    class Program
    {
        static void Main(string[] args)
        {
            // Parse command line args (optional)
            string configPath = args.Length > 0 ? args[0] : "config.json";

            // Initialize the application
            var app = new ServerApplication(configPath);

            // Register graceful shutdown
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;
                app.Shutdown();
            };

            // Start the application
            app.Run();
        }
    }
}
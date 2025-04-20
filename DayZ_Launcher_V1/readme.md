GameServerManager/
│
├── Program.cs
│
├── Core/
│   ├── ServerApplication.cs
│   ├── ILogger.cs
│   ├── ConsoleLogger.cs
│   ├── IConfigManager.cs
│   ├── JsonConfigManager.cs
│   └── ServerConfig.cs
│
├── Server/
│   ├── IServerManager.cs
│   ├── ServerStatusEventArgs.cs
│   ├── ServerStatus.cs
│   └── DayZServerManager.cs
│
├── Scheduling/
│   └── RestartScheduler.cs
│
├── Properties/
│   └── launchSettings.json
│
└── config.json
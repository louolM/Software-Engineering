# EasySave

A lightweight console backup tool written in C# / .NET 10.  
EasySave lets users define up to 5 backup jobs, each copying files from a source directory to a target directory using either a Full or Differential strategy. Every run is logged to a daily JSON file and its live progress is written to a state file that external monitors can poll.

## Features

- Create, list, run, and delete backup jobs via an interactive console menu.
- **Full backup**: copies every file from source to target, overwriting existing files.
- **Differential backup**: copies only files that are missing at the target or have been modified since the last backup.
- **Real-time state file** (`state.json`): updated after every file transfer so progress and current program state can be monitored externally in realtime.
- **Daily log file** (`logs/YYYY-MM-DD.json`): recording every file transfer attempt with its size, duration, and outcome.
- **Bilingual UI**: French or English, selected at startup.
- **CLI batch mode**: runs jobs non-interactively by passing a job ID / range / list as a command-line argument.

## Architecture overview

The solution follows a layered architecture with strict separation of concerns:

```
┌─────────────────────────────────────────┐
│         EasySave.ConsoleApp             │  Entry point, UI (View + ViewModel)
└────────────────┬────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────┐
│          EasySave.Services              │  Business logic (BackupService)
│     EasySave.Services / Interfaces      │  Contracts (IBackupService, etc.)
└────────────────┬────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────┐
│       EasySave.Infrastructure           │  File I/O, JSON persistence
└────────────────┬────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────┐
│           EasySave.Core                 │  Domain models (BackupJob, BackupState, BackupType)
└─────────────────────────────────────────┘
                 +
┌─────────────────────────────────────────┐
│              EasyLog                    │  Standalone logging library
└─────────────────────────────────────────┘
:wq
```

Each layer depends only on the layer below it. Infrastructure classes are hidden behind interfaces (IFileService, IConfigRepository, IStateRepository) defined in the Services layer, making the core logic independently testable.

## Project structure

```
EasySave/
│
├── EasyLog/                            # Logging library
│   ├── LogEntry.cs                     # Data model for one log record
│   └── Logger.cs                       # Appends entries to daily JSON log files
│
├── EasySave.Core/                      # Domain models (no dependencies)
│   ├── BackupJob.cs                    # Backup job configuration (id, paths, type)
│   ├── BackupState.cs                  # Live progress snapshot of a running job
│   └── BackupType.cs                   # Enum: Full | Differential
│
├── EasySave.Services/                  # Business logic
│   ├── BackupService.cs                # Executes a backup job end-to-end
│   └── Interfaces/
│       ├── IBackupService.cs
│       ├── IConfigRepository.cs
│       ├── IFileService.cs
│       └── IStateRepository.cs
│
├── EasySave.Infrastructure/            # File-system and JSON persistence
│   ├── ConfigRepository.cs             # Reads/writes config.json
│   ├── StateRepository.cs              # Writes state.json after each file
│   ├── FileService.cs                  # Wraps Directory / File operations
│   └── JsonService.cs                  # Generic JSON read/write utility
│
└── EasySave.ConsoleApp/                # Entry point and UI
    ├── Program.cs                      # Composition root + CLI argument parsing
    ├── TranslationService.cs           # FR / EN string lookup
    ├── ViewModels/
    │   └── JobViewModel.cs             # App state + commands (Create, Run, Delete)
    └── Views/
        └── JobView.cs                  # Console menus, prompts, and output
```

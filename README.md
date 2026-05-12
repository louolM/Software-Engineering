# EasySave 3.0

A graphical backup tool built with **Avalonia UI** and **.NET 10**, following an **MVVM architecture**.  
EasySave 3.0 lets users define unlimited backup jobs with **parallel processing**, encrypt files via CryptoSoft, manage **priority files**, detect running business software before launching a backup, pause/resume/stop jobs individually, and centralize logs on a **Docker server** for enterprise deployments.

> **Version history**  
> `1.0` : Console, 5 jobs max, JSON logs  
> `1.1` : Console, 5 jobs max, JSON **or XML** logs (user choice)  
> `2.0` : **Graphical UI (Avalonia)**, unlimited jobs, CryptoSoft encryption, business software detection  
> `3.0` : **Parallel backups**, priority files, pause/resume/stop per job, Docker log centralization, CryptoSoft single-instance

---

## Table of contents

1. [Features](#features)
2. [What's new in 3.0](#whats-new-in-30)
3. [Architecture overview](#architecture-overview)
4. [Project structure](#project-structure)
5. [Diagrams](#diagrams)
6. [Getting started](#getting-started)
7. [Usage](#usage)
8. [Output files](#output-files)
9. [Configuration reference](#configuration-reference)

---

## Features

- **Graphical interface** built with Avalonia : runs on Windows, Linux, and macOS.
- **Unlimited backup jobs** : create, run, and delete as many jobs as needed.
- **Full backup** : copies every file from source to target, overwriting existing files.
- **Differential backup** : copies only files that are missing or modified since the last backup.
- **Parallel backups** : multiple jobs run simultaneously, controlled by semaphores for large files and encryption.
- **Priority file management** : user-defined extensions backed up first; non-priority files wait if priority files are pending.
- **Large file throttling** : files larger than a configurable size (n KB) cannot be transferred simultaneously.
- **CryptoSoft encryption** : files matching user-defined extensions are encrypted via XOR after each copy. Single-instance to avoid conflicts.
- **Real-time job control** : pause, resume, or stop each job individually; monitor progress in real-time (percentage).
- **Business software detection** : if a defined process is running, all jobs pause automatically and resume when the process closes.
- **Real-time state file** (`state.json`) : updated after every file transfer, including progression percentage.
- **Daily log file** (`logs/YYYY-MM-DD.json` or `.xml`) : records every transfer with size, duration, and encryption time.
- **Docker log centralization** : option to send logs to a centralized Docker server (ASP.NET Core minimal API) for enterprise deployments.
- **Bilingual UI** : French and English, switchable at any time from Settings without restarting.
- **Log format choice** : JSON or XML, switchable from Settings.
- **CLI batch mode** : run jobs non-interactively by passing a job ID / range / list as a command-line argument (identical to v1.0).

---

## What's new in 3.0

### Parallel backup processing
Jobs now run **in parallel** instead of sequentially. A **semaphore** limits the number of large files (> n KB) transferred simultaneously to prevent bandwidth saturation. This is configurable in Settings.

### Priority file management
Extensions can be marked as **priority**. When a priority file is pending in any job, non-priority files in all jobs **wait**. This ensures critical files are backed up first.

Example: If `.docx` is priority and a 500 MB `.exe` is in queue, the `.exe` waits until all `.docx` files in that job are completed.

### Large file throttling
To avoid saturating bandwidth, files larger than n KB (configurable) cannot be transferred simultaneously. Only **one** large file per job is allowed at a time. Smaller files may still transfer in parallel.

### Real-time job control
Each job can be:
- **Paused** : the current file completes, then the job halts. Resume restarts from the next file.
- **Resumed** : restarts a paused job.
- **Stopped** : immediately halts the job and cancels the current transfer.

All operations can be performed per-job or on all jobs at once. Progress is updated in real-time (percentage, status).

### Automatic pause on business software detection
If a business process is detected, **all running jobs pause automatically**. When the process closes, jobs resume without user intervention. This is now more robust with a dedicated watcher thread.

### CryptoSoft single-instance enforcement
CryptoSoft is configured to run as a **single-instance** executable. Multiple jobs cannot invoke it simultaneously; encryption requests queue via a semaphore.

### Docker log centralization
A new **LogServer** (ASP.NET Core minimal API) accepts log entries via HTTP POST and centralizes them to a single daily JSON file on a Docker container. Settings now include three log destination options:
- **Local** : logs only to the local `logs/` directory.
- **Docker** : logs only to the remote Docker server.
- **Both** : logs to both local and Docker (high availability).

This is ideal for enterprises deploying EasySave on multiple servers.

---

## Architecture overview

The solution follows a strict **layered MVVM architecture**:

```
┌──────────────────────────────────────────────┐
│              EasySave.UI                     │  Avalonia graphical interface
│   ViewModels: MainWindow, JobList, Settings  │  (MVVM : View + ViewModel)
│   Views: MainWindow, JobListView, Settings   │  Real-time job control (pause/resume/stop)
│   TranslationService (centralized i18n)      │
└───────────────────┬──────────────────────────┘
                    │ uses interfaces
┌───────────────────▼──────────────────────────┐
│           EasySave.Services                  │  Business logic
│   BackupService (parallel, semaphores)       │  IBackupService, IConfigRepository,
│   BusinessSoftwareWatcher                    │  IFileService, IStateRepository,
│   JobController (pause/resume/stop state)    │  ISettingsRepository
│                                              │
└───────────────────┬──────────────────────────┘
                    │ uses interfaces
┌───────────────────▼──────────────────────────┐
│        EasySave.Infrastructure               │  File I/O and JSON/XML persistence
│   FileService, ConfigRepository,             │
│   StateRepository, SettingsRepository,       │
│   JsonService                                │
└───────────────────┬──────────────────────────┘
                    │ uses
┌───────────────────▼──────────────────────────┐
│           EasySave.Core                      │  Domain models - no dependencies
│   BackupJob, BackupState, BackupType,        │
│   AppSettings, JobController                 │
└──────────────────────────────────────────────┘
              +
┌──────────────────────────────────────────────┐
│               EasyLog                        │  Standalone logging DLL
│   Logger (JSON/XML)                          │  DockerLogService (HTTP POST to LogServer)
│   LogEntry                                   │
└──────────────────────────────────────────────┘
              +
┌──────────────────────────────────────────────┐
│            LogServer                         │  ASP.NET Core minimal API (Docker)
│   POST /api/logs : receives and centralizes  │  Runs on http://localhost:5000
│   log entries from multiple machines         │
└──────────────────────────────────────────────┘
              +
┌──────────────────────────────────────────────┐
│            EasySave.ConsoleApp               │  Console interface (v1.1  kept in parallel)
└──────────────────────────────────────────────┘
```

Each layer depends only on the layer below it. Infrastructure classes are hidden behind interfaces defined in the Services layer, making business logic independently testable and the UI swappable.

---

## Project structure

```
EasySave/
│
├── EasyLog/                                # Standalone logging DLL
│   ├── LogEntry.cs                         # Log record model (includes EncryptionTime)
│   ├── Logger.cs                           # Writes daily JSON or XML log files
│   └── DockerLogService.cs                 # HTTP client to send logs to LogServer
│
├── EasySave.Core/                          # Domain models (no dependencies)
│   ├── AppSettings.cs                      # User settings (language, business software, encryption, log format, log destination)
│   ├── BackupJob.cs                        # Job definition (id, name, paths, type)
│   ├── BackupState.cs                      # Live progress snapshot (includes Progression %)
│   ├── BackupType.cs                       # Enum: Full | Differential
│   └── JobController.cs                    # Per-job state machine (pause/resume/stop, CancellationToken)
│
├── EasySave.Services/                      # Business logic
│   ├── BackupService.cs                    # Parallel backup with semaphores (large files, CryptoSoft)
│   ├── BusinessSoftwareWatcher.cs          # Monitors process; pauses/resumes jobs
│   └── Interfaces/
│       ├── IBackupService.cs
│       ├── IConfigRepository.cs
│       ├── IFileService.cs
│       ├── ISettingsRepository.cs
│       └── IStateRepository.cs
│
├── EasySave.Infrastructure/                # Persistence and file system
│   ├── ConfigRepository.cs                 # Reads/writes config.json
│   ├── FileService.cs                      # Wraps Directory/File operations
│   ├── JsonService.cs                      # Generic JSON read/write utility
│   ├── SettingsRepository.cs               # Reads/writes settings.json
│   └── StateRepository.cs                  # Writes state.json after each file
│
├── EasySave.UI/                            # Avalonia graphical interface (v3.0)
│   ├── App.axaml / App.axaml.cs            # Application entry point
│   ├── Program.cs                          # Avalonia bootstrap + CLI argument support
│   ├── TranslationService.cs               # Centralized EN/FR string dictionary
│   ├── ViewLocator.cs                      # Maps ViewModels to Views automatically
│   ├── Tools/
│   │   └── CryptoSoft.exe                  # External encryption executable (single-instance)
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs
│   │   ├── MainWindowViewModel.cs          # Navigation + language/settings propagation
│   │   ├── JobListViewModel.cs             # Job CRUD, pause/resume/stop per job, translations
│   │   ├── JobProgressItem.cs              # Bindable job progress (status, percent, type, source, target)
│   │   ├── SettingsViewModel.cs            # Settings form, validation, events
│   │   └── StatusTo*Converter.cs           # Value converters (pause/resume/stop button states)
│   └── Views/
│       ├── MainWindow.axaml                # Shell with navigation bar
│       ├── JobListView.axaml               # Job list + creation form + pause/resume/stop buttons
│       └── SettingsView.axaml              # Settings panel (log destination: Local/Docker/Both)
│
├── EasySave.ConsoleApp/                    # Console interface (v1.1 kept in parallel)
│   ├── Program.cs
│   ├── TranslationService.cs
│   ├── ViewModels/
│   │   └── JobViewModel.cs
│   └── Views/
│       └── JobView.cs
│
└── LogServer/                              # ASP.NET Core minimal API (Docker)
    ├── Program.cs                          # Configures POST /api/logs endpoint
    ├── Dockerfile                          # Docker image definition
    ├── docker-compose.yml                  # Docker Compose configuration
    ├── central-logs/                       # Output directory for centralized logs
    │   └── YYYY-MM-DD.json                 # Daily centralized log file
    └── appsettings.json                    # Log server configuration
```

---

## Diagrams

### Activity diagram (v3.0 updates)

```mermaid
flowchart TD
  Start((' '))
  RT{Run type?}
  IM['Run in graphic mode']
  RA['Run with arguments']
  Menu{Jobs}
  CreateJob['New job']
  DefName['Define name and directories']
  ChooseType['Choose job type full / differential']
  RunJob['Run a selected job']
  RunAllJob['Run all jobs (in parallel)']
  PauseJob['Pause a running job']
  ResumeJob['Resume a paused job']
  StopJob['Stop a running job']
  PauseAll['Pause all jobs']
  ResumeAll['Resume all jobs']
  StopAll['Stop all jobs']
  RunArgs{Run with arguments?}
  SelectJobRun['Select job']
  FullBackup{Full backup?}
  CopyAll['Copy/Paste all between directories']
  CheckDiff['Check differentials between directories']
  CopyDiff['Copy/Paste differentials between directories']
  DeleteJob['Delete a job']
  SelectJobDel['Select job']
  Leave['Leave menu']
  Settings['Open settings']
  SettingsMenu{Settings}
  Language['Choose language']
  BusinessSoftwareDetection['Choose the business software Detection']
  CryptoSoftEncryption['CryptoSoft Encryption']
  Extensions['Choose the extensions to Encrypt']
  Priority['Mark priority extensions']
  Key['Choose the Encryption Key']
  LogFileFormat['Choose the Log File Format JSON/XML']
  LogDestination['Choose log destination: Local / Docker / Both']
  MaxParallelSize['Set max parallel file size (KB)']
  End((' '))

  Start --> RT
  RT -->|interactive| IM
  RT -->|arguments| RA
  IM --> Menu
  RA --> RunJob

  Menu -->|create| CreateJob
  Menu -->|run| RunJob
  Menu -->|runAll| RunAllJob
  Menu -->|pause| PauseJob
  Menu -->|resume| ResumeJob
  Menu -->|stop| StopJob
  Menu -->|pauseAll| PauseAll
  Menu -->|resumeAll| ResumeAll
  Menu -->|stopAll| StopAll
  Menu -->|delete| DeleteJob
  Menu -->|leave| Leave
  Menu -->|settings| Settings

  Settings --> SettingsMenu
  SettingsMenu --> Language
  SettingsMenu --> BusinessSoftwareDetection
  SettingsMenu --> CryptoSoftEncryption
  CryptoSoftEncryption --> Extensions
  CryptoSoftEncryption --> Priority
  CryptoSoftEncryption --> Key
  SettingsMenu --> LogFileFormat
  SettingsMenu --> LogDestination
  SettingsMenu --> MaxParallelSize

  Language --> Menu
  BusinessSoftwareDetection --> Menu
  Extensions --> Menu
  Priority --> Menu
  Key --> Menu
  LogFileFormat --> Menu
  LogDestination --> Menu
  MaxParallelSize --> Menu

  CreateJob --> DefName --> ChooseType --> Menu

  RunJob --> RunArgs
  RunArgs -->|yes| FullBackup
  RunArgs -->|no| SelectJobRun
  SelectJobRun --> FullBackup
  FullBackup -->|yes| CopyAll --> Menu
  FullBackup -->|no| CheckDiff --> CopyDiff --> Menu

  RunAllJob --> Menu
  PauseJob --> Menu
  ResumeJob --> Menu
  StopJob --> Menu
  PauseAll --> Menu
  ResumeAll --> Menu
  StopAll --> Menu

  DeleteJob --> SelectJobDel --> Menu
  Leave --> End
```

### Comparison: v2.0 → v3.0

| Function | Version 2.0 | Version 3.0 |
|---|---|---|
| **Interface** | Graphical (Avalonia) | Graphical (Avalonia) |
| **Multilingual** | English and French | English and French |
| **Backup jobs** | Unlimited | Unlimited |
| **Daily log file** | Yes (JSON, XML) | Yes (JSON, XML) |
| **User can pause/resume/stop jobs** | No | Yes (per-job or all) |
| **Status File** | Yes | Yes |
| **Backup operating mode** | Sequential | **Parallel** |
| **Stop if business software is detected** | Yes (blocks startup) | Yes (pauses automatically) |
| **Using CryptoSoft** | Yes | Yes (**single-instance**) |
| **Priority File Management** | No | **Yes** |
| **Prohibition of simultaneous large file transfers** | No | **Yes** (configurable size) |
| **Centralization of daily log files** | No | **Yes (Docker)** |
| **Command line** | Yes (identical to v1.0) | Yes (identical to v1.0) |

---

## Getting started

### Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| Avalonia | 12.0.2 (restored automatically) |
| Docker | 20.0+ (optional, for LogServer) |
| OS | Windows / Linux / macOS |

### Build

```bash
dotnet build
```

### Run the graphical interface (v3.0)

```bash
cd EasySave.UI
dotnet run
```

### Run the console interface (v1.1)

```bash
cd EasySave.ConsoleApp
dotnet run
```

### Run LogServer (optional, Docker)

```bash
cd LogServer
docker-compose up -d
```

This starts the log server on `http://localhost:5000` and listens for POST requests on `/api/logs`.

---

## Usage

### Graphical interface

Launch `EasySave.UI` to open the graphical interface. The navigation bar at the top gives access to two sections:

**📋 Jobs** : manage and run backup jobs.  
**⚙ Settings** : configure language, business software detection, CryptoSoft encryption, log format, and Docker integration.

#### Creating a job

Click **＋ New Job** and fill in the form:

| Field | Description | Example |
|---|---|---|
| Name | Unique job name | `Documents backup` |
| Source | Source folder (Browse button available) | `C:\Users\Alice\Documents` |
| Target | Target folder (Browse button available) | `D:\Backups\Documents` |
| Differential | Check to enable differential mode | ☐ (unchecked = Full) |

Click **Save** to confirm. The job appears immediately in the list.

#### Modifying a job

Select a job in the list and click **✎ Modify** to edit its name, source path, target path, and backup type.

#### Running jobs

- **Run Selected** : runs the selected job. Other jobs run in parallel if they were already started.
- **Run All** : starts all jobs in parallel. Each job respects priority file rules and large file throttling.

If a business software is running (configured in Settings), all jobs pause automatically. They resume when the process closes.

#### Pausing, resuming, and stopping jobs

For each running job:
- **⏸ Pause** : the current file completes, then the job halts. Only active when Status = ACTIVE.
- **▶ Resume** : restarts a paused job. Only active when Status = PAUSED.
- **⏹ Stop** : immediately halts the job and cancels the current transfer. Active when Status = ACTIVE or PAUSED.

You can also control all jobs at once:
- **Pause All**
- **Resume All**
- **Stop All**

Progress is updated in real-time (percentage, status).

#### Deleting a job

Select a job and click **🗑 Delete**.

#### Settings

| Setting | Description | Default |
|---|---|---|
| **Language** | English (EN) or Français (FR) , applied immediately on save | EN |
| **Business Software** | Process name to detect (e.g. `Calculator`). Pauses backups if running. | (empty) |
| **Encrypted extensions** | Space-separated list of extensions to encrypt (e.g. `.txt .docx .pdf`) | (empty) |
| **Priority extensions** | Space-separated list of priority extensions (backed up first) | (empty) |
| **Encryption Key** | XOR key passed to CryptoSoft | `defaultkey` |
| **Log File Format** | JSON or XML | JSON |
| **Max parallel file size (KB)** | Files larger than this are transferred one at a time | 5000 |
| **Log destination** | Local / Docker / Both | Local |
| **Docker log server URL** | Address of LogServer (if Docker log destination is enabled) | `http://localhost:5000` |

### CLI batch mode

Pass a job selector as a command-line argument to run jobs without opening the interface:

```bash
# Run a single job
dotnet run -- 2

# Run a range of jobs
dotnet run -- 1-3

# Run a specific list
dotnet run -- 1;4;5
```

---

## Output files

All output files are written in the application's working directory.

### `settings.json` : application settings

```json
{
  "BusinessSoftware": "Calculator",
  "EncryptedExtensions": [".txt", ".docx"],
  "PriorityExtensions": [".docx"],
  "EncryptionKey": "defaultkey",
  "LogFormat": "JSON",
  "Language": "EN",
  "MaxParallelSizeKB": 5000,
  "LogDestination": "Both",
  "DockerServerUrl": "http://localhost:5000"
}
```

### `config.json` : job definitions

```json
[
  {
    "Id": 1,
    "Name": "Documents backup",
    "SourcePath": "C:\\Users\\Alice\\Documents",
    "TargetPath": "D:\\Backups\\Documents",
    "Type": 0
  }
]
```

Type values: `0` = Full, `1` = Differential.

### `state.json` : live backup progress

```json
[
  {
    "Name": "Documents backup",
    "LastActionTime": "2026-05-12T10:32:10",
    "Status": "ACTIVE",
    "TotalFiles": 120,
    "RemainingFiles": 47,
    "TotalSize": 524288000,
    "RemainingSize": 196608000,
    "Progression": 60.8,
    "CurrentSourceFile": "\\\\DESKTOP\\C$\\Users\\Alice\\Documents\\report.docx",
    "CurrentTargetFile": "\\\\DESKTOP\\D$\\Backups\\Documents\\report.docx"
  }
]
```

Status values: `"ACTIVE"` while running, `"IDLE"` when finished, `"PAUSED"` when paused, `"STOPPED"` when stopped.  
Paths are stored in **UNC format** (`\\machine\drive$\...`).

### `logs/YYYY-MM-DD.json` : daily local transfer log

```json
[
  {
    "Timestamp": "2026-05-12T10:32:10",
    "BackupName": "Documents backup",
    "SourcePath": "\\\\DESKTOP\\C$\\Users\\Alice\\Documents\\report.docx",
    "TargetPath": "\\\\DESKTOP\\D$\\Backups\\Documents\\report.docx",
    "FileSize": 204800,
    "TransferTime": 37,
    "EncryptionTime": 12
  }
]
```

### LogServer centralized logs (Docker)

When log destination is set to "Docker" or "Both", entries are also sent to the LogServer (running on Docker). The centralized log is stored at:

```
LogServer/central-logs/YYYY-MM-DD.json
```

Same structure as the local log, but aggregated from all machines.

| Field | Description |
|---|---|
| `FileSize` | Source file size in bytes. `0` = failed copy. |
| `TransferTime` | Copy duration in ms. `-1` = failed copy. |
| `EncryptionTime` | `0` = not encrypted, `>0` = duration in ms, `<0` = CryptoSoft error. |
| `TargetPath` | Empty string `""` when the copy failed. |

XML format follows the same structure when selected in Settings.

---

## Configuration reference

| File | Location | Purpose |
|---|---|---|
| `settings.json` | Working directory | Language, business software, encryption, log format, log destination, max parallel size |
| `config.json` | Working directory | Backup job definitions |
| `state.json` | Working directory | Live progress of all running jobs |
| `logs/YYYY-MM-DD.json` or `.xml` | `logs/` sub-directory | Immutable daily audit log (local) |
| `LogServer/central-logs/YYYY-MM-DD.json` | LogServer container | Immutable daily audit log (centralized on Docker) |

All files are created automatically on first use , no manual setup required.

---

## Performance & scalability

**Parallel processing**: EasySave 3.0 can run multiple jobs simultaneously. Bandwidth-intensive operations (large file transfers, CryptoSoft encryption) are controlled via semaphores to prevent system overload.

**Priority files**: Critical file types (e.g., `.docx`) are backed up first, ensuring important data is secured before less critical files.

**Business software awareness**: The watcher thread monitors process creation/termination in real-time, pausing and resuming jobs as needed without user intervention.

**Enterprise logging**: The Docker LogServer aggregates logs from multiple machines into a single daily file, simplifying audit trails and compliance reporting.

---

## Known limitations

- **Large backup sets**: Transferring millions of files may cause UI responsiveness delays during state updates.
- **Network paths**: UNC paths are supported, but network latency may impact performance.
- **CryptoSoft**: Encryption is XOR-based (not cryptographically secure); for production use, consider replacing with AES or other industry-standard algorithms.
- **Docker LogServer**: Does not include authentication; restrict network access in production.
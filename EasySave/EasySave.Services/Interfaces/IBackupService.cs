using EasySave.Core;

namespace EasySave.Services.Interfaces;

// Defines the contract for executing a backup job.

// Implementations are responsible for copying files from the job's source directory to its target directory, logging results, and updating the live progress state.
public interface IBackupService
{
    void RunBackup(BackupJob job, AppSettings settings, IProgress<double>? progress = null);
}
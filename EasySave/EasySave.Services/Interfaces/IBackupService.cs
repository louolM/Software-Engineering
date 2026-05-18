using EasySave.Core;

namespace EasySave.Services.Interfaces;

public interface IBackupService
{
    // Runs a backup job asynchronously.
    // The JobController allows the caller to pause, resume, or stop the job mid-run.
    // The optional IProgress<double> callback receives the completion percentage
    // after every file so the UI can update a progress bar in real time.
    Task RunBackupAsync(BackupJob job, AppSettings settings,
                        JobController controller, IProgress<double>? progress = null);
}
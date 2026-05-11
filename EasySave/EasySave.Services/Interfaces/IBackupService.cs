using EasySave.Core;

namespace EasySave.Services.Interfaces;

public interface IBackupService
{
    /// <summary>
    /// Lance un backup de façon asynchrone.
    /// Le JobController permet de Pause / Resume / Stop pendant l'exécution.
    /// </summary>
    Task RunBackupAsync(BackupJob job, AppSettings settings,
                        JobController controller, IProgress<double>? progress = null);
}
using EasySave.Core;

namespace EasySave.Services.Interfaces;

public interface IBackupService
{
    void RunBackup(BackupJob job);
}
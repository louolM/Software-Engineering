using EasySave.Core;

var service = new BackupService();

var job = new BackupJob
{
    Id = 1,
    Name = "TestBackup",
    SourcePath = "C:\\TestSource",
    TargetPath = "C:\\TestTarget",
    Type = BackupType.Full
};

service.RunBackup(job);
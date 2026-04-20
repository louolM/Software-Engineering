using EasyLog;
using EasySave.Infrastructure;

namespace EasySave.Core;

public class BackupService
{
    private readonly FileService _fileService = new();
    private readonly Logger _logger = new();

    public void RunBackup(BackupJob job)
    {
        var files = _fileService.GetAllFiles(job.SourcePath);

        foreach (var file in files)
        {
            try
            {
                var relativePath = Path.GetRelativePath(job.SourcePath, file);
                var targetFile = Path.Combine(job.TargetPath, relativePath);

                var start = DateTime.Now;

                _fileService.CopyFile(file, targetFile);

                var duration = (long)(DateTime.Now - start).TotalMilliseconds;

                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = file,
                    TargetPath = targetFile,
                    FileSize = new FileInfo(file).Length,
                    TransferTime = duration
                });
            }
            catch
            {
                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = file,
                    TargetPath = "",
                    FileSize = 0,
                    TransferTime = -1
                });
            }
        }
    }
}
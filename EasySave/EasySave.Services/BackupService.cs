using EasyLog;
using EasySave.Core;
using EasySave.Services.Interfaces;

namespace EasySave.Services;

// Core service that executes a backup job.
//
// This class is the concrete implementation of IBackupService.
// It orchestrates the full backup workflow for a single BackupJob
//
// 1.  Enumerate all source files.
// 2.  Initialize a BackupState progress snapshot.
// 3.  For each file: optionally skip it (differential strategy), copy it, log the outcome, and update the live state.
// 4.  Mark the job as "DONE" and write the final state.

// Dependencies are injected through the constructor so they can be replaced with mocks in unit tests without touching the file system or log files.
public class BackupService : IBackupService
{
    private readonly IFileService _fileService;
    private readonly Logger _logger;
    private readonly IStateRepository _stateRepo;

    public BackupService(IFileService fileService, Logger logger, IStateRepository stateRepo)
    {
        _fileService = fileService;
        _logger = logger;
        _stateRepo = stateRepo;
    }

    public void RunBackup(BackupJob job)
    {
        var files = _fileService.GetAllFiles(job.SourcePath!);
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        var state = new BackupState
        {
            Name = job.Name,
            Status = "ACTIVE",
            LastActionTime = DateTime.Now,
            TotalFiles = files.Count(),
            RemainingFiles = files.Count(),
            TotalSize = totalSize,
            RemainingSize = totalSize
        };

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(job.SourcePath!, file);
            var targetFile = Path.Combine(job.TargetPath!, relativePath);
            var fileSize = new FileInfo(file).Length;

            if (job.Type == BackupType.Differential && File.Exists(targetFile))
            {
                var sourceInfo = new FileInfo(file);
                var targetInfo = new FileInfo(targetFile);
                if (sourceInfo.LastWriteTime <= targetInfo.LastWriteTime)
                {
                    state.RemainingFiles--;
                    state.RemainingSize -= fileSize;
                    continue;
                }
            }

            try
            {
                state.CurrentSourceFile = file;
                state.CurrentTargetFile = targetFile;
                state.LastActionTime = DateTime.Now;

                var start = DateTime.Now;
                _fileService.CopyFile(file, targetFile);
                var duration = (long)(DateTime.Now - start).TotalMilliseconds;

                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = file,
                    TargetPath = targetFile,
                    FileSize = fileSize,
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

            state.RemainingFiles--;
            state.RemainingSize -= fileSize;
            _stateRepo.Save(new List<BackupState> { state });
        }

        state.Status = "DONE";
        state.LastActionTime = DateTime.Now;
        _stateRepo.Save(new List<BackupState> { state });
    }
}
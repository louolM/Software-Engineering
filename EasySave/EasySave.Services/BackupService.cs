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
        // ✅ Chemins locaux pour les opérations fichiers
        var sourcePath = job.SourcePath!;
        var targetPath = job.TargetPath!;

        var files = _fileService.GetAllFiles(sourcePath);
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

        _stateRepo.Save(new List<BackupState> { state });

        foreach (var file in files)
        {
            // ✅ Chemin local pour la copie
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var targetFile = Path.Combine(targetPath, relativePath);
            var fileSize = new FileInfo(file).Length;

            // Differential : skip si fichier identique
            if (job.Type == BackupType.Differential && File.Exists(targetFile))
            {
                var sourceInfo = new FileInfo(file);
                var targetInfo = new FileInfo(targetFile);
                if (sourceInfo.LastWriteTime <= targetInfo.LastWriteTime)
                {
                    state.RemainingFiles--;
                    state.RemainingSize -= fileSize;
                    _stateRepo.Save(new List<BackupState> { state });
                    continue;
                }
            }

            try
            {
                // ✅ Format UNC uniquement pour l'affichage dans state.json et les logs
                state.CurrentSourceFile = ToUNC(file);
                state.CurrentTargetFile = ToUNC(targetFile);
                state.LastActionTime = DateTime.Now;

                var start = DateTime.Now;
                _fileService.CopyFile(file, targetFile); // ✅ chemins locaux
                var duration = (long)(DateTime.Now - start).TotalMilliseconds;

                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),       // ✅ UNC dans les logs
                    TargetPath = ToUNC(targetFile),
                    FileSize = fileSize,
                    TransferTime = duration
                });
            }
            catch (Exception ex)
            {
                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),
                    TargetPath = "",
                    FileSize = 0,
                    TransferTime = -1
                });
            }

            state.RemainingFiles--;
            state.RemainingSize -= fileSize;
            _stateRepo.Save(new List<BackupState> { state });
        }

        // Statut final : INACTIVE
        state.Status = "INACTIVE";
        state.LastActionTime = DateTime.Now;
        state.CurrentSourceFile = null;
        state.CurrentTargetFile = null;
        _stateRepo.Save(new List<BackupState> { state });
    }

    // Conversion chemin local → format UNC (pour logs et state uniquement)
    private static string ToUNC(string path)
    {
        if (path.StartsWith(@"\\")) return path;
        if (path.Length >= 2 && path[1] == ':')
        {
            var drive = path[0].ToString().ToUpper();
            var rest = path[2..].Replace('/', '\\');
            return $@"\\{Environment.MachineName}\{drive}${rest}";
        }
        return path;
    }
}
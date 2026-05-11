using EasyLog;
using EasySave.Core;
using EasySave.Services.Interfaces;
using System.Diagnostics;

namespace EasySave.Services;

public class BackupService : IBackupService
{
    private readonly IFileService _fileService;
    private readonly Logger _logger;
    private readonly IStateRepository _stateRepo;

    // ── Mutex statique partagé entre tous les jobs ────────────────────────
    // Empêche deux fichiers > MaxParallelFileSize d'être copiés en même temps
    private static readonly SemaphoreSlim _largeCopySlot = new(1, 1);

    // ── Mutex pour CryptoSoft single-instance ─────────────────────────────
    private static readonly SemaphoreSlim _cryptoSlot = new(1, 1);

    public BackupService(IFileService fileService, Logger logger, IStateRepository stateRepo)
    {
        _fileService = fileService;
        _logger = logger;
        _stateRepo = stateRepo;
    }

    public async Task RunBackupAsync(BackupJob job, AppSettings settings,
                                     JobController controller, IProgress<double>? progress = null)
    {
        var sourcePath = job.SourcePath!;
        var targetPath = job.TargetPath!;

        var allFiles = _fileService.GetAllFiles(sourcePath).ToList();
        var totalSize = allFiles.Sum(f => new FileInfo(f).Length);

        var state = new BackupState
        {
            Name = job.Name,
            Status = "ACTIVE",
            LastActionTime = DateTime.Now,
            TotalFiles = allFiles.Count,
            RemainingFiles = allFiles.Count,
            TotalSize = totalSize,
            RemainingSize = totalSize
        };

        _stateRepo.Save(new List<BackupState> { state });

        // ── Tri : fichiers prioritaires d'abord ───────────────────────────
        var priorityExts = settings.PriorityExtensions
            .Select(e => e.ToLower()).ToHashSet();

        var orderedFiles = priorityExts.Count > 0
            ? allFiles
                .OrderByDescending(f => priorityExts.Contains(
                    Path.GetExtension(f).ToLower()))
                .ToList()
            : allFiles;

        foreach (var file in orderedFiles)
        {
            // ── Pause / Stop ──────────────────────────────────────────────
            try
            {
                controller.WaitIfPaused();
            }
            catch (OperationCanceledException)
            {
                break; // job stoppé
            }

            if (controller.IsStopped) break;

            var relativePath = Path.GetRelativePath(sourcePath, file);
            var targetFile = Path.Combine(targetPath, relativePath);
            var fileSize = new FileInfo(file).Length;
            var ext = Path.GetExtension(file).ToLower();

            // ── Priorité : bloquer les fichiers non-prioritaires ──────────
            // si des fichiers prioritaires sont encore en attente
            if (priorityExts.Count > 0 && !priorityExts.Contains(ext))
            {
                // Vérifier s'il reste des fichiers prioritaires non copiés
                var remaining = orderedFiles
                    .SkipWhile(f => f != file)
                    .Skip(1)
                    .Any(f => priorityExts.Contains(
                        Path.GetExtension(f).ToLower()));

                // Si des prioritaires sont encore dans la liste avant ce fichier
                var hasPriorityBefore = orderedFiles
                    .TakeWhile(f => f != file)
                    .Any(f => priorityExts.Contains(
                        Path.GetExtension(f).ToLower()) &&
                        !File.Exists(Path.Combine(targetPath,
                            Path.GetRelativePath(sourcePath, f))));

                if (hasPriorityBefore)
                {
                    // Reporte ce fichier à la fin
                    continue;
                }
            }

            // ── Differential : skip si fichier identique ──────────────────
            if (job.Type == BackupType.Differential && File.Exists(targetFile))
            {
                var srcInfo = new FileInfo(file);
                var tgtInfo = new FileInfo(targetFile);
                if (srcInfo.LastWriteTime <= tgtInfo.LastWriteTime)
                {
                    state.RemainingFiles--;
                    state.RemainingSize -= fileSize;
                    _stateRepo.Save(new List<BackupState> { state });
                    progress?.Report(state.Progression);
                    continue;
                }
            }

            try
            {
                state.CurrentSourceFile = ToUNC(file);
                state.CurrentTargetFile = ToUNC(targetFile);
                state.LastActionTime = DateTime.Now;

                // ── Limite taille parallèle ───────────────────────────────
                var maxSize = settings.MaxParallelFileSize * 1024; // KB → bytes
                bool isLarge = maxSize > 0 && fileSize > maxSize;

                if (isLarge) await _largeCopySlot.WaitAsync(controller.Token);

                long transferDuration;
                try
                {
                    var start = DateTime.Now;
                    await Task.Run(() => _fileService.CopyFile(file, targetFile),
                                   controller.Token);
                    transferDuration = (long)(DateTime.Now - start).TotalMilliseconds;
                }
                finally
                {
                    if (isLarge) _largeCopySlot.Release();
                }

                // ── CryptoSoft single-instance ────────────────────────────
                long encryptionTime = 0;
                if (settings.EncryptedExtensions.Contains(ext))
                {
                    await _cryptoSlot.WaitAsync(controller.Token);
                    try
                    {
                        encryptionTime = await Task.Run(() =>
                            RunCryptoSoft(targetFile, settings.EncryptionKey));
                    }
                    finally
                    {
                        _cryptoSlot.Release();
                    }
                }

                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),
                    TargetPath = ToUNC(targetFile),
                    FileSize = fileSize,
                    TransferTime = transferDuration,
                    EncryptionTime = encryptionTime
                });
            }
            catch (OperationCanceledException)
            {
                break; // stoppé pendant la copie
            }
            catch
            {
                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),
                    TargetPath = "",
                    FileSize = 0,
                    TransferTime = -1,
                    EncryptionTime = 0
                });
            }

            state.RemainingFiles--;
            state.RemainingSize -= fileSize;
            _stateRepo.Save(new List<BackupState> { state });
            progress?.Report(state.Progression);
        }

        state.Status = controller.IsStopped ? "STOPPED" : "INACTIVE";
        state.LastActionTime = DateTime.Now;
        state.CurrentSourceFile = null;
        state.CurrentTargetFile = null;
        _stateRepo.Save(new List<BackupState> { state });
    }

    private static long RunCryptoSoft(string filePath, string key)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                             "Tools", "CryptoSoft.exe"),
                    Arguments = $"\"{filePath}\" \"{key}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch { return -99; }
    }

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
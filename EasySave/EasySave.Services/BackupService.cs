using CryptoSoft;
using EasyLog;
using EasySave.Core;
using EasySave.Services.Interfaces;
using System.Diagnostics;

namespace EasySave.Services;

// Executes backup jobs and coordinates file copying, encryption, state tracking, and logging.
//
// Two static semaphores enforce concurrency rules across all running jobs:
//   _largeCopySlot  - ensures only one large file is copied at a time (threshold from settings)
//   _cryptoSlot     - ensures only one file is encrypted at a time
// Both are static so they are shared even when multiple BackupService instances exist.
public class BackupService : IBackupService
{
    private readonly IFileService _fileService;
    private readonly Logger _logger;
    private readonly IStateRepository _stateRepo;
    private readonly DockerLogService? _dockerLog;

    // SemaphoreSlim(1, 1) creates a binary semaphore: at most one caller can hold it at a time.
    private static readonly SemaphoreSlim _largeCopySlot = new(1, 1);
    private static readonly SemaphoreSlim _cryptoSlot = new(1, 1);

    public BackupService(IFileService fileService, Logger logger,
                         IStateRepository stateRepo,
                         DockerLogService? dockerLog = null)
    {
        _fileService = fileService;
        _logger = logger;
        _stateRepo = stateRepo;
        _dockerLog = dockerLog;
    }

    public async Task RunBackupAsync(BackupJob job, AppSettings settings,
                                     JobController controller,
                                     IProgress<double>? progress = null)
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
        // Writes initial state so monitoring tools see the job as active immediately.
        _stateRepo.Save(new List<BackupState> { state });

        // Build a set of priority extensions for O(1) lookup during the sort.
        var priorityExts = settings.PriorityExtensions
            .Select(e => e.ToLower()).ToHashSet();

        // Sort files so priority extensions come first. OrderByDescending on a bool puts
        // "true" (is priority) before "false" (not priority).
        var orderedFiles = priorityExts.Count > 0
            ? allFiles.OrderByDescending(f =>
                priorityExts.Contains(Path.GetExtension(f).ToLower())).ToList()
            : allFiles;

        foreach (var file in orderedFiles)
        {
            // Wait asynchronously if the job is paused, or exit if it has been stopped.
            try { await controller.WaitIfPausedAsync(controller.Token); }
            catch (OperationCanceledException) { break; }
            
            if (controller.IsStopped) break;

            var relativePath = Path.GetRelativePath(sourcePath, file);
            var targetFile = Path.Combine(targetPath, relativePath);
            var fileSize = new FileInfo(file).Length;
            var ext = Path.GetExtension(file).ToLower();

            // Differential mode: skip files that already exist at the target
            // and have not been modified since the last backup.
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

                var maxSize = settings.MaxParallelFileSize * 1024;
                bool isLarge = maxSize > 0 && fileSize > maxSize;

                // Acquire the large-file slot before copying so that at most one
                // oversized file is being transferred at any given time.
                if (isLarge) await _largeCopySlot.WaitAsync(controller.Token);
                long transferDuration;
                try
                {
                    var start = DateTime.Now;
                    await Task.Run(() => _fileService.CopyFile(file, targetFile), controller.Token);
                    transferDuration = (long)(DateTime.Now - start).TotalMilliseconds;
                }
                catch (OperationCanceledException) when (isLarge)
                {
                    // The job was cancelled while waiting for or holding the slot.
                    // Break out of the loop; the finally block still releases the semaphore.
                    break;
                }
                // Always release the semaphore, even on cancellation, to avoid deadlocking
                // other jobs waiting for the large-file slot.
                finally { if (isLarge) _largeCopySlot.Release(); }

                long encryptionTime = 0;
                if (settings.EncryptedExtensions.Contains(ext))
                {
                    // Acquire the crypto slot so only one file is encrypted at a time.
                    await _cryptoSlot.WaitAsync(controller.Token);
                    try { encryptionTime = await Task.Run(() => RunCryptoSoft(targetFile, settings.EncryptionKey)); }
                    finally { _cryptoSlot.Release(); }
                }
                // Log a failed transfer with TransferTime = -1 to signal the error
                // without interrupting the rest of the job.
                WriteLog(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),
                    TargetPath = ToUNC(targetFile),
                    FileSize = fileSize,
                    TransferTime = transferDuration,
                    EncryptionTime = encryptionTime
                }, settings);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                WriteLog(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),
                    TargetPath = "",
                    FileSize = 0,
                    TransferTime = -1,
                    EncryptionTime = 0
                }, settings);
            }

            state.RemainingFiles--;
            state.RemainingSize -= fileSize;
            _stateRepo.Save(new List<BackupState> { state });
            progress?.Report(state.Progression);
        }

        // Mark the job as STOPPED if cancelled, or INACTIVE when it finished normally.
        state.Status = controller.IsStopped ? "STOPPED" : "INACTIVE";
        state.LastActionTime = DateTime.Now;
        state.CurrentSourceFile = null;
        state.CurrentTargetFile = null;
        _stateRepo.Save(new List<BackupState> { state });
    }

    // Routes the log entry to the local logger, the Docker server, or both,
    // depending on the LogDestination setting.
    private void WriteLog(LogEntry entry, AppSettings settings)
    {
        if (settings.LogDestination is "Local" or "Both")
            _logger.Write(entry);

        if (settings.LogDestination is "Docker" or "Both")
            _dockerLog?.Send(entry);
    }

    // Runs the CryptoSoft XOR encryption on the copied file and returns the elapsed
    // time in milliseconds. Returns -99 on failure.
    private static long RunCryptoSoft(string filePath, string key)
    {
        try
        {
            var fileManager = new FileManager(filePath, key);
            return fileManager.TransformFile();
        }
        catch { return -99; }
    }

    // Converts a local path to a UNC path (\\MachineName\C$\...) so that log entries
    // are machine-agnostic and unambiguous when read on a different host.
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
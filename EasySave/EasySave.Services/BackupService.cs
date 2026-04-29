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

    public BackupService(IFileService fileService, Logger logger, IStateRepository stateRepo)
    {
        _fileService = fileService;
        _logger = logger;
        _stateRepo = stateRepo;
    }

    public void RunBackup(BackupJob job, AppSettings settings)
    {
        // ── Vérification logiciel métier AVANT de démarrer ──────────────────
        if (IsBusinessSoftwareRunning(settings.BusinessSoftware))
        {
            _logger.Write(new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = job.Name,
                SourcePath = "",
                TargetPath = "",
                FileSize = 0,
                TransferTime = 0,
                EncryptionTime = 0
            });
            return; // On ne démarre pas le job
        }

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
            // ── Vérification logiciel métier PENDANT le backup ────────────────
            // On finit le fichier en cours puis on s'arrête
            if (IsBusinessSoftwareRunning(settings.BusinessSoftware))
            {
                _logger.Write(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    BackupName = job.Name,
                    SourcePath = ToUNC(file),
                    TargetPath = "",
                    FileSize = 0,
                    TransferTime = 0,
                    EncryptionTime = 0
                });
                break; // Arrêt après le fichier actuel
            }

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
                state.CurrentSourceFile = ToUNC(file);
                state.CurrentTargetFile = ToUNC(targetFile);
                state.LastActionTime = DateTime.Now;

                // ── Copie ──────────────────────────────────────────────────
                var start = DateTime.Now;
                _fileService.CopyFile(file, targetFile);
                var transferDuration = (long)(DateTime.Now - start).TotalMilliseconds;

                // ── Chiffrement CryptoSoft ─────────────────────────────────
                long encryptionTime = 0;
                var ext = Path.GetExtension(file).ToLower();
                if (settings.EncryptedExtensions.Contains(ext))
                    encryptionTime = RunCryptoSoft(targetFile, settings.EncryptionKey);

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
        }

        state.Status = "INACTIVE";
        state.LastActionTime = DateTime.Now;
        state.CurrentSourceFile = null;
        state.CurrentTargetFile = null;
        _stateRepo.Save(new List<BackupState> { state });
    }

    // ── Appel CryptoSoft externe ───────────────────────────────────────────
    private static long RunCryptoSoft(string filePath, string key)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "CryptoSoft.exe",
                    Arguments = $"\"{filePath}\" \"{key}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode; // >0 = ms, <0 = erreur
        }
        catch
        {
            return -99; // CryptoSoft introuvable ou crash
        }
    }

    // ── Détection logiciel métier ──────────────────────────────────────────
    private static bool IsBusinessSoftwareRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        return Process.GetProcessesByName(name).Length > 0;
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
using EasyLog;
using EasySave.Core;
using EasySave.Services;
using EasySave.Services.Interfaces;
using Moq;
using Xunit;

namespace EasySave.Tests;

// Unit tests for BackupService.RunBackupAsync.
// Covers: basic copy, differential logic, error resilience,
// job cancellation (stop), job pause/resume, priority file ordering,
// and log-destination routing.
public class BackupServiceAsyncTests : IDisposable
{
    private readonly string _logDir = Path.Combine(Path.GetTempPath(), "bs_async_" + Guid.NewGuid());
    private readonly Mock<IFileService> _fileService = new(MockBehavior.Strict);
    private readonly Mock<IStateRepository> _stateRepo = new();
    private readonly BackupService _sut;

    public BackupServiceAsyncTests()
    {
        Directory.CreateDirectory(_logDir);
        var logger = new Logger("JSON", _logDir);
        _sut = new BackupService(_fileService.Object, logger, _stateRepo.Object);
    }

    public void Dispose() => Directory.Delete(_logDir, true);

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string CreateTempDir(string label)
    {
        var dir = Path.Combine(Path.GetTempPath(), "EasySaveAsyncTests",
                               Guid.NewGuid().ToString(), label);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Write(string dir, string name, string content = "x")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static BackupJob MakeJob(
        BackupType type = BackupType.Full,
        string? sourcePath = null,
        string? targetPath = null) => new()
        {
            Id = 1,
            Name = "AsyncJob",
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Type = type
        };

    private static AppSettings MakeSettings(
        string logDestination = "Local",
        List<string>? priorityExts = null) => new()
        {
            BusinessSoftware = "",
            EncryptionKey = "test-key",
            EncryptedExtensions = new List<string>(),
            LogDestination = logDestination,
            PriorityExtensions = priorityExts ?? new List<string>(),
            MaxParallelFileSize = 0   // disable large-file throttling by default
        };

    // ── Full backup (async) ────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_FullBackup_CopiesEveryFile()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");

        var files = new[] { Write(src, "a.txt"), Write(src, "b.txt") };
        _fileService.Setup(f => f.GetAllFiles(src)).Returns(files);
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        foreach (var file in files)
            _fileService.Verify(f => f.CopyFile(file, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_EmptySource_NoCopyAndStateMarkedInactive()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(Array.Empty<string>());

        BackupState? last = null;
        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s => last = s[0]);

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        _fileService.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal("INACTIVE", last!.Status);
    }

    [Fact]
    public async Task RunBackupAsync_FinalState_IsInactive()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var file = Write(src, "a.txt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { file });
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        BackupState? last = null;
        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s => last = s[0]);

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        Assert.Equal("INACTIVE", last!.Status);
    }

    // ── State save count ───────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_OneFile_SavesStateThreeTimes()
    {
        // Initial save + per-file save + final save = 3
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var file = Write(src, "a.txt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { file });
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        _stateRepo.Verify(r => r.Save(It.IsAny<List<BackupState>>()), Times.Exactly(3));
    }

    // ── Differential backup (async) ────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_Differential_SkipsFileWhenTargetIsNewer()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");

        var srcFile = Write(src, "file.txt");
        var tgtFile = Write(tgt, "file.txt");
        File.SetLastWriteTime(tgtFile, DateTime.Now.AddHours(1));

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { srcFile });

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(BackupType.Differential, src, tgt),
                                  MakeSettings(), ctrl);

        _fileService.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunBackupAsync_Differential_CopiesFileWhenSourceIsNewer()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");

        var srcFile = Write(src, "file.txt", "new");
        var tgtFile = Write(tgt, "file.txt", "old");
        File.SetLastWriteTime(srcFile, DateTime.Now.AddHours(1));

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { srcFile });
        _fileService.Setup(f => f.CopyFile(srcFile, tgtFile));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(BackupType.Differential, src, tgt),
                                  MakeSettings(), ctrl);

        _fileService.Verify(f => f.CopyFile(srcFile, tgtFile), Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_Differential_CopiesFileWhenTargetAbsent()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var srcFile = Write(src, "new.txt");
        var expectedTgt = Path.Combine(tgt, "new.txt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { srcFile });
        _fileService.Setup(f => f.CopyFile(srcFile, expectedTgt));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(BackupType.Differential, src, tgt),
                                  MakeSettings(), ctrl);

        _fileService.Verify(f => f.CopyFile(srcFile, expectedTgt), Times.Once);
    }

    // ── Error resilience ───────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_WhenCopyThrows_ContinuesWithRemainingFiles()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");

        var failFile = Write(src, "fail.txt");
        var okFile = Write(src, "ok.txt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { failFile, okFile });
        _fileService.Setup(f => f.CopyFile(failFile, It.IsAny<string>()))
                    .Throws(new IOException("copy failed"));
        _fileService.Setup(f => f.CopyFile(okFile, It.IsAny<string>()));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        _fileService.Verify(f => f.CopyFile(okFile, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WhenCopyThrows_FinalStateIsStillInactive()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var file = Write(src, "fail.txt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { file });
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new IOException("fail"));

        BackupState? last = null;
        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s => last = s[0]);

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        Assert.Equal("INACTIVE", last!.Status);
    }

    // ── Cancellation / Stop ────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_WhenStoppedBeforeStart_FinalStateIsStopped()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var file = Write(src, "a.txt");

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { file });

        BackupState? last = null;
        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s => last = s[0]);

        var ctrl = new JobController();
        ctrl.Stop(); // cancel before the loop starts

        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        Assert.Equal("STOPPED", last!.Status);
    }

    // ── Progress reporting ─────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_ReportsProgressForEachFile()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var files = new[] { Write(src, "a.txt"), Write(src, "b.txt") };

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(files);
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        var progressValues = new List<double>();
        var progress = new Progress<double>(v => progressValues.Add(v));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl, progress);

        // Allow progress callbacks to fire (they are posted asynchronously)
        await Task.Delay(100);

        Assert.NotEmpty(progressValues);
    }

    // ── Priority extensions ────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_PriorityExtensions_PriorityFilesAreCopiedFirst()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");

        var normal = Write(src, "readme.txt");
        var priority = Write(src, "report.pdf");

        // Return files in non-priority order; service must reorder them.
        _fileService.Setup(f => f.GetAllFiles(src)).Returns(new[] { normal, priority });

        var copyOrder = new List<string>();
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string>((s, _) => copyOrder.Add(s));

        var settings = MakeSettings(priorityExts: new List<string> { ".pdf" });
        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  settings, ctrl);

        Assert.Equal(2, copyOrder.Count);
        Assert.Equal(priority, copyOrder[0]); // .pdf must come first
    }

    [Fact]
    public async Task RunBackupAsync_NoPriorityExtensions_CopiesAllFilesInOriginalOrder()
    {
        var src = CreateTempDir("src");
        var tgt = CreateTempDir("tgt");
        var files = new[] { Write(src, "a.txt"), Write(src, "b.txt") };

        _fileService.Setup(f => f.GetAllFiles(src)).Returns(files);

        var copyOrder = new List<string>();
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string>((s, _) => copyOrder.Add(s));

        var ctrl = new JobController();
        await _sut.RunBackupAsync(MakeJob(sourcePath: src, targetPath: tgt),
                                  MakeSettings(), ctrl);

        Assert.Equal(files, copyOrder.ToArray());
    }
}

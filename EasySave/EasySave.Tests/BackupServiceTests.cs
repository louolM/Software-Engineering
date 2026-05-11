using EasyLog;
using EasySave.Core;
using EasySave.Services;
using EasySave.Services.Interfaces;
using Moq;
using Xunit;

namespace EasySave.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _logDir = Path.Combine(Path.GetTempPath(), "bs_tests_" + Guid.NewGuid());
    private readonly List<string> _tempDirs = new();

    private readonly Mock<IFileService> _fileService = new(MockBehavior.Strict);
    private readonly Mock<IStateRepository> _stateRepo = new();

    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        Directory.CreateDirectory(_logDir);

        var logger = new Logger("JSON", _logDir);

        _sut = new BackupService(_fileService.Object, logger, _stateRepo.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDir))
            Directory.Delete(_logDir, true);

        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private string CreateTempDir(string name)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "EasySaveTests",
            Guid.NewGuid().ToString(),
            name);

        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static BackupJob MakeJob(
        BackupType type = BackupType.Full,
        string sourcePath = null!,
        string targetPath = null!) => new()
        {
            Id = 1,
            Name = "TestJob",
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Type = type
        };

    private static AppSettings MakeSettings() => new()
    {
        BusinessSoftware = "",
        EncryptionKey = "test-key",
        EncryptedExtensions = new List<string>(),
        PriorityExtensions = new List<string>(),
        MaxParallelFileSize = 0,
        LogDestination = "Local"
    };

    private static JobController MakeController() => new();

    private static string Write(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);

        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);

        File.WriteAllText(path, content);
        return path;
    }

    // ── Full backup ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_FullBackup_CopiesEveryFile()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var files = new[]
        {
            Write(srcDir, "a.txt", "a"),
            Write(srcDir, "b.txt", "b")
        };

        _fileService.Setup(f => f.GetAllFiles(srcDir))
            .Returns(files);

        _fileService.Setup(f =>
            f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        await _sut.RunBackupAsync(
            MakeJob(sourcePath: srcDir, targetPath: tgtDir),
            MakeSettings(),
            MakeController());

        foreach (var file in files)
        {
            _fileService.Verify(
                f => f.CopyFile(file, It.IsAny<string>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task RunBackupAsync_FullBackup_SavesState_InitialPlusPerFilePlusFinal()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var files = new[]
        {
            Write(srcDir, "a.txt", "a")
 };

        _fileService.Setup(f => f.GetAllFiles(srcDir)).Returns(files);
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        await _sut.RunBackupAsync(
            MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings(), MakeController());

        _stateRepo.Verify(
            r => r.Save(It.IsAny<List<BackupState>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RunBackupAsync_FullBackup_FinalStateIsInactive()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var file = Write(srcDir, "a.txt", "a");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { file });

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        BackupState? last = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
        .Callback<List<BackupState>>(s => last = s[0]);

        await _sut.RunBackupAsync(
            MakeJob(sourcePath: srcDir, targetPath: tgtDir),
            MakeSettings(),
            MakeController());

        Assert.NotNull(last);
        Assert.Equal("INACTIVE", last!.Status);
    }

    [Fact]
    public async Task RunBackupAsync_EmptySource_NoCopyAndTwoStateSaves()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(Array.Empty<string>());

        await _sut.RunBackupAsync(
            MakeJob(sourcePath: srcDir, targetPath: tgtDir),
            MakeSettings(),
            MakeController());

        _fileService.Verify(
            f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        _stateRepo.Verify(
            r => r.Save(It.IsAny<List<BackupState>>()),
            Times.Exactly(2));
    }

    // ── Differential backup ────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_Differential_SkipsFileWhenTargetIsNewer()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var srcFile = Write(srcDir, "file.txt", "hello");
        var tgtFile = Write(tgtDir, "file.txt", "hello");

        File.SetLastWriteTimeUtc(
            tgtFile, DateTime.UtcNow.AddHours(1));

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { srcFile });

        await _sut.RunBackupAsync(
            MakeJob(BackupType.Differential, srcDir, tgtDir),
            MakeSettings(),
            MakeController());

        _fileService.Verify(
            f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RunBackupAsync_Differential_CopiesFileWhenSourceIsNewer()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var srcFile = Write(srcDir, "file.txt", "new");
        var tgtFile = Write(tgtDir, "file.txt", "old");

        File.SetLastWriteTimeUtc(
            srcFile,
            DateTime.UtcNow.AddHours(1));

        _fileService.Setup(f => f.GetAllFiles(srcDir))
            .Returns(new[] { srcFile });

        _fileService.Setup(f =>
            f.CopyFile(srcFile, tgtFile));

        await _sut.RunBackupAsync(
            MakeJob(BackupType.Differential, srcDir, tgtDir),
            MakeSettings(),
            MakeController());

        _fileService.Verify(
            f => f.CopyFile(srcFile, tgtFile),
            Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_Differential_CopiesFileWhenTargetDoesNotExist()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var srcFile = Write(srcDir, "new.txt", "content");
        var tgtFile = Path.Combine(tgtDir, "new.txt");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
            .Returns(new[] { srcFile });

        _fileService.Setup(f => f.CopyFile(srcFile, tgtFile));

        await _sut.RunBackupAsync(
            MakeJob(BackupType.Differential, srcDir, tgtDir),
            MakeSettings(),
            MakeController());

        _fileService.Verify(
            f => f.CopyFile(srcFile, tgtFile),
            Times.Once);
    }

    // ── Error resilience ───────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_WhenCopyThrows_ContinuesWithRemainingFiles()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var failFile = Write(srcDir, "fail.txt", "x");
        var okFile = Write(srcDir, "ok.txt", "x");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { failFile, okFile });

        _fileService.Setup(f => f.CopyFile(failFile, It.IsAny<string>()))
        .Throws(new IOException("fail"));

        _fileService.Setup(f => f.CopyFile(okFile, It.IsAny<string>()));

        await _sut.RunBackupAsync(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings(), MakeController());

        _fileService.Verify(
            f => f.CopyFile(okFile, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WhenCopyThrows_FinalStateIsStillInactive()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var file = Write(srcDir, "fail.txt", "x");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { file });

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
        .Throws(new IOException("fail"));

        BackupState? last = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
        .Callback<List<BackupState>>(s => last = s[0]);

        await _sut.RunBackupAsync(
            MakeJob(sourcePath: srcDir, targetPath: tgtDir),
            MakeSettings(),
            MakeController());

        Assert.NotNull(last);
        Assert.Equal("INACTIVE", last!.Status);
    }

    // ── Controller behavior ────────────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_WhenStopped_FinalStateIsStopped()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var file = Write(srcDir, "a.txt", "a");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
            .Returns(new[] { file });

        var controller = MakeController();
        controller.Stop();

        BackupState? last = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
            .Callback<List<BackupState>>(s => last = s[0]);

        await _sut.RunBackupAsync(
            MakeJob(sourcePath: srcDir, targetPath: tgtDir),
            MakeSettings(),
            controller);

        Assert.NotNull(last);
        Assert.Equal("STOPPED", last!.Status);
    }
}
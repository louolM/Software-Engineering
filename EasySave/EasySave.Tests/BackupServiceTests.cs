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

    private readonly Mock<IFileService> _fileService = new(MockBehavior.Strict);
    private readonly Mock<IStateRepository> _stateRepo = new();

    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        Directory.CreateDirectory(_logDir);

        var logger = new Logger("JSON", _logDir);

        _sut = new BackupService(_fileService.Object, logger, _stateRepo.Object);
    }

    public void Dispose() => Directory.Delete(_logDir, true);

    // ── helpers ────────────────────────────────────────────────────────────

    private static string CreateTempDir(string name)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
                               "EasySaveTests",
                               Guid.NewGuid().ToString(),
                               name);

        Directory.CreateDirectory(dir);
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
        EncryptedExtensions = new List<string>()
    };

    private static string Write(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void Cleanup(params string[] dirs)
    {
        foreach (var d in dirs)
        {
            if (Directory.Exists(d))
                Directory.Delete(d, true);
        }
    }

    // ── Full backup ────────────────────────────────────────────────────────

    [Fact]
    public void RunBackup_FullBackup_CopiesEveryFile()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var files = new[]
        {
            Write(srcDir, "a.txt", "a"),
            Write(srcDir, "b.txt", "b")
        };

        _fileService.Setup(f => f.GetAllFiles(srcDir)).Returns(files);
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        foreach (var file in files)
        {
            _fileService.Verify(
                f => f.CopyFile(file, It.IsAny<string>()),
                Times.Once);
        }
    }

    [Fact]
    public void RunBackup_FullBackup_SavesState_InitialPlusPerFilePlusFinal()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var files = new[]
        {
            Write(srcDir, "a.txt", "a"),
 };

        _fileService.Setup(f => f.GetAllFiles(srcDir)).Returns(files);
        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        _stateRepo.Verify(
            r => r.Save(It.IsAny<List<BackupState>>()),
            Times.Exactly(4));
    }

    [Fact]
    public void RunBackup_FullBackup_FinalStateIsInactive()
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

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        Assert.NotNull(last);
        Assert.Equal("INACTIVE", last!.Status);
    }

    [Fact]
    public void RunBackup_EmptySource_NoCopyAndTwoStateSaves()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(Array.Empty<string>());

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        _fileService.Verify(
            f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        _stateRepo.Verify(
            r => r.Save(It.IsAny<List<BackupState>>()),
            Times.Exactly(2));
    }

    // ── Differential backup ────────────────────────────────────────────────

    [Fact]
    public void RunBackup_Differential_SkipsFileWhenTargetIsNewer()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var srcFile = Write(srcDir, "file.txt", "hello");
        var tgtFile = Write(tgtDir, "file.txt", "hello");

        File.SetLastWriteTime(tgtFile, DateTime.Now.AddHours(1));

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { srcFile });

        _sut.RunBackup(MakeJob(BackupType.Differential, srcDir, tgtDir), MakeSettings());

        _fileService.Verify(
            f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
    }

    [Fact]
    public void RunBackup_Differential_CopiesFileWhenSourceIsNewer()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var srcFile = Write(srcDir, "file.txt", "new");
        var tgtFile = Write(tgtDir, "file.txt", "old");

        File.SetLastWriteTime(srcFile, DateTime.Now.AddHours(1));

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { srcFile });

        _fileService.Setup(f => f.CopyFile(srcFile, tgtFile));

        _sut.RunBackup(MakeJob(BackupType.Differential, srcDir, tgtDir), MakeSettings());

        _fileService.Verify(f => f.CopyFile(srcFile, tgtFile), Times.Once);
    }

    [Fact]
    public void RunBackup_Differential_CopiesFileWhenTargetDoesNotExist()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var srcFile = Write(srcDir, "new.txt", "content");
        var tgtFile = Path.Combine(tgtDir, "new.txt");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { srcFile });

        _fileService.Setup(f => f.CopyFile(srcFile, tgtFile));

        _sut.RunBackup(MakeJob(BackupType.Differential, srcDir, tgtDir), MakeSettings());

        _fileService.Verify(f => f.CopyFile(srcFile, tgtFile), Times.Once);
    }

    // ── Error resilience ───────────────────────────────────────────────────

    [Fact]
    public void RunBackup_WhenCopyThrows_ContinuesWithRemainingFiles()
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

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        _fileService.Verify(
            f => f.CopyFile(okFile, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void RunBackup_WhenCopyThrows_FinalStateIsStillInactive()
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

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        Assert.Equal("INACTIVE", last!.Status);
    }

    // ── UNC path test ──────────────────────────────────────────────────────

    [Fact]
    public void RunBackup_StateAndLog_UseUNCPaths()
    {
        var srcDir = CreateTempDir("source");
        var tgtDir = CreateTempDir("target");

        var file = Write(srcDir, "a.txt", "x");

        _fileService.Setup(f => f.GetAllFiles(srcDir))
        .Returns(new[] { file });

        BackupState? captured = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
        .Callback<List<BackupState>>(s =>
        {
            if (s[0].CurrentSourceFile != null)
                captured = s[0];
        });

        _sut.RunBackup(MakeJob(sourcePath: srcDir, targetPath: tgtDir), MakeSettings());

        Assert.NotNull(captured?.CurrentSourceFile);
        Assert.StartsWith(@"\\", captured!.CurrentSourceFile);
    }
}

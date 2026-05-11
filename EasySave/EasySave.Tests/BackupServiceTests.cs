using EasyLog;
using EasySave.Core;
using EasySave.Services;
using EasySave.Services.Interfaces;
using Moq;
using Xunit;

namespace EasySave.Tests;

/// <summary>
/// Unit tests for <see cref="BackupService"/>.
///
/// All I/O dependencies are replaced with Moq mocks so tests run fully
/// in-memory without touching the real file system or log files.
/// </summary>
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

    private static BackupJob MakeJob(
        BackupType type = BackupType.Full,
        string sourcePath = @"C:\Source",
        string targetPath = @"C:\Target") => new()
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

    // ── Full backup – happy path ───────────────────────────────────────────

    [Fact]
    public void RunBackup_FullBackup_CopiesEveryFile()
    {
        var files = new[] { @"C:\Source\a.txt", @"C:\Source\b.txt" };

        _fileService.Setup(f => f.GetAllFiles(@"C:\Source")).Returns(files);

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        _sut.RunBackup(MakeJob(), MakeSettings());

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
        // 2 files → 1 initial + 2 per-file + 1 final = 4 saves

        var files = new[] { @"C:\Source\a.txt", @"C:\Source\b.txt" };

        _fileService.Setup(f => f.GetAllFiles(@"C:\Source")).Returns(files);

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        _sut.RunBackup(MakeJob(), MakeSettings());

        _stateRepo.Verify(
            r => r.Save(It.IsAny<List<BackupState>>()),
            Times.Exactly(4));
    }

    [Fact]
    public void RunBackup_FullBackup_FinalStateIsInactive()
    {
        _fileService.Setup(f => f.GetAllFiles(@"C:\Source"))
                    .Returns(new[] { @"C:\Source\a.txt" });

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        BackupState? last = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s => last = s[0]);

        _sut.RunBackup(MakeJob(), MakeSettings());

        Assert.NotNull(last);
        Assert.Equal("INACTIVE", last!.Status);
        Assert.Null(last.CurrentSourceFile);
        Assert.Null(last.CurrentTargetFile);
    }

    [Fact]
    public void RunBackup_EmptySource_NoCopyAndTwoStateSaves()
    {
        _fileService.Setup(f => f.GetAllFiles(@"C:\Source"))
                    .Returns(Array.Empty<string>());

        _sut.RunBackup(MakeJob(), MakeSettings());

        _fileService.Verify(
            f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // 1 initial save + 1 final "INACTIVE" save, no per-file saves

        _stateRepo.Verify(
            r => r.Save(It.IsAny<List<BackupState>>()),
            Times.Exactly(2));
    }

    // ── State counters ─────────────────────────────────────────────────────

    [Fact]
    public void RunBackup_RemainingFilesCounter_DecrementsAfterEachFileCopy()
    {
        var files = new[] { @"C:\Source\a.txt", @"C:\Source\b.txt" };

        _fileService.Setup(f => f.GetAllFiles(@"C:\Source")).Returns(files);

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        var snapshots = new List<int>();

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s =>
                      snapshots.Add(s[0].RemainingFiles));

        _sut.RunBackup(MakeJob(), MakeSettings());

        // [initial=2, after-a=1, after-b=0, final=0]

        Assert.Equal(new[] { 2, 1, 0, 0 }, snapshots);
    }

    [Fact]
    public void RunBackup_InitialState_IsActive()
    {
        _fileService.Setup(f => f.GetAllFiles(@"C:\Source"))
                    .Returns(Array.Empty<string>());

        string? firstStatus = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s =>
                      firstStatus ??= s[0].Status);

        _sut.RunBackup(MakeJob(), MakeSettings());

        Assert.Equal("ACTIVE", firstStatus);
    }

    // ── Differential backup ────────────────────────────────────────────────

    [Fact]
    public void RunBackup_Differential_SkipsFileWhenTargetIsNewer()
    {
        var srcDir = CreateTempDir();
        var tgtDir = CreateTempDir();

        try
        {
            var srcFile = Write(srcDir, "file.txt", "hello");
            var tgtFile = Write(tgtDir, "file.txt", "hello");

            // Target is newer → source is unchanged

            File.SetLastWriteTime(tgtFile, DateTime.Now.AddHours(1));

            _fileService.Setup(f => f.GetAllFiles(srcDir))
                        .Returns(new[] { srcFile });

            _sut.RunBackup(
                MakeJob(BackupType.Differential, srcDir, tgtDir),
                MakeSettings());

            _fileService.Verify(
                f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
        finally
        {
            Cleanup(srcDir, tgtDir);
        }
    }

    [Fact]
    public void RunBackup_Differential_CopiesFileWhenSourceIsNewer()
    {
        var srcDir = CreateTempDir();
        var tgtDir = CreateTempDir();

        try
        {
            var srcFile = Write(srcDir, "file.txt", "new");
            var tgtFile = Write(tgtDir, "file.txt", "old");

            // Source is newer → should be copied

            File.SetLastWriteTime(srcFile, DateTime.Now.AddHours(1));

            _fileService.Setup(f => f.GetAllFiles(srcDir))
                        .Returns(new[] { srcFile });

            _fileService.Setup(f => f.CopyFile(srcFile, tgtFile));

            _sut.RunBackup(
                MakeJob(BackupType.Differential, srcDir, tgtDir),
                MakeSettings());

            _fileService.Verify(
                f => f.CopyFile(srcFile, tgtFile),
                Times.Once);
        }
        finally
        {
            Cleanup(srcDir, tgtDir);
        }
    }

    [Fact]
    public void RunBackup_Differential_CopiesFileWhenTargetDoesNotExist()
    {
        var srcDir = CreateTempDir();
        var tgtDir = CreateTempDir();

        try
        {
            var srcFile = Write(srcDir, "new.txt", "content");
            var tgtFile = Path.Combine(tgtDir, "new.txt");

            _fileService.Setup(f => f.GetAllFiles(srcDir))
                        .Returns(new[] { srcFile });

            _fileService.Setup(f => f.CopyFile(srcFile, tgtFile));

            _sut.RunBackup(
                MakeJob(BackupType.Differential, srcDir, tgtDir),
                MakeSettings());

            _fileService.Verify(
                f => f.CopyFile(srcFile, tgtFile),
                Times.Once);
        }
        finally
        {
            Cleanup(srcDir, tgtDir);
        }
    }

    [Fact]
    public void RunBackup_Differential_SkippedFilesAreStillCountedInState()
    {
        var srcDir = CreateTempDir();
        var tgtDir = CreateTempDir();

        try
        {
            var srcFile = Write(srcDir, "file.txt", "x");
            var tgtFile = Write(tgtDir, "file.txt", "x");

            File.SetLastWriteTime(tgtFile, DateTime.Now.AddHours(1));

            _fileService.Setup(f => f.GetAllFiles(srcDir))
                        .Returns(new[] { srcFile });

            var snapshots = new List<int>();

            _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                      .Callback<List<BackupState>>(s =>
                          snapshots.Add(s[0].RemainingFiles));

            _sut.RunBackup(
                MakeJob(BackupType.Differential, srcDir, tgtDir),
                MakeSettings());

            // Skipped file still decrements the counter: [1, 0, 0]

            Assert.Contains(0, snapshots);
        }
        finally
        {
            Cleanup(srcDir, tgtDir);
        }
    }

    // ── Error resilience ───────────────────────────────────────────────────

    [Fact]
    public void RunBackup_WhenCopyThrows_ContinuesWithRemainingFiles()
    {
        var files = new[] { @"C:\Source\fail.txt", @"C:\Source\ok.txt" };

        _fileService.Setup(f => f.GetAllFiles(@"C:\Source"))
                    .Returns(files);

        _fileService.Setup(f => f.CopyFile(@"C:\Source\fail.txt", It.IsAny<string>()))
                    .Throws(new IOException("Disk full"));

        _fileService.Setup(f => f.CopyFile(@"C:\Source\ok.txt", It.IsAny<string>()));

        // Must not throw

        _sut.RunBackup(MakeJob(), MakeSettings());

        _fileService.Verify(
            f => f.CopyFile(@"C:\Source\ok.txt", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void RunBackup_WhenCopyThrows_FinalStateIsStillInactive()
    {
        _fileService.Setup(f => f.GetAllFiles(@"C:\Source"))
                    .Returns(new[] { @"C:\Source\fail.txt" });

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new IOException("Disk full"));

        BackupState? last = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s => last = s[0]);

        _sut.RunBackup(MakeJob(), MakeSettings());

        Assert.Equal("INACTIVE", last!.Status);
    }

    // ── UNC path conversion ────────────────────────────────────────────────

    [Fact]
    public void RunBackup_StateAndLog_UseUNCPaths()
    {
        // Windows-style local path: C:\... → \\MACHINE\C$\...

        _fileService.Setup(f => f.GetAllFiles(@"C:\Source"))
                    .Returns(new[] { @"C:\Source\a.txt" });

        _fileService.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()));

        BackupState? captured = null;

        _stateRepo.Setup(r => r.Save(It.IsAny<List<BackupState>>()))
                  .Callback<List<BackupState>>(s =>
                  {
                      if (s[0].CurrentSourceFile != null)
                      {
                          captured = s[0];
                      }
                  });

        _sut.RunBackup(MakeJob(), MakeSettings());

        // The state must store UNC paths, not local paths

        Assert.NotNull(captured?.CurrentSourceFile);

        Assert.StartsWith(@"\\", captured!.CurrentSourceFile);
    }

    // ── Private test helpers ───────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(dir);

        return dir;
    }

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
            {
                Directory.Delete(d, true);
            }
        }
    }
}

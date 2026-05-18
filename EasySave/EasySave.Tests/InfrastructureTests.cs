using EasySave.Core;
using EasySave.Infrastructure;
using Xunit;

namespace EasySave.Tests;

// Unit tests for StateRepository.
// StateRepository hard-codes "state.json" relative to CWD, so we redirect CWD per test.
public class StateRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "staterepo_" + Guid.NewGuid());
    private readonly string _statePath;
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public StateRepositoryTests()
    {
        Directory.CreateDirectory(_dir);
        Directory.SetCurrentDirectory(_dir);
        _statePath = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Directory.Delete(_dir, true);
    }

    private static BackupState MakeState(string name = "Job1", string status = "ACTIVE") => new()
    {
        Name = name,
        Status = status,
        TotalFiles = 10,
        RemainingFiles = 5,
        LastActionTime = DateTime.Now
    };

    // ── Save ───────────────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesStateJsonFile()
    {
        var repo = new StateRepository();
        repo.Save(new List<BackupState> { MakeState() });

        Assert.True(File.Exists(_statePath));
    }

    [Fact]
    public void Save_WritesValidJsonArray()
    {
        var repo = new StateRepository();
        repo.Save(new List<BackupState> { MakeState("A"), MakeState("B") });

        var json = File.ReadAllText(_statePath).Trim();
        Assert.StartsWith("[", json);
        Assert.EndsWith("]", json);
    }

    [Fact]
    public void Save_ContainsJobNamesAndStatuses()
    {
        var repo = new StateRepository();
        repo.Save(new List<BackupState>
        {
            MakeState("JobAlpha", "ACTIVE"),
            MakeState("JobBeta", "INACTIVE")
        });

        var json = File.ReadAllText(_statePath);
        Assert.Contains("JobAlpha", json);
        Assert.Contains("JobBeta", json);
        Assert.Contains("ACTIVE", json);
        Assert.Contains("INACTIVE", json);
    }

    [Fact]
    public void Save_OverwritesPreviousState()
    {
        var repo = new StateRepository();
        repo.Save(new List<BackupState> { MakeState("OldJob") });
        repo.Save(new List<BackupState> { MakeState("NewJob") });

        var json = File.ReadAllText(_statePath);
        Assert.DoesNotContain("OldJob", json);
        Assert.Contains("NewJob", json);
    }

    [Fact]
    public void Save_EmptyList_WritesEmptyJsonArray()
    {
        var repo = new StateRepository();
        repo.Save(new List<BackupState>());

        var json = File.ReadAllText(_statePath).Trim();
        Assert.Equal("[]", json);
    }
}

// Unit tests for FileService.
public class FileServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fileservice_" + Guid.NewGuid());
    private readonly FileService _sut = new();

    public FileServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private string TempDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Write(string dir, string name, string content = "data")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // ── GetAllFiles ────────────────────────────────────────────────────────

    [Fact]
    public void GetAllFiles_FlatDirectory_ReturnsAllFiles()
    {
        var src = TempDir("flat_src");
        Write(src, "a.txt");
        Write(src, "b.txt");

        var files = _sut.GetAllFiles(src).ToList();

        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void GetAllFiles_NestedSubdirectories_ReturnsAllFilesRecursively()
    {
        var src = TempDir("nested_src");
        var sub = Path.Combine(src, "sub");
        Directory.CreateDirectory(sub);
        Write(src, "root.txt");
        Write(sub, "child.txt");

        var files = _sut.GetAllFiles(src).ToList();

        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void GetAllFiles_EmptyDirectory_ReturnsEmptyEnumerable()
    {
        var src = TempDir("empty_src");

        var files = _sut.GetAllFiles(src).ToList();

        Assert.Empty(files);
    }

    [Fact]
    public void GetAllFiles_ReturnsAbsolutePaths()
    {
        var src = TempDir("abs_src");
        Write(src, "file.txt");

        var file = _sut.GetAllFiles(src).Single();

        Assert.True(Path.IsPathRooted(file));
    }

    // ── CopyFile ───────────────────────────────────────────────────────────

    [Fact]
    public void CopyFile_TargetFileIsCreated()
    {
        var src = TempDir("copy_src");
        var tgt = TempDir("copy_tgt");

        var srcFile = Write(src, "hello.txt", "hello");
        var tgtFile = Path.Combine(tgt, "hello.txt");

        _sut.CopyFile(srcFile, tgtFile);

        Assert.True(File.Exists(tgtFile));
    }

    [Fact]
    public void CopyFile_TargetContentsMatchSource()
    {
        var src = TempDir("content_src");
        var tgt = TempDir("content_tgt");

        var srcFile = Write(src, "data.txt", "unique-content-123");
        var tgtFile = Path.Combine(tgt, "data.txt");

        _sut.CopyFile(srcFile, tgtFile);

        Assert.Equal("unique-content-123", File.ReadAllText(tgtFile));
    }

    [Fact]
    public void CopyFile_OverwritesExistingTargetFile()
    {
        var src = TempDir("overwrite_src");
        var tgt = TempDir("overwrite_tgt");

        var srcFile = Write(src, "f.txt", "new");
        var tgtFile = Write(tgt, "f.txt", "old");

        _sut.CopyFile(srcFile, tgtFile);

        Assert.Equal("new", File.ReadAllText(tgtFile));
    }

    [Fact]
    public void CopyFile_CreatesIntermediateDirectoriesAutomatically()
    {
        var src = TempDir("deep_src");
        var srcFile = Write(src, "file.txt", "hi");

        // Target is in a subdirectory that does not exist yet
        var tgtFile = Path.Combine(_root, "new_parent", "sub", "file.txt");

        _sut.CopyFile(srcFile, tgtFile);

        Assert.True(File.Exists(tgtFile));
    }
}

using EasySave.Core;
using EasySave.Infrastructure;
using Xunit;

namespace EasySave.Tests;

// Unit tests for ConfigRepository.
// Each test runs in its own isolated temp directory with a fresh config.json path
// so tests can run in parallel without touching each other or the real config.json.
public class ConfigRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "configrepo_" + Guid.NewGuid());
    private readonly string _configPath;

    // ConfigRepository hard-codes "config.json" relative to the working directory,
    // so we change the working directory to the temp directory for every test.
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public ConfigRepositoryTests()
    {
        Directory.CreateDirectory(_dir);
        Directory.SetCurrentDirectory(_dir);
        _configPath = Path.Combine(_dir, "config.json");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Directory.Delete(_dir, true);
    }

    private static BackupJob MakeJob(int id = 1, string name = "Job1") => new()
    {
        Id = id,
        Name = name,
        SourcePath = @"C:\src",
        TargetPath = @"C:\tgt",
        Type = BackupType.Full
    };

    // ── Load ───────────────────────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var repo = new ConfigRepository();
        var result = repo.Load();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Load_AfterSave_ReturnsSameJobs()
    {
        var repo = new ConfigRepository();
        var jobs = new List<BackupJob> { MakeJob(1, "A"), MakeJob(2, "B") };

        repo.Save(jobs);
        var loaded = repo.Load();

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, j => j.Name == "A");
        Assert.Contains(loaded, j => j.Name == "B");
    }

    [Fact]
    public void Load_PreservesAllJobFields()
    {
        var repo = new ConfigRepository();
        var job = new BackupJob
        {
            Id = 42,
            Name = "MyJob",
            SourcePath = @"C:\source",
            TargetPath = @"D:\target",
            Type = BackupType.Differential
        };

        repo.Save(new List<BackupJob> { job });
        var loaded = repo.Load()[0];

        Assert.Equal(42, loaded.Id);
        Assert.Equal("MyJob", loaded.Name);
        Assert.Equal(@"C:\source", loaded.SourcePath);
        Assert.Equal(@"D:\target", loaded.TargetPath);
        Assert.Equal(BackupType.Differential, loaded.Type);
    }

    [Fact]
    public void Load_EmptyJsonArray_ReturnsEmptyList()
    {
        File.WriteAllText(_configPath, "[]");

        var repo = new ConfigRepository();
        var result = repo.Load();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── Save ───────────────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesConfigJsonFile()
    {
        var repo = new ConfigRepository();
        repo.Save(new List<BackupJob> { MakeJob() });

        Assert.True(File.Exists(_configPath));
    }

    [Fact]
    public void Save_OverwritesPreviousContent()
    {
        var repo = new ConfigRepository();
        repo.Save(new List<BackupJob> { MakeJob(1, "First") });
        repo.Save(new List<BackupJob> { MakeJob(2, "Second") });

        var loaded = repo.Load();
        Assert.Single(loaded);
        Assert.Equal("Second", loaded[0].Name);
    }

    [Fact]
    public void Save_EmptyList_WritesEmptyJsonArray()
    {
        var repo = new ConfigRepository();
        repo.Save(new List<BackupJob>());

        var json = File.ReadAllText(_configPath).Trim();
        Assert.Equal("[]", json);
    }

    [Fact]
    public void Save_ProducesValidJsonFile()
    {
        var repo = new ConfigRepository();
        repo.Save(new List<BackupJob> { MakeJob() });

        var json = File.ReadAllText(_configPath);
        // Should be indented (contains newlines) and parseable
        Assert.Contains('\n', json);
        var loaded = repo.Load();
        Assert.Single(loaded);
    }
}

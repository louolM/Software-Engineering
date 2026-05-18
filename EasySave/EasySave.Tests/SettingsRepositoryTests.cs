using EasySave.Core;
using EasySave.Infrastructure;
using Xunit;

namespace EasySave.Tests;

// Unit tests for SettingsRepository.
// Isolates each test by redirecting the working directory to a fresh temp folder,
// because SettingsRepository hard-codes "settings.json" relative to CWD.
public class SettingsRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "settingsrepo_" + Guid.NewGuid());
    private readonly string _settingsPath;
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public SettingsRepositoryTests()
    {
        Directory.CreateDirectory(_dir);
        Directory.SetCurrentDirectory(_dir);
        _settingsPath = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Directory.Delete(_dir, true);
    }

    // ── Load ───────────────────────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var repo = new SettingsRepository();
        var result = repo.Load();

        Assert.NotNull(result);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_DefaultSettingsAreValid()
    {
        var repo = new SettingsRepository();
        var result = repo.Load();

        // Defaults should be non-null and not throw
        Assert.NotNull(result.EncryptedExtensions);
        Assert.NotNull(result.LogFormat);
        Assert.NotNull(result.Language);
    }

    [Fact]
    public void Load_AfterSave_ReturnsSameSettings()
    {
        var repo = new SettingsRepository();
        var settings = new AppSettings
        {
            BusinessSoftware = "notepad.exe",
            EncryptionKey = "my-secret",
            LogFormat = "XML",
            Language = "FR",
            EncryptedExtensions = new List<string> { ".txt", ".pdf" },
            MaxParallelFileSize = 512,
            LogDestination = "Both"
        };

        repo.Save(settings);
        var loaded = repo.Load();

        Assert.Equal("notepad.exe", loaded.BusinessSoftware);
        Assert.Equal("my-secret", loaded.EncryptionKey);
        Assert.Equal("XML", loaded.LogFormat);
        Assert.Equal("FR", loaded.Language);
        Assert.Equal(new List<string> { ".txt", ".pdf" }, loaded.EncryptedExtensions);
        Assert.Equal(512, loaded.MaxParallelFileSize);
        Assert.Equal("Both", loaded.LogDestination);
    }

    [Fact]
    public void Load_PreservesPriorityExtensions()
    {
        var repo = new SettingsRepository();
        var settings = new AppSettings
        {
            PriorityExtensions = new List<string> { ".docx", ".xlsx" }
        };

        repo.Save(settings);
        var loaded = repo.Load();

        Assert.Equal(new List<string> { ".docx", ".xlsx" }, loaded.PriorityExtensions);
    }

    // ── Save ───────────────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesSettingsJsonFile()
    {
        var repo = new SettingsRepository();
        repo.Save(new AppSettings());

        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Save_OverwritesPreviousSettings()
    {
        var repo = new SettingsRepository();
        repo.Save(new AppSettings { Language = "EN" });
        repo.Save(new AppSettings { Language = "FR" });

        var loaded = repo.Load();
        Assert.Equal("FR", loaded.Language);
    }

    [Fact]
    public void Save_ProducesIndentedJson()
    {
        var repo = new SettingsRepository();
        repo.Save(new AppSettings());

        var json = File.ReadAllText(_settingsPath);
        Assert.Contains('\n', json);
    }
}

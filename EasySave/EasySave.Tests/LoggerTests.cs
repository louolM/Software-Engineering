using EasyLog;
using Xunit;

namespace EasySave.Tests;

// Unit tests for Logger
// Each test class instance gets its own isolated temp directory so tests
// can run in parallel without interfering with each other or the real "logs/" folder.
public class LoggerTests : IDisposable
{
    private readonly string _logDir = Path.Combine(Path.GetTempPath(), "logger_tests_" + Guid.NewGuid());

    public LoggerTests()  => Directory.CreateDirectory(_logDir);
    public void Dispose() => Directory.Delete(_logDir, true);

    // ── JSON format ────────────────────────────────────────────────────────

    [Fact]
    public void Write_Json_CreatesLogDirectory_WhenMissing()
    {
        var dir    = Path.Combine(Path.GetTempPath(), "logger_newdir_" + Guid.NewGuid());
        var logger = new Logger("JSON", dir);
        try
        {
            logger.Write(MakeEntry());
            Assert.True(Directory.Exists(dir));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Write_Json_CreatesJsonFileNamedByDate()
    {
        var logger = new Logger("JSON", _logDir);
        logger.Write(MakeEntry());

        Assert.True(File.Exists(TodayPath("json")));
    }

    [Fact]
    public void Write_Json_SingleEntry_FileIsJsonArray()
    {
        var logger = new Logger("JSON", _logDir);
        logger.Write(MakeEntry("JobA"));

        var json = File.ReadAllText(TodayPath("json")).Trim();
        Assert.StartsWith("[", json);
        Assert.EndsWith("]",   json);
        Assert.Contains("JobA", json);
    }

    [Fact]
    public void Write_Json_MultipleEntries_AllAppendedToSameFile()
    {
        var logger = new Logger("JSON", _logDir);
        logger.Write(MakeEntry("First"));
        logger.Write(MakeEntry("Second"));
        logger.Write(MakeEntry("Third"));

        var json = File.ReadAllText(TodayPath("json"));
        Assert.Contains("First",  json);
        Assert.Contains("Second", json);
        Assert.Contains("Third",  json);
    }

    [Fact]
    public void Write_Json_MultipleEntries_ProducesExactCount()
    {
        var logger = new Logger("JSON", _logDir);
        logger.Write(MakeEntry());
        logger.Write(MakeEntry());
        logger.Write(MakeEntry());

        var json    = File.ReadAllText(TodayPath("json"));
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<LogEntry>>(json);
        Assert.Equal(3, entries?.Count);
    }

    [Fact]
    public void Write_Json_PreservesAllFields()
    {
        var logger = new Logger("JSON", _logDir);
        var entry  = new LogEntry
        {
            Timestamp    = new DateTime(2024, 4, 22, 10, 0, 0),
            BackupName   = "MyBackup",
            SourcePath   = @"\\SERVER\C$\src\file.txt",
            TargetPath   = @"\\SERVER\C$\tgt\file.txt",
            FileSize     = 4096,
            TransferTime = 55
        };

        logger.Write(entry);

        var json = File.ReadAllText(TodayPath("json"));
        Assert.Contains("MyBackup", json);
        Assert.Contains("4096",     json);
        Assert.Contains("55",       json);
    }

    // ── JSON corruption recovery ───────────────────────────────────────────

    [Fact]
    public void Write_Json_CorruptedFile_RenamesCorruptedAndStartsFresh()
    {
        var logPath = TodayPath("json");
        File.WriteAllText(logPath, "{ this is not valid json !!!"); // corrupt

        var logger = new Logger("JSON", _logDir);

        // Must not throw
        logger.Write(MakeEntry("AfterCorruption"));

        // The corrupted file should have been renamed
        var files = Directory.GetFiles(_logDir, "*_corrupted_*.json");
        Assert.Single(files);

        // A fresh log file should now exist with the new entry
        Assert.True(File.Exists(logPath));
        var json = File.ReadAllText(logPath);
        Assert.Contains("AfterCorruption", json);
    }

    [Fact]
    public void Write_Json_CorruptedFile_NewFileIsValidJsonArray()
    {
        File.WriteAllText(TodayPath("json"), "NOT JSON");

        var logger = new Logger("JSON", _logDir);
        logger.Write(MakeEntry());

        var json = File.ReadAllText(TodayPath("json")).Trim();
        Assert.StartsWith("[", json);
        Assert.EndsWith("]",   json);
    }

    // ── XML format ─────────────────────────────────────────────────────────

    [Fact]
    public void Write_Xml_CreatesXmlFileNamedByDate()
    {
        var logger = new Logger("XML", _logDir);
        logger.Write(MakeEntry());

        Assert.True(File.Exists(TodayPath("xml")));
    }

    [Fact]
    public void Write_Xml_FileContainsEntryData()
    {
        var logger = new Logger("XML", _logDir);
        logger.Write(MakeEntry("XmlJob"));

        var xml = File.ReadAllText(TodayPath("xml"));
        Assert.Contains("XmlJob", xml);
    }

    [Fact]
    public void Write_Xml_MultipleEntries_AllAppendedToSameFile()
    {
        var logger = new Logger("XML", _logDir);
        logger.Write(MakeEntry("A"));
        logger.Write(MakeEntry("B"));
        logger.Write(MakeEntry("C"));

        var xml = File.ReadAllText(TodayPath("xml"));
        Assert.Contains("A", xml);
        Assert.Contains("B", xml);
        Assert.Contains("C", xml);
    }

    [Fact]
    public void Write_Xml_ProducesWellFormedXml()
    {
        var logger = new Logger("XML", _logDir);
        logger.Write(MakeEntry());
        logger.Write(MakeEntry());

        var xml = File.ReadAllText(TodayPath("xml")).Trim();
        // An XmlSerializer-produced document starts with the XML declaration or a root element
        Assert.True(xml.StartsWith("<?xml") || xml.StartsWith("<Logs"));
    }

    [Fact]
    public void Write_Xml_PreservesAllFields()
    {
        var logger = new Logger("XML", _logDir);
        var entry = new LogEntry
        {
            Timestamp    = new DateTime(2024, 4, 22, 10, 0, 0),
            BackupName   = "XmlBackup",
            SourcePath   = @"\\SERVER\C$\src",
            TargetPath   = @"\\SERVER\C$\tgt",
            FileSize     = 2048,
            TransferTime = 77
        };

        logger.Write(entry);

        var xml = File.ReadAllText(TodayPath("xml"));
        Assert.Contains("XmlBackup", xml);
        Assert.Contains("2048",      xml);
        Assert.Contains("77",        xml);
    }

    // ── Format selection ───────────────────────────────────────────────────

    [Theory]
    [InlineData("json", "json")]
    [InlineData("JSON", "json")]
    [InlineData("Json", "json")]
    [InlineData("xml",  "xml")]
    [InlineData("XML",  "xml")]
    [InlineData("Xml",  "xml")]
    public void Write_FormatIsCaseInsensitive(string format, string expectedExtension)
    {
        var logger = new Logger(format, _logDir);
        logger.Write(MakeEntry());

        Assert.True(File.Exists(TodayPath(expectedExtension)));
    }

    [Fact]
    public void Write_UnknownFormat_DefaultsToJson()
    {
        // Anything other than "XML" should fall back to JSON
        var logger = new Logger("CSV", _logDir);
        logger.Write(MakeEntry());

        Assert.True(File.Exists(TodayPath("json")));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private string TodayPath(string ext) =>
        Path.Combine(_logDir, $"{DateTime.Now:yyyy-MM-dd}.{ext}");

    private static LogEntry MakeEntry(string name = "TestJob") => new()
    {
        Timestamp    = DateTime.Now,
        BackupName   = name,
        SourcePath   = @"\\SERVER\C$\src\file.txt",
        TargetPath   = @"\\SERVER\C$\tgt\file.txt",
        FileSize     = 512,
        TransferTime = 10
    };
}

using EasySave.Infrastructure;
using Xunit;

namespace EasySave.Tests;

// Unit tests for JsonService
public class JsonServiceTests : IDisposable
{
    private readonly string      _dir = Path.Combine(Path.GetTempPath(), "jsonservice_" + Guid.NewGuid());
    private readonly JsonService _sut = new();

    public JsonServiceTests() => Directory.CreateDirectory(_dir);
    public void Dispose()     => Directory.Delete(_dir, true);

    private string TempFile(string name = "test.json") => Path.Combine(_dir, name);

    // ── Round-trip ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteAndRead_RoundTrip_String()
    {
        _sut.Write(TempFile(), "hello world");
        Assert.Equal("hello world", _sut.Read<string>(TempFile()));
    }

    [Fact]
    public void WriteAndRead_RoundTrip_ListOfStrings()
    {
        var data = new List<string> { "alpha", "beta", "gamma" };
        _sut.Write(TempFile(), data);

        var result = _sut.Read<List<string>>(TempFile());
        Assert.Equal(data, result);
    }

    [Fact]
    public void WriteAndRead_RoundTrip_ComplexObject()
    {
        var obj = new SampleDto { Id = 7, Name = "test" };
        _sut.Write(TempFile(), obj);

        var result = _sut.Read<SampleDto>(TempFile());
        Assert.NotNull(result);
        Assert.Equal(7,      result!.Id);
        Assert.Equal("test", result.Name);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────

    [Fact]
    public void Read_MissingFile_ReturnsDefault()
    {
        var result = _sut.Read<List<string>>(TempFile("missing.json"));
        Assert.Null(result);
    }

    [Fact]
    public void Write_ProducesIndentedJson()
    {
        _sut.Write(TempFile(), new SampleDto { Id = 1, Name = "x" });
        Assert.Contains('\n', File.ReadAllText(TempFile()));
    }

    [Fact]
    public void Write_OverwritesExistingFile()
    {
        _sut.Write(TempFile(), "original");
        _sut.Write(TempFile(), "overwritten");
        Assert.Equal("overwritten", _sut.Read<string>(TempFile()));
    }

    // ── Helper DTO ─────────────────────────────────────────────────────────

    private sealed class SampleDto
    {
        public int    Id   { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

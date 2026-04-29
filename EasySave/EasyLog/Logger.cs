using System.Text.Json;
using System.Xml.Serialization;

namespace EasyLog;

// Simple Logger that writes log entries in daily files

// Fonctionnement global :
// Logs are stored in a "logs" folder
// A file a day is created (format : yyyy-MM-dd.json ou .xml)
// Each writing the current day file is read (if existing), new entry is appended to the list and the file is overwritten with all entries
// Output format can be JSON or XML (default is JSON )

// NB : It's better to rewrite all instead of apending bcause of file format constraints, because xml tags or json serialization would cause eventual issues

public class Logger
{
    private readonly string _logDirectory = "logs";
    private readonly string _format; // "JSON" ou "XML"

    public Logger(string format = "JSON")
    {
        _format = format.ToUpper() == "XML" ? "XML" : "JSON (Default)";
    }

    public void Write(LogEntry entry)
    {
        if (!Directory.Exists(_logDirectory))
            Directory.CreateDirectory(_logDirectory);

        if (_format == "XML")
            WriteXml(entry);
        else
            WriteJson(entry);
    }

    private void WriteJson(LogEntry entry)
    {
        var fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
        var fullPath = Path.Combine(_logDirectory, fileName);
        var options = new JsonSerializerOptions { WriteIndented = true };

        List<LogEntry> logs = File.Exists(fullPath)
            ? JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(fullPath)) ?? new()
            : new();

        logs.Add(entry);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(logs, options));
    }

    private void WriteXml(LogEntry entry)
    {
        var fileName = $"{DateTime.Now:yyyy-MM-dd}.xml";
        var fullPath = Path.Combine(_logDirectory, fileName);

        var serializer = new XmlSerializer(typeof(List<LogEntry>),
                             new XmlRootAttribute("Logs"));

        List<LogEntry> logs = new();

        if (File.Exists(fullPath))
        {
            using var readStream = File.OpenRead(fullPath);
            logs = (List<LogEntry>?)serializer.Deserialize(readStream) ?? new();
        }

        logs.Add(entry);

        using var writeStream = File.Create(fullPath);
        serializer.Serialize(writeStream, logs);
    }
}
using System.Text.Json;
using System.Xml.Serialization;

namespace EasyLog;

public class Logger
{
    private readonly string _logDirectory;
    private readonly string _format; // "JSON" or "XML"
 
    public Logger(string format = "JSON") : this(format, "logs") { }
    public Logger(string format, string logDirectory)
    {
        _format = format.ToUpper() == "XML" ? "XML" : "JSON";
        _logDirectory = logDirectory;
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

        List<LogEntry> logs = new();
        if (File.Exists(fullPath))
        {
            try
            {
                logs = JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(fullPath)) ?? new();
            }
            catch (JsonException)
            {
                // File is corrupted — rename it for inspection and start fresh
                File.Move(fullPath, fullPath.Replace(".json", $"_corrupted_{DateTime.Now:HHmmss}.json"));
                logs = new();
            }
        }

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
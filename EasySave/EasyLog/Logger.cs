using System.Text.Json;
using System.Xml.Serialization;

namespace EasyLog;

public class Logger
{
    private readonly string _logDirectory = "logs";
    private readonly string _format; // "JSON" ou "XML"

    public Logger(string format = "JSON")
    {
        _format = format.ToUpper() == "XML" ? "XML" : "JSON";
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
<<<<<<< Updated upstream

=======
>>>>>>> Stashed changes
// Handles writing LogEntry records to daily JSON log files.
//
// Log files are stored in the "logs/" folder relative to the application's working directory.
// One file is created per calendar day, named after the current date (e.g., "logs/2024-04-22.json").
<<<<<<< Updated upstream
// Each file contains a JSON array of LogEntry objects, appended on every write.
=======
// Each file contains a JSON array of LogEntry objects, appended on every write.
>>>>>>> Stashed changes

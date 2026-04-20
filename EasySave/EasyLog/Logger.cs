using System.Text.Json;

namespace EasyLog;

public class Logger
{
    private readonly string _logDirectory = "logs";

    public void Write(LogEntry entry)
    {
        if (!Directory.Exists(_logDirectory))
            Directory.CreateDirectory(_logDirectory);

        var fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
        var fullPath = Path.Combine(_logDirectory, fileName);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        List<LogEntry> logs;

        if (File.Exists(fullPath))
        {
            var existingJson = File.ReadAllText(fullPath);
            logs = JsonSerializer.Deserialize<List<LogEntry>>(existingJson) ?? new List<LogEntry>();
        }
        else
        {
            logs = new List<LogEntry>();
        }

        logs.Add(entry);

        var json = JsonSerializer.Serialize(logs, options);
        File.WriteAllText(fullPath, json);
    }
}
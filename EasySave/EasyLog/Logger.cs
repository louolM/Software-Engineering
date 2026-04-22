using System.Text.Json;

namespace EasyLog;

// Handles writing LogEntry records to daily JSON log files.
//
// Log files are stored in the "logs/" folder relative to the application's working directory.
// One file is created per calendar day, named after the current date (e.g., "logs/2024-04-22.json").
// Each file contains a JSON array of LogEntry objects, appended on every write.
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
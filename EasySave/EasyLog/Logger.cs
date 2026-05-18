using System.Text.Json;
using System.Xml.Serialization;

namespace EasyLog;

// Writes log entries to daily rotating files in JSON or XML format.
// Supports concurrent calls from multiple backup jobs running in parallel
// by using a static semaphore to serialise all write operations.
public class Logger
{
    private readonly string _logDirectory;
    private readonly string _format;

    // Static lock shared across all Logger instances and all threads.
    // Prevents concurrent writes from corrupting the log file when multiple
    // jobs are running at the same time.
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    public Logger(string format = "JSON", string logDirectory = "logs")
    {
        _format = format.ToUpper() == "XML" ? "XML" : "JSON";
        _logDirectory = logDirectory;
    }

    // Writes a single log entry to today's log file.
    // Acquires the semaphore before writing and releases it in the finally block
    // so the lock is always freed even if serialisation throws.
    public void Write(LogEntry entry)
    {
        if (!Directory.Exists(_logDirectory))
            Directory.CreateDirectory(_logDirectory);

        _writeLock.Wait();
        try
        {
            if (_format == "XML")
                WriteXml(entry);
            else
                WriteJson(entry);
        }
        finally
        {
            _writeLock.Release();
        }
    }
    
    // Appends an entry to the daily JSON log file.
    // Reads the existing list, adds the new entry, then writes the whole list back.
    // If the file is corrupted (invalid JSON), it is renamed with a timestamp suffix
    // and a fresh file is started so logging can continue.
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
                logs = JsonSerializer.Deserialize<List<LogEntry>>(
                    File.ReadAllText(fullPath)) ?? new();
            }
            catch (JsonException)
            {
                File.Move(fullPath,
                    fullPath.Replace(".json",
                        $"_corrupted_{DateTime.Now:HHmmss}.json"));
                logs = new();
            }
        }

        logs.Add(entry);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(logs, options));
    }

    // Appends an entry to the daily XML log file.
    // Reads the existing list (or starts a new one), adds the entry, and
    // overwrites the file with the updated list under a root <Logs> element.
    private void WriteXml(LogEntry entry)
    {
        var fileName = $"{DateTime.Now:yyyy-MM-dd}.xml";
        var fullPath = Path.Combine(_logDirectory, fileName);
        
        // XmlSerializer is created with an explicit root attribute so the output
        // uses <Logs> as the root element instead of the default generic name.
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
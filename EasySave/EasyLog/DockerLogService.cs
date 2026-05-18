using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EasyLog;

// Sends log entries to the centralised Docker log server via HTTP POST.
// Used by BackupService when LogDestination is "Docker" or "Both".
public class DockerLogService
{
    // A single static HttpClient is reused across all calls to avoid socket exhaustion
    // from creating a new instance per request.
    private static readonly HttpClient _http = new();
    private readonly string _serverUrl;
    private readonly string _format;

    public DockerLogService(string serverUrl, string format = "JSON")
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _format = format.ToUpper() == "XML" ? "XML" : "JSON";
    }

    // Sends a log entry to the server asynchronously and fire-and-forget.
    // Errors are silently swallowed so a network failure never blocks or
    // interrupts the backup job that triggered the call.
    public void Send(LogEntry entry)
    {
        // The discard assignment (_ = ...) explicitly suppresses the compiler warning
        // about an unawaited task, "fire-and-forget" logic.
        
        _ = Task.Run(async () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(entry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"{_serverUrl}/logs?format={_format}";
                await _http.PostAsync(url, content);
            }
            catch
            {
                // Intentionally silent: the backup continues even if the Docker server is unreachable.
            }
        });
    }
}
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EasyLog;

/// <summary>
/// Envoie les logs au serveur Docker centralisé via HTTP POST.
/// Utilisé par BackupService quand LogDestination = "Docker" ou "Both".
/// </summary>
public class DockerLogService
{
    private static readonly HttpClient _http = new();
    private readonly string _serverUrl;
    private readonly string _format;

    public DockerLogService(string serverUrl, string format = "JSON")
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _format = format.ToUpper() == "XML" ? "XML" : "JSON";
    }

    /// <summary>
    /// Envoie une entrée de log au serveur Docker.
    /// Fire-and-forget : les erreurs sont silencieuses pour ne pas bloquer le backup.
    /// </summary>
    public void Send(LogEntry entry)
    {
        // Fire and forget - ne bloque pas le backup
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
                // Silencieux - le backup continue même si Docker est inaccessible
            }
        });
    }
}
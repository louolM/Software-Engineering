using EasySave.Core;
using EasySave.Services.Interfaces;
using System.Text.Json;

namespace EasySave.Infrastructure;

public class ConfigRepository : IConfigRepository
{
    private const string ConfigPath = "config.json";

    public List<BackupJob> Load()
    {
        if (!File.Exists(ConfigPath))
            return new List<BackupJob>();

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<List<BackupJob>>(json)
               ?? new List<BackupJob>();
    }

    public void Save(List<BackupJob> jobs)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(jobs, options));
    }
}
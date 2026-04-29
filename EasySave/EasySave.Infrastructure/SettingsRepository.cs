using EasySave.Core;
using EasySave.Services.Interfaces;
using System.Text.Json;

namespace EasySave.Infrastructure;

public class SettingsRepository : ISettingsRepository
{
    private const string SettingsPath = "settings.json";

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, options));
    }
}
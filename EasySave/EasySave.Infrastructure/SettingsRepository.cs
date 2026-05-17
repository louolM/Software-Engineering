using EasySave.Core;
using EasySave.Services.Interfaces;
using System.Text.Json;

namespace EasySave.Infrastructure;

// Persists and retrieves application settings using a local JSON file ("settings.json").
// Returns a default AppSettings instance when the file does not exist yet,
// so the rest of the application always receives a valid, non-null object.
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
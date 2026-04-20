namespace EasySave.Infrastructure;

public class ConfigRepository
{
    private readonly JsonService _jsonService = new();

    private const string ConfigPath = "config.json";

    public List<T> Load<T>()
    {
        return _jsonService.Read<List<T>>(ConfigPath) ?? new List<T>();
    }

    public void Save<T>(List<T> jobs)
    {
        _jsonService.Write(ConfigPath, jobs);
    }
}
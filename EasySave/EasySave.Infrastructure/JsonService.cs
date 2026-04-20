using System.Text.Json;

namespace EasySave.Infrastructure;

public class JsonService
{
    public void Write<T>(string path, T data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json);
    }

    public T Read<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json);
    }
}
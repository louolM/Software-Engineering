using System.Text.Json;

namespace EasySave.Infrastructure;

// A generic utility for reading from and writing to JSON files.
//
// This service centralizes the serialization settings (indented output) and the file-not-found guard used by multiple repositories. A
// Any class that needs to persist data as JSON should use this service rather than calling System.Text.Json.JsonSerializer and File methods directly.
public class JsonService
{
    public void Write<T>(string path, T data)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(data, options));
    }

    public T? Read<T>(string path)
    {
        if (!File.Exists(path)) return default;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
    }
}
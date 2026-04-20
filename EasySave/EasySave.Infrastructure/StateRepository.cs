namespace EasySave.Infrastructure;

public class StateRepository
{
    private readonly JsonService _jsonService = new();

    private const string StatePath = "state.json";

    public void Save<T>(List<T> states)
    {
        _jsonService.Write(StatePath, states);
    }
}
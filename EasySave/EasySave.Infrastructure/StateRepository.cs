using EasySave.Core;

namespace EasySave.Infrastructure;

public class StateRepository : IStateRepository  // ← ajouter : IStateRepository
{
    private readonly JsonService _jsonService = new();
    private const string StatePath = "state.json";

    public void Save(List<BackupState> states)  // ← typer explicitement BackupState
    {
        _jsonService.Write(StatePath, states);
    }
}
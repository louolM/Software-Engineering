using EasySave.Core;
using EasySave.Services.Interfaces;

namespace EasySave.Infrastructure;

// Persists real-time job progress to "state.json" using JsonService.
// Only implements Save because state is written by the application and
// read by external monitoring tools directly from the file.
public class StateRepository : IStateRepository
{
    private readonly JsonService _jsonService = new();
    private const string StatePath = "state.json";

    public void Save(List<BackupState> states)
        => _jsonService.Write(StatePath, states);
}
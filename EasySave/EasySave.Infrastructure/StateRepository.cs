using EasySave.Core;
using EasySave.Services.Interfaces;

namespace EasySave.Infrastructure;

public class StateRepository : IStateRepository
{
    private readonly JsonService _jsonService = new();
    private const string StatePath = "state.json";

    public void Save(List<BackupState> states)
        => _jsonService.Write(StatePath, states);
}
using EasySave.Core;

namespace EasySave.Services.Interfaces;

// Defines the contract for persisting real-time backup progress.
//
// The state is written after every file transfer so that external tools (e.g., a monitoring dashboard) can poll for live progress without being coupled to the application process.
public interface IStateRepository
{
    void Save(List<BackupState> states);
}
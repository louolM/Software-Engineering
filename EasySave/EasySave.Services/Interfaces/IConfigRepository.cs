using EasySave.Core;

namespace EasySave.Services.Interfaces;

// Defines the contract for loading and saving the list of backup jobs.
//
// Implementations decide how and where jobs are persisted (JSON file, database, etc.).
public interface IConfigRepository
{
    List<BackupJob> Load();
    void Save(List<BackupJob> jobs);
}
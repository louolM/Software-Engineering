using EasySave.Core;

namespace EasySave.Services.Interfaces;

public interface IConfigRepository
{
    List<BackupJob> Load();
    void Save(List<BackupJob> jobs);
}
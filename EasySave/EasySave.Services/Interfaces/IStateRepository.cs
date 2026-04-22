using EasySave.Core;

namespace EasySave.Services.Interfaces;

public interface IStateRepository
{
    void Save(List<BackupState> states);
}
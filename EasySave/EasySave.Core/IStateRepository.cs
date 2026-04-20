namespace EasySave.Core;

public interface IStateRepository
{
    void Save(List<BackupState> states);
}
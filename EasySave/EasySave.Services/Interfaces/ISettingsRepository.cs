using EasySave.Core;

namespace EasySave.Services.Interfaces;

public interface ISettingsRepository
{
    AppSettings Load();
    void Save(AppSettings settings);
}
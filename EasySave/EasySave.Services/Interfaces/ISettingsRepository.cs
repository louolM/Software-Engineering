using EasySave.Core;

namespace EasySave.Services.Interfaces;

// Defines the contract for loading and saving application settings.
// Implementations decide how settings are persisted (JSON file, registry, etc.).
public interface ISettingsRepository
{
    AppSettings Load();
    void Save(AppSettings settings);
}
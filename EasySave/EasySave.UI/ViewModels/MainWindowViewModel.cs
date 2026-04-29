using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasySave.Infrastructure;
using EasySave.Services;
using EasySave.Services.Interfaces;
using EasyLog;

namespace EasySave.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentView;
    [ObservableProperty] private string _navJobs = "📋 Jobs";
    [ObservableProperty] private string _navSettings = "⚙ Settings";

    private readonly JobListViewModel _jobListVm;
    private readonly SettingsViewModel _settingsVm;

    public MainWindowViewModel()
    {
        IFileService fileService = new FileService();
        IStateRepository stateRepo = new StateRepository();
        IConfigRepository configRepo = new ConfigRepository();
        ISettingsRepository settingsRepo = new SettingsRepository();

        var settings = settingsRepo.Load();
        IBackupService backupSvc = new BackupService(fileService, new Logger(settings.LogFormat), stateRepo);

        _settingsVm = new SettingsViewModel(settingsRepo);
        _jobListVm = new JobListViewModel(configRepo, backupSvc, settingsRepo);

        // Écoute les changements de langue depuis SettingsViewModel
        _settingsVm.LanguageChanged += OnLanguageChanged;

        _currentView = _jobListVm;
        ApplyLanguage(settings.Language);
    }

    private void OnLanguageChanged(string lang)
    {
        ApplyLanguage(lang);
        _jobListVm.ApplyLanguage(lang);
    }

    private void ApplyLanguage(string lang)
    {
        if (lang == "FR")
        {
            NavJobs = "📋 Travaux";
            NavSettings = "⚙ Paramètres";
        }
        else
        {
            NavJobs = "📋 Jobs";
            NavSettings = "⚙ Settings";
        }
    }

    [RelayCommand] private void ShowJobs() => CurrentView = _jobListVm;
    [RelayCommand] private void ShowSettings() => CurrentView = _settingsVm;
}
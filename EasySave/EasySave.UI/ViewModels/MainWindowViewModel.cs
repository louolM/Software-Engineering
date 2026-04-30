using Avalonia.Controls;
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
    private readonly IFileService _fileService;
    private readonly IStateRepository _stateRepo;
    private readonly ISettingsRepository _settingsRepo;
    private TranslationService _t;

    public MainWindowViewModel()
    {
        _fileService = new FileService();
        _stateRepo = new StateRepository();
        _settingsRepo = new SettingsRepository();
        IConfigRepository configRepo = new ConfigRepository();

        var settings = _settingsRepo.Load();
        var backupSvc = new BackupService(_fileService, new Logger(settings.LogFormat), _stateRepo);

        _t = new TranslationService(settings.Language);
        _settingsVm = new SettingsViewModel(_settingsRepo, _t);
        _jobListVm = new JobListViewModel(configRepo, backupSvc, _settingsRepo, _t);

        _settingsVm.LanguageChanged += OnLanguageChanged;
        _settingsVm.SettingsSaved += OnSettingsSaved;

        _currentView = _jobListVm;
        ApplyLanguage();
    }

    public void SetWindow(Window window) => _jobListVm.ParentWindow = window;

    private void OnLanguageChanged(string lang)
    {
        // Recrée le TranslationService avec la nouvelle langue
        _t = new TranslationService(lang);
        _jobListVm.UpdateTranslations(_t);
        _settingsVm.UpdateTranslations(_t);
        ApplyLanguage();
    }

    private void OnSettingsSaved()
    {
        // Recrée le Logger et BackupService avec le nouveau format de log
        var settings = _settingsRepo.Load();
        var newLogger = new Logger(settings.LogFormat);
        var newBackup = new BackupService(_fileService, newLogger, _stateRepo);
        _jobListVm.UpdateBackupService(newBackup);
    }

    private void ApplyLanguage()
    {
        NavJobs = _t.T("nav.jobs");
        NavSettings = _t.T("nav.settings");
    }

    [RelayCommand] private void ShowJobs() => CurrentView = _jobListVm;
    [RelayCommand] private void ShowSettings() => CurrentView = _settingsVm;
}
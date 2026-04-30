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
    private TranslationService _t;

    public MainWindowViewModel()
    {
        IFileService fileService = new FileService();
        IStateRepository stateRepo = new StateRepository();
        IConfigRepository configRepo = new ConfigRepository();
        ISettingsRepository settingsRepo = new SettingsRepository();

        var settings = settingsRepo.Load();
        IBackupService backupSvc = new BackupService(fileService, new Logger(settings.LogFormat), stateRepo);

        _t = new TranslationService(settings.Language);
        _settingsVm = new SettingsViewModel(settingsRepo, _t);
        _jobListVm = new JobListViewModel(configRepo, backupSvc, settingsRepo, _t);

        _settingsVm.LanguageChanged += OnLanguageChanged;

        _currentView = _jobListVm;
        ApplyLanguage();
    }

    public void SetWindow(Window window) => _jobListVm.ParentWindow = window;

    private void OnLanguageChanged(string lang)
    {
        // Recrée le service de traduction avec la nouvelle langue
        _t = new TranslationService(lang);
        _jobListVm.UpdateTranslations(_t);
        _settingsVm.UpdateTranslations(_t);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        NavJobs = _t.T("nav.jobs");
        NavSettings = _t.T("nav.settings");
    }

    [RelayCommand] private void ShowJobs() => CurrentView = _jobListVm;
    [RelayCommand] private void ShowSettings() => CurrentView = _settingsVm;
}
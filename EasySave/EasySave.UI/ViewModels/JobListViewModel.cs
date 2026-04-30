using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasySave.Core;
using EasySave.Services.Interfaces;

namespace EasySave.UI.ViewModels;

public partial class JobListViewModel : ViewModelBase
{
    private readonly IConfigRepository _configRepo;
    private readonly IBackupService _backupService;
    private readonly ISettingsRepository _settingsRepo;
    private TranslationService _t;

    public Window? ParentWindow { get; set; }

    [ObservableProperty] private ObservableCollection<BackupJob> _jobs = new();
    [ObservableProperty] private BackupJob? _selectedJob;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;
    [ObservableProperty] private bool _isFormVisible = false;

    // Libellés — tous alimentés depuis _t
    [ObservableProperty] private string _btnNewJob = string.Empty;
    [ObservableProperty] private string _btnRunSelected = string.Empty;
    [ObservableProperty] private string _btnRunAll = string.Empty;
    [ObservableProperty] private string _btnDelete = string.Empty;
    [ObservableProperty] private string _formTitle = string.Empty;
    [ObservableProperty] private string _lblName = string.Empty;
    [ObservableProperty] private string _lblSource = string.Empty;
    [ObservableProperty] private string _lblTarget = string.Empty;
    [ObservableProperty] private string _lblDiff = string.Empty;
    [ObservableProperty] private string _btnCancel = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _btnBrowse = string.Empty;

    // Champs formulaire
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formSource = string.Empty;
    [ObservableProperty] private string _formTarget = string.Empty;
    [ObservableProperty] private bool _formIsDifferential = false;
    [ObservableProperty] private string _formNameError = string.Empty;
    [ObservableProperty] private string _formSourceError = string.Empty;
    [ObservableProperty] private string _formTargetError = string.Empty;

    private bool _isEditing = false;

    public JobListViewModel(IConfigRepository configRepo, IBackupService backupService,
                            ISettingsRepository settingsRepo, TranslationService t)
    {
        _configRepo = configRepo;
        _backupService = backupService;
        _settingsRepo = settingsRepo;
        _t = t;
        UpdateTranslations(_t);
        Refresh();
    }

    public void UpdateTranslations(TranslationService t)
    {
        _t = t;
        BtnNewJob = _t.T("jobs.new");
        BtnRunSelected = _t.T("jobs.runSelected");
        BtnRunAll = _t.T("jobs.runAll");
        BtnDelete = _t.T("jobs.delete");
        FormTitle = _t.T(_isEditing ? "form.title.edit" : "form.title.create");
        LblName = _t.T("form.name");
        LblSource = _t.T("form.source");
        LblTarget = _t.T("form.target");
        LblDiff = _t.T("form.diff");
        BtnCancel = _t.T("form.cancel");
        BtnSave = _t.T("form.save");
        BtnBrowse = _t.T("form.browse");
    }

    private void Refresh()
    {
        Jobs = new ObservableCollection<BackupJob>(_configRepo.Load());
        StatusMessage = string.Empty;
        StatusIsError = false;
    }

    private static string CleanPath(string path) => path.Trim().Trim('"').Trim('\'').Trim();

    [RelayCommand]
    private async Task BrowseSource()
    {
        var path = await PickFolder();
        if (path != null) FormSource = path;
    }

    [RelayCommand]
    private async Task BrowseTarget()
    {
        var path = await PickFolder();
        if (path != null) FormTarget = path;
    }

    private async Task<string?> PickFolder()
    {
        if (ParentWindow == null) return null;
        var result = await ParentWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = _t.T("picker.title"), AllowMultiple = false });
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    private bool ValidateForm()
    {
        var valid = true;

        if (string.IsNullOrWhiteSpace(FormName))
        { FormNameError = _t.T("err.nameRequired"); valid = false; }
        else FormNameError = string.Empty;

        var src = CleanPath(FormSource);
        if (string.IsNullOrWhiteSpace(src))
        { FormSourceError = _t.T("err.sourceRequired"); valid = false; }
        else if (!Directory.Exists(src))
        { FormSourceError = string.Format(_t.T("err.sourceNotFound"), src); valid = false; }
        else FormSourceError = string.Empty;

        var tgt = CleanPath(FormTarget);
        if (string.IsNullOrWhiteSpace(tgt))
        { FormTargetError = _t.T("err.targetRequired"); valid = false; }
        else FormTargetError = string.Empty;

        return valid;
    }

    [RelayCommand]
    private void ShowCreateForm()
    {
        _isEditing = false;
        FormName = FormSource = FormTarget = string.Empty;
        FormNameError = FormSourceError = FormTargetError = string.Empty;
        FormIsDifferential = false;
        FormTitle = _t.T("form.title.create");
        IsFormVisible = true;
    }

    [RelayCommand]
    private void SaveJob()
    {
        if (!ValidateForm()) return;

        var src = CleanPath(FormSource);
        var tgt = CleanPath(FormTarget);
        var jobs = _configRepo.Load();

        if (!_isEditing && jobs.Any(j => j.Name!.Equals(FormName, StringComparison.OrdinalIgnoreCase)))
        { FormNameError = _t.T("err.nameExists"); return; }

        if (_isEditing && SelectedJob != null)
        {
            var ex = jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);
            if (ex != null) { ex.Name = FormName; ex.SourcePath = src; ex.TargetPath = tgt; ex.Type = FormIsDifferential ? BackupType.Differential : BackupType.Full; }
        }
        else
        {
            var id = jobs.Count == 0 ? 1 : jobs.Max(j => j.Id) + 1;
            jobs.Add(new BackupJob { Id = id, Name = FormName, SourcePath = src, TargetPath = tgt, Type = FormIsDifferential ? BackupType.Differential : BackupType.Full });
        }

        _configRepo.Save(jobs);
        IsFormVisible = false;
        SetSuccess(_t.T(_isEditing ? "jobs.updated" : "jobs.created"));
        Refresh();
    }

    [RelayCommand]
    private void CancelForm() { IsFormVisible = false; FormNameError = FormSourceError = FormTargetError = string.Empty; }

    [RelayCommand]
    private async Task RunSelected()
    {
        if (SelectedJob == null) { SetError(_t.T("jobs.selectFirst")); return; }
        var settings = _settingsRepo.Load();
        if (TryRun(SelectedJob, settings))
            await SetSuccessAutoHide(string.Format(_t.T("jobs.backupDone"), SelectedJob.Name));
        else
            SetError(string.Format(_t.T("jobs.blocked"), settings.BusinessSoftware));
    }

    [RelayCommand]
    private async Task RunAll()
    {
        if (Jobs.Count == 0) { SetError(_t.T("jobs.noJobs")); return; }
        var settings = _settingsRepo.Load();
        int ran = 0, blocked = 0;
        foreach (var job in Jobs) { if (TryRun(job, settings)) ran++; else blocked++; }

        if (blocked > 0) SetError(string.Format(_t.T("jobs.blockedMany"), blocked, ran));
        else await SetSuccessAutoHide(string.Format(_t.T("jobs.allDone"), ran));
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedJob == null) { SetError(_t.T("jobs.selectFirst")); return; }
        var jobs = _configRepo.Load();
        jobs.RemoveAll(j => j.Id == SelectedJob.Id);
        _configRepo.Save(jobs);
        await SetSuccessAutoHide(string.Format(_t.T("jobs.deleted"), SelectedJob.Name));
        Refresh();
    }
    [RelayCommand]
    private void OpenLogs()
    {
        var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsPath); // ensures it exists
        Process.Start(new ProcessStartInfo(logsPath) { UseShellExecute = true });
    }

    private async Task<bool> TryRunWithProgress(BackupJob job, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BusinessSoftware))
        {
            var name = settings.BusinessSoftware.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (Process.GetProcessesByName(name).Length > 0) return false;
        }

        IsRunning = true;
        CurrentTaskLabel = $"Running: {job.Name}";
        ProgressPercent = 0;
        ProgressText = "0%";

        await Task.Run(() => _backupService.RunBackup(job, settings));

        ProgressPercent = 100;
        ProgressText = "100%";
        IsRunning = false;
        return true;
    }

    private async Task SetSuccessAutoHide(string msg)
    {
        StatusIsError = false; StatusMessage = msg;
        await Task.Delay(5000);
        StatusMessage = string.Empty;
    }

    private void SetError(string msg) { StatusMessage = msg; StatusIsError = true; }
    private void SetSuccess(string msg) { StatusMessage = msg; StatusIsError = false; }
    
// Progress
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _currentTaskLabel = string.Empty;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _btnOpenLogs = string.Empty;
}
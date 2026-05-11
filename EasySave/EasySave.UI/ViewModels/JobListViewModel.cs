using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasySave.Core;
using EasySave.Services;
using EasySave.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EasySave.UI.ViewModels;

public partial class JobListViewModel : ViewModelBase
{
    private readonly IConfigRepository _configRepo;
    private IBackupService _backupService;
    private readonly ISettingsRepository _settingsRepo;
    private TranslationService _t;

    public Window? ParentWindow { get; set; }

    [ObservableProperty] private ObservableCollection<BackupJob> _jobs = new();
    [ObservableProperty] private ObservableCollection<JobProgressItem> _jobProgress = new();
    [ObservableProperty] private BackupJob? _selectedJob;
    [ObservableProperty] private JobProgressItem? _selectedProgressItem;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;
    [ObservableProperty] private bool _isFormVisible = false;

    // Libellés
    [ObservableProperty] private string _btnNewJob = string.Empty;
    [ObservableProperty] private string _btnRunSelected = string.Empty;
    [ObservableProperty] private string _btnRunAll = string.Empty;
    [ObservableProperty] private string _btnDelete = string.Empty;
    [ObservableProperty] private string _btnPauseAll = string.Empty;  // ← AJOUT
    [ObservableProperty] private string _btnResumeAll = string.Empty;  // ← AJOUT
    [ObservableProperty] private string _btnStopAll = string.Empty;  // ← AJOUT
    [ObservableProperty] private string _formTitle = string.Empty;
    [ObservableProperty] private string _lblName = string.Empty;
    [ObservableProperty] private string _lblSource = string.Empty;
    [ObservableProperty] private string _lblTarget = string.Empty;
    [ObservableProperty] private string _lblDiff = string.Empty;
    [ObservableProperty] private string _btnCancel = string.Empty;
    [ObservableProperty] private string _btnSave = string.Empty;
    [ObservableProperty] private string _btnBrowse = string.Empty;
    [ObservableProperty] private string _btnOpenLogs = string.Empty;

    // Champs formulaire
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formSource = string.Empty;
    [ObservableProperty] private string _formTarget = string.Empty;
    [ObservableProperty] private bool _formIsDifferential = false;
    [ObservableProperty] private string _formNameError = string.Empty;
    [ObservableProperty] private string _formSourceError = string.Empty;
    [ObservableProperty] private string _formTargetError = string.Empty;

    private bool _isEditing = false;
    private readonly Dictionary<int, JobController> _controllers = new();
    private BusinessSoftwareWatcher? _watcher;

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

    partial void OnSelectedProgressItemChanged(JobProgressItem? value)
    {
        if (value == null) { SelectedJob = null; return; }
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == value.JobId);
    }

    public void UpdateBackupService(IBackupService backupService)
        => _backupService = backupService;

    public void UpdateTranslations(TranslationService t)
    {
        _t = t;
        BtnNewJob = _t.T("jobs.new");
        BtnRunSelected = _t.T("jobs.runSelected");
        BtnRunAll = _t.T("jobs.runAll");
        BtnDelete = _t.T("jobs.delete");
        BtnPauseAll = _t.T("jobs.pauseAll");   // ← AJOUT
        BtnResumeAll = _t.T("jobs.resumeAll");  // ← AJOUT
        BtnStopAll = _t.T("jobs.stopAll");    // ← AJOUT
        FormTitle = _t.T(_isEditing ? "form.title.edit" : "form.title.create");
        LblName = _t.T("form.name");
        LblSource = _t.T("form.source");
        LblTarget = _t.T("form.target");
        LblDiff = _t.T("form.diff");
        BtnCancel = _t.T("form.cancel");
        BtnSave = _t.T("form.save");
        BtnBrowse = _t.T("form.browse");
        BtnOpenLogs = _t.T("jobs.openLogs");
    }

    private void Refresh()
    {
        Jobs = new ObservableCollection<BackupJob>(_configRepo.Load());
        JobProgress = new ObservableCollection<JobProgressItem>(
            Jobs.Select(j => new JobProgressItem(j.Id, j.Name ?? "")));
        StatusMessage = string.Empty;
        StatusIsError = false;
    }

    private JobProgressItem? GetProgress(int jobId)
        => JobProgress.FirstOrDefault(p => p.JobId == jobId);

    private static string CleanPath(string path) => path.Trim().Trim('"').Trim('\'').Trim();

    // ── Browse ────────────────────────────────────────────────────────────
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

    // ── Formulaire ────────────────────────────────────────────────────────
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
            if (ex != null)
            {
                ex.Name = FormName; ex.SourcePath = src;
                ex.TargetPath = tgt;
                ex.Type = FormIsDifferential ? BackupType.Differential : BackupType.Full;
            }
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
    private void CancelForm()
    {
        IsFormVisible = false;
        FormNameError = FormSourceError = FormTargetError = string.Empty;
    }

    // ── Run ───────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RunSelected()
    {
        if (SelectedJob == null) { SetError(_t.T("jobs.selectFirst")); return; }
        var settings = _settingsRepo.Load();

        // ← PATCH 1 : vérifier business software AVANT de démarrer
        if (IsBusinessSoftwareActive(settings.BusinessSoftware))
        {
            SetError(string.Format(_t.T("jobs.blockedBusiness"), settings.BusinessSoftware));
            return;
        }

        StartWatcher(settings);
        await StartJob(SelectedJob, settings);
        StopWatcher();
    }

    [RelayCommand]
    private async Task RunAll()
    {
        if (Jobs.Count == 0) { SetError(_t.T("jobs.noJobs")); return; }
        var settings = _settingsRepo.Load();

        // ← PATCH 1 : vérifier business software AVANT de démarrer
        if (IsBusinessSoftwareActive(settings.BusinessSoftware))
        {
            SetError(string.Format(_t.T("jobs.blockedBusiness"), settings.BusinessSoftware));
            return;
        }

        StartWatcher(settings);
        var tasks = Jobs.Select(job => StartJob(job, settings)).ToList();
        await Task.WhenAll(tasks);
        StopWatcher();
        ShowSuccessAutoHide(string.Format(_t.T("jobs.allDone"), Jobs.Count));
    }

    private static bool IsBusinessSoftwareActive(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();
        return Process.GetProcessesByName(name).Length > 0;
    }

    private async Task StartJob(BackupJob job, AppSettings settings)
    {
        var prog = GetProgress(job.Id);
        if (prog == null) return;

        if (!_controllers.TryGetValue(job.Id, out var controller))
        {
            controller = new JobController();
            _controllers[job.Id] = controller;
        }
        else controller.Reset();

        prog.Status = "ACTIVE";
        prog.Percent = 0;
        prog.ProgressText = "0%";

        var progress = new Progress<double>(pct =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                prog.Percent = pct;
                prog.ProgressText = $"{pct:F0}%";
            });
        });

        await Task.Run(() => _backupService.RunBackupAsync(job, settings, controller, progress));

        prog.Percent = controller.IsStopped ? prog.Percent : 100;
        prog.ProgressText = controller.IsStopped ? _t.T("jobs.stopped") : "100%";
        prog.Status = controller.IsStopped ? "STOPPED" : "DONE";

        if (!controller.IsStopped)
            ShowSuccessAutoHide(string.Format(_t.T("jobs.backupDone"), job.Name));

        _ = ResetProgressAfterDelay(prog);
    }

    // ── Watcher ───────────────────────────────────────────────────────────
    private void StartWatcher(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BusinessSoftware)) return;
        _watcher = new BusinessSoftwareWatcher(
            settings.BusinessSoftware, _controllers.Values.ToList());
        _watcher.Start();
    }

    private void StopWatcher() { _watcher?.Stop(); _watcher = null; }

    // ── Pause / Resume / Stop par job ─────────────────────────────────────
    [RelayCommand]
    private void PauseJob(int jobId)
    {
        if (_controllers.TryGetValue(jobId, out var ctrl) && !ctrl.IsPaused)
        {
            ctrl.Pause();
            var p = GetProgress(jobId);
            if (p != null) p.Status = "PAUSED";
        }
    }

    [RelayCommand]
    private void ResumeJob(int jobId)
    {
        if (_controllers.TryGetValue(jobId, out var ctrl) && ctrl.IsPaused)
        {
            ctrl.Resume();
            var p = GetProgress(jobId);
            if (p != null) p.Status = "ACTIVE";
        }
    }

    [RelayCommand]
    private void StopJob(int jobId)
    {
        if (_controllers.TryGetValue(jobId, out var ctrl))
        {
            ctrl.Stop();
            var p = GetProgress(jobId);
            if (p != null) p.Status = "STOPPED";
        }
    }

    // ── Pause / Resume / Stop — TOUS ─────────────────────────────────────
    [RelayCommand]
    private void PauseAll()
    {
        foreach (var (id, ctrl) in _controllers)
            if (!ctrl.IsPaused && !ctrl.IsStopped)
            {
                ctrl.Pause();
                var p = GetProgress(id);
                if (p != null) p.Status = "PAUSED";
            }
    }

    [RelayCommand]
    private void ResumeAll()
    {
        foreach (var (id, ctrl) in _controllers)
            if (ctrl.IsPaused)
            {
                ctrl.Resume();
                var p = GetProgress(id);
                if (p != null) p.Status = "ACTIVE";
            }
    }

    [RelayCommand]
    private void StopAll()
    {
        foreach (var (id, ctrl) in _controllers)
        {
            ctrl.Stop();
            var p = GetProgress(id);
            if (p != null) p.Status = "STOPPED";
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedJob == null) { SetError(_t.T("jobs.selectFirst")); return; }
        var jobs = _configRepo.Load();
        jobs.RemoveAll(j => j.Id == SelectedJob.Id);
        _configRepo.Save(jobs);
        ShowSuccessAutoHide(string.Format(_t.T("jobs.deleted"), SelectedJob.Name));
        Refresh();
    }

    [RelayCommand]
    private void OpenLogs()
    {
        var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsPath);
        Process.Start(new ProcessStartInfo(logsPath) { UseShellExecute = true });
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private async Task SetSuccessAutoHide(string msg)
    {
        StatusIsError = false;
        StatusMessage = msg;
        await Task.Delay(5000);
        StatusMessage = string.Empty;
    }

    private void SetError(string msg) { StatusMessage = msg; StatusIsError = true; }
    private void SetSuccess(string msg) { StatusMessage = msg; StatusIsError = false; }
    private async Task ResetProgressAfterDelay(JobProgressItem prog)
    {
        await Task.Delay(5000);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            prog.Percent = 0;
            prog.ProgressText = "0%";
            prog.Status = "IDLE";
        });
    }

    private async void ShowSuccessAutoHide(string msg)
    {
        StatusIsError = false;
        StatusMessage = msg;

        await Task.Delay(5000);

        StatusMessage = string.Empty;
    }
}
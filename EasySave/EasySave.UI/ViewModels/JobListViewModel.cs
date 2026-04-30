using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    // Référence à la fenêtre pour les dialogues
    public Window? ParentWindow { get; set; }

    [ObservableProperty] private ObservableCollection<BackupJob> _jobs = new();
    [ObservableProperty] private BackupJob? _selectedJob;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;
    [ObservableProperty] private bool _isFormVisible = false;

    // Libellés traduits
    [ObservableProperty] private string _btnNewJob = "＋ New Job";
    [ObservableProperty] private string _btnRunSelected = "▶ Run Selected";
    [ObservableProperty] private string _btnRunAll = "▶▶ Run All";
    [ObservableProperty] private string _btnDelete = "🗑 Delete";
    [ObservableProperty] private string _formTitle = "New Job";
    [ObservableProperty] private string _lblName = "Name";
    [ObservableProperty] private string _lblSource = "Source path";
    [ObservableProperty] private string _lblTarget = "Target path";
    [ObservableProperty] private string _lblDiff = "Differential backup";
    [ObservableProperty] private string _btnCancel = "Cancel";
    [ObservableProperty] private string _btnSave = "Save";
    [ObservableProperty] private string _btnBrowse = "Browse...";

    // Champs formulaire
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formSource = string.Empty;
    [ObservableProperty] private string _formTarget = string.Empty;
    [ObservableProperty] private bool _formIsDifferential = false;
    [ObservableProperty] private string _formNameError = string.Empty;
    [ObservableProperty] private string _formSourceError = string.Empty;
    [ObservableProperty] private string _formTargetError = string.Empty;

    private bool _isEditing = false;
    private string _lang = "EN";

    public JobListViewModel(IConfigRepository configRepo, IBackupService backupService, ISettingsRepository settingsRepo)
    {
        _configRepo = configRepo;
        _backupService = backupService;
        _settingsRepo = settingsRepo;
        var settings = settingsRepo.Load();
        ApplyLanguage(settings.Language);
        Refresh();
    }

    public void ApplyLanguage(string lang)
    {
        _lang = lang;
        if (lang == "FR")
        {
            BtnNewJob = "＋ Nouveau";
            BtnRunSelected = "▶ Lancer";
            BtnRunAll = "▶▶ Tout lancer";
            BtnDelete = "🗑 Supprimer";
            FormTitle = _isEditing ? "Modifier le job" : "Nouveau job";
            LblName = "Nom";
            LblSource = "Chemin source";
            LblTarget = "Chemin destination";
            LblDiff = "Sauvegarde différentielle";
            BtnCancel = "Annuler";
            BtnSave = "Sauvegarder";
            BtnBrowse = "Parcourir...";
        }
        else
        {
            BtnNewJob = "＋ New Job";
            BtnRunSelected = "▶ Run Selected";
            BtnRunAll = "▶▶ Run All";
            BtnDelete = "🗑 Delete";
            FormTitle = _isEditing ? "Edit Job" : "New Job";
            LblName = "Name";
            LblSource = "Source path";
            LblTarget = "Target path";
            LblDiff = "Differential backup";
            BtnCancel = "Cancel";
            BtnSave = "Save";
            BtnBrowse = "Browse...";
        }
    }

    private void Refresh()
    {
        Jobs = new ObservableCollection<BackupJob>(_configRepo.Load());
        StatusMessage = string.Empty;
        StatusIsError = false;
    }

    private static string CleanPath(string path) => path.Trim().Trim('"').Trim('\'').Trim();

    // ── Ouvre l'explorateur pour choisir le dossier source ────────────────
    [RelayCommand]
    private async Task BrowseSource()
    {
        var path = await PickFolder();
        if (path != null) FormSource = path;
    }

    // ── Ouvre l'explorateur pour choisir le dossier cible ────────────────
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
            new FolderPickerOpenOptions
            {
                Title = _lang == "FR" ? "Sélectionner un dossier" : "Select a folder",
                AllowMultiple = false
            });

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    private bool ValidateForm()
    {
        var valid = true;

        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormNameError = _lang == "FR" ? "⚠ Le nom est requis." : "⚠ Name is required.";
            valid = false;
        }
        else FormNameError = string.Empty;

        var src = CleanPath(FormSource);
        if (string.IsNullOrWhiteSpace(src))
        {
            FormSourceError = _lang == "FR" ? "⚠ Le chemin source est requis." : "⚠ Source path is required.";
            valid = false;
        }
        else if (!Directory.Exists(src))
        {
            FormSourceError = _lang == "FR" ? $"⚠ Dossier introuvable : {src}" : $"⚠ Directory not found: {src}";
            valid = false;
        }
        else FormSourceError = string.Empty;

        var tgt = CleanPath(FormTarget);
        if (string.IsNullOrWhiteSpace(tgt))
        {
            FormTargetError = _lang == "FR" ? "⚠ Le chemin destination est requis." : "⚠ Target path is required.";
            valid = false;
        }
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
        FormTitle = _lang == "FR" ? "Nouveau job" : "New Job";
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
        {
            FormNameError = _lang == "FR" ? "⚠ Un job avec ce nom existe déjà." : "⚠ A job with this name already exists.";
            return;
        }

        if (_isEditing && SelectedJob != null)
        {
            var ex = jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);
            if (ex != null)
            {
                ex.Name = FormName;
                ex.SourcePath = src;
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
        SetSuccess(_lang == "FR"
            ? (_isEditing ? "✔ Job modifié." : "✔ Job créé.")
            : (_isEditing ? "✔ Job updated." : "✔ Job created."));
        Refresh();
    }

    [RelayCommand]
    private void CancelForm()
    {
        IsFormVisible = false;
        FormNameError = FormSourceError = FormTargetError = string.Empty;
    }

    [RelayCommand]
    private async Task RunSelected()
    {
        if (SelectedJob == null) { SetError(_lang == "FR" ? "⚠ Sélectionnez un job d'abord." : "⚠ Please select a job first."); return; }
        var settings = _settingsRepo.Load();
        if (TryRun(SelectedJob, settings))
            await SetSuccessAutoHide(_lang == "FR" ? $"✔ Sauvegarde terminée : {SelectedJob.Name}" : $"✔ Backup done: {SelectedJob.Name}");
        else
            SetError(_lang == "FR" ? $"⛔ Bloqué : '{settings.BusinessSoftware}' est en cours d'exécution." : $"⛔ Blocked: '{settings.BusinessSoftware}' is running.");
    }

    [RelayCommand]
    private async Task RunAll()
    {
        if (Jobs.Count == 0) { SetError(_lang == "FR" ? "⚠ Aucun job à lancer." : "⚠ No jobs to run."); return; }
        var settings = _settingsRepo.Load();
        int ran = 0, blocked = 0;
        foreach (var job in Jobs) { if (TryRun(job, settings)) ran++; else blocked++; }

        if (blocked > 0) SetError(_lang == "FR" ? $"⛔ {blocked} bloqué(s), {ran} terminé(s)." : $"⛔ {blocked} blocked, {ran} completed.");
        else await SetSuccessAutoHide(_lang == "FR" ? $"✔ {ran} sauvegarde(s) terminée(s)." : $"✔ All {ran} backup(s) done.");
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedJob == null) { SetError(_lang == "FR" ? "⚠ Sélectionnez un job d'abord." : "⚠ Please select a job first."); return; }
        var jobs = _configRepo.Load();
        jobs.RemoveAll(j => j.Id == SelectedJob.Id);
        _configRepo.Save(jobs);
        await SetSuccessAutoHide(_lang == "FR" ? $"✔ Job '{SelectedJob.Name}' supprimé." : $"✔ Job '{SelectedJob.Name}' deleted.");
        Refresh();
    }

    private bool TryRun(BackupJob job, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BusinessSoftware))
        {
            var name = settings.BusinessSoftware.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0) return false;
        }
        _backupService.RunBackup(job, settings);
        return true;
    }

    private async Task SetSuccessAutoHide(string msg)
    {
        StatusIsError = false;
        StatusMessage = msg;
        await Task.Delay(5000);
        StatusMessage = string.Empty;
    }

    private void SetError(string msg) { StatusMessage = msg; StatusIsError = true; }
    private void SetSuccess(string msg) { StatusMessage = msg; StatusIsError = false; }
}
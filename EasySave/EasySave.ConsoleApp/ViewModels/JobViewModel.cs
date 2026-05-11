using EasySave.Core;
using EasySave.Services.Interfaces;

namespace EasySave.ConsoleApp.ViewModels;

public class JobViewModel
{
    private readonly IConfigRepository _configRepo;
    private readonly IBackupService _backupService;
    private readonly ISettingsRepository _settingsRepo;

    public List<BackupJob> Jobs { get; private set; } = new();
    public string Message { get; private set; } = string.Empty;
    public bool HasError { get; private set; }

    public JobViewModel(IConfigRepository configRepo, IBackupService backupService, ISettingsRepository settingsRepo)
    {
        _configRepo = configRepo;
        _backupService = backupService;
        _settingsRepo = settingsRepo;
        Refresh();
    }

    public void Refresh()
    {
        Jobs = _configRepo.Load();
        ClearMessage();
    }

    public void CreateJob(string name, string source, string target, BackupType type)
    {
        Refresh();

        if (Jobs.Count >= 5) { SetError("createMaxReached"); return; }
        if (Jobs.Any(j => j.Name!.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { SetError("createNameExists"); return; }

        var newId = Jobs.Count == 0 ? 1 : Jobs.Max(j => j.Id) + 1;
        Jobs.Add(new BackupJob { Id = newId, Name = name, SourcePath = source, TargetPath = target, Type = type });
        _configRepo.Save(Jobs);
        SetSuccess("createOk");
    }

    public void RunJobs(IEnumerable<int> ids)
    {
        Refresh();
        var settings = _settingsRepo.Load();
        var results = new List<string>();

        foreach (var id in ids)
        {
            var job = Jobs.FirstOrDefault(j => j.Id == id);
            if (job != null)
            {
                // ← RunBackupAsync appelé de façon synchrone en console
                var controller = new JobController();
                _backupService.RunBackupAsync(job, settings, controller)
                              .GetAwaiter().GetResult();
                results.Add($"runDone:{job.Name}");
            }
            else
            {
                results.Add($"runNotFound:{id}");
            }
        }

        Message = string.Join("|", results);
        HasError = false;
    }

    public void DeleteJob(int id)
    {
        Refresh();
        var job = Jobs.FirstOrDefault(j => j.Id == id);
        if (job == null) { SetError("deleteNotFound"); return; }

        Jobs.Remove(job);
        _configRepo.Save(Jobs);
        SetSuccess("deleteOk");
    }

    private void SetSuccess(string key) { Message = key; HasError = false; }
    private void SetError(string key) { Message = key; HasError = true; }
    private void ClearMessage() { Message = string.Empty; HasError = false; }
}
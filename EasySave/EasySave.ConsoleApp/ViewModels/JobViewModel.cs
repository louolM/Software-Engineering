using EasySave.Core;
using EasySave.Infrastructure;

namespace EasySave.ConsoleApp.ViewModels;

/// <summary>
/// ViewModel principal : contient toute la logique métier exposée à la View.
/// La View ne fait qu'afficher et transmettre les saisies utilisateur.
/// </summary>
public class JobViewModel
{
    private readonly ConfigRepository _configRepo;
    private readonly BackupService _backupService;

    // ── État observable ────────────────────────────────────────────────────
    public List<BackupJob> Jobs { get; private set; } = new();
    public string Message { get; private set; } = string.Empty;
    public bool HasError { get; private set; }

    public JobViewModel(ConfigRepository configRepo, BackupService backupService)
    {
        _configRepo = configRepo;
        _backupService = backupService;
        Refresh();
    }

    // ── Commands ───────────────────────────────────────────────────────────

    /// <summary>Recharge la liste depuis config.json</summary>
    public void Refresh()
    {
        Jobs = _configRepo.Load();
        ClearMessage();
    }

    /// <summary>Crée un nouveau job (max 5, nom unique)</summary>
    public void CreateJob(string name, string source, string target, BackupType type)
    {
        Refresh();

        if (Jobs.Count >= 5)
        {
            SetError("createMaxReached");
            return;
        }

        if (Jobs.Any(j => j.Name!.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            SetError("createNameExists");
            return;
        }

        var newId = Jobs.Count == 0 ? 1 : Jobs.Max(j => j.Id) + 1;

        Jobs.Add(new BackupJob
        {
            Id = newId,
            Name = name,
            SourcePath = source,
            TargetPath = target,
            Type = type
        });

        _configRepo.Save(Jobs);
        SetSuccess("createOk");
    }

    /// <summary>Lance un ou plusieurs jobs par leurs IDs</summary>
    public void RunJobs(IEnumerable<int> ids)
    {
        Refresh();
        var results = new List<string>();

        foreach (var id in ids)
        {
            var job = Jobs.FirstOrDefault(j => j.Id == id);
            if (job != null)
            {
                _backupService.RunBackup(job);
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

    /// <summary>Supprime un job par son ID</summary>
    public void DeleteJob(int id)
    {
        Refresh();
        var job = Jobs.FirstOrDefault(j => j.Id == id);

        if (job == null)
        {
            SetError("deleteNotFound");
            return;
        }

        Jobs.Remove(job);
        _configRepo.Save(Jobs);
        SetSuccess("deleteOk");
    }

    // ── Helpers internes ───────────────────────────────────────────────────
    private void SetSuccess(string messageKey) { Message = messageKey; HasError = false; }
    private void SetError(string messageKey) { Message = messageKey; HasError = true; }
    private void ClearMessage() { Message = string.Empty; HasError = false; }
}
using EasyLog;
using EasySave.ConsoleApp;
using EasySave.Core;
using EasySave.Infrastructure;

// ── Services ────────────────────────────────────────────────────────────────
var configRepo = new ConfigRepository();
var backupSvc = new BackupService(new FileService(), new Logger(), new StateRepository());

// ── Langue ──────────────────────────────────────────────────────────────────
Console.Write("FR / EN ? ");
var lang = Console.ReadLine()?.Trim().ToUpper();
var t = new TranslationService(lang);

// ── Mode ligne de commande (ex: dotnet run -- 1-3) ──────────────────────────
if (args.Length > 0)
{
    var jobs = configRepo.Load();
    foreach (var id in ParseIds(args[0]))
    {
        var job = jobs.FirstOrDefault(j => j.Id == id);
        if (job != null) backupSvc.RunBackup(job);
        else Console.WriteLine(t.T("runNotFound") + id);
    }
    return;
}

// ── Boucle principale ────────────────────────────────────────────────────────
while (true)
{
    Console.WriteLine();
    Console.WriteLine(t.T("menu"));
    Console.WriteLine(t.T("opt1"));
    Console.WriteLine(t.T("opt2"));
    Console.WriteLine(t.T("opt3"));
    Console.WriteLine(t.T("opt4"));
    Console.WriteLine(t.T("opt5"));
    Console.Write(t.T("choice"));

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": CreateJob(); break;
        case "2": ViewJobs(); break;
        case "3": RunJob(); break;
        case "4": DeleteJob(); break;
        case "5": return;
        default: Console.WriteLine(t.T("invalidChoice")); break;
    }
}

// ── Créer un job ─────────────────────────────────────────────────────────────
void CreateJob()
{
    var jobs = configRepo.Load();

    if (jobs.Count >= 5)
    {
        Console.WriteLine(t.T("createMaxReached"));
        return;
    }

    Console.Write(t.T("createName"));
    var name = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(name)) return;

    if (jobs.Any(j => j.Name!.Equals(name, StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine(t.T("createNameExists"));
        return;
    }

    Console.Write(t.T("createSource"));
    var source = Console.ReadLine()?.Trim();

    Console.Write(t.T("createTarget"));
    var target = Console.ReadLine()?.Trim();

    Console.Write(t.T("createType"));
    var typeInput = Console.ReadLine()?.Trim();
    var type = typeInput == "2" ? BackupType.Differential : BackupType.Full;

    var newId = jobs.Count == 0 ? 1 : jobs.Max(j => j.Id) + 1;

    jobs.Add(new BackupJob
    {
        Id = newId,
        Name = name,
        SourcePath = source,
        TargetPath = target,
        Type = type
    });

    configRepo.Save(jobs);
    Console.WriteLine(t.T("createOk"));
}

// ── Voir les jobs ─────────────────────────────────────────────────────────────
void ViewJobs()
{
    var jobs = configRepo.Load();

    if (jobs.Count == 0)
    {
        Console.WriteLine(t.T("noJobs"));
        return;
    }

    Console.WriteLine(t.T("jobHeader"));
    Console.WriteLine(t.T("jobSep"));

    foreach (var j in jobs)
    {
        Console.WriteLine($"{j.Id,2} | {j.Name,-20} | {j.Type,-12} | {j.SourcePath} -> {j.TargetPath}");
    }
}

// ── Lancer un job ─────────────────────────────────────────────────────────────
void RunJob()
{
    ViewJobs();
    Console.WriteLine();
    Console.Write(t.T("runPrompt"));
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(input)) return;

    var jobs = configRepo.Load();

    foreach (var id in ParseIds(input))
    {
        var job = jobs.FirstOrDefault(j => j.Id == id);
        if (job != null)
        {
            backupSvc.RunBackup(job);
            Console.WriteLine(t.T("runDone") + job.Name);
        }
        else
        {
            Console.WriteLine(t.T("runNotFound") + id);
        }
    }
}

// ── Supprimer un job ──────────────────────────────────────────────────────────
void DeleteJob()
{
    ViewJobs();
    Console.WriteLine();
    Console.Write(t.T("deletePrompt"));
    var input = Console.ReadLine()?.Trim();

    if (!int.TryParse(input, out var id))
    {
        Console.WriteLine(t.T("invalidId"));
        return;
    }

    var jobs = configRepo.Load();
    var job = jobs.FirstOrDefault(j => j.Id == id);

    if (job == null)
    {
        Console.WriteLine(t.T("deleteNotFound"));
        return;
    }

    jobs.Remove(job);
    configRepo.Save(jobs);
    Console.WriteLine(t.T("deleteOk"));
}

// ── Helpers ───────────────────────────────────────────────────────────────────
static IEnumerable<int> ParseIds(string input)
{
    if (input.Contains('-'))
    {
        var parts = input.Split('-');
        if (int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
            for (int i = start; i <= end; i++) yield return i;
    }
    else if (input.Contains(';'))
    {
        foreach (var p in input.Split(';'))
            if (int.TryParse(p.Trim(), out var id)) yield return id;
    }
    else
    {
        if (int.TryParse(input, out var id)) yield return id;
    }
}
using EasyLog;
using EasySave.ConsoleApp;
using EasySave.ConsoleApp.ViewModels;
using EasySave.ConsoleApp.Views;
using EasySave.Infrastructure;
using EasySave.Services;
using EasySave.Services.Interfaces;

// ── Composition Root ──────────────────────────────────────────────────────────
IFileService fileService = new FileService();
IStateRepository stateRepo = new StateRepository();
IConfigRepository configRepo = new ConfigRepository();
IBackupService backupSvc = new BackupService(fileService, new Logger(), stateRepo);

Console.Write("EN(default) / FR ? ");
var lang = Console.ReadLine()?.Trim().ToUpper();
var t = new TranslationService(lang);

var vm = new JobViewModel(configRepo, backupSvc);

// ── Mode ligne de commande ────────────────────────────────────────────────────
if (args.Length > 0)
{
    vm.RunJobs(ParseIds(args[0]));
    return;
}

// ── Lancement de la View ──────────────────────────────────────────────────────
var view = new JobView(vm, t);
view.Run();

static IEnumerable<int> ParseIds(string input)
{
    if (input.Contains('-'))
    {
        var parts = input.Split('-');
        if (int.TryParse(parts[0], out var s) && int.TryParse(parts[1], out var e))
            for (int i = s; i <= e; i++) yield return i;
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
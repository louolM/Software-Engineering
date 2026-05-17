using EasyLog;
using EasySave.ConsoleApp;
using EasySave.ConsoleApp.ViewModels;
using EasySave.ConsoleApp.Views;
using EasySave.Infrastructure;
using EasySave.Services;
using EasySave.Services.Interfaces;

// Ask the user to choose a language at startup. Defaults to English.
Console.Write("EN(default) / FR ? ");
var lang = Console.ReadLine()?.Trim().ToUpper();
var t = new TranslationService(lang);

Console.Write("Log format JSON / XML ? ");
var logFormat = Console.ReadLine()?.Trim().ToUpper() ?? "JSON";

// Composition root: build and wire all dependencies before entering the UI loop.
IFileService fileService = new FileService();
IStateRepository stateRepo = new StateRepository();
IConfigRepository configRepo = new ConfigRepository();
ISettingsRepository settingsRepo = new SettingsRepository();  // ← AJOUT
IBackupService backupSvc = new BackupService(fileService, new Logger(logFormat), stateRepo);

var vm = new JobViewModel(configRepo, backupSvc, settingsRepo);  // ← MODIFIÉ

// Command-line mode: if job IDs are passed as arguments, run those jobs
// headlessly without entering the interactive menu, then exit.
if (args.Length > 0)
{
    vm.RunJobs(ParseIds(args[0]));
    return;
}

// Interactive mode: hand control to the console view.
var view = new JobView(vm, t);
view.Run();

// Parses a job ID argument into one or more integer IDs.
// Supports three formats:
//   "3"     -> a single ID
//   "1-3"   -> a range (1, 2, 3)
//   "1;3;5" -> a list of individual IDs
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
using Avalonia;
using EasySave.Core;
using EasySave.Infrastructure;
using EasySave.Services;
using EasySave.Services.Interfaces;
using EasyLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EasySave.UI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        // ── Mode ligne de commande ────────────────────────────────────────
        if (args.Length > 0)
        {
            Console.WriteLine($"Argument reçu: '{args[0]}'");
            var ids = ParseIds(args[0]).ToList();
            Console.WriteLine($"IDs parsés: {string.Join(", ", ids)}");

            IFileService fileService = new FileService();
            IStateRepository stateRepo = new StateRepository();
            IConfigRepository configRepo = new ConfigRepository();
            ISettingsRepository settingsRepo = new SettingsRepository();

            var settings = settingsRepo.Load();
            var backupSvc = new BackupService(fileService, new Logger(settings.LogFormat), stateRepo);
            var jobs = configRepo.Load();

            foreach (var id in ids)
            {
                var job = jobs.FirstOrDefault(j => j.Id == id);
                if (job != null)
                {
                    Console.WriteLine($"Running job: {job.Name}");
                    var controller = new JobController();
                    backupSvc.RunBackupAsync(job, settings, controller).GetAwaiter().GetResult();
                    Console.WriteLine($"Done: {job.Name}");
                }
                else
                {
                    Console.WriteLine($"Job not found: {id}");
                }
            }
            return;
        }

        // ── Mode graphique ────────────────────────────────────────────────
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static IEnumerable<int> ParseIds(string input)
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
}
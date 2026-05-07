using Avalonia;
using EasySave.Infrastructure;
using EasySave.Services;
using EasySave.Services.Interfaces;
using EasyLog;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace EasySave.UI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        // ── CLI Mode ────────────────────────────────────────
        if (args.Length > 0)
        {
            IFileService fileService = new FileService();
            IStateRepository stateRepo = new StateRepository();
            IConfigRepository configRepo = new ConfigRepository();
            ISettingsRepository settingsRepo = new SettingsRepository();

            var settings = settingsRepo.Load();
            var backupSvc = new BackupService(fileService, new Logger(settings.LogFormat), stateRepo);
            var jobs = configRepo.Load();

            foreach (var id in ParseIds(args[0]))
            {
                var job = jobs.FirstOrDefault(j => j.Id == id);
                if (job != null)
                {
                    Console.WriteLine($"Running job: {job.Name}");
                    backupSvc.RunBackup(job, settings);
                    Console.WriteLine($"Done: {job.Name}");
                }
                else
                {
                    Console.WriteLine($"Job not found: {id}");
                }
            }
            return;
        }

        // ── Graphic Mode ────────────────────────────────────────────────
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
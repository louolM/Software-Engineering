using Avalonia;
using System;

namespace EasySave.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
<<<<<<< Updated upstream
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
=======
    public static void Main(string[] args)
    {
        
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        
        // ── CLI Mode ────────────────────────────────────────
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
>>>>>>> Stashed changes

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

using EasyLog;
using EasySave.ConsoleApp;
using EasySave.Core;
using EasySave.Infrastructure;
using System.Linq;

var service = new BackupService(new FileService(), new Logger(), new StateRepository());

var jobs = new List<BackupJob>
{
    new BackupJob
    {
        Id = 1,
        Name = "Test",
        SourcePath = "C:\\Users\\Jules\\Documents\\GitHub\\Software-Engineering\\EasySave\\TestSource",
        TargetPath = "C:\\Users\\Jules\\Documents\\GitHub\\Software-Engineering\\EasySave\\TestTarget",
        Type = BackupType.Full
    }
};


if (args.Length > 0)
{
    var input = args[0];
    var jobsToRun = new List<int>();

    if (input.Contains("-"))
    {
        var parts = input.Split('-');
        int start = int.Parse(parts[0]);
        int end = int.Parse(parts[1]);

        for (int i = start; i <= end; i++)
            jobsToRun.Add(i);
    }
    else if (input.Contains(";"))
    {
        var parts = input.Split(';');
        foreach (var p in parts)
            jobsToRun.Add(int.Parse(p));
    }
    else
    {
        jobsToRun.Add(int.Parse(input));
    }

    foreach (var id in jobsToRun)
    {
        var job = jobs.FirstOrDefault(j => j.Id == id);
        if (job != null)
        {
            service.RunBackup(job);
        }
    }

    return;
}


Console.WriteLine("FR / EN ?");
var lang = Console.ReadLine()?.ToUpper();

var t = new TranslationService(lang);

while (true)
{
    Console.WriteLine(t.T("menu"));
    Console.WriteLine(t.T("run"));
    Console.WriteLine(t.T("exit"));

    var choice = Console.ReadLine();

    if (choice == "1")
    {
        Console.WriteLine(t.T("available"));

        foreach (var j in jobs)s
        {
            Console.WriteLine($"- {j.Name}");
        }

        Console.WriteLine(t.T("chooseJob"));
        var name = Console.ReadLine();

        var job = jobs.FirstOrDefault(j =>
            j.Name.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (job != null)
        {
            service.RunBackup(job);
            Console.WriteLine("Done !");
        }
        else
        {
            Console.WriteLine("Job not found");
        }
    }
    else if (choice == "2")
    {
        break;
    }
}
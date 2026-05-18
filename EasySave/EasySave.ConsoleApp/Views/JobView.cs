using EasySave.ConsoleApp.ViewModels;
using EasySave.Core;

namespace EasySave.ConsoleApp.Views;

// Console view for the job management UI.
// Contains no business logic: it reads user input, delegates to the ViewModel,
// then displays whatever the ViewModel puts in Jobs, Message, and HasError.
public class JobView
{
    private readonly JobViewModel _vm;
    private readonly TranslationService _t;

    public JobView(JobViewModel vm, TranslationService t)
    {
        _vm = vm;
        _t = t;
    }

    // Main interactive loop. Displays the menu and dispatches to the
    // appropriate handler until the user chooses to quit.
    public void Run()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(_t.T("menu"));
            Console.WriteLine(_t.T("opt1"));
            Console.WriteLine(_t.T("opt2"));
            Console.WriteLine(_t.T("opt3"));
            Console.WriteLine(_t.T("opt4"));
            Console.WriteLine(_t.T("opt5"));
            Console.Write(_t.T("choice"));

            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1": ShowCreate(); break;
                case "2": ShowList(); break;
                case "3": ShowRun(); break;
                case "4": ShowDelete(); break;
                case "5": return;
                default: Console.WriteLine(_t.T("invalidChoice")); break;
            }
        }
    }
    // Prompts the user for job fields, calls CreateJob, and prints the result.

    private void ShowCreate()
    {
        Console.Write(_t.T("createName"));
        var name = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        Console.Write(_t.T("createSource"));
        var source = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.Write(_t.T("createTarget"));
        var target = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.Write(_t.T("createType"));
        var typeInput = Console.ReadLine()?.Trim();
        var type = typeInput == "2" ? BackupType.Differential : BackupType.Full;

        _vm.CreateJob(name, source, target, type);
        PrintMessage();
    }

    // Prints all saved jobs in a fixed-width table format.
    private void ShowList()
    {
        _vm.Refresh();

        if (_vm.Jobs.Count == 0)
        {
            Console.WriteLine(_t.T("noJobs"));
            return;
        }

        Console.WriteLine(_t.T("jobHeader"));
        Console.WriteLine(_t.T("jobSep"));

        foreach (var j in _vm.Jobs)
            Console.WriteLine($"{j.Id,2} | {j.Name,-20} | {j.Type,-12} | {j.SourcePath} -> {j.TargetPath}");
    }

    
    // Shows the job list, prompts for IDs, runs the selected jobs, then prints
    // the composite result message returned by the ViewModel.
    private void ShowRun()
    {
        ShowList();
        Console.WriteLine();
        Console.Write(_t.T("runPrompt"));
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        _vm.RunJobs(ParseIds(input));

        // The ViewModel packs results as "translationKey:value" pairs separated by "|".
        // Split them here and translate each key to display a localised line per job.
        foreach (var part in _vm.Message.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = part.Split(':', 2);
            var key = segments[0];
            var value = segments.Length > 1 ? segments[1] : string.Empty;

            Console.WriteLine(_t.T(key) + value);
        }
    }

    // Shows the job list, prompts for an ID, and deletes the selected job.
    private void ShowDelete()
    {
        ShowList();
        Console.WriteLine();
        Console.Write(_t.T("deletePrompt"));
        var input = Console.ReadLine()?.Trim();

        if (!int.TryParse(input, out var id))
        {
            Console.WriteLine(_t.T("invalidId"));
            return;
        }

        _vm.DeleteJob(id);
        PrintMessage();
    }

    // Translates the ViewModel's message key and prints it to the console.
    private void PrintMessage()
    {
        if (!string.IsNullOrEmpty(_vm.Message))
            Console.WriteLine(_t.T(_vm.Message));
    }

    // Parses a job ID argument into one or more integer IDs.
    // Supports three formats:
    //   "3"     -> a single ID
    //   "1-3"   -> a range (1, 2, 3)
    //   "1;3;5" -> a list of individual IDs
    private static IEnumerable<int> ParseIds(string input)
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
}
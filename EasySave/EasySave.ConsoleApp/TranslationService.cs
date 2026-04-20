namespace EasySave.ConsoleApp;

public class TranslationService
{
    private readonly Dictionary<string, string> _translations;

    public TranslationService(string? language)
    {
        if (language == "FR")
        {
            _translations = new Dictionary<string, string>
            {
                { "menu", "Choisissez une action :" },
                { "run", "1. Lancer un backup" },
                { "exit", "2. Quitter" },
                { "chooseJob", "Entrez le nom du backup :" },
                { "available", "Backups disponibles :" }
                
            };
        }
        else
        {
            _translations = new Dictionary<string, string>
            {
                { "menu", "Choose an action:" },
                { "run", "1. Run backup" },
                { "exit", "2. Exit" },
                { "available", "Available backups:" },
                { "chooseJob", "Enter backup name:" }
            };
        }
    }

    public string T(string key)
    {
        return _translations.ContainsKey(key) ? _translations[key] : key;
    }
}
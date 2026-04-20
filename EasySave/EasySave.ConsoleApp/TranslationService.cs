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
                { "menu",           "=== MENU ===" },
                { "opt1",           "1. Créer un job" },
                { "opt2",           "2. Voir les jobs" },
                { "opt3",           "3. Lancer un job" },
                { "opt4",           "4. Supprimer un job" },
                { "opt5",           "5. Quitter" },
                { "choice",         "Votre choix : " },
                { "invalidChoice",  "Choix invalide." },

                // Créer
                { "createName",     "Nom du job : " },
                { "createSource",   "Chemin source : " },
                { "createTarget",   "Chemin destination : " },
                { "createType",     "Type (1=Full, 2=Differential) : " },
                { "createOk",       "Job créé avec succès !" },
                { "createMaxReached","Limite de 5 jobs atteinte. Supprimez un job avant d'en créer un nouveau." },
                { "createNameExists","Un job avec ce nom existe déjà." },

                // Voir
                { "noJobs",         "Aucun job disponible." },
                { "jobHeader",      "ID | Nom                 | Type         | Source -> Destination" },
                { "jobSep",         "------------------------------------------------------------" },

                // Lancer
                { "runPrompt",      "Entrez l'ID ou la plage à lancer (ex: 1 / 1-3 / 1;3) : " },
                { "runDone",        "Backup terminé : " },
                { "runNotFound",    "Job introuvable : " },

                // Supprimer
                { "deletePrompt",   "Entrez l'ID du job à supprimer : " },
                { "deleteOk",       "Job supprimé." },
                { "deleteNotFound", "Job introuvable." },

                // Général
                { "invalidId",      "ID invalide." },
            };
        }
        else
        {
            _translations = new Dictionary<string, string>
            {
                { "menu",           "=== MENU ===" },
                { "opt1",           "1. Create a job" },
                { "opt2",           "2. View jobs" },
                { "opt3",           "3. Run a job" },
                { "opt4",           "4. Delete a job" },
                { "opt5",           "5. Quit" },
                { "choice",         "Your choice: " },
                { "invalidChoice",  "Invalid choice." },

                // Create
                { "createName",     "Job name: " },
                { "createSource",   "Source path: " },
                { "createTarget",   "Target path: " },
                { "createType",     "Type (1=Full, 2=Differential): " },
                { "createOk",       "Job created successfully!" },
                { "createMaxReached","5 job limit reached. Delete a job before creating a new one." },
                { "createNameExists","A job with this name already exists." },

                // View
                { "noJobs",         "No jobs available." },
                { "jobHeader",      "ID | Name                 | Type         | Source -> Destination" },
                { "jobSep",         "------------------------------------------------------------" },

                // Run
                { "runPrompt",      "Enter ID or range to run (e.g. 1 / 1-3 / 1;3): " },
                { "runDone",        "Backup done: " },
                { "runNotFound",    "Job not found: " },

                // Delete
                { "deletePrompt",   "Enter job ID to delete: " },
                { "deleteOk",       "Job deleted." },
                { "deleteNotFound", "Job not found." },

                // General
                { "invalidId",      "Invalid ID." },
            };
        }
    }

    public string T(string key) =>
        _translations.TryGetValue(key, out var val) ? val : key;
}
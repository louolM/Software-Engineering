using System.Collections.Generic;

namespace EasySave.UI;

/// <summary>
/// Service de traduction centralisé.
/// Toutes les chaînes UI sont ici. Pour ajouter une langue, ajouter un bloc.
/// </summary>
public class TranslationService
{
    private readonly Dictionary<string, string> _translations;

    public string Language { get; private set; } = "EN";

    public TranslationService(string language = "EN")
    {
        Language = language;
        _translations = language == "FR" ? French() : English();
    }

    public string T(string key) =>
        _translations.TryGetValue(key, out var val) ? val : key;

    // ── ENGLISH ──────────────────────────────────────────────────────────
    private static Dictionary<string, string> English() => new()
    {
        // Navigation
        ["nav.jobs"] = "📋 Jobs",
        ["nav.settings"] = "⚙ Settings",

        // JobList — boutons
        ["jobs.new"] = "＋ New Job",
        ["jobs.runSelected"] = "▶ Run Selected",
        ["jobs.runAll"] = "▶▶ Run All",
        ["jobs.delete"] = "🗑 Delete",

        // JobList — formulaire
        ["form.title.create"] = "New Job",
        ["form.title.edit"] = "Edit Job",
        ["form.name"] = "Name",
        ["form.source"] = "Source path",
        ["form.target"] = "Target path",
        ["form.diff"] = "Differential backup",
        ["form.browse"] = "Browse...",
        ["form.cancel"] = "Cancel",
        ["form.save"] = "Save",

        // JobList — messages
        ["jobs.created"] = "✔ Job created.",
        ["jobs.updated"] = "✔ Job updated.",
        ["jobs.deleted"] = "✔ Job '{0}' deleted.",
        ["jobs.backupDone"] = "✔ Backup done: {0}",
        ["jobs.allDone"] = "✔ All {0} backup(s) done.",
        ["jobs.blocked"] = "⛔ Blocked: '{0}' is running.",
        ["jobs.blockedMany"] = "⛔ {0} blocked, {1} completed.",
        ["jobs.noJobs"] = "⚠ No jobs to run.",
        ["jobs.selectFirst"] = "⚠ Please select a job first.",

        // JobList — erreurs formulaire
        ["err.nameRequired"] = "⚠ Name is required.",
        ["err.nameExists"] = "⚠ A job with this name already exists.",
        ["err.sourceRequired"] = "⚠ Source path is required.",
        ["err.sourceNotFound"] = "⚠ Directory not found: {0}",
        ["err.targetRequired"] = "⚠ Target path is required.",

        // Settings — titres
        ["settings.title"] = "⚙ Settings",
        ["settings.language"] = "Language",
        ["settings.business.title"] = "Business Software Detection",
        ["settings.business.desc"] = "If this process is running, backup jobs will be blocked. (e.g. Calculator)",
        ["settings.encrypt.title"] = "CryptoSoft Encryption",
        ["settings.encrypt.desc"] = "Extensions to encrypt (space-separated). e.g: .txt .docx .pdf",
        ["settings.encrypt.keyLabel"] = "Encryption Key",
        ["settings.log.title"] = "Log File Format",
        ["settings.save"] = "💾 Save Settings",
        ["settings.saved"] = "✔ Settings saved.",

        // Settings — erreurs
        ["err.invalidExtensions"] = "⚠ Invalid extensions (must start with '.'): {0}",
        ["err.keyRequired"] = "⚠ Encryption key required.",

        // Divers
        ["picker.title"] = "Select a folder",
    };

    // ── FRANÇAIS ─────────────────────────────────────────────────────────
    private static Dictionary<string, string> French() => new()
    {
        // Navigation
        ["nav.jobs"] = "📋 Tâche",
        ["nav.settings"] = "⚙ Paramètres",

        // JobList — boutons
        ["jobs.new"] = "＋ Nouveau",
        ["jobs.runSelected"] = "▶ Lancer",
        ["jobs.runAll"] = "▶▶ Tout lancer",
        ["jobs.delete"] = "🗑 Supprimer",

        // JobList — formulaire
        ["form.title.create"] = "Nouvelle tâche",
        ["form.title.edit"] = "Modifier la tâche",
        ["form.name"] = "Nom",
        ["form.source"] = "Chemin source",
        ["form.target"] = "Chemin destination",
        ["form.diff"] = "Sauvegarde différentielle",
        ["form.browse"] = "Parcourir...",
        ["form.cancel"] = "Annuler",
        ["form.save"] = "Sauvegarder",

        // JobList — messages
        ["jobs.created"] = "✔ tâche créée.",
        ["jobs.updated"] = "✔ Tâche modifiée.",
        ["jobs.deleted"] = "✔ Tâche '{0}' supprimée.",
        ["jobs.backupDone"] = "✔ Sauvegarde terminée : {0}",
        ["jobs.allDone"] = "✔ {0} sauvegarde(s) terminée(s).",
        ["jobs.blocked"] = "⛔ Bloqué : '{0}' est en cours d'exécution.",
        ["jobs.blockedMany"] = "⛔ {0} bloqué(s), {1} terminé(s).",
        ["jobs.noJobs"] = "⚠ Aucune tâche à lancer.",
        ["jobs.selectFirst"] = "⚠ Sélectionnez une tâche d'abord.",

        // JobList — erreurs formulaire
        ["err.nameRequired"] = "⚠ Le nom est requis.",
        ["err.nameExists"] = "⚠ Une tâche avec ce nom existe déjà.",
        ["err.sourceRequired"] = "⚠ Le chemin source est requis.",
        ["err.sourceNotFound"] = "⚠ Dossier introuvable : {0}",
        ["err.targetRequired"] = "⚠ Le chemin destination est requis.",

        // Settings — titres
        ["settings.title"] = "⚙ Paramètres",
        ["settings.language"] = "Langue",
        ["settings.business.title"] = "Détection logiciel métier",
        ["settings.business.desc"] = "Si ce processus est en cours, les sauvegardes seront bloquées. (ex: Calculator)",
        ["settings.encrypt.title"] = "Chiffrement CryptoSoft",
        ["settings.encrypt.desc"] = "Extensions à chiffrer (séparées par un espace). ex: .txt .docx .pdf",
        ["settings.encrypt.keyLabel"] = "Clé de chiffrement",
        ["settings.log.title"] = "Format du fichier log",
        ["settings.save"] = "💾 Sauvegarder",
        ["settings.saved"] = "✔ Paramètres sauvegardés.",

        // Settings — erreurs
        ["err.invalidExtensions"] = "⚠ Extensions invalides (doivent commencer par '.'): {0}",
        ["err.keyRequired"] = "⚠ Clé de chiffrement requise.",

        // Divers
        ["picker.title"] = "Sélectionner un dossier",
    };
}
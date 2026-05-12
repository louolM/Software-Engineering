namespace EasySave.Core;

public class AppSettings
{
    // ── v1.0 / v2.0 ──────────────────────────────────────────────────────
    public string BusinessSoftware { get; set; } = "";
    public List<string> EncryptedExtensions { get; set; } = new();
    public string EncryptionKey { get; set; } = "defaultkey";
    public string LogFormat { get; set; } = "JSON";
    public string Language { get; set; } = "EN";

    // ── v3.0 - nouveaux champs ────────────────────────────────────────────

    /// <summary>Extensions prioritaires : aucun fichier non-prioritaire
    /// ne sera copié tant qu'il reste des fichiers prioritaires en attente.
    /// Ex: [".pdf", ".docx"]</summary>
    public List<string> PriorityExtensions { get; set; } = new();

    /// <summary>Taille maximale (en KB) pour transfert parallèle.
    /// Deux fichiers dépassant cette taille ne peuvent pas être copiés
    /// en même temps. 0 = pas de limite.</summary>
    public long MaxParallelFileSize { get; set; } = 1024;

    /// <summary>Destination des logs : "Local", "Docker", "Both".</summary>
    public string LogDestination { get; set; } = "Local";

    /// <summary>URL du serveur Docker pour la centralisation des logs.
    /// Ex: "http://localhost:5000/logs"</summary>
    public string DockerLogUrl { get; set; } = "";
}
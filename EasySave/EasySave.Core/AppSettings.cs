namespace EasySave.Core;

// Holds all user-configurable application settings.
// Persisted to "settings.json" by SettingsRepository and loaded at startup.
public class AppSettings
{
    // ── v1.0 / v2.0 ──────────────────────────────────────────────────────
    public string BusinessSoftware { get; set; } = "";
    public List<string> EncryptedExtensions { get; set; } = new();
    public string EncryptionKey { get; set; } = "defaultkey";
    public string LogFormat { get; set; } = "JSON";
    public string Language { get; set; } = "EN";

    // ── v3.0 - nouveaux champs ────────────────────────────────────────────

    // File extensions that must be fully transferred before any non-priority
    // file is started. For example [".pdf", ".docx"].
    // 0 priority extensions means all files are treated equally.
    public List<string> PriorityExtensions { get; set; } = new();

    // Maximum file size in KB that allows parallel copying.
    // When two files both exceed this threshold they cannot be copied at the
    // same time; one waits for the other to finish.
    // 0 disables the limit and allows all files to copy in parallel.
    public long MaxParallelFileSize { get; set; } = 1024;

    // Controls where log entries are written: "Local", "Docker", or "Both".
    public string LogDestination { get; set; } = "Local";

    // HTTP endpoint of the central log server when LogDestination includes Docker.
    // Example: "http://localhost:5000"
    public string DockerLogUrl { get; set; } = "";
}
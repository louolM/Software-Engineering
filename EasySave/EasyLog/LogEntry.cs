using System.Xml.Serialization;

namespace EasyLog;

[XmlRoot("LogEntry")]
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string? BackupName { get; set; }
    public string? SourcePath { get; set; }
    public string? TargetPath { get; set; }
    public long FileSize { get; set; }
    public long TransferTime { get; set; }
    public long EncryptionTime { get; set; } // (0=pas chiffré, >0=ms, <0=erreur)
}
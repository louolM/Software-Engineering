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
}

// Represents a single entry written to the daily backup log.
// Each time a file is copied (successfully or not), one LogEntry is created and appended to the JSON log file for the current day containing timestamp, directories infos, file info, transfer size .
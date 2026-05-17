using System.Xml.Serialization;

namespace EasyLog;

// Represents a single file-transfer event recorded after each file is copied.
// The [XmlRoot] attribute sets the element name used when serializing to XML.
[XmlRoot("LogEntry")]
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string? BackupName { get; set; }
    public string? SourcePath { get; set; }
    public string? TargetPath { get; set; }
    public long FileSize { get; set; }
    // Transfer duration in milliseconds. -1 means the copy failed.
    public long TransferTime { get; set; }
    
    // Encryption duration in milliseconds.
    // 0 means the file was not encrypted, a positive value is the duration,
    // and -99 means encryption failed.
    public long EncryptionTime { get; set; } 
}
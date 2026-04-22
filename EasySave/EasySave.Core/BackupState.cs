namespace EasySave.Core;
// Represents the real-time progress snapshot of a running (or completed) backup job.
//
// This object is serialized to "state.json" after every file copy so that
// an external monitoring tool can poll the file and display live progress
// without being coupled to the application itself.i:w:wq:q
public class BackupState
{
    public string? Name { get; set; }
    public DateTime LastActionTime { get; set; }
    public string? Status { get; set; }
    public int TotalFiles { get; set; }
    public int RemainingFiles { get; set; }
    public long TotalSize { get; set; }
    public long RemainingSize { get; set; }
    public string? CurrentSourceFile { get; set; }
    public string? CurrentTargetFile { get; set; }
}
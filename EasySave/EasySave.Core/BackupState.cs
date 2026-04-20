namespace EasySave.Core;

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
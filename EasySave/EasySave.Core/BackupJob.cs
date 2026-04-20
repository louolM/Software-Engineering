using EasySave.Core;

public class BackupJob
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string SourcePath { get; set; }
    public string TargetPath { get; set; }
    public BackupType Type { get; set; }
}
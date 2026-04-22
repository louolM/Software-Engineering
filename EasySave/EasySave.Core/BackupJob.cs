namespace EasySave.Core;

// Represents a backup job configuration.
//
// A backup job is the basic unit managed by the application. It describes
// where files should be copied from, where they should go, and which backup
// strategy should be applied. Jobs are persisted to "config.json"
public class BackupJob
{
    // Unique numeric identifier for this job.
    // Assigned automatically when the job is created
    public int Id { get; set; }
    
    // Human-readable name given to the job, Must be unique across all jobs (case sensitive)
    public string? Name { get; set; }
    
    // Absolute path to the directory whose contents will be backed up.
    public string? SourcePath { get; set; }
    
    // Absolute path to the directory where files will be copied.
    public string? TargetPath { get; set; }
   
    // Full ou diff
    public BackupType Type { get; set; }
}
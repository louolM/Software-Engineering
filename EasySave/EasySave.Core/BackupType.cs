namespace EasySave.Core;

// Defines the backup strategy applied when a BackupJob is executed.
public enum BackupType
{
    // Full backup: every file in the source directory is copied to the target,
    // regardless of whether it already exists there or when it was last modified.
    // This is the safest strategy but may copy unchanged files unnecessarily.
    Full,
    
    // Differential backup: a file is only copied if it does not yet exist at the
    // target path, or if the source file was modified more recently than the target file (based on last-write timestamps).
    // This avoids redundant copies and is faster for large, mostly-unchanged datasets. 
    Differential
}
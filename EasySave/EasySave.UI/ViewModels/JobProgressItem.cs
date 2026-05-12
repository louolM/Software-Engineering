using CommunityToolkit.Mvvm.ComponentModel;

namespace EasySave.UI.ViewModels;

/// <summary>
/// État observable d'un job en cours - affiché en temps réel dans la liste.
/// </summary>
public partial class JobProgressItem : ObservableObject
{
    public int JobId { get; }
    public string JobName { get; }
    public string JobType { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }

    [ObservableProperty] private double _percent;
    [ObservableProperty] private string _progressText = "0%";
    [ObservableProperty] private string _status = "IDLE";

    public JobProgressItem(int jobId, string jobName, string jobType, string sourcePath, string targetPath)
    {
        JobId = jobId;
        JobName = jobName;
        JobType = jobType;
        SourcePath = sourcePath;
        TargetPath = targetPath;
    }
}
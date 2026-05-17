using EasySave.Core;
using System.Diagnostics;

namespace EasySave.Services;

// Monitors a configured business software process in the background.
// Automatically pauses all active JobControllers when the process starts,
// and resumes them automatically when the process exits.
public class BusinessSoftwareWatcher : IDisposable
{
    private readonly string _processName;
    private readonly List<JobController> _controllers;
    private readonly CancellationTokenSource _cts = new();
    // Tracks whether the process was running on the last check,
    // so transitions (not-running -> running and vice-versa) are only acted on once.
    private bool _wasRunning = false;

    public BusinessSoftwareWatcher(string processName, List<JobController> controllers)
    {
        // Strip ".exe" because Process.GetProcessesByName does not include it.
        _processName = processName
            .Replace(".exe", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        _controllers = controllers;
    }

    // Starts the background polling loop.
    // Does nothing if no process name is configured.
    public void Start()
    {
        if (string.IsNullOrWhiteSpace(_processName)) return;

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var isRunning = Process.GetProcessesByName(_processName).Length > 0;

                if (isRunning && !_wasRunning)
                {
                    // Business software just started: pause all running jobs.
                    foreach (var ctrl in _controllers)
                        if (!ctrl.IsPaused && !ctrl.IsStopped)
                            ctrl.Pause();

                    _wasRunning = true;
                }
                else if (!isRunning && _wasRunning)
                {
                    // Business software just closed: resume all paused jobs.
                    foreach (var ctrl in _controllers)
                        if (ctrl.IsPaused)
                            ctrl.Resume();

                    _wasRunning = false;
                }

                // Polls every 2 seconds to keep CPU usage negligible.
                await Task.Delay(2000, _cts.Token);
            }
        }, _cts.Token);
    }

    public void Stop() => _cts.Cancel();

    public void Dispose() => _cts.Dispose();
}
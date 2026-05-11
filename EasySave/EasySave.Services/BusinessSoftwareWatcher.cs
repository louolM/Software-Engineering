using EasySave.Core;
using System.Diagnostics;

namespace EasySave.Services;

/// <summary>
/// Surveille en arrière-plan si un logiciel métier est lancé/fermé.
/// Pause automatiquement tous les JobControllers actifs si détecté,
/// les reprend automatiquement quand le logiciel est fermé.
/// </summary>
public class BusinessSoftwareWatcher : IDisposable
{
    private readonly string _processName;
    private readonly List<JobController> _controllers;
    private readonly CancellationTokenSource _cts = new();
    private bool _wasRunning = false;

    public BusinessSoftwareWatcher(string processName, List<JobController> controllers)
    {
        _processName = processName
            .Replace(".exe", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        _controllers = controllers;
    }

    /// <summary>Démarre la surveillance en arrière-plan.</summary>
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
                    // Logiciel métier vient de démarrer → pause tous les jobs
                    foreach (var ctrl in _controllers)
                        if (!ctrl.IsPaused && !ctrl.IsStopped)
                            ctrl.Pause();

                    _wasRunning = true;
                }
                else if (!isRunning && _wasRunning)
                {
                    // Logiciel métier vient de se fermer → reprend tous les jobs
                    foreach (var ctrl in _controllers)
                        if (ctrl.IsPaused)
                            ctrl.Resume();

                    _wasRunning = false;
                }

                // Vérifie toutes les 2 secondes
                await Task.Delay(2000, _cts.Token);
            }
        }, _cts.Token);
    }

    public void Stop() => _cts.Cancel();

    public void Dispose() => _cts.Dispose();
}
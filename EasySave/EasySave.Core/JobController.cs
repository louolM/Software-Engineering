namespace EasySave.Core;

/// <summary>
/// Contrôleur individuel pour un job de backup.
/// Permet de Pause / Resume / Stop un job en cours d'exécution.
/// </summary>
public class JobController
{
    private CancellationTokenSource _cts = new();
    private ManualResetEventSlim _pauseEvent = new(true); // true = non pausé

    public CancellationToken Token => _cts.Token;
    public bool IsPaused { get; private set; }
    public bool IsStopped => _cts.IsCancellationRequested;

    /// <summary>Met le job en pause après le fichier en cours.</summary>
    public void Pause()
    {
        IsPaused = true;
        _pauseEvent.Reset();
    }

    /// <summary>Reprend un job en pause.</summary>
    public void Resume()
    {
        IsPaused = false;
        _pauseEvent.Set();
    }

    /// <summary>Arrête immédiatement le job.</summary>
    public void Stop()
    {
        _cts.Cancel();
        _pauseEvent.Set(); // débloque le wait si en pause
    }

    /// <summary>
    /// Appelé par BackupService à chaque fichier.
    /// Bloque si en pause, lance OperationCanceledException si stoppé.
    /// </summary>
    public void WaitIfPaused() => _pauseEvent.Wait(_cts.Token);

    /// <summary>Réinitialise le contrôleur pour pouvoir relancer le job.</summary>
    public void Reset()
    {
        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        IsPaused = false;
    }
}
namespace EasySave.Core;

// Controls the execution flow of a single running backup job.
// Allows the caller to pause, resume, or stop the job at any point between file copies.
public class JobController
{
    private CancellationTokenSource _cts = new();
    // ManualResetEventSlim starts in the "set" state (true), meaning the job is not paused.
    // Calling Reset() blocks threads waiting on it; Set() unblocks them.
    private ManualResetEventSlim _pauseEvent = new(true); // true = non pausé

    // CancellationToken passed to async operations inside BackupService so they can
    // observe a stop request and exit cleanly.
    public CancellationToken Token => _cts.Token;
    // True once Stop() has been called and the cancellation has been requested.
    public bool IsPaused { get; private set; }
    // True once Stop() has been called and the cancellation has been requested.
    public bool IsStopped => _cts.IsCancellationRequested;

    // Signals the job to pause after the current file finishes.
    public void Pause()
    {
        IsPaused = true;
        _pauseEvent.Reset();
    }

    // Unblocks a paused job so it continues with the next file.
    public void Resume()
    {
        IsPaused = false;
        _pauseEvent.Set();
    }

    // Requests immediate cancellation and unblocks any pause wait so the job loop can exit.
    public void Stop()
    {
        _cts.Cancel();
        _pauseEvent.Set(); // unblocks WaitIfPausedAsync if the job is currently paused
    }

    // Called by BackupService before each file transfer.
    // Polls every 100ms while paused, and throws OperationCanceledException if the job
    // is stopped while waiting, so the caller's catch block handles the exit.
    public async Task WaitIfPausedAsync(CancellationToken token)
    {
        while (IsPaused && !token.IsCancellationRequested)
            await Task.Delay(100, token);
    }

    // Resets the controller to its initial state so the same job can be run again.
    // A new CancellationTokenSource is needed because a cancelled source cannot be reused.
    public void Reset()
    {
        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        IsPaused = false;
    }
}
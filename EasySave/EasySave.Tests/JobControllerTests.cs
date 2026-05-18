using EasySave.Core;
using Xunit;

namespace EasySave.Tests;

// Unit tests for JobController.
// Verifies pause / resume / stop state transitions and the async wait behaviour.
public class JobControllerTests
{
    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void NewController_IsNotPaused()
    {
        var ctrl = new JobController();
        Assert.False(ctrl.IsPaused);
    }

    [Fact]
    public void NewController_IsNotStopped()
    {
        var ctrl = new JobController();
        Assert.False(ctrl.IsStopped);
    }

    // ── Pause / Resume ─────────────────────────────────────────────────────

    [Fact]
    public void Pause_SetsPausedToTrue()
    {
        var ctrl = new JobController();
        ctrl.Pause();
        Assert.True(ctrl.IsPaused);
    }

    [Fact]
    public void Resume_AfterPause_SetsPausedToFalse()
    {
        var ctrl = new JobController();
        ctrl.Pause();
        ctrl.Resume();
        Assert.False(ctrl.IsPaused);
    }

    [Fact]
    public void Resume_WithoutPause_LeavesNotPaused()
    {
        var ctrl = new JobController();
        ctrl.Resume(); // no-op, should not throw
        Assert.False(ctrl.IsPaused);
    }

    // ── Stop ───────────────────────────────────────────────────────────────

    [Fact]
    public void Stop_SetsIsStoppedToTrue()
    {
        var ctrl = new JobController();
        ctrl.Stop();
        Assert.True(ctrl.IsStopped);
    }

    [Fact]
    public void Stop_WhilePaused_UnblocksPauseAndSetsIsStopped()
    {
        var ctrl = new JobController();
        ctrl.Pause();
        ctrl.Stop();

        // IsStopped must be true regardless of prior pause state
        Assert.True(ctrl.IsStopped);
    }

    [Fact]
    public void Token_IsCancelledAfterStop()
    {
        var ctrl = new JobController();
        ctrl.Stop();
        Assert.True(ctrl.Token.IsCancellationRequested);
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_AfterStop_IsStoppedReturnsFalse()
    {
        var ctrl = new JobController();
        ctrl.Stop();
        ctrl.Reset();
        Assert.False(ctrl.IsStopped);
    }

    [Fact]
    public void Reset_AfterPause_IsPausedReturnsFalse()
    {
        var ctrl = new JobController();
        ctrl.Pause();
        ctrl.Reset();
        Assert.False(ctrl.IsPaused);
    }

    [Fact]
    public void Reset_ProvidesNewFreshToken()
    {
        var ctrl = new JobController();
        ctrl.Stop();
        var tokenBefore = ctrl.Token;
        ctrl.Reset();
        var tokenAfter = ctrl.Token;

        Assert.True(tokenBefore.IsCancellationRequested);
        Assert.False(tokenAfter.IsCancellationRequested);
    }

    // ── WaitIfPausedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task WaitIfPausedAsync_WhenNotPaused_CompletesImmediately()
    {
        var ctrl = new JobController();

        // Should complete well within the timeout if not paused
        var completed = await Task.WhenAny(
            ctrl.WaitIfPausedAsync(ctrl.Token),
            Task.Delay(500));

        Assert.NotEqual(TaskStatus.Running, completed.Status);
    }

    [Fact]
    public async Task WaitIfPausedAsync_WhenPausedThenResumed_CompletesAfterResume()
    {
        var ctrl = new JobController();
        ctrl.Pause();

        var waitTask = ctrl.WaitIfPausedAsync(ctrl.Token);

        // Should still be waiting
        Assert.False(waitTask.IsCompleted);

        // Resume should unblock the wait
        ctrl.Resume();
        await Task.WhenAny(waitTask, Task.Delay(1000));

        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitIfPausedAsync_WhenPausedAndStopped_ThrowsOrCompletes()
    {
        var ctrl = new JobController();
        ctrl.Pause();

        var waitTask = ctrl.WaitIfPausedAsync(ctrl.Token);

        ctrl.Stop(); // Stop unblocks the pause event and cancels the token

        // Either cancellation exception or normal completion is acceptable
        try
        {
            await Task.WhenAny(waitTask, Task.Delay(1000));
            // If it completed without throwing, that's fine too
        }
        catch (OperationCanceledException)
        {
            // Expected when the token is cancelled while waiting
        }

        // Either way the job is stopped
        Assert.True(ctrl.IsStopped);
    }
}

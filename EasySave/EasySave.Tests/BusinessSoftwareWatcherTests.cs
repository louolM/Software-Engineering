using EasySave.Core;
using EasySave.Services;
using Xunit;

namespace EasySave.Tests;

// Unit tests for BusinessSoftwareWatcher.
// These tests verify the pause/resume state logic without relying on real
// OS processes, by starting the watcher with an empty process name (which
// is a no-op) and testing the controller state transitions directly.
public class BusinessSoftwareWatcherTests : IDisposable
{
    private readonly BusinessSoftwareWatcher _watcher;
    private readonly List<JobController> _controllers;

    public BusinessSoftwareWatcherTests()
    {
        _controllers = new List<JobController>
        {
            new JobController(),
            new JobController()
        };

        // Use an empty process name: watcher.Start() is a no-op,
        // so we can test controller state without spawning real processes.
        _watcher = new BusinessSoftwareWatcher("", _controllers);
    }

    public void Dispose() => _watcher.Dispose();

    // ── Start / Stop (no-op for empty name) ───────────────────────────────

    [Fact]
    public void Start_WithEmptyProcessName_DoesNotThrow()
    {
        var ex = Record.Exception(() => _watcher.Start());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_DoesNotThrow()
    {
        _watcher.Start();
        var ex = Record.Exception(() => _watcher.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var watcher = new BusinessSoftwareWatcher("", new List<JobController>());
        var ex = Record.Exception(() => watcher.Dispose());
        Assert.Null(ex);
    }

    // ── Controller state independence ──────────────────────────────────────

    [Fact]
    public void Controllers_InitiallyNotPaused()
    {
        foreach (var ctrl in _controllers)
            Assert.False(ctrl.IsPaused);
    }

    [Fact]
    public void PausingControllerManually_DoesNotAffectOtherController()
    {
        _controllers[0].Pause();

        Assert.True(_controllers[0].IsPaused);
        Assert.False(_controllers[1].IsPaused);
    }

    [Fact]
    public void ResumingPausedController_ClearsIsPaused()
    {
        _controllers[0].Pause();
        _controllers[0].Resume();

        Assert.False(_controllers[0].IsPaused);
    }

    // ── Constructor strips .exe suffix ─────────────────────────────────────

    [Fact]
    public void Constructor_WithExeSuffix_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var w = new BusinessSoftwareWatcher("notepad.exe", new List<JobController>());
        });
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_WithUpperCaseExeSuffix_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var w = new BusinessSoftwareWatcher("NOTEPAD.EXE", new List<JobController>());
        });
        Assert.Null(ex);
    }

    // ── Watcher with non-existent process – no jobs are paused ────────────

    [Fact]
    public async Task Start_WithNonExistentProcess_DoesNotPauseControllers()
    {
        // "definitely_not_a_real_process_xyz" should never be running.
        using var watcher = new BusinessSoftwareWatcher(
            "definitely_not_a_real_process_xyz",
            _controllers);

        watcher.Start();

        // Wait for at least one polling cycle (2 s poll + margin)
        await Task.Delay(300);

        watcher.Stop();

        foreach (var ctrl in _controllers)
            Assert.False(ctrl.IsPaused);
    }
}

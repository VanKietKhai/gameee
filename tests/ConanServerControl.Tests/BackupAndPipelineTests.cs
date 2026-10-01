using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;
using ConanServerControl.Core.Updates;

namespace ConanServerControl.Tests;

public class BackupRetentionTests
{
    [Fact]
    public void Keeps_latest_n_and_those_within_keep_days()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var backups = Enumerable.Range(0, 20).Select(i => new BackupRecord
        {
            Id = $"b{i}",
            CreatedAt = now.AddDays(-i),
            DirectoryPath = $"/tmp/b{i}"
        }).ToList();

        var settings = new BackupSettings
        {
            KeepLatest = BackupKeepLatest.Five,
            KeepDays = 3
        };

        var doomed = BackupRetentionPolicy.SelectForDeletion(backups, settings, now);
        var doomedIds = doomed.Select(d => d.Id).ToHashSet();
        Assert.DoesNotContain("b0", doomedIds);
        Assert.DoesNotContain("b1", doomedIds);
        Assert.DoesNotContain("b2", doomedIds);
        Assert.DoesNotContain("b3", doomedIds);
        Assert.DoesNotContain("b4", doomedIds);
        Assert.Contains("b10", doomedIds);
    }
}

public class UpdatePipelineTests
{
    [Fact]
    public void Allows_happy_path_and_rejects_illegal_jumps()
    {
        var machine = new UpdatePipelineStateMachine();
        machine.Begin();
        machine.TransitionTo(UpdatePipelineState.Checking);
        machine.TransitionTo(UpdatePipelineState.Backup);
        machine.TransitionTo(UpdatePipelineState.Stopping);
        machine.TransitionTo(UpdatePipelineState.UpdatingServer);
        Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(UpdatePipelineState.Cancelled));
        machine.TransitionTo(UpdatePipelineState.Validating);
        machine.TransitionTo(UpdatePipelineState.Starting);
        machine.TransitionTo(UpdatePipelineState.HealthCheck);
        machine.TransitionTo(UpdatePipelineState.Completed);
        Assert.Equal(UpdatePipelineState.Completed, machine.State);
        Assert.False(UpdatePipelineStateMachine.IsAllowedTransition(UpdatePipelineState.Idle, UpdatePipelineState.UpdatingMods));
    }

    [Fact]
    public void Fail_from_active_state()
    {
        var machine = new UpdatePipelineStateMachine();
        machine.Begin();
        machine.TransitionTo(UpdatePipelineState.Checking);
        var snapshot = machine.Fail("SteamCMD exit code: 1");
        Assert.Equal(UpdatePipelineState.Failed, snapshot.State);
        Assert.Equal("SteamCMD exit code: 1", snapshot.Error);
    }
}

using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Updates;

public sealed class UpdatePipelineStateMachine
{
    private static readonly UpdatePipelineState[] DefaultFlow =
    [
        UpdatePipelineState.Checking,
        UpdatePipelineState.NotifyingPlayers,
        UpdatePipelineState.Backup,
        UpdatePipelineState.Stopping,
        UpdatePipelineState.UpdatingServer,
        UpdatePipelineState.UpdatingMods,
        UpdatePipelineState.Validating,
        UpdatePipelineState.Starting,
        UpdatePipelineState.HealthCheck,
        UpdatePipelineState.Completed
    ];

    public UpdatePipelineState State { get; private set; } = UpdatePipelineState.Idle;

    public int CurrentStep { get; private set; }

    public int TotalSteps { get; private set; }

    public string? StepDescription { get; private set; }

    public string? Error { get; private set; }

    public bool CanStart => State is UpdatePipelineState.Idle or UpdatePipelineState.Completed or UpdatePipelineState.Failed or UpdatePipelineState.Cancelled;

    public PipelineProgress Snapshot() => new()
    {
        State = State,
        CurrentStep = CurrentStep,
        TotalSteps = TotalSteps,
        StepDescription = StepDescription,
        Error = Error
    };

    public PipelineProgress Begin(IReadOnlyList<UpdatePipelineState>? flow = null)
    {
        if (!CanStart)
        {
            throw new InvalidOperationException($"Cannot start an update pipeline while state is {State}.");
        }

        var steps = flow is { Count: > 0 } ? flow : DefaultFlow;
        TotalSteps = steps.Count(s => s is not UpdatePipelineState.Completed);
        CurrentStep = 0;
        Error = null;
        State = UpdatePipelineState.Idle;
        StepDescription = "Preparing update pipeline...";
        return Snapshot();
    }

    public PipelineProgress TransitionTo(UpdatePipelineState next, string? description = null)
    {
        if (next is UpdatePipelineState.Failed)
        {
            State = UpdatePipelineState.Failed;
            StepDescription = description ?? "Update pipeline failed.";
            return Snapshot();
        }

        if (next is UpdatePipelineState.Cancelled)
        {
            if (State is UpdatePipelineState.UpdatingServer or UpdatePipelineState.UpdatingMods or UpdatePipelineState.Validating)
            {
                throw new InvalidOperationException("Cannot cancel during critical file replacement.");
            }

            State = UpdatePipelineState.Cancelled;
            StepDescription = description ?? "Update pipeline cancelled.";
            return Snapshot();
        }

        if (State is UpdatePipelineState.Failed or UpdatePipelineState.Cancelled)
        {
            throw new InvalidOperationException($"Cannot transition from terminal state {State} to {next}.");
        }

        if (!IsAllowedTransition(State, next))
        {
            throw new InvalidOperationException($"Illegal update pipeline transition: {State} -> {next}.");
        }

        State = next;
        if (next is not UpdatePipelineState.Idle and not UpdatePipelineState.Failed and not UpdatePipelineState.Cancelled)
        {
            if (next is not UpdatePipelineState.Completed)
            {
                CurrentStep++;
            }
        }

        StepDescription = description ?? Describe(next);
        return Snapshot();
    }

    public PipelineProgress Fail(string error)
    {
        Error = error;
        return TransitionTo(UpdatePipelineState.Failed, error);
    }

    public static bool IsAllowedTransition(UpdatePipelineState from, UpdatePipelineState to)
    {
        if (from == to)
        {
            return true;
        }

        if (to is UpdatePipelineState.Failed)
        {
            return from is not UpdatePipelineState.Idle;
        }

        if (to is UpdatePipelineState.Cancelled)
        {
            return from is not UpdatePipelineState.UpdatingServer
                and not UpdatePipelineState.UpdatingMods
                and not UpdatePipelineState.Validating
                and not UpdatePipelineState.Completed
                and not UpdatePipelineState.Failed;
        }

        return (from, to) switch
        {
            (UpdatePipelineState.Idle, UpdatePipelineState.Checking) => true,
            (UpdatePipelineState.Idle, UpdatePipelineState.NotifyingPlayers) => true,
            (UpdatePipelineState.Idle, UpdatePipelineState.Backup) => true,
            (UpdatePipelineState.Checking, UpdatePipelineState.NotifyingPlayers) => true,
            (UpdatePipelineState.Checking, UpdatePipelineState.Backup) => true,
            (UpdatePipelineState.Checking, UpdatePipelineState.Stopping) => true,
            (UpdatePipelineState.Checking, UpdatePipelineState.UpdatingServer) => true,
            (UpdatePipelineState.Checking, UpdatePipelineState.UpdatingMods) => true,
            (UpdatePipelineState.Checking, UpdatePipelineState.Completed) => true,
            (UpdatePipelineState.NotifyingPlayers, UpdatePipelineState.Backup) => true,
            (UpdatePipelineState.NotifyingPlayers, UpdatePipelineState.Stopping) => true,
            (UpdatePipelineState.Backup, UpdatePipelineState.Stopping) => true,
            (UpdatePipelineState.Backup, UpdatePipelineState.UpdatingServer) => true,
            (UpdatePipelineState.Backup, UpdatePipelineState.UpdatingMods) => true,
            (UpdatePipelineState.Stopping, UpdatePipelineState.UpdatingServer) => true,
            (UpdatePipelineState.Stopping, UpdatePipelineState.UpdatingMods) => true,
            (UpdatePipelineState.Stopping, UpdatePipelineState.Starting) => true,
            (UpdatePipelineState.UpdatingServer, UpdatePipelineState.UpdatingMods) => true,
            (UpdatePipelineState.UpdatingServer, UpdatePipelineState.Validating) => true,
            (UpdatePipelineState.UpdatingMods, UpdatePipelineState.Validating) => true,
            (UpdatePipelineState.UpdatingMods, UpdatePipelineState.Starting) => true,
            (UpdatePipelineState.Validating, UpdatePipelineState.Starting) => true,
            (UpdatePipelineState.Validating, UpdatePipelineState.Completed) => true,
            (UpdatePipelineState.Starting, UpdatePipelineState.HealthCheck) => true,
            (UpdatePipelineState.HealthCheck, UpdatePipelineState.Completed) => true,
            (UpdatePipelineState.Completed, UpdatePipelineState.Idle) => true,
            (UpdatePipelineState.Failed, UpdatePipelineState.Idle) => true,
            (UpdatePipelineState.Cancelled, UpdatePipelineState.Idle) => true,
            _ => false
        };
    }

    public static string Describe(UpdatePipelineState state) => state switch
    {
        UpdatePipelineState.Idle => "Idle",
        UpdatePipelineState.Checking => "Checking for updates...",
        UpdatePipelineState.Backup => "Creating backup...",
        UpdatePipelineState.NotifyingPlayers => "Notifying players...",
        UpdatePipelineState.Stopping => "Stopping server...",
        UpdatePipelineState.UpdatingServer => "Updating Conan dedicated server files...",
        UpdatePipelineState.UpdatingMods => "Updating Steam Workshop mods...",
        UpdatePipelineState.Validating => "Validating installed files...",
        UpdatePipelineState.Starting => "Starting server...",
        UpdatePipelineState.HealthCheck => "Waiting for server health check...",
        UpdatePipelineState.Completed => "Completed",
        UpdatePipelineState.Failed => "Failed",
        UpdatePipelineState.Cancelled => "Cancelled",
        _ => state.ToString()
    };
}

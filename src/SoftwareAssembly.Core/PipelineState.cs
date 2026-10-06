namespace SoftwareAssembly.Core;

public enum PipelineState
{
    Pending,
    Admitted,
    ContractReady,
    RedVerified,
    Implementing,
    GreenVerified,
    Validated,
    AwaitingReview,
    Completed,
    Blocked,
    Cancelled
}

public static class PipelineTransitions
{
    private static readonly HashSet<(PipelineState From, PipelineState To)> Allowed =
    [
        (PipelineState.Pending, PipelineState.Admitted),
        (PipelineState.Admitted, PipelineState.ContractReady),
        (PipelineState.ContractReady, PipelineState.RedVerified),
        (PipelineState.RedVerified, PipelineState.Implementing),
        (PipelineState.Implementing, PipelineState.GreenVerified),
        (PipelineState.GreenVerified, PipelineState.Validated),
        (PipelineState.Validated, PipelineState.AwaitingReview),
        (PipelineState.AwaitingReview, PipelineState.Completed)
    ];

    public static bool CanMove(PipelineState from, PipelineState to)
    {
        if (!Enum.IsDefined(from) || !Enum.IsDefined(to) || IsTerminal(from))
            return false;

        return to is PipelineState.Blocked or PipelineState.Cancelled || Allowed.Contains((from, to));
    }

    public static PipelineState Move(PipelineState from, PipelineState to) =>
        CanMove(from, to)
            ? to
            : throw new InvalidOperationException($"Invalid transition: {from} -> {to}.");

    private static bool IsTerminal(PipelineState state) =>
        state is PipelineState.Completed or PipelineState.Blocked or PipelineState.Cancelled;
}
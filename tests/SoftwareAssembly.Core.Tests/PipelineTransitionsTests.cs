using SoftwareAssembly.Core;
using Xunit;

namespace SoftwareAssembly.Core.Tests;

public class PipelineTransitionsTests
{
    [Fact]
    public void HappyPathAdvancesInOrder()
    {
        PipelineState[] path =
        [
            PipelineState.Pending, PipelineState.Admitted, PipelineState.ContractReady,
            PipelineState.RedVerified, PipelineState.Implementing, PipelineState.GreenVerified,
            PipelineState.Validated, PipelineState.AwaitingReview, PipelineState.Completed
        ];

        foreach (var (from, to) in path.Zip(path.Skip(1)))
            Assert.Equal(to, PipelineTransitions.Move(from, to));
    }

    [Fact]
    public void EveryOtherPairIsRejectedExceptBlockAndCancel()
    {
        var states = Enum.GetValues<PipelineState>();
        foreach (var from in states)
        foreach (var to in states)
        {
            var terminal = from is PipelineState.Completed or PipelineState.Blocked or PipelineState.Cancelled;
            var expected = !terminal && (to is PipelineState.Blocked or PipelineState.Cancelled
                || ((int)to == (int)from + 1 && to <= PipelineState.Completed));
            Assert.Equal(expected, PipelineTransitions.CanMove(from, to));
            if (!expected)
                Assert.Throws<InvalidOperationException>(() => PipelineTransitions.Move(from, to));
        }
    }

    [Fact]
    public void UndefinedStatesAreRejected()
    {
        Assert.False(PipelineTransitions.CanMove((PipelineState)999, PipelineState.Blocked));
        Assert.False(PipelineTransitions.CanMove(PipelineState.Pending, (PipelineState)999));
    }
}
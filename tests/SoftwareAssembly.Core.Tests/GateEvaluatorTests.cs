using SoftwareAssembly.Core;
using Xunit;

namespace SoftwareAssembly.Core.Tests;

public class GateEvaluatorTests
{
    private readonly GateEvaluator evaluator = new();

    [Fact]
    public void DeliveryIsAllowedWhenEveryRequiredGatePassedWithEvidence()
    {
        var result = evaluator.Evaluate(["build", "tests"], [Passed("build"), Passed("tests")]);

        Assert.True(result.CanDeliver);
        Assert.Empty(result.BlockingGateIds);
    }

    [Fact]
    public void MissingRequiredGateBlocksDelivery()
    {
        var result = evaluator.Evaluate(["build", "tests"], [Passed("build")]);

        Assert.False(result.CanDeliver);
        Assert.Equal(new[] { "tests" }, result.BlockingGateIds);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("not-applicable")]
    [InlineData("not-executed")]
    [InlineData("unknown")]
    public void AnyNonPassedRequiredGateBlocksDelivery(string status)
    {
        var result = evaluator.Evaluate(["tests"], [new GateResult("tests", status, ["report"], "Not passed.")]);

        Assert.False(result.CanDeliver);
        Assert.Equal(new[] { "tests" }, result.BlockingGateIds);
    }

    [Fact]
    public void PassedGateWithoutEvidenceBlocksDelivery()
    {
        var result = evaluator.Evaluate(["tests"], [new GateResult("tests", "passed", [])]);

        Assert.False(result.CanDeliver);
        Assert.Equal(new[] { "tests" }, result.BlockingGateIds);
    }

    [Fact]
    public void DuplicateRequiredGateIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(["tests", "tests"], [Passed("tests")]));
    }

    [Fact]
    public void DuplicateGateResultsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(["tests"], [Passed("tests"), Passed("tests")]));
    }

    [Fact]
    public void EmptyRequiredGatePolicyIsRejected()
    {
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate([], []));
    }

    private static GateResult Passed(string id) => new(id, "passed", ["test-report"]);
}
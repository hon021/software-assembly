namespace SoftwareAssembly.Core;

public sealed record GateResult(string Id, string Status, IReadOnlyList<string> Evidence, string? Reason = null);

public sealed record GateEvaluation(bool CanDeliver, IReadOnlyList<string> BlockingGateIds);

public sealed class GateEvaluator
{
    public GateEvaluation Evaluate(IEnumerable<string> requiredGateIds, IEnumerable<GateResult> results)
    {
        ArgumentNullException.ThrowIfNull(requiredGateIds);
        ArgumentNullException.ThrowIfNull(results);

        var required = requiredGateIds.ToArray();
        if (required.Length == 0 || required.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one valid required gate ID is required.", nameof(requiredGateIds));
        if (required.Distinct(StringComparer.Ordinal).Count() != required.Length)
            throw new ArgumentException("Required gate IDs must be unique.", nameof(requiredGateIds));

        var gateResults = results.ToArray();
        if (gateResults.Any(result => result is null
            || string.IsNullOrWhiteSpace(result.Id)
            || string.IsNullOrWhiteSpace(result.Status)
            || result.Evidence is null))
            throw new ArgumentException("Gate results must contain valid IDs, statuses, and evidence collections.", nameof(results));
        if (gateResults.Select(result => result.Id).Distinct(StringComparer.Ordinal).Count() != gateResults.Length)
            throw new ArgumentException("Gate result IDs must be unique.", nameof(results));

        var resultsById = gateResults.ToDictionary(result => result.Id, StringComparer.Ordinal);
        var blockingGateIds = required.Where(id =>
            !resultsById.TryGetValue(id, out var result)
            || result.Status != "passed"
            || result.Evidence.Count == 0
            || result.Evidence.Any(string.IsNullOrWhiteSpace)).ToArray();

        return new GateEvaluation(blockingGateIds.Length == 0, Array.AsReadOnly(blockingGateIds));
    }
}
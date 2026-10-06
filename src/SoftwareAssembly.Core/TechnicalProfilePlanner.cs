using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SoftwareAssembly.Core;

public sealed record ToolRequirement(string Id, string Version);

public sealed record PlannedStage(
    string Id, string Component, string Operation, string Tool, IReadOnlyList<string> Arguments, string WorkingDirectory,
    IReadOnlyList<string> DependsOn, string When, int TimeoutSeconds, IReadOnlyList<string> EvidenceDirectories,
    IReadOnlyDictionary<string, string> Environment);

public sealed record DependencyOverride(string Parent, string Package, string Version, string Advisory);

public sealed record TechnicalExecutionPlan(ConfigurationPin Profile, IReadOnlyList<ToolRequirement> Tools,
    IReadOnlyList<PlannedStage> Stages, IReadOnlyList<DependencyOverride> DependencyOverrides);

public sealed class TechnicalProfilePlanner(ConfigurationResolver validator)
{
    public static void VerifyToolchain(TechnicalExecutionPlan plan, IReadOnlyDictionary<string, string> observedVersions)
    {
        foreach (var tool in plan.Tools)
        {
            if (!observedVersions.TryGetValue(tool.Id, out var observed))
                throw new InvalidOperationException($"PROFILE_TOOL_MISSING: {tool.Id}.");
            if (observed != tool.Version)
                throw new InvalidOperationException($"PROFILE_TOOL_VERSION_MISMATCH: {tool.Id}.");
        }
    }

    public TechnicalExecutionPlan Create(JsonObject profile, bool includeE2e = false)
    {
        validator.Validate("profile", profile);
        if (profile["schemaVersion"]!.GetValue<string>() != "1.1")
            throw new InvalidOperationException("PROFILE_NOT_EXECUTABLE.");

        var layout = profile["layout"]!.AsObject();
        foreach (var property in layout)
            CheckPath(property.Value!.GetValue<string>());

        var backend = layout["backend"]!.GetValue<string>();
        var frontend = layout["frontend"]!.GetValue<string>();
        if (backend == frontend || !IsWithin(layout["solution"]!.GetValue<string>(), backend)
            || !IsWithin(layout["packageManifest"]!.GetValue<string>(), frontend)
            || !IsWithin(layout["lockfile"]!.GetValue<string>(), frontend))
            throw new InvalidOperationException("PROFILE_LAYOUT_INVALID.");

        var stages = profile["stages"]!.AsArray().Select(stage => new PlannedStage(
            stage!["id"]!.GetValue<string>(), stage["component"]!.GetValue<string>(), stage["operation"]!.GetValue<string>(),
            stage["tool"]!.GetValue<string>(), ReadStrings(stage["arguments"]!),
            stage["workingDirectory"]!.GetValue<string>(), ReadStrings(stage["dependsOn"]!),
            stage["when"]!.GetValue<string>(), stage["timeoutSeconds"]!.GetValue<int>(), ReadStrings(stage["evidenceDirectories"]!),
            new ReadOnlyDictionary<string, string>((stage["environment"]?.AsObject() ?? new JsonObject())
                .ToDictionary(variable => variable.Key, variable => variable.Value!.GetValue<string>(), StringComparer.Ordinal)))).ToArray();

        if (stages.Select(stage => stage.Id).Distinct(StringComparer.Ordinal).Count() != stages.Length)
            throw new InvalidOperationException("PROFILE_STAGE_DUPLICATE.");

        var byId = stages.ToDictionary(stage => stage.Id, StringComparer.Ordinal);
        foreach (var stage in stages)
        {
            CheckPath(stage.WorkingDirectory);
            if (stage.WorkingDirectory != layout[stage.Component]!.GetValue<string>())
                throw new InvalidOperationException("PROFILE_WORKING_DIRECTORY_INVALID.");
            foreach (var evidence in stage.EvidenceDirectories)
            {
                CheckPath(evidence);
                if (!IsWithin(evidence, stage.WorkingDirectory))
                    throw new InvalidOperationException("PROFILE_EVIDENCE_PATH_INVALID.");
            }
            foreach (var variable in stage.Environment)
                CheckPath(variable.Value);
            if (stage.Operation is "test" or "e2e" && stage.EvidenceDirectories.Count == 0)
                throw new InvalidOperationException("PROFILE_EVIDENCE_REQUIRED.");
            if ((stage.Operation is "prepare-e2e" or "e2e") != (stage.When == "e2e"))
                throw new InvalidOperationException("PROFILE_STAGE_CONDITION_INVALID.");
            foreach (var dependency in stage.DependsOn)
            {
                if (!byId.TryGetValue(dependency, out var prerequisite))
                    throw new InvalidOperationException("PROFILE_DEPENDENCY_UNKNOWN.");
                if (stage.When == "always" && prerequisite.When == "e2e")
                    throw new InvalidOperationException("PROFILE_OPTIONAL_DEPENDENCY.");
            }
        }

        var remaining = stages.ToList();
        var ordered = new List<PlannedStage>();
        var completed = new HashSet<string>(StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            var ready = remaining.FirstOrDefault(stage => stage.DependsOn.All(completed.Contains))
                ?? throw new InvalidOperationException("PROFILE_DEPENDENCY_CYCLE.");
            remaining.Remove(ready);
            ordered.Add(ready);
            completed.Add(ready.Id);
        }

        (string Component, string Operation)[] requiredOperations =
        [
            ("backend", "restore"), ("backend", "build"), ("backend", "test"),
            ("frontend", "restore"), ("frontend", "lint"), ("frontend", "build"), ("frontend", "test")
        ];
        foreach (var (component, operation) in requiredOperations)
        {
            var matches = ordered.Where(stage => stage.Component == component && stage.Operation == operation).ToArray();
            if (matches.Length != 1 || matches[0].When != "always")
                throw new InvalidOperationException("PROFILE_REQUIRED_STAGE_MISSING.");
            var prerequisiteOperation = operation switch { "lint" => "restore", "build" => component == "frontend" ? "lint" : "restore", "test" => "build", _ => null };
            if (prerequisiteOperation is not null && !HasAncestor(matches[0], component, prerequisiteOperation, byId))
                throw new InvalidOperationException("PROFILE_REQUIRED_DEPENDENCY_MISSING.");
        }
        if (includeE2e)
        {
            var e2e = ordered.Where(stage => stage.Operation == "e2e").ToArray();
            if (e2e.Length != 1 || !HasAncestor(e2e[0], "backend", "test", byId)
                || !HasAncestor(e2e[0], "frontend", "test", byId)
                || !HasAncestor(e2e[0], "frontend", "prepare-e2e", byId))
                throw new InvalidOperationException("PROFILE_E2E_STAGE_MISSING.");
        }

        var tools = profile["toolchain"]!.AsObject().Select(tool => new ToolRequirement(tool.Key, tool.Value!.GetValue<string>())).ToArray();
        var overrides = (profile["dependencyOverrides"]?.AsArray() ?? new JsonArray()).Select(item => new DependencyOverride(
            item!["parent"]!.GetValue<string>(), item["package"]!.GetValue<string>(),
            item["version"]!.GetValue<string>(), item["advisory"]!.GetValue<string>())).ToArray();
        if (overrides.DistinctBy(item => (item.Parent, item.Package)).Count() != overrides.Length)
            throw new InvalidOperationException("PROFILE_OVERRIDE_DUPLICATE.");
        var selectedStages = ordered.Where(stage => includeE2e || stage.When == "always").ToArray();
        return new TechnicalExecutionPlan(
            new ConfigurationPin(profile["id"]!.GetValue<string>(), profile["version"]!.GetValue<string>(), ConfigurationResolver.Digest(profile)),
            Array.AsReadOnly(tools), Array.AsReadOnly(selectedStages), Array.AsReadOnly(overrides));
    }

    private static IReadOnlyList<string> ReadStrings(JsonNode value) =>
        Array.AsReadOnly(value.AsArray().Select(item => item!.GetValue<string>()).ToArray());

    private static bool HasAncestor(PlannedStage stage, string component, string operation, IReadOnlyDictionary<string, PlannedStage> byId) =>
        stage.DependsOn.Any(dependency => (byId[dependency].Component == component && byId[dependency].Operation == operation)
            || HasAncestor(byId[dependency], component, operation, byId));

    private static void CheckPath(string path)
    {
        if (path != "." && path.Split('/').Any(segment => segment is "." or ".."))
            throw new InvalidOperationException("PROFILE_PATH_UNSAFE.");
    }

    private static bool IsWithin(string path, string directory) =>
        directory == "." ? path != "." : path.StartsWith(directory + "/", StringComparison.Ordinal);
}
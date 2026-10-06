using System.Text.Json.Nodes;
using Xunit;

namespace SoftwareAssembly.Core.Tests;

public class TechnicalProfilePlannerTests
{
    private readonly ConfigurationResolver validator = new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "fixtures", "schemas", "configuration.schema.json")));

    [Fact]
    public void ProfileIncludesBackendAndFrontendWithPinnedTools()
    {
        var plan = Create(Read());
        Assert.Equal("2.0.2", plan.Profile.Version);
        Assert.Equal(64, plan.Profile.Sha256.Length);
        Assert.Equal(10, plan.Tools.Count);
        Assert.Contains(plan.Stages, stage => stage.Id == "backend-test");
        Assert.Contains(plan.Stages, stage => stage.Id == "frontend-test");
        Assert.Equal(7, plan.Stages.Count);
        Assert.All(plan.Tools, tool => Assert.True(Version.TryParse(tool.Version, out _)));
    }

    [Fact]
    public void E2eIsIncludedOnlyWhenRequestedAndAfterPrerequisites()
    {
        var plan = Create(Read(), true);
        Assert.Equal(9, plan.Stages.Count);
        Assert.Equal("frontend-e2e", plan.Stages[^1].Id);
        var completed = new HashSet<string>();
        foreach (var stage in plan.Stages)
        {
            Assert.All(stage.DependsOn, dependency => Assert.Contains(dependency, completed));
            completed.Add(stage.Id);
        }
        Assert.DoesNotContain(Create(Read()).Stages, stage => stage.When == "e2e");
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("backend/../outside")]
    [InlineData("/outside")]
    [InlineData("C:/outside")]
    [InlineData("backend\\outside")]
    public void UnsafeWorkingDirectoriesAreRejected(string path)
    {
        var profile = Read();
        profile["stages"]![0]!["workingDirectory"] = path;
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void UnknownToolIsRejected()
    {
        var profile = Read();
        profile["stages"]![0]!["tool"] = "powershell";
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void DuplicateStagesAreRejected()
    {
        var profile = Read();
        profile["stages"]!.AsArray().Add(profile["stages"]![0]!.DeepClone());
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void UnknownDependencyIsRejected()
    {
        var profile = Read();
        profile["stages"]![0]!["dependsOn"] = new JsonArray("missing");
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void CyclesAreRejected()
    {
        var profile = Read();
        profile["stages"]![0]!["dependsOn"] = new JsonArray("backend-test");
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void RequiredStageCannotDependOnOptionalStage()
    {
        var profile = Read();
        profile["stages"]![0]!["dependsOn"] = new JsonArray("browser-install");
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("22.x")]
    [InlineData("^22.16.0")]
    public void FloatingToolVersionsAreRejected(string version)
    {
        var profile = Read();
        profile["toolchain"]!["node"] = version;
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void ShellCannotBeEnabled()
    {
        var profile = Read();
        profile["execution"]!["shell"] = true;
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void LegacyProfileCanBeReadButCannotBePlanned()
    {
        var profile = Read("1.0.0");
        validator.Validate("profile", profile);
        Assert.Equal("PROFILE_NOT_EXECUTABLE.", Assert.Throws<InvalidOperationException>(() => Create(profile)).Message);
    }

    [Fact]
    public void LayoutCannotPointFrontendManifestAtBackend()
    {
        var profile = Read();
        profile["layout"]!["packageManifest"] = "backend/package.json";
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void PlanIsIndependentOfSubsequentInputMutation()
    {
        var profile = Read();
        var plan = Create(profile);
        profile["stages"]![0]!["arguments"]![0] = "changed";
        Assert.Equal("restore", plan.Stages[0].Arguments[0]);
        Assert.NotEqual(ConfigurationResolver.Digest(profile), plan.Profile.Sha256);
    }

    [Theory]
    [InlineData("backend-restore")]
    [InlineData("backend-build")]
    [InlineData("backend-test")]
    [InlineData("frontend-install")]
    [InlineData("frontend-lint")]
    [InlineData("frontend-build")]
    [InlineData("frontend-test")]
    public void RequiredStagesCannotBeRemoved(string id)
    {
        var profile = Read();
        var stage = profile["stages"]!.AsArray().Single(stage => stage!["id"]!.GetValue<string>() == id);
        profile["stages"]!.AsArray().Remove(stage);
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void TestMustDependOnBuild()
    {
        var profile = Read();
        profile["stages"]![2]!["dependsOn"] = new JsonArray();
        Assert.Equal("PROFILE_REQUIRED_DEPENDENCY_MISSING.", Assert.Throws<InvalidOperationException>(() => Create(profile)).Message);
    }

    [Fact]
    public void BackendCannotRunInFrontendDirectory()
    {
        var profile = Read();
        profile["stages"]![0]!["workingDirectory"] = "frontend";
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void E2eRequiresItsOwnReportingStage()
    {
        var profile = Read();
        profile["stages"]!.AsArray().RemoveAt(8);
        Assert.Throws<InvalidOperationException>(() => Create(profile, true));
    }

    [Fact]
    public void EvidenceCannotPointOutsideItsComponent()
    {
        var profile = Read();
        profile["stages"]![2]!["evidenceDirectories"] = new JsonArray("frontend/results");
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void ReportEnvironmentIsPinnedAndCannotContainSecrets()
    {
        var profile = Read();
        var stage = Create(profile, true).Stages[^1];
        Assert.Equal("test-results/e2e.xml", stage.Environment["PLAYWRIGHT_JUNIT_OUTPUT_FILE"]);
        profile["stages"]![8]!["environment"]!["API_TOKEN"] = "forbidden";
        Assert.Throws<InvalidOperationException>(() => Create(profile, true));
    }

    [Fact]
    public void MatchingObservedVersionsPassPreflight()
    {
        var plan = Create(Read());
        TechnicalProfilePlanner.VerifyToolchain(plan, plan.Tools.ToDictionary(tool => tool.Id, tool => tool.Version));
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("node")]
    [InlineData("pnpm")]
    [InlineData("angular")]
    [InlineData("typescript")]
    [InlineData("vitest")]
    [InlineData("playwright")]
    public void MissingToolIsRejectedByPreflight(string toolId)
    {
        var plan = Create(Read());
        var versions = plan.Tools.ToDictionary(tool => tool.Id, tool => tool.Version);
        versions.Remove(toolId);
        Assert.Equal($"PROFILE_TOOL_MISSING: {toolId}.",
            Assert.Throws<InvalidOperationException>(() => TechnicalProfilePlanner.VerifyToolchain(plan, versions)).Message);
    }

    [Fact]
    public void DifferentObservedVersionIsRejectedByPreflight()
    {
        var plan = Create(Read());
        var versions = plan.Tools.ToDictionary(tool => tool.Id, tool => tool.Version);
        versions["node"] = "22.17.0";
        Assert.Equal("PROFILE_TOOL_VERSION_MISMATCH: node.",
            Assert.Throws<InvalidOperationException>(() => TechnicalProfilePlanner.VerifyToolchain(plan, versions)).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1801)]
    public void InvalidTimeoutIsRejected(int seconds)
    {
        var profile = Read();
        profile["stages"]![0]!["timeoutSeconds"] = seconds;
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    private TechnicalExecutionPlan Create(JsonObject profile, bool includeE2e = false) =>
        new TechnicalProfilePlanner(validator).Create(profile, includeE2e);

    [Fact]
    public void SecurityOverrideIsPinnedAndIncludedInPlan()
    {
        var item = Assert.Single(Create(Read()).DependencyOverrides);
        Assert.Equal("@angular/cli", item.Parent);
        Assert.Equal("@modelcontextprotocol/sdk", item.Package);
        Assert.Equal("1.31.0", item.Version);
        Assert.Equal("GHSA-6qxp-vccf-f47h", item.Advisory);
    }

    [Fact]
    public void FloatingSecurityOverrideIsRejected()
    {
        var profile = Read();
        profile["dependencyOverrides"]![0]!["version"] = "latest";
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    [Fact]
    public void ConflictingOverridesForSameDependencyAreRejected()
    {
        var profile = Read();
        var item = profile["dependencyOverrides"]![0]!.DeepClone();
        item["version"] = "1.32.0";
        profile["dependencyOverrides"]!.AsArray().Add(item);
        Assert.Throws<InvalidOperationException>(() => Create(profile));
    }

    private static JsonObject Read(string version = "2.0.2") =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "profiles", "dotnet-angular", version, "profile.json")))!.AsObject();
}
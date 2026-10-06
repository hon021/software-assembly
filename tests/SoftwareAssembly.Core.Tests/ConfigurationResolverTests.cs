using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace SoftwareAssembly.Core.Tests;

public class ConfigurationResolverTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "fixtures");
    private readonly ConfigurationResolver resolver = new(File.ReadAllText(Path.Combine(FixtureRoot, "schemas", "configuration.schema.json")));

    [Theory]
    [InlineData("tienda", "comercio", "pedido-con-productos")]
    [InlineData("distribucion", "logistica", "envio-con-destino")]
    public void TwoBusinessesUseTheSameProfileWithoutMixingRules(string applicationId, string domainId, string ruleId)
    {
        var result = Resolve(Read($"applications/{applicationId}/application.json"));
        Assert.Equal(domainId, result.Domain.Id);
        Assert.Equal("dotnet-angular", result.TechnicalProfile.Id);
        Assert.Equal("2.0.2", result.TechnicalProfile.Version);
        Assert.Equal(ruleId, Assert.Single(result.RuleIds));
        Assert.Equal(64, result.Domain.Sha256.Length);
    }

    [Theory]
    [InlineData("domain")]
    [InlineData("technicalProfile")]
    [InlineData("deliveryPolicy")]
    public void MissingRequiredSelectionIsRejected(string property)
    {
        var application = Read("applications/tienda/application.json");
        application.Remove(property);
        Assert.Throws<InvalidOperationException>(() => Resolve(application));
    }

    [Theory]
    [InlineData("domain")]
    [InlineData("technicalProfile")]
    [InlineData("deliveryPolicy")]
    public void UnknownVersionIsRejected(string property)
    {
        var application = Read("applications/tienda/application.json");
        application[property]!["version"] = "9.0.0";
        Assert.Throws<InvalidOperationException>(() => Resolve(application));
    }

    [Fact]
    public void DraftDomainIsRejected()
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        domain["approval"]!["status"] = "draft";
        Assert.Throws<InvalidOperationException>(() => Resolve(Read("applications/tienda/application.json"), [domain]));
    }

    [Fact]
    public void ForeignContextIsRejected()
    {
        var application = Read("applications/tienda/application.json");
        application["domain"]!["boundedContexts"] = new JsonArray("envios");
        Assert.Throws<InvalidOperationException>(() => Resolve(application));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("context")]
    public void InvalidRuleReferenceIsRejected(string property)
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        domain["rules"]![0]![property] = "missing";
        Assert.Throws<InvalidOperationException>(() => Resolve(Read("applications/tienda/application.json"), [domain]));
    }

    [Fact]
    public void DuplicateVersionsAreRejected()
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        Assert.Throws<InvalidOperationException>(() => Resolve(Read("applications/tienda/application.json"), [domain, domain]));
    }

    [Fact]
    public void UnknownFieldsAreRejected()
    {
        var application = Read("applications/tienda/application.json");
        application["overrideSecurity"] = true;
        Assert.Throws<InvalidOperationException>(() => Resolve(application));
    }

    [Fact]
    public void ApprovedDomainRequiresAReviewer()
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        domain["approval"]!.AsObject().Remove("approvedBy");
        Assert.Throws<InvalidOperationException>(() => resolver.Validate("domain", domain));
    }

    [Theory]
    [InlineData("sources")]
    [InlineData("rules")]
    public void DuplicateIdentifiersAreRejected(string property)
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        domain[property]!.AsArray().Add(domain[property]![0]!.DeepClone());
        Assert.Throws<InvalidOperationException>(() => Resolve(Read("applications/tienda/application.json"), [domain]));
    }

    [Fact]
    public void OnlySelectedContextsContributeRules()
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        domain["boundedContexts"]!.AsArray().Add("catalogo");
        var otherRule = domain["rules"]![0]!.DeepClone();
        otherRule["id"] = "producto-con-nombre";
        otherRule["context"] = "catalogo";
        domain["rules"]!.AsArray().Add(otherRule);
        var result = Resolve(Read("applications/tienda/application.json"), [domain]);
        Assert.Equal("pedido-con-productos", Assert.Single(result.RuleIds));
    }

    [Fact]
    public void DigestIgnoresObjectPropertyOrderButDetectsContentChanges()
    {
        var first = JsonNode.Parse("{\"id\":\"example\",\"nested\":{\"a\":1,\"b\":2}}")!.AsObject();
        var reordered = JsonNode.Parse("{\"nested\":{\"b\":2,\"a\":1},\"id\":\"example\"}")!.AsObject();
        Assert.Equal(ConfigurationResolver.Digest(first), ConfigurationResolver.Digest(reordered));
        reordered["nested"]!["a"] = 3;
        Assert.NotEqual(ConfigurationResolver.Digest(first), ConfigurationResolver.Digest(reordered));
    }

    [Fact]
    public void ResolvedPinDoesNotChangeWhenInputIsMutated()
    {
        var domain = Read("domains/comercio/1.0.0/domain.json");
        var result = Resolve(Read("applications/tienda/application.json"), [domain]);
        domain["version"] = "2.0.0";
        Assert.Equal("1.0.0", result.Domain.Version);
        Assert.NotEqual(ConfigurationResolver.Digest(domain), result.Domain.Sha256);
    }

    [Fact]
    public void PolicyCannotEnableAutonomousMerge()
    {
        var policy = Read("policies/entrega-estandar/1.0.0/policy.json");
        policy["autonomousMerge"] = true;
        Assert.Throws<InvalidOperationException>(() => resolver.Validate("policy", policy));
    }

    [Theory]
    [InlineData("passed", true)]
    [InlineData("failed", true)]
    [InlineData("not-applicable", true)]
    [InlineData("not-executed", true)]
    [InlineData("unknown", false)]
    public void GateStatusesHaveExplicitContracts(string status, bool valid)
    {
        var gate = new JsonObject
        {
            ["schemaVersion"] = "1.0", ["id"] = "tests", ["status"] = status,
            ["evidence"] = new JsonArray("test-report"), ["reason"] = "Synthetic test case."
        };
        if (valid)
            resolver.Validate("gate", gate);
        else
            Assert.Throws<InvalidOperationException>(() => resolver.Validate("gate", gate));
    }

    [Fact]
    public void PassedGateWithoutEvidenceIsRejected()
    {
        var gate = new JsonObject { ["schemaVersion"] = "1.0", ["id"] = "tests", ["status"] = "passed", ["evidence"] = new JsonArray() };
        Assert.Throws<InvalidOperationException>(() => resolver.Validate("gate", gate));
    }

    [Fact]
    public void ResolverWithoutApprovalVerifierCannotAdmitAnApplication()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            Read("applications/tienda/application.json"), [Read("domains/comercio/1.0.0/domain.json")],
            [Read("profiles/dotnet-angular/2.0.2/profile.json")], [Read("policies/entrega-estandar/1.0.0/policy.json")]));
        Assert.Equal("DOMAIN_APPROVAL_REQUIRED.", exception.Message);
    }

    private ResolvedConfiguration Resolve(JsonObject application, JsonObject[]? domains = null)
    {
        var packages = domains ?? [Read("domains/comercio/1.0.0/domain.json"), Read("domains/logistica/1.0.0/domain.json")];
        var uniquePackages = packages.DistinctBy(domain => (domain["id"]!.GetValue<string>(), domain["version"]!.GetValue<string>())).ToArray();
        var directory = Path.Combine(Path.GetTempPath(), "software-assembly-tests", Guid.NewGuid().ToString("N"));
        using var signingKey = RSA.Create(2048);
        var authorities = uniquePackages.Select(domain => new DomainApprovalAuthority(domain["id"]!.GetValue<string>(),
            domain["approval"]?["approvedBy"]?.GetValue<string>() ?? "draft-reviewer", signingKey.ExportSubjectPublicKeyInfoPem()));
        var registry = new ApprovedDomainRegistry(resolver, directory, authorities);
        try
        {
            foreach (var domain in uniquePackages)
            {
                resolver.ValidateDomain(domain);
                var statement = new DomainApprovalStatement(domain["id"]!.GetValue<string>(), domain["version"]!.GetValue<string>(),
                    ConfigurationResolver.Digest(domain), domain["approval"]!["approvedBy"]!.GetValue<string>());
                var signature = signingKey.SignData(ApprovedDomainRegistry.GetSigningPayload(statement),
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                registry.Publish(domain, new SignedDomainApproval(statement, Convert.ToBase64String(signature)));
            }

            return new ConfigurationResolver(File.ReadAllText(Path.Combine(FixtureRoot, "schemas", "configuration.schema.json")), registry)
                .Resolve(application, packages, [Read("profiles/dotnet-angular/2.0.2/profile.json")],
                    [Read("policies/entrega-estandar/1.0.0/policy.json")]);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonObject Read(string relativePath) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot, relativePath)))!.AsObject();
}
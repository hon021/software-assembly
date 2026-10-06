using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace SoftwareAssembly.Core.Tests;

public sealed class ApprovedDomainRegistryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "software-assembly-tests", Guid.NewGuid().ToString("N"));
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly ConfigurationResolver resolver = new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "fixtures", "schemas", "configuration.schema.json")));

    [Theory]
    [InlineData("comercio")]
    [InlineData("logistica")]
    public void AuthorizedSignaturePublishesAndReadsExactPackage(string domainId)
    {
        var domain = ReadDomain(domainId);
        var registry = CreateRegistry(domainId);
        var pin = registry.Publish(domain, Sign(domain));
        Assert.Equal(ConfigurationResolver.Digest(domain), pin.Sha256);
        Assert.Equal(pin.Sha256, ConfigurationResolver.Digest(registry.Get(domainId, "1.0.0")));
        Assert.Equal(pin.Sha256, ConfigurationResolver.Digest(registry.Get(pin)));
        registry.Verify(domain);
    }

    [Fact]
    public void DeclaredApprovalWithoutPublicationIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Verify(ReadDomain()));
    }

    [Fact]
    public void UnauthorizedApproverCannotPublish()
    {
        var domain = ReadDomain();
        var registry = new ApprovedDomainRegistry(resolver, directory, []);
        Assert.Throws<InvalidOperationException>(() => registry.Publish(domain, Sign(domain)));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void AuthorityForAnotherDomainCannotApproveThisDomain()
    {
        var domain = ReadDomain();
        Assert.Throws<InvalidOperationException>(() => CreateRegistry("logistica").Publish(domain, Sign(domain)));
    }

    [Fact]
    public void SignatureFromAnotherKeyIsRejected()
    {
        var domain = ReadDomain();
        using var foreignKey = RSA.Create(2048);
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Publish(domain, Sign(domain, foreignKey)));
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("")]
    public void MalformedSignatureIsRejected(string signature)
    {
        var domain = ReadDomain();
        var approval = Sign(domain) with { Signature = signature };
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Publish(domain, approval));
    }

    [Fact]
    public void ChangedPackageCannotUsePreviousApproval()
    {
        var domain = ReadDomain();
        var approval = Sign(domain);
        domain["rules"]![0]!["description"] = "Changed behavior.";
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Publish(domain, approval));
    }

    [Fact]
    public void ApprovalCannotBeReusedForAnotherVersion()
    {
        var domain = ReadDomain();
        var approval = Sign(domain);
        domain["version"] = "2.0.0";
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Publish(domain, approval));
    }

    [Fact]
    public void ApprovalCannotBeReusedForAnotherApprover()
    {
        var domain = ReadDomain();
        var approval = Sign(domain);
        domain["approval"]!["approvedBy"] = "another-reviewer";
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Publish(domain, approval));
    }

    [Fact]
    public void VersionCannotBeOverwrittenEvenWithNewValidSignature()
    {
        var registry = CreateRegistry();
        var domain = ReadDomain();
        var pin = registry.Publish(domain, Sign(domain));
        domain["rules"]![0]!["description"] = "Changed behavior.";
        Assert.Throws<IOException>(() => registry.Publish(domain, Sign(domain)));
        Assert.Equal(pin.Sha256, ConfigurationResolver.Digest(registry.Get("comercio", "1.0.0")));
    }

    [Fact]
    public void NewVersionCanBePublishedWithoutChangingOldVersion()
    {
        var registry = CreateRegistry();
        var domain = ReadDomain();
        var previous = registry.Publish(domain, Sign(domain));
        domain["version"] = "2.0.0";
        var next = registry.Publish(domain, Sign(domain));
        Assert.Equal(previous.Sha256, ConfigurationResolver.Digest(registry.Get("comercio", "1.0.0")));
        Assert.Equal(next.Sha256, ConfigurationResolver.Digest(registry.Get("comercio", "2.0.0")));
    }

    [Fact]
    public void CallerMutationDoesNotChangePublishedSnapshot()
    {
        var registry = CreateRegistry();
        var domain = ReadDomain();
        var pin = registry.Publish(domain, Sign(domain));
        domain["owner"] = "changed-owner";
        var loaded = registry.Get("comercio", "1.0.0");
        Assert.Equal(pin.Sha256, ConfigurationResolver.Digest(loaded));
        loaded["owner"] = "changed-owner";
        Assert.Equal(pin.Sha256, ConfigurationResolver.Digest(registry.Get("comercio", "1.0.0")));
        Assert.Throws<InvalidOperationException>(() => registry.Verify(domain));
    }

    [Fact]
    public void ReadWithUnexpectedPinIsRejected()
    {
        var registry = CreateRegistry();
        var domain = ReadDomain();
        var pin = registry.Publish(domain, Sign(domain));
        Assert.Throws<InvalidOperationException>(() => registry.Get(pin with { Sha256 = new string('0', 64) }));
    }

    [Fact]
    public void TamperedStoredPackageIsRejectedAfterRestart()
    {
        var domain = ReadDomain();
        CreateRegistry().Publish(domain, Sign(domain));
        var filename = Assert.Single(Directory.GetFiles(directory));
        var envelope = JsonNode.Parse(File.ReadAllText(filename))!;
        envelope["Package"]!["owner"] = "changed-owner";
        File.WriteAllText(filename, envelope.ToJsonString());
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Get("comercio", "1.0.0"));
    }

    [Fact]
    public void DuplicateAuthoritiesAreRejected()
    {
        var authority = new DomainApprovalAuthority("comercio", "revisor-sintetico", signingKey.ExportSubjectPublicKeyInfoPem());
        Assert.Throws<ArgumentException>(() => new ApprovedDomainRegistry(resolver, directory, [authority, authority]));
    }

    [Fact]
    public void RemovedAuthorityInvalidatesPreviouslyPublishedApproval()
    {
        var domain = ReadDomain();
        CreateRegistry().Publish(domain, Sign(domain));
        var registry = new ApprovedDomainRegistry(resolver, directory, []);
        Assert.Throws<InvalidOperationException>(() => registry.Get("comercio", "1.0.0"));
    }

    [Fact]
    public void TruncatedPublicationIsRejected()
    {
        var domain = ReadDomain();
        CreateRegistry().Publish(domain, Sign(domain));
        File.WriteAllText(Assert.Single(Directory.GetFiles(directory)), "{");
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Get("comercio", "1.0.0"));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("context")]
    public void InvalidRulesCannotBePublishedEvenWithValidSignature(string property)
    {
        var domain = ReadDomain();
        domain["rules"]![0]![property] = "missing";
        Assert.Throws<InvalidOperationException>(() => CreateRegistry().Publish(domain, Sign(domain)));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task ConcurrentPublicationsCannotOverwriteEachOther()
    {
        var domain = ReadDomain();
        var approval = Sign(domain);
        var registry = CreateRegistry();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try
            {
                registry.Publish(domain, approval);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        })));
        Assert.Single(outcomes, outcome => outcome);
        Assert.Equal(ConfigurationResolver.Digest(domain), ConfigurationResolver.Digest(registry.Get("comercio", "1.0.0")));
    }

    [Fact]
    public void ResolverRejectsChangedDomainEvenIfItsDeclaredApprovalIsUnchanged()
    {
        var domain = ReadDomain();
        var registry = CreateRegistry();
        registry.Publish(domain, Sign(domain));
        domain["rules"]![0]!["description"] = "Changed behavior.";
        var verifiedResolver = new ConfigurationResolver(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "schemas", "configuration.schema.json")), registry);
        var application = ReadFixture("applications/tienda/application.json");
        Assert.Throws<InvalidOperationException>(() => verifiedResolver.Resolve(application, [domain],
            [ReadFixture("profiles/dotnet-angular/2.0.2/profile.json")],
            [ReadFixture("policies/entrega-estandar/1.0.0/policy.json")]));
    }

    private ApprovedDomainRegistry CreateRegistry(string domainId = "comercio") =>
        new(resolver, directory, [new DomainApprovalAuthority(domainId, "revisor-sintetico", signingKey.ExportSubjectPublicKeyInfoPem())]);

    private SignedDomainApproval Sign(JsonObject domain, RSA? key = null)
    {
        var statement = new DomainApprovalStatement(domain["id"]!.GetValue<string>(), domain["version"]!.GetValue<string>(),
            ConfigurationResolver.Digest(domain), domain["approval"]!["approvedBy"]!.GetValue<string>());
        var signature = (key ?? signingKey).SignData(ApprovedDomainRegistry.GetSigningPayload(statement),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return new SignedDomainApproval(statement, Convert.ToBase64String(signature));
    }

    private static JsonObject ReadDomain(string domainId = "comercio") =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "domains", domainId, "1.0.0", "domain.json")))!.AsObject();

    private static JsonObject ReadFixture(string relativePath) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath)))!.AsObject();

    public void Dispose()
    {
        signingKey.Dispose();
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
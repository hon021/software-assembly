using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SoftwareAssembly.Core;

public sealed record DomainApprovalAuthority(string DomainId, string ApproverId, string PublicKeyPem);

public sealed record DomainApprovalStatement(string DomainId, string Version, string Sha256, string ApproverId);

public sealed record SignedDomainApproval(DomainApprovalStatement Statement, string Signature);

public interface IDomainApprovalVerifier
{
    void Verify(JsonObject domain);
}

public sealed class ApprovedDomainRegistry : IDomainApprovalVerifier
{
    private sealed record PublishedDomain(JsonObject Package, SignedDomainApproval Approval);

    private readonly ConfigurationResolver resolver;
    private readonly string directory;
    private readonly DomainApprovalAuthority[] authorities;

    public ApprovedDomainRegistry(
        ConfigurationResolver resolver,
        string directory,
        IEnumerable<DomainApprovalAuthority> authorities)
    {
        this.resolver = resolver;
        this.directory = Path.GetFullPath(directory);
        this.authorities = authorities.ToArray();
        if (this.authorities.Any(authority => string.IsNullOrWhiteSpace(authority.DomainId)
                || string.IsNullOrWhiteSpace(authority.ApproverId) || string.IsNullOrWhiteSpace(authority.PublicKeyPem))
            || this.authorities.GroupBy(authority => (authority.DomainId, authority.ApproverId)).Any(group => group.Count() > 1))
            throw new ArgumentException("APPROVAL_AUTHORITIES_INVALID.", nameof(authorities));
    }

    public ConfigurationPin Publish(JsonObject domain, SignedDomainApproval approval)
    {
        var snapshot = domain.DeepClone().AsObject();
        VerifyApproval(snapshot, approval);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PublishedDomain(snapshot, approval));
        Directory.CreateDirectory(directory);
        using var stream = new FileStream(GetPath(approval.Statement.DomainId, approval.Statement.Version),
            FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        return new ConfigurationPin(approval.Statement.DomainId, approval.Statement.Version, approval.Statement.Sha256);
    }

    public JsonObject Get(string domainId, string version)
    {
        var published = Read(domainId, version);
        return published.Package.DeepClone().AsObject();
    }

    public JsonObject Get(ConfigurationPin expectedPin)
    {
        var domain = Get(expectedPin.Id, expectedPin.Version);
        if (ConfigurationResolver.Digest(domain) != expectedPin.Sha256)
            throw new InvalidOperationException("DOMAIN_PIN_MISMATCH.");
        return domain;
    }

    public void Verify(JsonObject domain)
    {
        resolver.ValidateDomain(domain);
        var published = Read(domain["id"]!.GetValue<string>(), domain["version"]!.GetValue<string>());
        if (ConfigurationResolver.Digest(domain) != published.Approval.Statement.Sha256)
            throw new InvalidOperationException("DOMAIN_CONTENT_CHANGED.");
    }

    public static byte[] GetSigningPayload(DomainApprovalStatement statement) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            Purpose = "software-assembly-domain-approval-v1",
            statement.DomainId,
            statement.Version,
            statement.Sha256,
            statement.ApproverId
        });

    private PublishedDomain Read(string domainId, string version)
    {
        var path = GetPath(domainId, version);
        if (!File.Exists(path))
            throw new InvalidOperationException("DOMAIN_NOT_REGISTERED.");

        PublishedDomain published;
        try
        {
            published = JsonSerializer.Deserialize<PublishedDomain>(File.ReadAllBytes(path))
                ?? throw new InvalidOperationException("DOMAIN_REGISTRY_INVALID.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("DOMAIN_REGISTRY_INVALID.");
        }

        if (published.Package is null || published.Approval?.Statement is null)
            throw new InvalidOperationException("DOMAIN_REGISTRY_INVALID.");

        VerifyApproval(published.Package, published.Approval);
        if (published.Approval.Statement.DomainId != domainId || published.Approval.Statement.Version != version)
            throw new InvalidOperationException("DOMAIN_REGISTRY_INVALID.");

        return published;
    }

    private void VerifyApproval(JsonObject domain, SignedDomainApproval approval)
    {
        resolver.ValidateDomain(domain);
        if (approval?.Statement is null || string.IsNullOrWhiteSpace(approval.Signature))
            throw new InvalidOperationException("DOMAIN_SIGNATURE_INVALID.");

        var statement = approval.Statement;
        if (statement.DomainId != domain["id"]!.GetValue<string>()
            || statement.Version != domain["version"]!.GetValue<string>()
            || statement.Sha256 != ConfigurationResolver.Digest(domain)
            || statement.ApproverId != domain["approval"]!["approvedBy"]!.GetValue<string>())
            throw new InvalidOperationException("DOMAIN_APPROVAL_MISMATCH.");

        var authority = authorities.SingleOrDefault(candidate =>
            candidate.DomainId == statement.DomainId && candidate.ApproverId == statement.ApproverId)
            ?? throw new InvalidOperationException("DOMAIN_APPROVER_UNAUTHORIZED.");

        try
        {
            using var publicKey = RSA.Create();
            publicKey.ImportFromPem(authority.PublicKeyPem);
            if (publicKey.KeySize < 2048 || !publicKey.VerifyData(GetSigningPayload(statement),
                    Convert.FromBase64String(approval.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new InvalidOperationException("DOMAIN_SIGNATURE_INVALID.");
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException("DOMAIN_SIGNATURE_INVALID.");
        }
    }

    private string GetPath(string domainId, string version)
    {
        var referenceBytes = JsonSerializer.SerializeToUtf8Bytes(new { DomainId = domainId, Version = version });
        var filename = Convert.ToHexStringLower(SHA256.HashData(referenceBytes)) + ".json";
        return Path.Combine(directory, filename);
    }
}
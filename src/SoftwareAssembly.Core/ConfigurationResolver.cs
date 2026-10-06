using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;

namespace SoftwareAssembly.Core;

public sealed record ConfigurationPin(string Id, string Version, string Sha256);

public sealed record ResolvedConfiguration(
    string ApplicationId,
    string ApplicationSha256,
    ConfigurationPin Domain,
    ConfigurationPin TechnicalProfile,
    ConfigurationPin DeliveryPolicy,
    IReadOnlyList<string> BoundedContexts,
    IReadOnlyList<string> RuleIds);

public sealed class ConfigurationResolver(string schemaBundle, IDomainApprovalVerifier? domainApprovalVerifier = null)
{
    public void Validate(string kind, JsonObject document)
    {
        if (kind is not ("application" or "domain" or "profile" or "policy" or "gate"))
            throw new ArgumentException("Unknown configuration kind.", nameof(kind));

        var schema = JsonNode.Parse(schemaBundle)!.AsObject();
        schema["$ref"] = $"#/$defs/{kind}";
        if (!JsonSchema.FromText(schema.ToJsonString()).Evaluate(document).IsValid)
            throw new InvalidOperationException($"SCHEMA_INVALID: {kind}.");
    }

    public ResolvedConfiguration Resolve(
        JsonObject application,
        IEnumerable<JsonObject> domains,
        IEnumerable<JsonObject> profiles,
        IEnumerable<JsonObject> policies)
    {
        Validate("application", application);
        var domain = Find(application["domain"]!.AsObject(), domains, "domain");
        var profile = Find(application["technicalProfile"]!.AsObject(), profiles, "profile");
        var policy = Find(application["deliveryPolicy"]!.AsObject(), policies, "policy");
        _ = new TechnicalProfilePlanner(this).Create(profile);

        ValidateDomain(domain);
        var selectedContexts = Strings(application["domain"]!["boundedContexts"]!);
        var domainContexts = Strings(domain["boundedContexts"]!).ToHashSet(StringComparer.Ordinal);
        if (selectedContexts.Any(context => !domainContexts.Contains(context)))
            throw new InvalidOperationException("CONTEXT_NOT_FOUND.");

        if (domainApprovalVerifier is null)
            throw new InvalidOperationException("DOMAIN_APPROVAL_REQUIRED.");
        domainApprovalVerifier.Verify(domain);

        var applicableRules = domain["rules"]!.AsArray()
            .Where(rule => selectedContexts.Contains(rule!["context"]!.GetValue<string>(), StringComparer.Ordinal))
            .Select(rule => rule!["id"]!.GetValue<string>()).ToArray();

        return new ResolvedConfiguration(
            application["id"]!.GetValue<string>(), Digest(application),
            Pin(domain), Pin(profile), Pin(policy),
            Array.AsReadOnly(selectedContexts), Array.AsReadOnly(applicableRules));
    }

    public void ValidateDomain(JsonObject domain)
    {
        Validate("domain", domain);
        if (domain["approval"]!["status"]!.GetValue<string>() != "approved")
            throw new InvalidOperationException("DOMAIN_NOT_APPROVED.");

        var domainContexts = Strings(domain["boundedContexts"]!).ToHashSet(StringComparer.Ordinal);
        var sources = domain["sources"]!.AsArray().Select(source => source!["id"]!.GetValue<string>()).ToArray();
        var rules = domain["rules"]!.AsArray();
        var ruleIds = rules.Select(rule => rule!["id"]!.GetValue<string>()).ToArray();
        if (sources.Distinct(StringComparer.Ordinal).Count() != sources.Length
            || ruleIds.Distinct(StringComparer.Ordinal).Count() != ruleIds.Length)
            throw new InvalidOperationException("DUPLICATE_IDENTIFIER.");

        foreach (var rule in rules)
        {
            if (!domainContexts.Contains(rule!["context"]!.GetValue<string>())
                || !sources.Contains(rule["source"]!.GetValue<string>(), StringComparer.Ordinal))
                throw new InvalidOperationException("RULE_REFERENCE_INVALID.");
        }

    }

    public static string Digest(JsonObject document) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalize(document)!.ToJsonString())));

    private JsonObject Find(JsonObject reference, IEnumerable<JsonObject> candidates, string kind)
    {
        var matches = candidates.Where(candidate =>
            candidate["id"]?.GetValue<string>() == reference["id"]!.GetValue<string>()
            && candidate["version"]?.GetValue<string>() == reference["version"]!.GetValue<string>()).ToArray();

        if (matches.Length != 1)
            throw new InvalidOperationException($"REFERENCE_UNRESOLVED: {kind}.");

        Validate(kind, matches[0]);
        return matches[0];
    }

    private static ConfigurationPin Pin(JsonObject document) =>
        new(document["id"]!.GetValue<string>(), document["version"]!.GetValue<string>(), Digest(document));

    private static string[] Strings(JsonNode value) =>
        value.AsArray().Select(item => item!.GetValue<string>()).ToArray();

    private static JsonNode? Canonicalize(JsonNode? value) => value switch
    {
        JsonObject objectValue => new JsonObject(objectValue.OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => new KeyValuePair<string, JsonNode?>(property.Key, Canonicalize(property.Value)))),
        JsonArray arrayValue => new JsonArray(arrayValue.Select(Canonicalize).ToArray()),
        _ => value?.DeepClone()
    };
}
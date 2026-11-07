using System.Text.Json;
using System.Text.Json.Nodes;

namespace NEXUS.Modules.Audit.Application;

public sealed record AuditRedactionResult(string? Json, bool Restricted);

/// <summary>
/// Removes credentials and sensitive personal payloads from audit JSON before it
/// is persisted or returned, and optionally masks monetary detail for callers
/// without financial read permission.
/// </summary>
public static class AuditFieldRedactor
{
    public const string RedactedValue = "[redacted]";
    public const string RestrictedValue = "[restricted]";

    private static readonly string[] SecretFragments =
    [
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "api-key",
        "credential", "connectionstring", "connection_string", "privatekey", "private_key",
        "clientsecret", "client_secret", "refresh_token", "refreshtoken", "access_token",
        "accesstoken", "authorization", "bearer", "signature", "salt", "certificate",
        "cardnumber", "card_number", "cvv", "iban", "ssn", "socialsecurity", "nationalid"
    ];

    private static readonly string[] FinancialFragments =
    [
        "amount", "budget", "cost", "price", "rate", "salary", "payment", "currency",
        "invoice", "approved", "committed", "forecast", "actual", "plannedvalue",
        "earnedvalue", "margin", "fee", "tax", "balance", "spend", "expense"
    ];

    /// <summary>Secret-only sanitisation applied before an audit row is written.</summary>
    public static string? Sanitize(string? json) => Redact(json, includeFinancialDetail: true).Json;

    /// <summary>
    /// Redacts an audit JSON payload. Returns the original text unchanged when it
    /// is not valid JSON (for example a bare status string).
    /// </summary>
    public static AuditRedactionResult Redact(string? json, bool includeFinancialDetail)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AuditRedactionResult(json, false);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return new AuditRedactionResult(json, false);
        }

        if (node is null) return new AuditRedactionResult(json, false);

        var restricted = false;
        var sanitized = Walk(node, includeFinancialDetail, ref restricted);
        return new AuditRedactionResult(sanitized.ToJsonString(), restricted);
    }

    private static JsonNode Walk(JsonNode node, bool includeFinancialDetail, ref bool restricted)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var clone = new JsonObject();
                foreach (var pair in obj)
                {
                    var key = pair.Key;
                    if (IsSecret(key))
                    {
                        clone[key] = RedactedValue;
                        continue;
                    }
                    if (!includeFinancialDetail && IsFinancial(key))
                    {
                        clone[key] = RestrictedValue;
                        restricted = true;
                        continue;
                    }
                    clone[key] = pair.Value is null ? null : Walk(pair.Value, includeFinancialDetail, ref restricted);
                }
                return clone;
            }
            case JsonArray array:
            {
                var clone = new JsonArray();
                foreach (var item in array)
                {
                    clone.Add(item is null ? null : Walk(item, includeFinancialDetail, ref restricted));
                }
                return clone;
            }
            default:
                return node.DeepClone();
        }
    }

    private static bool IsSecret(string key)
    {
        var normalized = Normalize(key);
        return SecretFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }

    private static bool IsFinancial(string key)
    {
        var normalized = Normalize(key);
        return FinancialFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }

    private static string Normalize(string key) =>
        key.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
}

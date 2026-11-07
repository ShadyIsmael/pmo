using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

internal static class FinancialAuditEntryFactory
{
    public static AuditEntry ForCurrentRecord(
        DbContext context,
        string entityType,
        Guid entityId,
        string action,
        string userId,
        string? userName,
        string? originalValues,
        object? newValues,
        string? referenceOverride = null,
        string? currencyOverride = null)
    {
        Guid? projectId = null;
        var tenantId = Guid.Empty;
        string? reference = null;
        decimal? amount = null;
        string? currency = null;

        switch (entityType)
        {
            case "Budget":
                if (context.Set<Budget>().Local.FirstOrDefault(item => item.Id == entityId) is { } budget)
                {
                    projectId = budget.ProjectId;
                    tenantId = budget.TenantId;
                    reference = budget.Name;
                    amount = budget.ApprovedAmount;
                    currency = budget.Currency;
                }
                break;
            case "CostEntry":
                if (context.Set<CostEntry>().Local.FirstOrDefault(item => item.Id == entityId) is { } cost)
                {
                    projectId = cost.ProjectId;
                    tenantId = cost.TenantId;
                    reference = string.IsNullOrWhiteSpace(cost.Reference) ? cost.Description : cost.Reference;
                    amount = cost.Amount;
                    currency = cost.Currency;
                }
                break;
            case "PurchaseCommitment":
                if (context.Set<PurchaseCommitment>().Local.FirstOrDefault(item => item.Id == entityId) is { } commitment)
                {
                    projectId = commitment.ProjectId;
                    tenantId = commitment.TenantId;
                    reference = commitment.PurchaseOrderNumber;
                    amount = commitment.Amount;
                    currency = commitment.Currency;
                }
                break;
            case "BudgetRevision":
                if (context.Set<BudgetRevision>().Local.FirstOrDefault(item => item.Id == entityId) is { } revision)
                {
                    projectId = revision.ProjectId;
                    tenantId = revision.TenantId;
                    amount = revision.ProposedApprovedAmount;
                    var budgetReference = context.Set<Budget>().Local.FirstOrDefault(item => item.Id == revision.BudgetId);
                    reference = budgetReference?.Name ?? revision.BudgetId.ToString("D");
                    currency = budgetReference?.Currency;
                }
                break;
        }

        var isCreate = originalValues is null;
        reference = referenceOverride ?? reference ?? entityId.ToString("D");
        currency = currencyOverride ?? currency;
        var beforeAmount = ReadDecimal(originalValues, "approvedAmount", "amount", "proposedApprovedAmount");
        var beforeCurrency = ReadString(originalValues, "currency");
        var beforeReference = ReadString(originalValues, "name", "reference", "purchaseOrderNumber", "financialReference");
        if (!isCreate)
        {
            beforeAmount ??= amount;
            beforeCurrency ??= currency;
            beforeReference ??= reference;
        }

        return Create(
            entityType,
            entityId,
            action,
            userId,
            userName,
            projectId,
            tenantId,
            beforeReference,
            reference,
            beforeAmount,
            amount,
            beforeCurrency,
            currency,
            originalValues is null
                ? null
                : JsonSerializer.Serialize(new { financialReference = beforeReference, amount = beforeAmount, currency = beforeCurrency, previous = ParseJsonOrString(originalValues) }),
            new { financialReference = reference, amount, currency, values = newValues });
    }

    public static AuditEntry Create(
        string entityType,
        Guid entityId,
        string action,
        string userId,
        string? userName,
        Guid? projectId,
        Guid tenantId,
        string? beforeReference,
        string? afterReference,
        decimal? beforeAmount,
        decimal? afterAmount,
        string? beforeCurrency,
        string? afterCurrency,
        string? originalValues,
        object? newValues)
    {
        var actorId = Guid.TryParse(userId, out var parsedActor) ? parsedActor : (Guid?)null;
        return new AuditEntry
        {
            ProjectId = projectId,
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = actorId,
            UserName = userName,
            Timestamp = DateTime.UtcNow,
            Description = $"{entityType} {action}: {afterReference ?? beforeReference ?? entityId.ToString("D")}",
            Changes = JsonSerializer.Serialize(new
            {
                financialReference = new { from = beforeReference, to = afterReference },
                amount = new { from = beforeAmount, to = afterAmount },
                currency = new { from = beforeCurrency, to = afterCurrency }
            }),
            OriginalValues = originalValues,
            NewValues = newValues is null
                ? JsonSerializer.Serialize(new { financialReference = afterReference, amount = afterAmount, currency = afterCurrency })
                : JsonSerializer.Serialize(newValues),
            ChangedFields = "financialReference,amount,currency",
            CreatedBy = userId
        };
    }

    private static decimal? ReadDecimal(string? json, params string[] names)
    {
        var value = ReadElement(json, names);
        if (value is null) return null;
        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var number)) return number;
        return value.Value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static string? ReadString(string? json, params string[] names)
    {
        var value = ReadElement(json, names);
        return value is null ? null : value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : value.Value.ToString();
    }

    private static JsonElement? ReadElement(string? json, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var property in document.RootElement.EnumerateObject())
                if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) return property.Value.Clone();
        }
        catch (JsonException) { }
        return null;
    }

    private static object? ParseJsonOrString(string json)
    {
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return json; }
    }
}

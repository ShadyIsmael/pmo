using Microsoft.EntityFrameworkCore;
using NEXUS.Persistence;

namespace NEXUS.Api.Tests.Testing;

/// <summary>
/// Uses the production AppDbContext mappings and tenant filters in an isolated
/// SQLite test database, translating SQL Server max-length store types and LEN
/// checks, and omitting only SQL Server ISJSON checks that SQLite cannot execute
/// in this derived test model. It also strips SQL Server collation and
/// national-string literal syntax while preserving the represented checks and
/// filtered-index predicates.
/// </summary>
public class SqliteTestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (!string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
            return;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var constraint in entityType.GetDeclaredCheckConstraints().ToArray())
        {
            if (constraint.Name is not { } name)
                continue;

            if (constraint.Sql.Contains("ISJSON(", StringComparison.OrdinalIgnoreCase))
            {
                entityType.RemoveCheckConstraint(name);
                continue;
            }

            if (string.Equals(name, "CK_Roles_SystemRoleKeyRequiresSystemRole", StringComparison.Ordinal))
            {
                entityType.RemoveCheckConstraint(name);
                entityType.AddCheckConstraint(name, constraint.Sql
                    .Replace(" COLLATE Latin1_General_100_BIN2", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("N'", "'", StringComparison.Ordinal));
                continue;
            }

            if (constraint.Sql.Contains("N'", StringComparison.Ordinal))
            {
                entityType.RemoveCheckConstraint(name);
                entityType.AddCheckConstraint(name, constraint.Sql.Replace("N'", "'", StringComparison.Ordinal));
                continue;
            }

            if (constraint.Sql.Contains("LEN(", StringComparison.OrdinalIgnoreCase))
            {
                entityType.RemoveCheckConstraint(name);
                entityType.AddCheckConstraint(
                    name,
                    constraint.Sql.Replace("LEN(", "length(", StringComparison.OrdinalIgnoreCase));
            }
        }

        foreach (var index in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetIndexes()))
        {
            var filter = index.GetFilter();
            if (filter?.Contains("N'", StringComparison.Ordinal) != true)
                continue;

            index.SetFilter(filter.Replace("N'", "'", StringComparison.Ordinal));
        }

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties()))
        {
            var columnType = property.GetColumnType();
            if (columnType?.Contains("(max)", StringComparison.OrdinalIgnoreCase) != true)
                continue;

            property.SetColumnType(property.ClrType == typeof(byte[]) ? "BLOB" : "TEXT");
        }
    }
}

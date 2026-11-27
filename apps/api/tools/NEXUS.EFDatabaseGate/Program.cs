using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using NEXUS.Persistence;

return await EfDatabaseGate.RunAsync(args);

internal static class EfDatabaseGate
{
    private const string ConnectionEnvironmentVariable = "NEXUS_EF_DEVELOPMENT_CONNECTION_STRING";
    private const string MinimalSeedCapabilityProbe = "--supports-seed-minimal-only";
    private const string MinimalSeedCapabilityMarker = "NEXUS_MINIMAL_SEED_MODE=1";
    private const string DevelopmentFallback =
        "Server=localhost;Database=NexusPmo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        var action = args[0].ToLowerInvariant();
        if (action == "model")
            return await CheckModelAsync();

        if (action is not ("inspect" or "script" or "apply" or "reset" or "sync-project-plan-permissions"))
        {
            Console.Error.WriteLine("Unknown action. Use model, inspect, script, apply, reset, or sync-project-plan-permissions.");
            return 1;
        }

        try
        {
            var options = ParseOptions(args.Skip(1).ToArray());
            if (action == "sync-project-plan-permissions")
            {
                if (!SameEndpoint(Required(options, "expected-server"), "Shady")
                    || !SameEndpoint(Required(options, "expected-database"), "NexusPmo"))
                    throw new InvalidOperationException("Project Plan permission catalog synchronization is restricted to the verified Development Shady/NexusPmo database.");
                Console.WriteLine($"Task authorization reference: {Required(options, "task-authorization-reference")}");
            }
            var target = await OpenVerifiedDevelopmentTargetAsync(options, allowMissingDatabase: action == "reset");
            await using var context = target.Context;
            if (action == "reset")
                ValidateProductOwnerResetAuthorization(target.Server, target.Database, options);
            if (!target.DatabaseExistedAtProbe)
            {
                if (action != "reset")
                    throw new InvalidOperationException("The verified Development database is absent. Read-only and apply actions never create it; use the explicitly authorized reset action.");
                return await CreateMissingDevelopmentDatabaseAsync(context, target.Server, target.Database, options);
            }
            await using var connection = context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync();

            var status = await InspectAsync(context, target.Server, target.Database, options, writeScript: action == "inspect");

            if (action == "inspect")
            {
                Console.WriteLine("Backup verified: Not needed (read-only inspection).");
                Console.WriteLine("Migrations applied: None (read-only inspection).");
                return status.IsSynchronized ? 0 : 2;
            }

            if (action == "sync-project-plan-permissions")
            {
                if (status.HasPendingModelChanges
                    || status.PendingMigrations.Count > 0
                    || status.UnknownAppliedMigrations.Count > 0)
                    throw new InvalidOperationException("Permission catalog synchronization requires a synchronized EF model/history with no pending migrations or unknown applied migrations.");

                var protectedTables = new[]
                {
                    "dbo.Permissions",
                    "dbo.AuditEntries",
                    "dbo.RolePermissions",
                    "dbo.UserRoles",
                    "dbo.UserPermissionOverrides"
                };
                var relevantSchemaDifferences = status.MissingBaselineSchemaObjects
                    .Concat(status.MissingCurrentSchemaObjects)
                    .Where(item => protectedTables.Any(table => item.Contains(table, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (relevantSchemaDifferences.Length > 0)
                    throw new InvalidOperationException($"Permission catalog synchronization is blocked by physical-schema differences on a permission, grant, assignment, or audit table: {string.Join("; ", relevantSchemaDifferences)}.");

                if (options.TryGetValue("backup-verified-path", out var suppliedBackupPath)
                    && !string.IsNullOrWhiteSpace(suppliedBackupPath))
                {
                    if (!Path.IsPathFullyQualified(suppliedBackupPath))
                        throw new InvalidOperationException("--backup-verified-path must be an absolute SQL Server-accessible file path.");
                    var verifiedBackupPath = Path.GetFullPath(suppliedBackupPath);
                    await VerifyExistingBackupAsync(context.Database.GetDbConnection(), verifiedBackupPath, target.Server, target.Database);
                    Console.WriteLine($"Backup verified: Yes ({verifiedBackupPath})");
                }
                else
                {
                    Console.WriteLine("Backup verification: Not required for this explicitly authorized, catalog-only task operation.");
                }

                using var trustedTenantContext = NEXUS.SharedKernel.TrustedTenantExecutionContext.Enter(SeedDataProvider.TenantId);
                var result = await SeedDataProvider.SynchronizeProjectPlanPermissionsAsync(
                    context,
                    true,
                    target.Server,
                    target.Database);
                Console.WriteLine("Permission/grant/audit tables verified: Yes");
                Console.WriteLine($"Permission catalog sync target verified: {result.Server}/{result.Database}, database ID {result.DatabaseId}.");
                Console.WriteLine($"Catalog records inserted: {result.AddedCatalogPermissions}.");
                Console.WriteLine($"Existing canonical catalog records normalized: {result.NormalizedCatalogPermissions}.");
                Console.WriteLine($"Changed keys: {(result.ChangedKeys.Count == 0 ? "None" : string.Join(", ", result.ChangedKeys))}.");
                Console.WriteLine($"Unchanged keys: {(result.UnchangedKeys.Count == 0 ? "None" : string.Join(", ", result.UnchangedKeys))}.");
                Console.WriteLine("Role grants, role assignments, direct Allow/Deny overrides, and protected system roles: Not modified by this operation.");
                Console.WriteLine("Migrations applied: None.");
                return 0;
            }

            if (action == "script")
            {
                if (status.HasPendingModelChanges || status.UnknownAppliedMigrations.Count > 0)
                    return 2;
                if (status.PendingMigrations.Count == 0)
                    throw new InvalidOperationException("No pending migrations exist; there is no SQL script to generate.");
                var output = Required(options, "script-output");
                var script = GeneratePendingSql(context, status.AppliedMigrations, status.RepositoryMigrations);
                WriteReviewedScript(output, script);
                return 0;
            }

            if (action == "reset")
                return await ResetDevelopmentDatabaseAsync(context, target, status, options);

            return await ApplyReviewedMigrationsAsync(context, target, status, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EF database gate failed ({ex.GetType().Name}): {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CheckModelAsync()
    {
        try
        {
            await using var context = CreateContext(
                "Server=localhost;Database=__NEXUS_EF_MODEL_ONLY__;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True");
            var migrations = context.Database.GetMigrations().ToArray();
            var hasPendingModelChanges = context.Database.HasPendingModelChanges();
            Console.WriteLine($"Context: {typeof(AppDbContext).FullName}");
            Console.WriteLine($"Migration assembly: {typeof(AppDbContext).Assembly.GetName().Name}");
            Console.WriteLine($"Repository migrations: {migrations.Length}");
            foreach (var migration in migrations)
                Console.WriteLine($"  {migration}");
            Console.WriteLine($"EF Model synchronized: {(hasPendingModelChanges ? "No" : "Yes")}");
            Console.WriteLine("Database connection: Not opened (offline model check).");
            return hasPendingModelChanges ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Model check failed ({ex.GetType().Name}): {ex.Message}");
            return 1;
        }
    }

    private static async Task<(AppDbContext Context, string Server, string Database, int? DatabaseId, bool DatabaseExistedAtProbe)> OpenVerifiedDevelopmentTargetAsync(
        IReadOnlyDictionary<string, string> options,
        bool allowMissingDatabase)
    {
        if (!string.Equals(Required(options, "environment"), "Development", StringComparison.Ordinal))
            throw new InvalidOperationException("Only the explicitly named Development environment is allowed.");
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.Ordinal))
            throw new InvalidOperationException("Set ASPNETCORE_ENVIRONMENT=Development in this process before inspecting a database.");

        var expectedServer = Required(options, "expected-server");
        var expectedDatabase = Required(options, "expected-database");
        var suppliedConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(suppliedConnectionString))
            throw new InvalidOperationException($"Set {ConnectionEnvironmentVariable} through a secure environment or secret store.");

        var root = FindRepositoryRoot();
        var apiPath = Path.Combine(root, "apps", "api", "src", "NEXUS.Api");
        var runtimeConfiguration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var configuredOverride = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (configuredOverride is not null && string.IsNullOrWhiteSpace(configuredOverride))
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection is set to an empty value in this process, which overrides the API Development configuration and leaves the endpoint empty. " +
                "Remove that variable or set it to the active Development connection string. No connection was opened.");

        var runtimeConnectionString = runtimeConfiguration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(runtimeConnectionString))
            runtimeConnectionString = DevelopmentFallback;
        var supplied = ParseConnectionString(suppliedConnectionString, "supplied NEXUS_EF_DEVELOPMENT_CONNECTION_STRING");
        var configured = ParseConnectionString(runtimeConnectionString, "effective API Development connection (ConnectionStrings__DefaultConnection or appsettings.Development.json)");
        if (!SameEndpoint(supplied.DataSource, configured.DataSource) ||
            !SameEndpoint(supplied.InitialCatalog, configured.InitialCatalog))
        {
            throw new InvalidOperationException(
                $"The supplied target does not match the API's effective Development endpoint. " +
                $"API endpoint: server={configured.DataSource}, database={configured.InitialCatalog}; " +
                $"gate endpoint: server={supplied.DataSource}, database={supplied.InitialCatalog}. No connection was opened.");
        }

        var context = CreateContext(suppliedConnectionString);
        try
        {
            await context.Database.OpenConnectionAsync();
            var actual = await ReadIdentityAsync(context.Database.GetDbConnection());
            if (!SameEndpoint(actual.Server, expectedServer) || !SameEndpoint(actual.Database, expectedDatabase))
            {
                throw new InvalidOperationException(
                    $"Target identity mismatch. Expected server={expectedServer}, database={expectedDatabase}; " +
                    $"connected server={actual.Server}, database={actual.Database}, databaseId={actual.DatabaseId}. No migration action was performed.");
            }

            Console.WriteLine($"Environment: Development");
            Console.WriteLine($"Target Development DB verified: Yes (server={actual.Server}; database={actual.Database}; databaseId={actual.DatabaseId})");
            return (context, actual.Server, actual.Database, actual.DatabaseId, true);
        }
        catch (Exception ex) when (allowMissingDatabase && FindSqlException(ex)?.Number == 4060)
        {
            await context.DisposeAsync();
            var verifiedAbsent = await VerifyTargetDatabaseAbsentAsync(
                suppliedConnectionString, expectedServer, expectedDatabase);
            if (!verifiedAbsent)
                throw new InvalidOperationException(
                    "The target database exists but could not be opened. No reset or database creation was performed.");

            Console.WriteLine($"Environment: Development");
            Console.WriteLine($"Target database absent on verified server: {expectedServer}/{expectedDatabase}");
            return (CreateContext(suppliedConnectionString), expectedServer, expectedDatabase, null, false);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> VerifyTargetDatabaseAbsentAsync(
        string suppliedConnectionString,
        string expectedServer,
        string expectedDatabase)
    {
        if (!SameEndpoint(expectedServer, "Shady") || !SameEndpoint(expectedDatabase, "NexusPmo"))
            throw new InvalidOperationException("Missing-database creation is restricted to SQL Server Shady and Development database NexusPmo.");

        var masterConnectionString = new SqlConnectionStringBuilder(suppliedConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();
        var identity = await ReadIdentityAsync(connection);
        if (!SameEndpoint(identity.Server, expectedServer) || !SameEndpoint(identity.Database, "master"))
            throw new InvalidOperationException(
                $"Cannot verify the absent target on the expected SQL Server. Connected server={identity.Server}, database={identity.Database}.");

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@databaseName);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@databaseName";
        parameter.Value = expectedDatabase;
        command.Parameters.Add(parameter);
        var databaseId = await command.ExecuteScalarAsync();
        return databaseId is null || databaseId is DBNull;
    }

    private static async Task<int> CreateMissingDevelopmentDatabaseAsync(
        AppDbContext context,
        string server,
        string database,
        IReadOnlyDictionary<string, string> options)
    {
        ValidateProductOwnerResetAuthorization(server, database, options);
        if (context.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Database creation is blocked until the EF model and migration snapshot are synchronized.");

        var migrations = context.Database.GetMigrations().ToArray();
        if (migrations.Length == 0)
            throw new InvalidOperationException("Database creation is blocked because the repository has no EF migrations.");

        var emptyDatabaseScript = GeneratePendingSql(context, Array.Empty<string>(), migrations);
        if (string.IsNullOrWhiteSpace(emptyDatabaseScript))
            throw new InvalidOperationException("EF did not generate a migration path from an empty database.");
        Console.WriteLine($"Empty-database EF migration script SHA-256: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(emptyDatabaseScript)))}");

        await VerifyMinimalSeedApiSupportAsync();

        // The database was confirmed absent through master. A direct CREATE DATABASE
        // makes a concurrent creator fail safely instead of allowing MigrateAsync to
        // attach to a database that appeared after the absence check.
        var targetConnectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(targetConnectionString))
            throw new InvalidOperationException("The verified Development connection string is unavailable after the target absence check.");
        var masterConnectionString = new SqlConnectionStringBuilder(targetConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;
        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            var identity = await ReadIdentityAsync(master);
            if (!SameEndpoint(identity.Server, server) || !SameEndpoint(identity.Database, "master"))
                throw new InvalidOperationException("The SQL Server identity changed before Development database creation.");
            await using (var exists = master.CreateCommand())
            {
                exists.CommandText = "SELECT DB_ID(@databaseName);";
                exists.Parameters.Add(new SqlParameter("@databaseName", database));
                var existingDatabaseId = await exists.ExecuteScalarAsync();
                if (existingDatabaseId is not null && existingDatabaseId is not DBNull)
                    throw new InvalidOperationException("The confirmed Development database appeared after the absence check; creation was stopped.");
            }
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE {QuoteIdentifier(database)};";
            await create.ExecuteNonQueryAsync();
        }

        Console.WriteLine($"Empty Development database created: {server}/{database}; no pre-existing database data was present to back up.");
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();
        var actual = await ReadIdentityAsync(context.Database.GetDbConnection());
        if (!SameEndpoint(actual.Server, server) || !SameEndpoint(actual.Database, database) || actual.DatabaseId <= 0)
            throw new InvalidOperationException("EF migration connected to an unexpected database after creation.");
        Console.WriteLine($"Created Development DB ID verified: {actual.DatabaseId}");

        var postStatus = await InspectAsync(
            context,
            server,
            database,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            writeScript: false);
        Console.WriteLine($"EF migrations applied to new database: {postStatus.AppliedMigrations.Count}");
        Console.WriteLine($"Fresh database schema verified: {(postStatus.IsSynchronized ? "Yes" : "No")}");
        if (!postStatus.IsSynchronized || postStatus.AppliedMigrations.Count != migrations.Length)
            return 2;

        await context.Database.CloseConnectionAsync();
        await RunMinimalDevelopmentSeedTwiceAsync(targetConnectionString, actual.DatabaseId);
        await context.Database.OpenConnectionAsync();
        var postSeedIdentity = await ReadIdentityAsync(context.Database.GetDbConnection());
        if (!SameEndpoint(postSeedIdentity.Server, server)
            || !SameEndpoint(postSeedIdentity.Database, database)
            || postSeedIdentity.DatabaseId != actual.DatabaseId)
            throw new InvalidOperationException("Post-seed verification connected to an unexpected database.");
        await SeedDataProvider.VerifyMinimalDevelopmentSeedOutputAsync(context);
        Console.WriteLine("Minimal Seed output verified: one active Admin, complete protected Admin permissions, and no business/demo records.");
        var postSeedStatus = await InspectAsync(
            context,
            server,
            database,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            writeScript: false);
        Console.WriteLine("Minimal Development seed: Passed twice with stable table row counts.");
        Console.WriteLine($"Post-seed physical schema verified: {(postSeedStatus.IsSynchronized ? "Yes" : "No")}");
        if (!postSeedStatus.IsSynchronized || postSeedStatus.AppliedMigrations.Count != migrations.Length)
            return 2;
        Console.WriteLine("Reset and seed workflow: Complete.");
        return 0;
    }

    private static void ValidateProductOwnerResetAuthorization(
        string server,
        string database,
        IReadOnlyDictionary<string, string> options)
    {
        if (!SameEndpoint(server, "Shady") || !SameEndpoint(database, "NexusPmo"))
            throw new InvalidOperationException("Product Owner-authorized destructive reset is restricted to Development Shady/NexusPmo.");
        _ = Required(options, "product-owner-authorization");
        if (!SameEndpoint(Required(options, "confirm-reset-database"), database))
            throw new InvalidOperationException("Database reset requires --confirm-reset-database to exactly match the verified Development database name.");
        var bootstrapPassword = Environment.GetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD");
        if (string.IsNullOrWhiteSpace(bootstrapPassword) || bootstrapPassword.Length < 12)
            throw new InvalidOperationException("The Minimal Seed requires NEXUS_ADMIN_BOOTSTRAP_PASSWORD (minimum 12 characters) through a secure environment source before reset.");
        var apiAssembly = GetDevelopmentApiAssemblyPath();
        if (!File.Exists(apiAssembly))
            throw new InvalidOperationException($"The prebuilt Development API required for Minimal Seed is missing: {apiAssembly}. Build the API before reset.");
    }

    private static async Task<Inspection> InspectAsync(
        AppDbContext context,
        string server,
        string database,
        IReadOnlyDictionary<string, string> options,
        bool writeScript = true)
    {
        var repositoryMigrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var sourceSet = new HashSet<string>(repositoryMigrations, StringComparer.OrdinalIgnoreCase);
        var appliedSet = new HashSet<string>(appliedMigrations, StringComparer.OrdinalIgnoreCase);
        var pending = repositoryMigrations.Where(m => !appliedSet.Contains(m)).ToArray();
        var unknownApplied = appliedMigrations.Where(m => !sourceSet.Contains(m)).ToArray();
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();
        var currentSchema = await ComparePhysicalSchemaAsync(context);
        var baselineSchema = await CompareAppliedModelSchemaAsync(context, appliedMigrations);

        Console.WriteLine($"Target Development DB verified: Yes (server={server}; database={database})");
        Console.WriteLine($"EF Model synchronized: {(hasPendingModelChanges ? "No" : "Yes")}");
        Console.WriteLine($"Repository migrations: {repositoryMigrations.Length}");
        Console.WriteLine($"Applied migrations in dbo.__EFMigrationsHistory: {appliedMigrations.Length}");
        Console.WriteLine($"Pending migrations: {pending.Length}");
        foreach (var migration in pending)
            Console.WriteLine($"  PENDING {migration}");
        foreach (var migration in unknownApplied)
            Console.WriteLine($"  HISTORY-ONLY {migration}");
        Console.WriteLine($"Applied migration IDs:");
        foreach (var migration in appliedMigrations)
            Console.WriteLine($"  APPLIED {migration}");
        Console.WriteLine($"Physical schema verified against last applied model: {(baselineSchema.Missing.Count == 0 ? "Yes" : "No")}");
        Console.WriteLine($"Objects missing from last applied model: {baselineSchema.Missing.Count}");
        if (baselineSchema.Missing.Count > 0)
            Console.WriteLine($"Baseline schema drift SHA-256: {ComputeBaselineDriftHash(baselineSchema.Missing)}");
        foreach (var item in baselineSchema.Missing)
            Console.WriteLine($"  BASELINE-MISSING {item}");
        Console.WriteLine($"Objects missing from current repository model: {currentSchema.Missing.Count}");
        foreach (var item in currentSchema.Missing)
            Console.WriteLine($"  MODEL-MISSING {item}");

        var status = new Inspection(
            repositoryMigrations,
            appliedMigrations,
            pending,
            unknownApplied,
            hasPendingModelChanges,
            baselineSchema.Missing,
            currentSchema.Missing);
        if (writeScript && options.TryGetValue("script-output", out var scriptPath) && pending.Length > 0)
        {
            var script = GeneratePendingSql(context, appliedMigrations, repositoryMigrations);
            WriteReviewedScript(scriptPath, script);
        }
        else if (pending.Length > 0)
        {
            Console.WriteLine("Review SQL: rerun inspect with --script-output <file> or use the script action.");
        }
        return status;
    }

    private static async Task<int> ApplyReviewedMigrationsAsync(
        AppDbContext context,
        (AppDbContext Context, string Server, string Database, int? DatabaseId, bool DatabaseExistedAtProbe) target,
        Inspection status,
        IReadOnlyDictionary<string, string> options)
    {
        if (status.HasPendingModelChanges || status.UnknownAppliedMigrations.Count > 0)
            throw new InvalidOperationException("Apply is blocked until the model/snapshot and source-vs-history are reconciled.");
        if (status.PendingMigrations.Count == 0)
        {
            Console.WriteLine("No pending migrations; no database write was performed.");
            Console.WriteLine("Backup verified: Not needed.");
            return 0;
        }

        var scriptPath = ResolveRepositoryPath(Required(options, "script-file"));
        var approvedHash = Required(options, "approved-sha256");
        var approvedScript = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);
        var expectedScript = GeneratePendingSql(context, status.AppliedMigrations, status.RepositoryMigrations);
        var baselineDriftWillBeRepaired = false;
        if (status.MissingBaselineSchemaObjects.Count > 0)
        {
            var approvedDriftHash = Required(options, "allow-unrelated-baseline-drift-sha256");
            var observedDriftHash = ComputeBaselineDriftHash(status.MissingBaselineSchemaObjects);
            if (!string.Equals(observedDriftHash, approvedDriftHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Baseline schema drift changed since inspection. Current drift SHA-256: {observedDriftHash}. No database write was performed.");

            if (IsBaselineDriftDisjointFromMigration(
                    status.MissingBaselineSchemaObjects, expectedScript, out var driftTables, out var overlappingTables))
            {
                Console.WriteLine($"Acknowledged baseline schema drift SHA-256: {observedDriftHash}");
                Console.WriteLine($"Acknowledged baseline drift tables disjoint from pending SQL: {string.Join(", ", driftTables)}");
            }
            else if (IsBaselineDriftRepairedByMigration(status.MissingBaselineSchemaObjects, expectedScript))
            {
                baselineDriftWillBeRepaired = true;
                Console.WriteLine($"Acknowledged baseline schema drift SHA-256: {observedDriftHash}");
                Console.WriteLine("Pending migration SQL explicitly repairs every acknowledged baseline schema difference.");
            }
            else
            {
                throw new InvalidOperationException(
                    $"Baseline drift is neither disjoint from nor fully repaired by the pending migration. " +
                    $"Drift tables: {string.Join(", ", driftTables)}; overlapping tables: {string.Join(", ", overlappingTables)}. No database write was performed.");
            }
        }
        else if (options.ContainsKey("allow-unrelated-baseline-drift-sha256"))
        {
            throw new InvalidOperationException("A baseline drift hash was supplied, but no baseline schema drift is present.");
        }

        var approvedDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(approvedScript)));
        var generatedDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedScript)));
        if (!string.Equals(approvedDigest, approvedHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(generatedDigest, approvedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reviewed SQL hash does not match the current target's generated SQL. " +
                $"Expected reviewed hash: {generatedDigest}. No database write was performed.");
        }

        if (options.TryGetValue("backup-directory", out var backupDirectory)
            && !string.IsNullOrWhiteSpace(backupDirectory))
        {
            var safeDatabaseName = Regex.Replace(target.Database, @"[^A-Za-z0-9._-]", "_");
            var backupFile = Path.Combine(
                backupDirectory,
                $"{safeDatabaseName}-Development-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak");
            await CreateAndVerifyBackupAsync(context.Database.GetDbConnection(), target.Database, backupFile);
            Console.WriteLine($"Optional backup verified: Yes ({backupFile})");
        }
        else
        {
            Console.WriteLine("Backup: Not requested; Development apply does not require backup storage or RESTORE VERIFYONLY.");
        }

        var beforeApplyIdentity = await ReadIdentityAsync(context.Database.GetDbConnection());
        if (!SameEndpoint(beforeApplyIdentity.Server, target.Server) ||
            !SameEndpoint(beforeApplyIdentity.Database, target.Database) ||
            target.DatabaseId is null || beforeApplyIdentity.DatabaseId != target.DatabaseId.Value)
            throw new InvalidOperationException("The connected database identity changed before migration; no migration was run.");

        await context.Database.MigrateAsync();
        var afterApplyIdentity = await ReadIdentityAsync(context.Database.GetDbConnection());
        if (!SameEndpoint(afterApplyIdentity.Server, target.Server) ||
            !SameEndpoint(afterApplyIdentity.Database, target.Database) ||
            afterApplyIdentity.DatabaseId != target.DatabaseId)
            throw new InvalidOperationException("The connected database identity changed during migration.");

        var postStatus = await InspectAsync(context, target.Server, target.Database,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), writeScript: false);
        foreach (var migration in status.PendingMigrations)
            Console.WriteLine($"APPLIED {migration}");
        Console.WriteLine($"Migrations applied: {status.PendingMigrations.Count}");
        Console.WriteLine($"Post-apply EF Model synchronized: {(postStatus.IsModelSynchronized ? "Yes" : "No")}");
        Console.WriteLine($"Post-apply physical schema verified: {(postStatus.MissingCurrentSchemaObjects.Count == 0 ? "Yes" : "No")}");
        var expectedRemainingBaselineDrift = baselineDriftWillBeRepaired
            ? new HashSet<string>(StringComparer.Ordinal)
            : status.MissingBaselineSchemaObjects.ToHashSet(StringComparer.Ordinal);
        var remainingCurrentDrift = postStatus.MissingCurrentSchemaObjects.ToHashSet(StringComparer.Ordinal);
        var migrationSchemaVerified = expectedRemainingBaselineDrift.SetEquals(remainingCurrentDrift);
        Console.WriteLine($"Post-apply schema verified excluding acknowledged baseline drift: {(migrationSchemaVerified ? "Yes" : "No")}");
        Console.WriteLine($"Post-apply baseline schema drift remaining: {remainingCurrentDrift.Count}");
        Console.WriteLine("Persistence tests: Not run by this schema gate.");
        return postStatus.IsModelSynchronized && postStatus.PendingMigrations.Count == 0 &&
            postStatus.UnknownAppliedMigrations.Count == 0 && migrationSchemaVerified ? 0 : 2;
    }

    private static async Task<int> ResetDevelopmentDatabaseAsync(
        AppDbContext context,
        (AppDbContext Context, string Server, string Database, int? DatabaseId, bool DatabaseExistedAtProbe) target,
        Inspection status,
        IReadOnlyDictionary<string, string> options)
    {
        ValidateProductOwnerResetAuthorization(target.Server, target.Database, options);
        if (status.HasPendingModelChanges)
            throw new InvalidOperationException("Database reset is blocked until the EF model and migration snapshot are synchronized.");
        if (status.RepositoryMigrations.Count == 0)
            throw new InvalidOperationException("Database reset is blocked because the repository has no EF migrations.");

        // Generate the complete empty-database migration script before allowing a drop.
        // The reset itself is applied through EF MigrateAsync, not by executing a loose SQL file.
        var cleanDatabaseScript = GeneratePendingSql(context, Array.Empty<string>(), status.RepositoryMigrations);
        if (string.IsNullOrWhiteSpace(cleanDatabaseScript))
            throw new InvalidOperationException("EF did not generate a migration path from an empty database; reset was stopped.");
        var cleanDatabaseScriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cleanDatabaseScript)));

        if (options.TryGetValue("backup-directory", out var backupDirectory)
            && !string.IsNullOrWhiteSpace(backupDirectory))
        {
            if (!target.DatabaseExistedAtProbe || target.DatabaseId is null)
                throw new InvalidOperationException("An optional reset backup was requested, but the verified Development database does not exist to back up.");

            var beforeBackupIdentity = await ReadIdentityAsync(context.Database.GetDbConnection());
            if (!SameEndpoint(beforeBackupIdentity.Server, target.Server)
                || !SameEndpoint(beforeBackupIdentity.Database, target.Database)
                || beforeBackupIdentity.DatabaseId != target.DatabaseId.Value)
                throw new InvalidOperationException("The connected database identity changed before the optional reset backup; the database was not deleted.");

            var safeDatabaseName = Regex.Replace(target.Database, @"[^A-Za-z0-9._-]", "_");
            var backupFile = Path.Combine(
                backupDirectory,
                $"{safeDatabaseName}-Development-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak");
            await CreateAndVerifyBackupAsync(context.Database.GetDbConnection(), target.Database, backupFile);
            Console.WriteLine($"Optional reset backup verified: Yes ({backupFile})");
        }
        else
        {
            Console.WriteLine("Backup: Not requested; Development backups are optional. Product Owner authorization and reset safety checks remain required.");
        }
        Console.WriteLine($"Empty-database EF migration script SHA-256: {cleanDatabaseScriptHash}");
        Console.WriteLine($"Pre-reset applied migrations: {status.AppliedMigrations.Count}; repository migrations: {status.RepositoryMigrations.Count}");

        var beforeResetIdentity = await ReadIdentityAsync(context.Database.GetDbConnection());
        if (!SameEndpoint(beforeResetIdentity.Server, target.Server)
            || !SameEndpoint(beforeResetIdentity.Database, target.Database)
            || target.DatabaseId is null
            || beforeResetIdentity.DatabaseId != target.DatabaseId.Value)
            throw new InvalidOperationException("The connected database identity changed before reset; the database was not deleted.");

        await VerifyMinimalSeedApiSupportAsync();
        await context.Database.CloseConnectionAsync();
        var deleted = await context.Database.EnsureDeletedAsync();
        if (!deleted)
            throw new InvalidOperationException("EF did not delete the verified Development database; no migration was applied.");
        Console.WriteLine($"Development database deleted through EF: {target.Server}/{target.Database}");

        var targetConnectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(targetConnectionString))
            throw new InvalidOperationException("The verified Development connection string is unavailable after the database deletion.");
        await CreateDevelopmentDatabaseInMasterAsync(targetConnectionString, target.Server, target.Database);

        await context.DisposeAsync();
        await using var resetContext = CreateContext(targetConnectionString);
        await resetContext.Database.MigrateAsync();
        await resetContext.Database.OpenConnectionAsync();
        var afterResetIdentity = await ReadIdentityAsync(resetContext.Database.GetDbConnection());
        if (!SameEndpoint(afterResetIdentity.Server, target.Server)
            || !SameEndpoint(afterResetIdentity.Database, target.Database)
            || afterResetIdentity.DatabaseId <= 0)
            throw new InvalidOperationException("Post-reset EF migration connected to an unexpected database.");
        Console.WriteLine($"Recreated Development DB ID verified: {afterResetIdentity.DatabaseId}");

        var postStatus = await InspectAsync(
            resetContext,
            target.Server,
            target.Database,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            writeScript: false);
        Console.WriteLine($"EF migrations applied to clean database: {postStatus.AppliedMigrations.Count}");
        Console.WriteLine($"Fresh database schema verified: {(postStatus.IsSynchronized ? "Yes" : "No")}");
        if (!postStatus.IsSynchronized || postStatus.AppliedMigrations.Count != status.RepositoryMigrations.Count)
            return 2;

        await resetContext.Database.CloseConnectionAsync();
        await RunMinimalDevelopmentSeedTwiceAsync(targetConnectionString, afterResetIdentity.DatabaseId);
        await resetContext.Database.OpenConnectionAsync();
        var postSeedIdentity = await ReadIdentityAsync(resetContext.Database.GetDbConnection());
        if (!SameEndpoint(postSeedIdentity.Server, target.Server)
            || !SameEndpoint(postSeedIdentity.Database, target.Database)
            || postSeedIdentity.DatabaseId != afterResetIdentity.DatabaseId)
            throw new InvalidOperationException("Post-seed verification connected to an unexpected database.");
        await SeedDataProvider.VerifyMinimalDevelopmentSeedOutputAsync(resetContext);
        Console.WriteLine("Minimal Seed output verified: one active Admin, complete protected Admin permissions, and no business/demo records.");
        var postSeedStatus = await InspectAsync(
            resetContext,
            target.Server,
            target.Database,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            writeScript: false);
        Console.WriteLine("Minimal Development seed: Passed twice with stable table row counts.");
        Console.WriteLine($"Post-seed physical schema verified: {(postSeedStatus.IsSynchronized ? "Yes" : "No")}");
        if (!postSeedStatus.IsSynchronized || postSeedStatus.AppliedMigrations.Count != status.RepositoryMigrations.Count)
            return 2;
        Console.WriteLine("Reset and seed workflow: Complete.");
        return 0;
    }

    private static async Task RunMinimalDevelopmentSeedTwiceAsync(string connectionString, int expectedDatabaseId)
    {
        var apiAssembly = GetDevelopmentApiAssemblyPath();
        var bootstrapPassword = Environment.GetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD");
        if (string.IsNullOrWhiteSpace(bootstrapPassword))
            throw new InvalidOperationException("The mandatory Minimal Seed cannot run without NEXUS_ADMIN_BOOTSTRAP_PASSWORD.");

        IReadOnlyDictionary<string, long>? firstPassRowCounts = null;
        for (var pass = 1; pass <= 2; pass++)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(apiAssembly);
            startInfo.ArgumentList.Add("--seed-minimal-only");
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
            startInfo.Environment["ConnectionStrings__DefaultConnection"] = connectionString;
            startInfo.Environment[ConnectionEnvironmentVariable] = connectionString;
            startInfo.Environment["NEXUS_ADMIN_BOOTSTRAP_PASSWORD"] = bootstrapPassword;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the prebuilt Development API for the mandatory Minimal Seed.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            WriteSanitizedSeedOutput(stdout, connectionString, bootstrapPassword);
            WriteSanitizedSeedOutput(stderr, connectionString, bootstrapPassword, isError: true);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Minimal Development seed pass {pass} failed with exit code {process.ExitCode}; reset is incomplete.");
            Console.WriteLine($"Minimal Development seed pass {pass}: Passed.");

            var rowCounts = await ReadDevelopmentTableRowCountsAsync(connectionString, expectedDatabaseId);
            if (pass == 1)
            {
                firstPassRowCounts = rowCounts;
                continue;
            }

            var differences = firstPassRowCounts!
                .Keys
                .Union(rowCounts.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(table => !firstPassRowCounts.TryGetValue(table, out var firstCount)
                    || !rowCounts.TryGetValue(table, out var secondCount)
                    || firstCount != secondCount)
                .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (differences.Length > 0)
                throw new InvalidOperationException($"The second Development seed changed persisted table row counts; idempotency verification failed for: {string.Join(", ", differences)}.");

            Console.WriteLine($"Seed idempotency row-count verification: Passed for {rowCounts.Count} tables.");
        }
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadDevelopmentTableRowCountsAsync(string connectionString, int expectedDatabaseId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var identity = await ReadIdentityAsync(connection);
        if (!SameEndpoint(identity.Server, "Shady")
            || !SameEndpoint(identity.Database, "NexusPmo")
            || identity.DatabaseId != expectedDatabaseId)
            throw new InvalidOperationException("Seed idempotency verification connected to an unexpected SQL Server or database.");

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.name, t.name, SUM(CONVERT(bigint, p.row_count))
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.dm_db_partition_stats AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
            GROUP BY s.name, t.name;
            """;

        var rowCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rowCounts[$"{reader.GetString(0)}.{reader.GetString(1)}"] = reader.GetInt64(2);
        return rowCounts;
    }

    private static async Task VerifyMinimalSeedApiSupportAsync()
    {
        var apiAssembly = GetDevelopmentApiAssemblyPath();
        if (!File.Exists(apiAssembly))
            throw new InvalidOperationException($"Reset is blocked before deletion/creation because the prebuilt Development API is missing: {apiAssembly}. Build an API that supports Minimal Seed.");
        var apiAssemblyBytes = await File.ReadAllBytesAsync(apiAssembly);
        if (!ContainsAssemblyText(apiAssemblyBytes, MinimalSeedCapabilityProbe)
            || !ContainsAssemblyText(apiAssemblyBytes, MinimalSeedCapabilityMarker))
            throw new InvalidOperationException($"Reset is blocked before deletion/creation because the prebuilt API does not contain the Minimal Seed capability contract. Rebuild the Development API before reset.");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(apiAssembly);
        startInfo.ArgumentList.Add(MinimalSeedCapabilityProbe);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment.Remove(ConnectionEnvironmentVariable);
        startInfo.Environment.Remove("ConnectionStrings__DefaultConnection");
        startInfo.Environment.Remove("SQLCONNSTR_DefaultConnection");
        startInfo.Environment.Remove("SQLAZURECONNSTR_DefaultConnection");
        startInfo.Environment.Remove("CUSTOMCONNSTR_DefaultConnection");
        startInfo.Environment.Remove("NEXUS_ADMIN_BOOTSTRAP_PASSWORD");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Reset is blocked before deletion/creation because the Minimal Seed capability probe could not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new InvalidOperationException("Reset is blocked before deletion/creation because the Minimal Seed capability probe timed out.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0
            || !stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), MinimalSeedCapabilityMarker, StringComparison.Ordinal)))
        {
            var diagnostic = string.Join(" ", new[] { stdout, stderr }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            if (diagnostic.Length > 600)
                diagnostic = diagnostic[..600];
            throw new InvalidOperationException($"Reset is blocked before deletion/creation because the prebuilt API does not confirm Minimal Seed capability {MinimalSeedCapabilityMarker}. API probe exit code: {process.ExitCode}. {diagnostic}");
        }

        Console.WriteLine($"Prebuilt Development API Minimal Seed capability: Verified ({MinimalSeedCapabilityMarker}).");
    }

    private static bool ContainsAssemblyText(byte[] assemblyBytes, string value)
    {
        var bytes = assemblyBytes.AsSpan();
        return bytes.IndexOf(Encoding.Unicode.GetBytes(value)) >= 0
            || bytes.IndexOf(Encoding.UTF8.GetBytes(value)) >= 0;
    }

    private static string GetDevelopmentApiAssemblyPath() => Path.Combine(
        FindRepositoryRoot(), "apps", "api", "src", "NEXUS.Api", "bin", "Debug", "net10.0", "NEXUS.Api.dll");

    private static void WriteSanitizedSeedOutput(
        string output,
        string connectionString,
        string bootstrapPassword,
        bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;
        var sanitized = output
            .Replace(connectionString, "[connection string redacted]", StringComparison.Ordinal)
            .Replace(bootstrapPassword, "[bootstrap secret redacted]", StringComparison.Ordinal);
        if (isError)
            Console.Error.Write(sanitized);
        else
            Console.Write(sanitized);
    }

    private static async Task CreateDevelopmentDatabaseInMasterAsync(string targetConnectionString, string server, string database)
    {
        var masterConnectionString = new SqlConnectionStringBuilder(targetConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            var identity = await ReadIdentityAsync(master);
            if (!SameEndpoint(identity.Server, server) || !SameEndpoint(identity.Database, "master"))
                throw new InvalidOperationException("The SQL Server identity changed before Development database recreation.");
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE {QuoteIdentifier(database)};";
            await create.ExecuteNonQueryAsync();
        }

        Console.WriteLine($"Empty Development database recreated: {server}/{database}; no pre-existing database data remained after the verified reset.");
    }

    private static string ComputeBaselineDriftHash(IEnumerable<string> drift) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", drift.OrderBy(item => item, StringComparer.Ordinal)))));

    private static bool IsBaselineDriftDisjointFromMigration(
        IReadOnlyCollection<string> drift,
        string migrationSql,
        out string[] driftTables,
        out string[] overlappingTables)
    {
        driftTables = drift
            .SelectMany(item => Regex.Matches(item, @"\bdbo\.(?<table>[A-Za-z0-9_]+)")
                .Cast<Match>()
                .Select(match => match.Groups["table"].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        overlappingTables = driftTables
            .Where(table => migrationSql.Contains($"[{table}]", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return driftTables.Length > 0 && overlappingTables.Length == 0;
    }

    private static bool IsBaselineDriftRepairedByMigration(
        IReadOnlyCollection<string> drift,
        string migrationSql)
    {
        foreach (var item in drift)
        {
            var column = Regex.Match(item,
                @"^column definition: dbo\.(?<table>[A-Za-z0-9_]+)\.(?<column>[A-Za-z0-9_]+) expected (?<type>[^ ]+) nullable=(?<nullable>true|false), actual .+$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (column.Success)
            {
                var tableName = Regex.Escape(column.Groups["table"].Value);
                var columnName = Regex.Escape(column.Groups["column"].Value);
                var storeType = Regex.Escape(column.Groups["type"].Value);
                var nullability = string.Equals(column.Groups["nullable"].Value, "true", StringComparison.OrdinalIgnoreCase)
                    ? @"NULL"
                    : @"NOT\s+NULL";
                var alteration = $@"ALTER\s+TABLE\s+\[{tableName}\]\s+ALTER\s+COLUMN\s+\[{columnName}\]\s+{storeType}\s+{nullability}\s*;";
                if (!Regex.IsMatch(migrationSql, alteration, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return false;
                continue;
            }

            var foreignKey = Regex.Match(item,
                @"^foreign key: dbo\.(?<table>[A-Za-z0-9_]+)\.(?<name>[A-Za-z0-9_]+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (foreignKey.Success)
            {
                var tableName = Regex.Escape(foreignKey.Groups["table"].Value);
                var constraintName = Regex.Escape(foreignKey.Groups["name"].Value);
                var addConstraint = $@"ALTER\s+TABLE\s+\[{tableName}\]\s+ADD\s+CONSTRAINT\s+\[{constraintName}\]\s+FOREIGN\s+KEY";
                if (!Regex.IsMatch(migrationSql, addConstraint, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return false;
                continue;
            }

            var index = Regex.Match(item,
                @"^index: dbo\.(?<table>[A-Za-z0-9_]+)\.(?<name>[A-Za-z0-9_]+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (index.Success)
            {
                var tableName = Regex.Escape(index.Groups["table"].Value);
                var indexName = Regex.Escape(index.Groups["name"].Value);
                var createIndex = $@"CREATE\s+(?:UNIQUE\s+)?INDEX\s+\[{indexName}\]\s+ON\s+\[{tableName}\]";
                if (!Regex.IsMatch(migrationSql, createIndex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return false;
                continue;
            }

            var table = Regex.Match(item,
                @"^table: dbo\.(?<name>[A-Za-z0-9_]+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (table.Success)
            {
                var tableName = Regex.Escape(table.Groups["name"].Value);
                if (!Regex.IsMatch(migrationSql, $@"CREATE\s+TABLE\s+\[{tableName}\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return false;
                continue;
            }

            return false;
        }

        return drift.Count > 0;
    }

    private static async Task CreateAndVerifyBackupAsync(DbConnection connection, string database, string backupFile)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                $"BACKUP DATABASE {QuoteIdentifier(database)} TO DISK = @backupPath WITH COPY_ONLY, CHECKSUM;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@backupPath";
            parameter.Value = backupFile;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "RESTORE VERIFYONLY FROM DISK = @backupPath WITH CHECKSUM;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@backupPath";
            parameter.Value = backupFile;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyExistingBackupAsync(
        DbConnection connection,
        string backupFile,
        string expectedServer,
        string expectedDatabase)
    {
        await using (var headerCommand = connection.CreateCommand())
        {
            headerCommand.CommandText = "RESTORE HEADERONLY FROM DISK = @backupPath;";
            var headerParameter = headerCommand.CreateParameter();
            headerParameter.ParameterName = "@backupPath";
            headerParameter.Value = backupFile;
            headerCommand.Parameters.Add(headerParameter);
            await using var reader = await headerCommand.ExecuteReaderAsync();
            var backupSets = 0;
            while (await reader.ReadAsync())
            {
                backupSets++;
                var backupDatabase = Convert.ToString(reader["DatabaseName"]);
                var backupServer = Convert.ToString(reader["ServerName"]);
                var backupType = Convert.ToInt32(reader["BackupType"]);
                if (backupType != 1
                    || !string.Equals(backupDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(backupServer, expectedServer, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Backup file '{backupFile}' is not a full backup for the verified target {expectedServer}/{expectedDatabase}.");
            }

            if (backupSets != 1)
                throw new InvalidOperationException($"Backup file '{backupFile}' must contain exactly one full backup set for the verified target.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "RESTORE VERIFYONLY FROM DISK = @backupPath WITH CHECKSUM;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@backupPath";
        parameter.Value = backupFile;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<SchemaComparison> CompareAppliedModelSchemaAsync(
        AppDbContext context,
        IReadOnlyCollection<string> appliedMigrations)
    {
        if (appliedMigrations.Count == 0)
        {
            var actualTables = await ReadCatalogSetAsync(context, """
                SELECT s.name, t.name
                FROM sys.tables AS t
                INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id;
                """, 2);
            var existingApplicationTables = actualTables
                .Where(table => !string.Equals(table, Key("dbo", "__EFMigrationsHistory"), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return new SchemaComparison(existingApplicationTables
                .Select(table => $"user table exists without EF migration history: {table.Replace('\u001f', '.')}").ToArray());
        }

        var latestApplied = appliedMigrations.Last();
        var assembly = context.GetService<IMigrationsAssembly>();
        if (!assembly.Migrations.TryGetValue(latestApplied, out var migrationType))
            return new SchemaComparison([$"applied migration is missing from source: {latestApplied}"]);
        var migration = assembly.CreateMigration(migrationType, context.Database.ProviderName!);
        return await ComparePhysicalSchemaAsync(context, migration.TargetModel);
    }

    private static async Task<SchemaComparison> ComparePhysicalSchemaAsync(AppDbContext context, IModel? modelOverride = null)
    {
        var model = modelOverride ?? context.Model;
        var defaultSchema = model.GetDefaultSchema() ?? "dbo";
        var expectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedColumns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
        var expectedIndexes = new Dictionary<string, IndexDefinition>(StringComparer.OrdinalIgnoreCase);
        var expectedForeignKeys = new Dictionary<string, ForeignKeyDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
                continue;
            var schemaName = entityType.GetSchema() ?? defaultSchema;
            // Keep the EF model's StoreObjectIdentifier exactly as mapped. When no
            // schema is configured, EF uses a null schema in its metadata even though
            // SQL Server resolves the physical table to dbo. Using the fallback schema
            // here makes every table-specific GetColumnName call return null and silently
            // reduces the schema comparison to table names only.
            var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            expectedTables.Add(Key(schemaName, tableName));

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(table);
                if (string.IsNullOrWhiteSpace(columnName))
                    continue;
                var storeType = property.GetColumnType(table) ?? property.GetRelationalTypeMapping().StoreType;
                expectedColumns[Key(schemaName, tableName, columnName)] =
                    new ColumnDefinition(NormalizeStoreType(storeType), property.IsNullable);
            }

            foreach (var key in entityType.GetKeys())
            {
                var keyName = key.GetName(table);
                var columns = key.Properties
                    .Select(property => property.GetColumnName(table))
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .Cast<string>()
                    .ToArray();
                if (!string.IsNullOrWhiteSpace(keyName) && columns.Length > 0)
                    expectedIndexes[Key(schemaName, tableName, keyName)] = new IndexDefinition(true, columns);
            }

            foreach (var index in entityType.GetIndexes())
            {
                var indexName = index.GetDatabaseName(table);
                var columns = index.Properties
                    .Select(property => property.GetColumnName(table))
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .Cast<string>()
                    .ToArray();
                if (!string.IsNullOrWhiteSpace(indexName) && columns.Length > 0)
                    expectedIndexes[Key(schemaName, tableName, indexName)] = new IndexDefinition(index.IsUnique, columns);
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var constraintName = foreignKey.GetConstraintName();
                var principalType = foreignKey.PrincipalEntityType;
                var principalTableName = principalType.GetTableName();
                if (string.IsNullOrWhiteSpace(constraintName) || string.IsNullOrWhiteSpace(principalTableName))
                    continue;

                // Table-splitting ownership is represented as an EF relationship,
                // but SQL Server stores the owner and owned values on the same row
                // and EF emits no physical FK for that relationship. Ignore only
                // same-table ownership constraints; ordinary self-references such as
                // WBS parent links still require a real database FK.
                if (foreignKey.IsOwnership
                    && string.Equals(tableName, principalTableName, StringComparison.Ordinal)
                    && string.Equals(entityType.GetSchema(), principalType.GetSchema(), StringComparison.Ordinal))
                    continue;

                var principalSchema = principalType.GetSchema() ?? defaultSchema;
                var principalTable = StoreObjectIdentifier.Table(principalTableName, principalType.GetSchema());
                var dependentColumns = foreignKey.Properties
                    .Select(property => property.GetColumnName(table))
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .Cast<string>()
                    .ToArray();
                var principalColumns = foreignKey.PrincipalKey.Properties
                    .Select(property => property.GetColumnName(principalTable))
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .Cast<string>()
                    .ToArray();
                if (dependentColumns.Length != principalColumns.Length || dependentColumns.Length == 0)
                    continue;

                expectedForeignKeys[Key(schemaName, tableName, constraintName)] = new ForeignKeyDefinition(
                    dependentColumns,
                    principalSchema,
                    principalTableName,
                    principalColumns,
                    ToSqlDeleteAction(foreignKey.DeleteBehavior));
            }
        }

        var actualTables = await ReadCatalogSetAsync(context, """
            SELECT s.name, t.name
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id;
            """, 2);
        var actualColumns = await ReadColumnDefinitionsAsync(context);
        var actualIndexes = await ReadIndexDefinitionsAsync(context);
        var actualForeignKeys = await ReadForeignKeyDefinitionsAsync(context);

        var missing = new List<string>();
        AddMissing("table", expectedTables, actualTables, missing);
        CompareColumns(expectedColumns, actualColumns, missing);
        CompareIndexes(expectedIndexes, actualIndexes, missing);
        CompareForeignKeys(expectedForeignKeys, actualForeignKeys, missing);
        return new SchemaComparison(missing);
    }

    private static async Task<Dictionary<string, ColumnDefinition>> ReadColumnDefinitionsAsync(AppDbContext context)
    {
        var result = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT s.name, t.name, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = Key(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            var storeType = BuildSqlServerStoreType(
                reader.GetString(3),
                Convert.ToInt32(reader.GetValue(4)),
                Convert.ToByte(reader.GetValue(5)),
                Convert.ToByte(reader.GetValue(6)));
            result[key] = new ColumnDefinition(storeType, reader.GetBoolean(7));
        }
        return result;
    }

    private static async Task<Dictionary<string, IndexDefinition>> ReadIndexDefinitionsAsync(AppDbContext context)
    {
        var raw = new Dictionary<string, RawIndex>(StringComparer.OrdinalIgnoreCase);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT s.name, t.name, i.name, i.is_unique, ic.key_ordinal, c.name
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
            INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id AND c.column_id = ic.column_id
            WHERE i.index_id > 0 AND i.is_hypothetical = 0 AND i.name IS NOT NULL AND ic.key_ordinal > 0
            ORDER BY s.name, t.name, i.name, ic.key_ordinal;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var identity = Key(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            if (!raw.TryGetValue(identity, out var index))
            {
                index = new RawIndex(reader.GetBoolean(3));
                raw.Add(identity, index);
            }
            index.KeyColumns[Convert.ToInt32(reader.GetValue(4))] = reader.GetString(5);
        }

        return raw.ToDictionary(
            pair => pair.Key,
            pair => new IndexDefinition(pair.Value.IsUnique, pair.Value.KeyColumns.Values.ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, ForeignKeyDefinition>> ReadForeignKeyDefinitionsAsync(AppDbContext context)
    {
        var raw = new Dictionary<string, RawForeignKey>(StringComparer.OrdinalIgnoreCase);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT ps.name, pt.name, fk.name, pc.name, rs.name, rt.name, rc.name,
                   fkc.constraint_column_id, fk.delete_referential_action_desc
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
            INNER JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
            INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns AS pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
            INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
            INNER JOIN sys.columns AS rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
            ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var identity = Key(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            if (!raw.TryGetValue(identity, out var foreignKey))
            {
                foreignKey = new RawForeignKey(
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(8));
                raw.Add(identity, foreignKey);
            }
            var ordinal = Convert.ToInt32(reader.GetValue(7));
            foreignKey.DependentColumns[ordinal] = reader.GetString(3);
            foreignKey.PrincipalColumns[ordinal] = reader.GetString(6);
        }

        return raw.ToDictionary(
            pair => pair.Key,
            pair => new ForeignKeyDefinition(
                pair.Value.DependentColumns.Values.ToArray(),
                pair.Value.PrincipalSchema,
                pair.Value.PrincipalTable,
                pair.Value.PrincipalColumns.Values.ToArray(),
                pair.Value.DeleteAction),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void CompareColumns(
        IReadOnlyDictionary<string, ColumnDefinition> expected,
        IReadOnlyDictionary<string, ColumnDefinition> actual,
        List<string> missing)
    {
        foreach (var pair in expected.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!actual.TryGetValue(pair.Key, out var actualColumn))
            {
                missing.Add($"column: {pair.Key.Replace('\u001f', '.')}");
                continue;
            }

            if (!StoreTypesMatch(pair.Value.StoreType, actualColumn.StoreType) ||
                pair.Value.IsNullable != actualColumn.IsNullable)
            {
                missing.Add(
                    $"column definition: {pair.Key.Replace('\u001f', '.')} " +
                    $"expected {pair.Value.StoreType} nullable={pair.Value.IsNullable}, " +
                    $"actual {actualColumn.StoreType} nullable={actualColumn.IsNullable}");
            }
        }
    }

    private static void CompareIndexes(
        IReadOnlyDictionary<string, IndexDefinition> expected,
        IReadOnlyDictionary<string, IndexDefinition> actual,
        List<string> missing)
    {
        foreach (var pair in expected.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!actual.TryGetValue(pair.Key, out var actualIndex))
            {
                missing.Add($"index: {pair.Key.Replace('\u001f', '.')}");
                continue;
            }

            if (pair.Value.IsUnique != actualIndex.IsUnique ||
                !SequenceEqual(pair.Value.KeyColumns, actualIndex.KeyColumns))
            {
                missing.Add(
                    $"index definition: {pair.Key.Replace('\u001f', '.')} " +
                    $"expected unique={pair.Value.IsUnique} columns=[{string.Join(", ", pair.Value.KeyColumns)}], " +
                    $"actual unique={actualIndex.IsUnique} columns=[{string.Join(", ", actualIndex.KeyColumns)}]");
            }
        }
    }

    private static void CompareForeignKeys(
        IReadOnlyDictionary<string, ForeignKeyDefinition> expected,
        IReadOnlyDictionary<string, ForeignKeyDefinition> actual,
        List<string> missing)
    {
        foreach (var pair in expected.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!actual.TryGetValue(pair.Key, out var actualForeignKey))
            {
                missing.Add($"foreign key: {pair.Key.Replace('\u001f', '.')}");
                continue;
            }

            var expectedValue = pair.Value;
            if (!SequenceEqual(expectedValue.DependentColumns, actualForeignKey.DependentColumns) ||
                !string.Equals(expectedValue.PrincipalSchema, actualForeignKey.PrincipalSchema, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(expectedValue.PrincipalTable, actualForeignKey.PrincipalTable, StringComparison.OrdinalIgnoreCase) ||
                !SequenceEqual(expectedValue.PrincipalColumns, actualForeignKey.PrincipalColumns) ||
                !string.Equals(expectedValue.DeleteAction, actualForeignKey.DeleteAction, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(
                    $"foreign key definition: {pair.Key.Replace('\u001f', '.')} " +
                    $"expected [{string.Join(", ", expectedValue.DependentColumns)}] -> " +
                    $"{expectedValue.PrincipalSchema}.{expectedValue.PrincipalTable}([{string.Join(", ", expectedValue.PrincipalColumns)}]) " +
                    $"ON DELETE {expectedValue.DeleteAction}; actual [{string.Join(", ", actualForeignKey.DependentColumns)}] -> " +
                    $"{actualForeignKey.PrincipalSchema}.{actualForeignKey.PrincipalTable}([{string.Join(", ", actualForeignKey.PrincipalColumns)}]) " +
                    $"ON DELETE {actualForeignKey.DeleteAction}");
            }
        }
    }

    private static HashSet<string> GetExpectedTables(IModel model)
    {
        var expectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultSchema = model.GetDefaultSchema() ?? "dbo";
        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
                continue;
            var schemaName = entityType.GetSchema() ?? defaultSchema;
            expectedTables.Add(Key(schemaName, tableName));
        }
        return expectedTables;
    }

    private static async Task<HashSet<string>> ReadCatalogSetAsync(AppDbContext context, string sql, int keyParts)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var parts = new string[keyParts];
            for (var index = 0; index < keyParts; index++)
                parts[index] = reader.GetString(index);
            result.Add(Key(parts));
        }
        return result;
    }

    private static void AddMissing(string kind, HashSet<string> expected, HashSet<string> actual, List<string> missing)
    {
        foreach (var item in expected.Where(item => !actual.Contains(item)).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            missing.Add($"{kind}: {item.Replace('\u001f', '.')}");
    }

    private static string NormalizeStoreType(string storeType)
    {
        var normalized = Regex.Replace(storeType.Trim().ToLowerInvariant(), @"\s+", "");
        return normalized == "timestamp" ? "rowversion" : normalized;
    }

    private static string BuildSqlServerStoreType(string typeName, int maxLength, byte precision, byte scale)
    {
        var type = typeName.ToLowerInvariant();
        string? facets = null;
        if (type is "nchar" or "nvarchar")
            facets = maxLength < 0 ? "max" : (maxLength / 2).ToString();
        else if (type is "char" or "varchar" or "binary" or "varbinary")
            facets = maxLength < 0 ? "max" : maxLength.ToString();
        else if (type is "decimal" or "numeric")
            facets = $"{precision},{scale}";
        else if (type is "time" or "datetime2" or "datetimeoffset")
            facets = scale.ToString();

        return NormalizeStoreType(facets is null ? type : $"{type}({facets})");
    }

    private static bool StoreTypesMatch(string expected, string actual)
    {
        static (string Name, string? Facets) Split(string storeType)
        {
            var open = storeType.IndexOf('(');
            if (open < 0 || !storeType.EndsWith(')'))
                return (storeType, null);
            return (storeType[..open], storeType[(open + 1)..^1]);
        }

        var expectedParts = Split(expected);
        var actualParts = Split(actual);
        return string.Equals(expectedParts.Name, actualParts.Name, StringComparison.OrdinalIgnoreCase) &&
               (expectedParts.Facets is null ||
                string.Equals(expectedParts.Facets, actualParts.Facets, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SequenceEqual(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));

    private static string ToSqlDeleteAction(DeleteBehavior behavior) => behavior switch
    {
        DeleteBehavior.Cascade => "CASCADE",
        DeleteBehavior.SetNull => "SET_NULL",
        _ => "NO_ACTION"
    };

    private sealed record ColumnDefinition(string StoreType, bool IsNullable);
    private sealed record IndexDefinition(bool IsUnique, IReadOnlyList<string> KeyColumns);
    private sealed record ForeignKeyDefinition(
        IReadOnlyList<string> DependentColumns,
        string PrincipalSchema,
        string PrincipalTable,
        IReadOnlyList<string> PrincipalColumns,
        string DeleteAction);

    private sealed class RawIndex(bool isUnique)
    {
        public bool IsUnique { get; } = isUnique;
        public SortedDictionary<int, string> KeyColumns { get; } = new();
    }

    private sealed class RawForeignKey(string principalSchema, string principalTable, string deleteAction)
    {
        public string PrincipalSchema { get; } = principalSchema;
        public string PrincipalTable { get; } = principalTable;
        public string DeleteAction { get; } = deleteAction;
        public SortedDictionary<int, string> DependentColumns { get; } = new();
        public SortedDictionary<int, string> PrincipalColumns { get; } = new();
    }

    private static string GeneratePendingSql(
        AppDbContext context,
        IReadOnlyCollection<string> appliedMigrations,
        IReadOnlyCollection<string> repositoryMigrations)
    {
        var from = appliedMigrations.LastOrDefault();
        var to = repositoryMigrations.LastOrDefault();
        if (to is null)
            throw new InvalidOperationException("The repository contains no migrations.");
        var migrator = context.GetService<IMigrator>();
        var sql = migrator.GenerateScript(from, to, MigrationsSqlGenerationOptions.Idempotent);
        var reviewFlags = Regex.Matches(sql,
            @"(?im)^\s*(DROP\s+(TABLE|COLUMN|INDEX|CONSTRAINT)|TRUNCATE\s+TABLE|DELETE\s+FROM|ALTER\s+TABLE.*DROP\s+(COLUMN|CONSTRAINT))\b")
            .Select(match => match.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (reviewFlags.Length > 0)
        {
            Console.WriteLine("Review carefully; generated SQL includes potentially destructive statements:");
            foreach (var statement in reviewFlags)
                Console.WriteLine($"  {statement}");
        }
        return sql;
    }

    private static void WriteReviewedScript(string path, string script)
    {
        var fullPath = ResolveRepositoryPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        Console.WriteLine($"Reviewed SQL generated: {fullPath}");
        Console.WriteLine($"Reviewed SQL SHA-256: {hash}");
        Console.WriteLine("Inspect this file before authorizing any application.");
    }

    private static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(string Server, string Database, int DatabaseId)> ReadIdentityAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME(), CONVERT(int, DB_ID());
            """;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("SQL Server returned no database identity.");
        return (reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                throw new ArgumentException($"Expected a named option and value near '{key}'.");
            if (!options.TryAdd(key[2..], args[++i]))
                throw new ArgumentException($"Option '{key}' was supplied more than once.");
        }
        return options;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Required option '--{key}' is missing.");

    private static bool SameEndpoint(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static SqlConnectionStringBuilder ParseConnectionString(string? value, string source)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"The {source} is empty. Provide a valid SQL Server connection string in 'Server=<server>;Database=<database>;Integrated Security=True;TrustServerCertificate=True' format. No connection was opened.");
        try
        {
            return new SqlConnectionStringBuilder(value);
        }
        catch (ArgumentException ex)
        {
            // The ArgumentException message carries only the parse index; it never contains the connection string value.
            throw new InvalidOperationException(
                $"The {source} is not a valid 'key=value;key=value' connection string ({ex.Message}). The supplied value was not printed. " +
                "Approved Development Windows Authentication example: Server=Shady;Database=NexusPmo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true. No connection was opened.");
        }
    }

    private static SqlException? FindSqlException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException sqlException)
                return sqlException;
            exception = exception.InnerException;
        }
        return null;
    }

    private static string Key(params string[] parts) => string.Join('\u001f', parts);

    private static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string ResolveRepositoryPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(FindRepositoryRoot(), path);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "apps", "api", "src", "NEXUS.Api", "NEXUS.Api.csproj")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the NEXUS repository root from the tool's output directory.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            NEXUS EF database delivery gate

            model
              Offline model/snapshot and repository migration check. Does not connect to SQL Server.

            inspect --environment Development --expected-server <server> --expected-database <db>
                    [--script-output <file>]
              Read-only check against an explicit Development database.

            script --environment Development --expected-server <server> --expected-database <db>
                   --script-output <file>
              Verify the target read-only and generate pending SQL for human review.

            apply --environment Development --expected-server <server> --expected-database <db>
                  --script-file <reviewed-sql> --approved-sha256 <sha256>
                  [--backup-directory <SQL-Server-accessible-directory>]
                  [--allow-unrelated-baseline-drift-sha256 <inspect-drift-sha256>]
              Explicit Development application after target and SQL hash verification. A backup is optional;
              when requested, --backup-directory is used for a copy-only backup followed by RESTORE VERIFYONLY.
              Existing baseline drift is allowed only when its exact inspect hash is supplied and
              none of its affected dbo tables occur in the pending migration SQL.

            reset --environment Development --expected-server <server> --expected-database <db>
                  --confirm-reset-database <db> --product-owner-authorization <task-or-conversation-reference>
                  [--backup-directory <SQL-Server-accessible-directory>]
              Reset is limited to Development Shady/NexusPmo and requires explicit Product Owner
              authorization recorded in the active task/conversation. The reference is an audit pointer, not
              a substitute for that approval. The gate verifies the live target before destructive work,
              recreates through EF EnsureDeleted/Migrate, applies every migration, runs the prebuilt API's
              separate --seed-minimal-only path twice (never the demo --seed-only path), and verifies the
              target/schema before success. No other target or environment is eligible. A backup may be created
              before reset when --backup-directory is explicitly supplied; it is not required. Ordinary apply
              also runs without a backup after target, migration, SQL-hash, and schema safety checks pass.

            sync-project-plan-permissions --environment Development --expected-server Shady --expected-database NexusPmo
                                          --task-authorization-reference <active-task-or-conversation-reference>
                                          [--backup-verified-path <existing-backup-file>]
              Synchronizes only Planning.View, Planning.Edit, and ProjectPlan.Create canonical catalog rows via EF.
              Requires a synchronized model/history and matching physical permission/grant/audit tables. It never
              changes grants, assignments, direct overrides, or protected system roles. Use only for an explicitly
              authorized non-destructive Development catalog sync. This exact task-scoped operation does not require
              an independent backup; when a backup path is supplied, the gate verifies its target and checksum.
              The command does not run a Demo or Minimal Seed.

            Database actions require ASPNETCORE_ENVIRONMENT=Development and
            NEXUS_EF_DEVELOPMENT_CONNECTION_STRING in the process environment.
            """);
    }

    private sealed record SchemaComparison(IReadOnlyList<string> Missing);
    private sealed record Inspection(
        IReadOnlyList<string> RepositoryMigrations,
        IReadOnlyList<string> AppliedMigrations,
        IReadOnlyList<string> PendingMigrations,
        IReadOnlyList<string> UnknownAppliedMigrations,
        bool HasPendingModelChanges,
        IReadOnlyList<string> MissingBaselineSchemaObjects,
        IReadOnlyList<string> MissingCurrentSchemaObjects)
    {
        public bool IsModelSynchronized => !HasPendingModelChanges;
        public bool IsSynchronized =>
            !HasPendingModelChanges && PendingMigrations.Count == 0 &&
            UnknownAppliedMigrations.Count == 0 && MissingBaselineSchemaObjects.Count == 0 &&
            MissingCurrentSchemaObjects.Count == 0;
    }
}

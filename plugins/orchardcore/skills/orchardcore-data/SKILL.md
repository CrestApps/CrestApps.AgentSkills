---
name: orchardcore-data
description: Skill for configuring the Orchard Core data layer. Covers database provider selection, YesSql options (OrchardCore YesSql section), Sqlite options (Data Sqlite section), table naming presets (Data TableOptions section), document options (Documents section), connection string setup, DbConnection access, SQL dialect abstraction, table prefix and schema conventions, Dapper integration, transactions, and the Orchard Core 4.0 configuration section renames from OrchardCore_YesSql, OrchardCore_Data_Sqlite, OrchardCore_Data_TableOptions and OrchardCore_Documents. Use this skill when requests mention Orchard Core Data, Sqlite Connection Pooling, YesSql Configuration Options, Database Table Options, Creating a DbConnection, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Data, OrchardCore.Data.YesSql, IDbConnectionAccessor, IStore, ISqlDialect, YesSqlOptions, DocumentOptions, ShellSettings, Dapper.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Data - Prompt Templates

## Configure and Use the Orchard Core Data Layer

You are an Orchard Core expert. Generate code and configuration for database access, YesSql settings, Dapper queries, connection management, and table prefix handling.

### Guidelines

- Orchard Core uses YesSql as its document database abstraction over relational databases.
- Supported database providers: `Sqlite` (default), `SqlConnection` (SQL Server), `Postgres`, `MySql`. For SQL Server, MySQL and PostgreSQL, create the database first; Orchard Core creates the tables, not the database.
- Provider, connection string, table prefix and schema are tenant settings (setup screen or `IShellConfiguration`).
- Prefer `IContentManager` for content items and `ISession` for YesSql documents and indexes; use raw SQL only for custom relational tables.
- Use `IDbConnectionAccessor` (`OrchardCore.Data`, package `OrchardCore.Data.Abstractions`) to create `DbConnection` instances pointing to the tenant database.
- Use `IStore.Configuration` for the `SqlDialect`, `TablePrefix` (already includes the separator) and `Schema`.
- Quote table names with `dialect.QuoteForTableName(name, schema)` so tenants with a schema work too.
- Never pass user input into identifiers; pass data values as Dapper parameters.
- Create custom tables through data migrations (see `orchardcore-data-migrations`).
- Configuration sections are nested under `OrchardCore` and, since Orchard Core 4.0, are named `YesSql`, `Data:Sqlite`, `Data:TableOptions` and `Documents`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except view models.

### Sqlite Options

The `DatabaseName` shell setting controls the SQLite file name (`OrchardCore.db` by default) in the tenant's data folder.

`Microsoft.Data.Sqlite` pools connections by default. Disable it when file locking interferes with copying or replacing the database file (for example backups); this can reduce performance:

```json
{
  "OrchardCore": {
    "Data": {
      "Sqlite": {
        "UseConnectionPooling": false
      }
    }
  }
}
```

### YesSql Configuration Options

`OrchardCore:YesSql` binds to `YesSqlOptions` (namespace `OrchardCore.Data.YesSql`).

| Setting | Description | Default |
|---|---|---|
| `CommandsPageSize` | Max commands per command page before YesSql splits them | `500` |
| `QueryGatingEnabled` | Coalesces identical concurrent queries and shares the result | `true` |
| `EnableThreadSafetyChecks` | Helps diagnose concurrent use of a session | `false` |
| `IsolationLevel` | Default transaction isolation level | `ReadCommitted` |

`IdGenerator`, `IdentifierAccessorFactory`, `VersionAccessorFactory` and `ContentSerializer` can only be set in code.

#### Configure YesSql in appsettings.json

```json
{
  "OrchardCore": {
    "YesSql": {
      "CommandsPageSize": 1000,
      "QueryGatingEnabled": true,
      "EnableThreadSafetyChecks": false,
      "IsolationLevel": "ReadCommitted"
    }
  }
}
```

In a tenant-local `App_Data/Sites/{tenant}/appsettings.json`, omit the outer `OrchardCore` section.

#### Configure YesSql in Code

```csharp
using OrchardCore.Data.YesSql;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<YesSqlOptions>(options =>
        {
            options.CommandsPageSize = 1000;
        });
    }
}
```

### Database Table Options

`OrchardCore:Data:TableOptions` holds presets copied into a tenant's shell settings during initial setup. Changing them later doesn't rename existing tables or alter identity columns.

| Setting | Description | Default |
|---|---|---|
| `DefaultDocumentTable` | Document table name | `Document` |
| `DefaultTableNameSeparator` | Separator between prefix/collection and table name (one or more `_`, or `NULL` for none) | `_` |
| `DefaultIdentityColumnSize` | Identity column size (`Int32` or `Int64`) | `Int64` |

```json
{
  "OrchardCore": {
    "Data": {
      "TableOptions": {
        "DefaultDocumentTable": "Document",
        "DefaultTableNameSeparator": "_",
        "DefaultIdentityColumnSize": "Int64"
      }
    }
  }
}
```

### Document Options

`OrchardCore:Documents` sets the shared options of the documents managed by `IDocumentManager<TDocument>` (site settings, content definitions, ...): `CheckConcurrency` and `CheckConsistency` (default `true`), `SynchronizationLatency` (default 1 second), `FailoverRetryLatency` (default 30 seconds), `CompressThreshold` (default 10,000 characters), and `LockTimeout`/`LockExpiration` (default 10,000 ms, atomic updates only).

```json
{
  "OrchardCore": {
    "Documents": {
      "SynchronizationLatency": "00:00:02"
    }
  }
}
```

### Creating a DbConnection

```csharp
using Dapper;
using OrchardCore.Data;
using YesSql;

public sealed class MyDataService
{
    private readonly IDbConnectionAccessor _dbAccessor;
    private readonly IStore _store;

    public MyDataService(IDbConnectionAccessor dbAccessor, IStore store)
    {
        _dbAccessor = dbAccessor;
        _store = store;
    }

    public async Task<int> GetRecordCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = _dbAccessor.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var configuration = _store.Configuration;
        var table = configuration.SqlDialect.QuoteForTableName($"{configuration.TablePrefix}MyTable", configuration.Schema);

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition($"SELECT COUNT(*) FROM {table}", cancellationToken: cancellationToken));
    }
}
```

### SQL Dialect Abstraction

```csharp
using Dapper;
using OrchardCore.Data;
using YesSql;

public sealed class ProductRepository
{
    private readonly IDbConnectionAccessor _dbAccessor;
    private readonly IStore _store;

    public ProductRepository(IDbConnectionAccessor dbAccessor, IStore store)
    {
        _dbAccessor = dbAccessor;
        _store = store;
    }

    public async Task<IReadOnlyList<ProductRecord>> GetByCategoryAsync(string category, CancellationToken cancellationToken)
    {
        await using var connection = _dbAccessor.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var configuration = _store.Configuration;

        // TablePrefix already contains the separator; pass the schema when quoting.
        var tableName = configuration.SqlDialect.QuoteForTableName(
            $"{configuration.TablePrefix}ProductRecord",
            configuration.Schema);

        var command = new CommandDefinition(
            $"SELECT * FROM {tableName} WHERE Category = @Category",
            new { Category = category },
            cancellationToken: cancellationToken);

        return (await connection.QueryAsync<ProductRecord>(command)).AsList();
    }
}
```

### Dapper with Transactions

Open the connection before beginning the transaction, pass the transaction to every command, and rethrow after rollback:

```csharp
using Dapper;
using OrchardCore.Data;
using YesSql;

public sealed class OrderService
{
    private readonly IDbConnectionAccessor _dbAccessor;
    private readonly IStore _store;

    public OrderService(IDbConnectionAccessor dbAccessor, IStore store)
    {
        _dbAccessor = dbAccessor;
        _store = store;
    }

    public async Task ChangeStatusAsync(int orderId, string newStatus, CancellationToken cancellationToken)
    {
        var configuration = _store.Configuration;
        var dialect = configuration.SqlDialect;
        var ordersTable = dialect.QuoteForTableName($"{configuration.TablePrefix}Orders", configuration.Schema);
        var auditTable = dialect.QuoteForTableName($"{configuration.TablePrefix}OrderAudit", configuration.Schema);

        await using var connection = _dbAccessor.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"UPDATE {ordersTable} SET Status = @Status WHERE Id = @Id",
                new { Status = newStatus, Id = orderId },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                $"INSERT INTO {auditTable} (OrderId, Action, Timestamp) VALUES (@OrderId, @Action, @Timestamp)",
                new { OrderId = orderId, Action = $"Status changed to {newStatus}", Timestamp = DateTime.UtcNow },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

### Handling Table Prefixes

Each tenant can have a table prefix and a schema. Read them from `IStore.Configuration` (`TablePrefix` includes the separator, `Schema` may be `null`) rather than from `ShellSettings["TablePrefix"]`, which lacks the separator.

### Upgrading from 3.x

- `OrchardCore_YesSql` is now `OrchardCore:YesSql`.
- `OrchardCore_Data_Sqlite` is now `OrchardCore:Data:Sqlite`.
- `OrchardCore_Data_TableOptions` is now `OrchardCore:Data:TableOptions`.
- `OrchardCore_Documents` is now `OrchardCore:Documents`.
- The old names are deprecated but still read; when both exist, the new name wins. Environment variables follow the same shape, for example `OrchardCore__YesSql__CommandsPageSize`. The packages ship a `ConfigurationSchema.json` that gives `appsettings.json` IntelliSense and flags deprecated names.

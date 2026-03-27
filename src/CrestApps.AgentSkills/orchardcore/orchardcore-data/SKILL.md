---
name: orchardcore-data
description: Skill for configuring the Orchard Core data layer. Covers database provider selection, YesSql configuration options, connection string setup, DbConnection access, SQL dialect abstraction, table prefix conventions, Dapper integration, and data access patterns. Use this skill when requests mention Orchard Core Data, Configure and Use the Orchard Core Data Layer, Sqlite Connection Pooling Configuration, YesSql Configuration Options, Database Table Options, Creating a DbConnection, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Data, OrchardCore.Environment.Shell, IDbConnectionAccessor, IStore, ISqlDialect, ShellSettings, IServiceCollection, MyDataService, IEnumerable, OrderService, INSERT, INTO. It also helps with Database Table Options, Creating a DbConnection, SQL Dialect Abstraction, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Data - Prompt Templates

## Configure and Use the Orchard Core Data Layer

You are an Orchard Core expert. Generate code and configuration for database access, YesSql settings, Dapper queries, connection management, and table prefix handling.

### Guidelines

- Orchard Core uses YesSql as its document database abstraction over relational databases.
- Supported database providers: `Sqlite`, `SqlConnection` (SQL Server), `Postgres`, `MySql`.
- Use `IDbConnectionAccessor` to create `DbConnection` instances pointing to the tenant database.
- Use `IStore` to access `ISqlDialect` for database-agnostic query building.
- Always account for `TablePrefix` from `ShellSettings` when building raw SQL.
- Use `dialect.QuoteForTableName()` to properly quote table names for the active provider.
- Dapper can be used directly with `DbConnection` for custom queries.
- Configure YesSql options via `YesSqlOptions` in code or `OrchardCore_YesSql` in `appsettings.json`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Sqlite Connection Pooling Configuration

By default `Microsoft.Data.Sqlite` pools connections. Disable it if database file locking interferes with backups:

```json
{
  "OrchardCore_Data_Sqlite": {
    "UseConnectionPooling": false
  }
}
```

### YesSql Configuration Options

| Setting | Description | Default |
|---|---|---|
| `CommandsPageSize` | Max queries per command batch before splitting | `500` |
| `QueryGatingEnabled` | Enables query gating optimization | `true` |
| `EnableThreadSafetyChecks` | Aids diagnosing concurrency issues | `false` |
| `IsolationLevel` | Default transaction isolation level | `ReadCommitted` |
| `IdGenerator` | Custom ID generator implementation | Built-in |
| `ContentSerializer` | Custom content serializer | JSON |

#### Configure YesSql in appsettings.json

```json
{
  "OrchardCore_YesSql": {
    "CommandsPageSize": 1000,
    "QueryGatingEnabled": true,
    "EnableThreadSafetyChecks": false
  }
}
```

#### Configure YesSql in Code

```csharp
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

These settings are presets used before a tenant is set up:

| Setting | Description | Default |
|---|---|---|
| `DefaultDocumentTable` | Document table name | `Document` |
| `DefaultTableNameSeparator` | Table name separator (`_` or `NULL` for none) | `_` |
| `DefaultIdentityColumnSize` | Identity column size (`Int32` or `Int64`) | `Int64` |

```json
{
  "OrchardCore_Data_TableOptions": {
    "DefaultDocumentTable": "Document",
    "DefaultTableNameSeparator": "_",
    "DefaultIdentityColumnSize": "Int64"
  }
}
```

### Creating a DbConnection

Use `IDbConnectionAccessor` from `OrchardCore.Data` to obtain a connection to the tenant database:

```csharp
using OrchardCore.Data;

public sealed class MyDataService
{
    private readonly IDbConnectionAccessor _dbAccessor;

    public MyDataService(IDbConnectionAccessor dbAccessor)
    {
        _dbAccessor = dbAccessor;
    }

    public async Task<int> GetRecordCountAsync()
    {
        await using var connection = _dbAccessor.CreateConnection();

        // Use the connection with Dapper, ADO.NET, etc.
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM MyTable");
    }
}
```

### SQL Dialect Abstraction

Use `ISqlDialect` from `IStore.Configuration` to write provider-agnostic SQL:

```csharp
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using YesSql;

public sealed class ProductRepository
{
    private readonly IDbConnectionAccessor _dbAccessor;
    private readonly IStore _store;
    private readonly string _tablePrefix;

    public ProductRepository(
        IDbConnectionAccessor dbAccessor,
        IStore store,
        ShellSettings shellSettings)
    {
        _dbAccessor = dbAccessor;
        _store = store;
        _tablePrefix = shellSettings["TablePrefix"];
    }

    public async Task<IEnumerable<ProductRecord>> GetAllProductsAsync()
    {
        await using var connection = _dbAccessor.CreateConnection();

        var dialect = _store.Configuration.SqlDialect;
        var tableName = dialect.QuoteForTableName($"{_tablePrefix}ProductRecord");

        return await connection.QueryAsync<ProductRecord>(
            $"SELECT * FROM {tableName}");
    }
}
```

### Dapper with Transactions

Use explicit transactions for multi-statement operations:

```csharp
using Dapper;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using YesSql;

public sealed class OrderService
{
    private readonly IDbConnectionAccessor _dbAccessor;
    private readonly IStore _store;
    private readonly string _tablePrefix;

    public OrderService(
        IDbConnectionAccessor dbAccessor,
        IStore store,
        ShellSettings shellSettings)
    {
        _dbAccessor = dbAccessor;
        _store = store;
        _tablePrefix = shellSettings["TablePrefix"];
    }

    public async Task TransferOrderAsync(int orderId, string newStatus)
    {
        await using var connection = _dbAccessor.CreateConnection();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var dialect = _store.Configuration.SqlDialect;
            var ordersTable = dialect.QuoteForTableName($"{_tablePrefix}Orders");
            var auditTable = dialect.QuoteForTableName($"{_tablePrefix}OrderAudit");

            await connection.ExecuteAsync(
                $"UPDATE {ordersTable} SET Status = @Status WHERE Id = @Id",
                new { Status = newStatus, Id = orderId },
                transaction);

            await connection.ExecuteAsync(
                $"INSERT INTO {auditTable} (OrderId, Action, Timestamp) VALUES (@OrderId, @Action, @Timestamp)",
                new { OrderId = orderId, Action = $"Status changed to {newStatus}", Timestamp = DateTime.UtcNow },
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Handling Table Prefixes

Each Orchard Core tenant can have a table prefix. Always prepend the prefix to custom table names:

```csharp
using OrchardCore.Environment.Shell;

public sealed class AdminController : Controller
{
    private readonly string _tablePrefix;

    public AdminController(ShellSettings settings)
    {
        _tablePrefix = settings["TablePrefix"];
    }

    // Use _tablePrefix when referencing custom tables:
    // $"{_tablePrefix}MyCustomTable"
}
```

### Enabling the Data Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Data"
      ],
      "disable": []
    }
  ]
}
```

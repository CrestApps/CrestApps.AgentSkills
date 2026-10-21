# Tenant Hierarchy Reference

## Parent Policy Keys

Set from the platform's **Policy** screen, or in configuration as `OrchardCore:{tenant}:TenantHierarchy:Policy:{key}`.

| Key | Default | Meaning |
|---|---|---|
| `MaxChildren` | `100` | Quota, checked under a distributed lock |
| `ChildHostPattern` | `{business}.{parent host}` | Must start with `{business}.`; no wildcard, list or path |
| `Recipes` | Every setup recipe | Setup recipes a child can start from |
| `DatabaseStrategy` | `SqlitePerChild` | `SqlitePerChild`, `DatabasePerChild`, `SchemaPerChild` or `TablePrefixPerChild` |
| `DatabasePool` | | A pool name from `TenantHierarchy:DatabasePools` |
| `BlockedFeatures` | Empty | More features blocked in the children |
| `DeniedLocalPermissions` | Empty | Permissions the children's own users never get, such as `ManageRecipes` |
| `RequireMfa` | `false` | The parent sign-in must have used two-factor authentication |
| `SessionValidationInterval` | `00:02:00` | How often a child checks a session with the parent |
| `SessionIdleTimeout` / `SessionLifetime` | `00:30:00` / `08:00:00` | When a delegated session ends |
| `SwitcherMode` | `Hosted` | `Hosted` or `Embedded` |
| `Labels:Parent`, `Labels:Child`, `Labels:Children` | Parent tenant, Child tenant, Child tenants | Words the screens use |
| `RemovalGraceDays` | `0` | Days a removed child stays suspended and restorable |

Example for a parent tenant named `firma`:

```json
{
  "OrchardCore": {
    "firma": {
      "TenantHierarchy": {
        "Policy": {
          "MaxChildren": 250,
          "DatabaseStrategy": "DatabasePerChild",
          "DatabasePool": "firms",
          "DeniedLocalPermissions": [ "ManageRecipes" ],
          "RequireMfa": true,
          "Labels": { "Child": "Client", "Children": "Clients" }
        }
      }
    }
  }
}
```

## Shell Settings Keys

`TenantHierarchyConstants.SettingsKeys`: `TenantHierarchy:Role`, `TenantHierarchy:ParentTenantId`, `TenantHierarchy:ParentTenant`, `TenantHierarchy:Slug`, `TenantHierarchy:EntryId`, `TenantHierarchy:DisplayName`, `TenantHierarchy:Policy`, `TenantHierarchy:DatabaseStrategy`, `TenantHierarchy:DatabasePool`, `TenantHierarchy:ProvisionedResource`, `TenantHierarchy:SuspendedWithParent`. Only the platform and the module write them.

## Isolation Between Parents

| Door | What blocks it |
|---|---|
| Code in a parent calls `IShellHost` for another hierarchy | `GuardedShellHost` hides it from `GetAllSettings` and `TryGetSettings`; `GetScopeAsync`, `UpdateShellSettingsAsync`, `ReloadShellContextAsync` and `RemoveShellSettingsAsync` throw. Crafted settings are checked against the host's own copy |
| Broker or services get another parent's id | Registry, grants, codes and sessions live in each parent's database, so foreign ids do not exist; the child's settings must name the caller as parent |
| Admin screens get another parent's child | `/Admin/children/{id}` answers `404` whatever form `{id}` takes; bulk actions skip unknown ids |
| Delegated access to another parent's child | Codes are issued only for children in the caller's registry covered by a grant, bound to one child, its parent and the PKCE verifier |
| Sessions across hierarchies | A child validates and ends sessions only with its own parent |
| Sign-in carried to another tenant | Users, cookies and data protection keys are per tenant; cookies are host-only |
| Outbound requests | The egress guard refuses the host of any tenant before DNS, and internal addresses after it (DNS rebinding safe) |
| Platform screens | Default tenant only |
| Data | `DatabasePerChild` and `SchemaPerChild` give each child its own login; `SqlitePerChild` its own file; `TablePrefixPerChild` shares a login, so `OrchardCore.Queries.Sql` is blocked there |

Also: parent and child cookies get `__Host-`, `Path=/`, no domain and `Secure`; a parent refuses script requests from child pages (`Sec-Fetch-Site: same-site` with a mode other than `navigate`); in Default, requests for hosts of suspended or removed hierarchy tenants answer `404` with "This site is not available".

## Audit Trail Events

The Parent feature records life-cycle, access and session events in the parent's audit trail (category `TenantHierarchy`, correlated by the child's registry entry id). Each stores a `HierarchyAuditEvent` with the child, the user, the session reference, authentication methods, client address and details. **Activity** opens `/Admin/AuditTrail?q=category:TenantHierarchy`; a child's **Manage > Activity** opens `/Admin/AuditTrail/{entryId}`. Events are trimmed and can be turned off under **Settings > Audit Trail** (see `orchardcore-audit-trail`).

## Database Strategies

| Strategy | Provisioner | Creates |
|---|---|---|
| `SqlitePerChild` | Built in | A SQLite file in the child's folder |
| `DatabasePerChild` | SQL Server, PostgreSQL | A database and an owning login (PostgreSQL revokes `CONNECT` from `PUBLIC`) |
| `SchemaPerChild` | SQL Server, PostgreSQL | A schema in the pool database and an owning login |
| `TablePrefixPerChild` | Any provider | A random table prefix in the pool database |

Names are derived from the opaque tenant name (`th_u_...`) and must contain only letters, digits and underscores; generated passwords use letters and digits only. Removal drops the schema, or renames (`RetainRemovedDatabases`) or drops the database, and drops the login.

## Custom Provisioner

Contract (`CrestApps.OrchardCore.TenantHierarchy.Services`, models in `CrestApps.OrchardCore.TenantHierarchy.Models`):

- `bool CanProvision(ChildDatabaseProvisioningContext context)`: whether this provisioner handles the `Strategy` and `Pool` (`DatabasePoolOptions`: `DatabaseProvider`, `ConnectionString`, `RetainRemovedDatabases`).
- `Task<ChildDatabaseProvisioningResult> ProvisionAsync(context, cancellationToken)`: create the resource and return `DatabaseProvider`, `ConnectionString`, `TablePrefix`, `Schema`, `DatabaseName` and `ProvisionedResource`, or `ChildDatabaseProvisioningResult.Failed(error)`.
- `Task DeprovisionAsync(context, cancellationToken)`: remove `context.ProvisionedResource` after Orchard Core has dropped the tenant tables.

```csharp
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using MySqlConnector;

namespace MyCompany.Hosting;

public sealed partial class MySqlChildDatabaseProvisioner : IChildTenantDatabaseProvisioner
{
    public bool CanProvision(ChildDatabaseProvisioningContext context)
        => context.Strategy == ChildDatabaseStrategy.DatabasePerChild
        && string.Equals(context.Pool?.DatabaseProvider, "MySql", StringComparison.OrdinalIgnoreCase);

    public async Task<ChildDatabaseProvisioningResult> ProvisionAsync(
        ChildDatabaseProvisioningContext context,
        CancellationToken cancellationToken = default)
    {
        var name = "th_" + context.TenantName;

        if (!SafeName().IsMatch(name))
        {
            return ChildDatabaseProvisioningResult.Failed("The tenant name cannot be used as a database name.");
        }

        var password = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789", 32);

        await using var connection = new MySqlConnection(context.Pool.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var sql in new[]
        {
            $"CREATE DATABASE `{name}`;",
            $"CREATE USER '{name}'@'%' IDENTIFIED BY '{password}';",
            $"GRANT ALL PRIVILEGES ON `{name}`.* TO '{name}'@'%';",
        })
        {
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new MySqlConnectionStringBuilder(context.Pool.ConnectionString)
        {
            Database = name,
            UserID = name,
            Password = password,
        };

        return new ChildDatabaseProvisioningResult
        {
            DatabaseProvider = "MySql",
            ConnectionString = builder.ConnectionString,
            DatabaseName = name,
            ProvisionedResource = name,
        };
    }

    public async Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
    {
        var name = context.ProvisionedResource;

        if (string.IsNullOrEmpty(name) || !SafeName().IsMatch(name))
        {
            return;
        }

        await using var connection = new MySqlConnection(context.Pool.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var statements = context.Pool.RetainRemovedDatabases
            ? new[] { $"DROP USER IF EXISTS '{name}'@'%';" }
            : new[] { $"DROP DATABASE IF EXISTS `{name}`;", $"DROP USER IF EXISTS '{name}'@'%';" };

        foreach (var sql in statements)
        {
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex SafeName();
}
```

MySQL has no database rename, so this sample keeps a retained database under its own name and drops only the login. Install `MySqlConnector` in the web/startup project and register the provisioner after `AddTenantHierarchy()`:

```csharp
builder.Services
    .AddOrchardCms(orchardCore => orchardCore.AddTenantHierarchy());

builder.Services.AddSingleton<IChildTenantDatabaseProvisioner, MySqlChildDatabaseProvisioner>();
```

The CrestApps.OrchardCore repository has a detailed build guide with the naming, credential and removal rules and the tests to write, at `.agents/skills/crestapps-tenant-database-provisioner`.

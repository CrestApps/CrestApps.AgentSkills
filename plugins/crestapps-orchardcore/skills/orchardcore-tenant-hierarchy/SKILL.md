---
name: orchardcore-tenant-hierarchy
description: Skill for the CrestApps Orchard Core Tenant Hierarchy module, where the Default tenant makes tenants parents that create, manage and open their own child tenants. Covers the Platform, Parent and Child features, the AddTenantHierarchy host guards in Program.cs, the TenantHierarchy configuration section, parent policies, delegated access with a one-time code and PKCE, linked users, access grants, the tenant switcher, isolation guards such as GuardedShellHost and the egress guard, audit trail activity, database strategies and custom IChildTenantDatabaseProvisioner implementations. Use this skill when requests mention Orchard Core tenant hierarchy, parent and child tenants, agency multi-tenancy, entering a child tenant without signing in, or a database per tenant. Strong matches include CrestApps.OrchardCore.TenantHierarchy, ITenantHierarchyBroker, TenantHierarchyAccessDeniedException, ParentTenantPolicy, ChildDatabaseStrategy, DatabasePoolOptions, ManageTenantHierarchy and EnterChildTenants.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Tenant Hierarchy - Prompt Templates

## Parent Tenants with Their Own Child Tenants

You are an Orchard Core expert. Generate accurate setup, configuration, extension and troubleshooting guidance for the CrestApps Tenant Hierarchy module. A tenant the platform marks as a **parent** creates, manages and opens its own **child** tenants (for example a bookkeeping firm with one site per client business). The Default tenant stays the platform root and still manages every tenant through Orchard Core's Tenants admin.

### Guidelines

- Install `CrestApps.OrchardCore.TenantHierarchy` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Call `AddTenantHierarchy()` on the Orchard Core builder in `Program.cs`, after Orchard Core is added. Without it the platform refuses to make parents and a parent refuses to create children.
- Configuration is host configuration in the `TenantHierarchy` section of the application configuration. Tenant configuration cannot change it.
- Give every tenant its own host name, never a URL prefix, and serve every tenant over HTTPS so the `__Host-` cookies work.
- Code in one tenant of a hierarchy must reach another only through `ITenantHierarchyBroker`. Direct `IShellHost` calls across tenants throw `TenantHierarchyAccessDeniedException`, and HTTP calls between tenants are refused by the egress guard.
- The guards do not stop malicious server code: all tenants share one process, so install only trusted modules.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | Feature ID | Where it runs |
|---|---|---|
| Tenant Hierarchy Platform | `CrestApps.OrchardCore.TenantHierarchy.Platform` | Default tenant only (`DefaultTenantOnly`). Depends on `OrchardCore.Tenants` and `CrestApps.OrchardCore.Resources` |
| Parent Tenant | `CrestApps.OrchardCore.TenantHierarchy.Parent` | A parent, forced on when the platform makes it one. Depends on `OrchardCore.Users`, `OrchardCore.Roles`, `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.Users` (user picker) and `OrchardCore.AuditTrail` |
| Child Tenant | `CrestApps.OrchardCore.TenantHierarchy.Child` | Every child, forced on through the tenant `Features` configuration after setup. Depends on `OrchardCore.Users` and `OrchardCore.Roles` |

The Parent and Child features never depend on the Platform feature or `OrchardCore.Tenants`, because a dependency on a Default-only feature would make them unavailable elsewhere. See `orchardcore-tenants` for Orchard Core multi-tenancy and `orchardcore-crestapps-users` for the user picker.

### Install the Host Guards

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms(orchardCore => orchardCore
        .AddTenantHierarchy());

var app = builder.Build();

app.UseOrchardCore();

app.Run();
```

`AddTenantHierarchy()` (namespace `Microsoft.Extensions.DependencyInjection`, in `CrestApps.OrchardCore.TenantHierarchy.Core`) binds `TenantHierarchyOptions`, replaces `IShellHost` with `GuardedShellHost`, registers the built-in database provisioners, the parent removal guards, and per-tenant feature validation, egress guard and `__Host-` cookie setup, and adds the Fetch Metadata and unavailable-address middlewares. Each guard does nothing for a tenant that is neither a parent nor a child.

## Configuration

```json
{
  "TenantHierarchy": {
    "PlatformDomain": "platform.com",
    "Scheme": "https",
    "UseHostPrefixedCookies": true,
    "ReservedSlugs": [ "billing" ],
    "Egress": {
      "Enabled": true,
      "BlockPrivateNetworks": true,
      "AllowedHosts": [ "ollama.internal" ]
    },
    "DatabasePools": {
      "firms": {
        "DatabaseProvider": "Postgres",
        "ConnectionString": "Host=db;Username=provisioner;Password=...",
        "RetainRemovedDatabases": true
      }
    }
  }
}
```

| Key | Default | Meaning |
|---|---|---|
| `PlatformDomain` | Host of the request to Default | Parents are `{slug}.{PlatformDomain}`; may carry a port such as `localhost:5000` |
| `Scheme` | `https` (request scheme in development) | Scheme of addresses the module builds |
| `UseHostPrefixedCookies` | On, except in development | `__Host-` prefix for parent and child cookies; needs HTTPS |
| `ReservedSlugs` | Empty | Extra slugs no tenant may use (built in: `admin`, `www`, `api`, `mail`, `login` and others) |
| `Egress:Enabled` / `BlockPrivateNetworks` / `AllowedHosts` | `true` / `true` / empty | Outbound `HttpClient` guard |
| `DatabasePools:{name}:DatabaseProvider` | | `SqlConnection`, `Postgres`, `MySql` or `Sqlite` |
| `DatabasePools:{name}:ConnectionString` | | A login that can create databases, schemas and logins |
| `DatabasePools:{name}:RetainRemovedDatabases` | `true` | Rename a removed child database to `{name}_removed_{timestamp}` instead of dropping it |

Environment variables use `__`, for example `TenantHierarchy__PlatformDomain=platform.com`.

The parent policy (`ParentTenantPolicy`) is written into the parent's shell settings from the platform's **Policy** screen, or set as `OrchardCore:{tenant}:TenantHierarchy:Policy:{key}`. Keys and defaults are in [references/tenant-hierarchy-reference.md](references/tenant-hierarchy-reference.md).

## How It Works

- **Where facts live**: `TenantHierarchy:Role` (`Parent` or `Child`), the policy and a child's `TenantHierarchy:ParentTenantId` are shell settings (tenant admins cannot edit them). The child registry, access grants, one-time codes, delegated sessions and favorites are in the parent's database, collection `TenantHierarchy`. Activity is in the parent's audit trail, category `TenantHierarchy`. User links are in the child's database.
- **Naming**: a child has an opaque tenant name such as `u_7k2m9q4x1c`; its display name goes into the tenant `Description` and the parent's into `Category`.
- **Broker**: `ITenantHierarchyBroker` is the only path between tenants of a hierarchy. Requests carry an entry id from the parent's own registry, never a tenant name, and the link must hold both ways.
- **Scope guard**: `GuardedShellHost` lets a parent read its children and change them only inside a broker call; a child reaches its parent only inside a broker call; ordinary tenants cannot reach hierarchy tenants. Every tenant may reach Default.
- **Delegated access**: opening a child follows the OAuth 2.0 authorization code flow with PKCE, redeemed in process. Routes are `delegated-access/open/{entryId}`, `authorize`, `enter` and `callback`, accept only top-level navigations, and send `Cache-Control: no-store`. The code has 256 random bits, is stored hashed, lives 60 seconds and is burned by the first attempt.
- **Linked users**: on first entry the child creates a passwordless user named `{parent user name}+{parent slug}` with exactly the roles its grants give. Child administrators cannot sign in as, edit, delete, re-role or set a password on a linked user.
- **Sessions**: the child validates the session with the parent at `SessionValidationInterval` and ends it when idle, too old, the parent user is disabled or signs out, changes their security stamp, or loses the grant.
- **Switcher**: users who entered through delegated access get a navbar switcher. The list of other children is always served by the parent (`Hosted` links to the parent's picker, `Embedded` frames it with `frame-ancestors` for that child only).
- **Life cycle**: create validates the slug, checks the quota under a lock, places the database through a provisioner, writes shell settings and the registry entry, then runs setup after the request with a bootstrap admin that is disabled right after. Suspend, resume, reload, remove (with an optional restore period) and feature changes go through the broker. The **Tenant Hierarchy Maintenance** background task removes children whose restore period ended.
- **Removal guard**: a parent with children cannot be removed, even from Default's Tenants admin.

## Feature Guard

| Feature | Rule |
|---|---|
| Parent Tenant | Available only in a parent |
| Child Tenant | Never offered; forced on through configuration |
| Tenant Hierarchy Platform | Default only |
| `OrchardCore.OpenId.Validation` | Blocked in parents and children |
| `OrchardCore.Deployment.Remote` | Blocked in children |
| `OrchardCore.Queries.Sql` | Blocked in children sharing a database (schema or table prefix) |
| Policy `BlockedFeatures` | Blocked in that parent's children |

The guard filters what can be enabled; it does not turn off a feature already on (the parent screen lists such features). Setup recipe steps that enable a blocked feature are ignored.

## Permissions

| Permission | Allows |
|---|---|
| `ManageTenantHierarchy` | Platform screens (Default tenant) |
| `ViewChildTenants` | Child tenant list; implied by the next five |
| `CreateChildTenants` | Create, retry and discard children |
| `ManageChildTenants` | Edit, suspend, resume and reload |
| `ManageChildFeatures` | Enable and disable features in a child |
| `RemoveChildTenants` | Remove and restore |
| `ManageChildAccess` | Access grants |
| `EnterChildTenants` | Base gate for opening a child; a grant is still needed |

When a tenant becomes a parent, a parent-wide grant gives the parent's **Administrator** role the child role **Administrator** in every child. Activity needs the audit trail's `ViewAuditTrail` (`/Admin/AuditTrail?q=category:TenantHierarchy`).

## Custom Database Provisioner

Built-in strategies (`ChildDatabaseStrategy`): `SqlitePerChild` (default), `DatabasePerChild` and `SchemaPerChild` (SQL Server, PostgreSQL), `TablePrefixPerChild` (any provider). To support another server, implement `IChildTenantDatabaseProvisioner` and register it as a host singleton after `AddTenantHierarchy()`. The first provisioner whose `CanProvision` returns `true` wins, and built-ins are registered first, so claim only what they do not handle:

```csharp
builder.Services.AddSingleton<IChildTenantDatabaseProvisioner, MySqlChildDatabaseProvisioner>();
```

A full sample is in [references/tenant-hierarchy-reference.md](references/tenant-hierarchy-reference.md).

## Deployment Checklist

- Host names: `{firm}.platform.com` for parents and `{business}.{firm}.platform.com` for children, with wildcard DNS and certificates for `*.platform.com` and each `*.{firm}.platform.com`.
- Give the Default tenant its own host name such as `platform.com`.
- Register the platform domain in the private section of the Public Suffix List.
- With several nodes, use `OrchardCore.Tenants.Distributed`, a shared shell settings store and a shared data protection key ring.
- Background tasks run only for tenants that have served a request since the process started.

Not included: federated OpenID Connect for children in another application, custom domains for children, inviting the business owner at creation, idle release of inactive children.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Screens say **The host guards are not installed** | Call `AddTenantHierarchy()` in `Program.cs` and restart |
| Outbound request fails with *it is the address of a tenant of this application* | Tenants of a hierarchy do not call each other over HTTP. Use `ITenantHierarchyBroker` |
| Outbound request to an internal address fails | Add the host to `Egress:AllowedHosts` |
| Sign-in loops back to the sign-in page | `__Host-` cookies need HTTPS. Turn `UseHostPrefixedCookies` off only on a test machine |
| `TenantHierarchyAccessDeniedException` logged | Code reached another tenant outside the broker; the log names both tenants |
| Child setup fails | Read the application log. The pool login must create databases, schemas and logins. Fix it, then **Retry** |
| Users signed out on another node | Share the data protection key ring and shell settings |
| A suspended child's address shows the platform sign-in | The app runs without `AddTenantHierarchy()`, so the unavailable-address guard is missing |

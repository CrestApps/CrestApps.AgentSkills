---
name: orchardcore-signalr
description: Skill for SignalR in CrestApps Orchard Core 3.0, where hosting moved to the Orchard Core framework. Covers migrating from the deprecated CrestApps.OrchardCore.SignalR, CrestApps.OrchardCore.SignalR.Redis and CrestApps.OrchardCore.SignalR.Azure compatibility features to OrchardCore.SignalR, OrchardCore.SignalR.Redis and OrchardCore.SignalR.Azure, mapping hubs with SignalRHubRoutes.GetHubPath, tenant-aware client URLs with Html.SignalRHubUrl, tenant-isolated user and group destinations with TenantSignalRGroupName, the HubConnectionWork cancellation convention, the signalr script resource, and Redis or Azure SignalR backplanes. Use this skill when requests mention Orchard Core SignalR, CrestApps SignalR, tenant-aware hubs, HubRouteManager removal, real-time notifications, SignalR backplanes, or hub URLs under a tenant prefix. Strong matches include OrchardCore.SignalR, SignalRHubRoutes, SignalRHubUrl, TenantSignalRGroupName, HubConnectionWork, SignalRConstants, MapHub, and HubOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SignalR - Prompt Templates

## Build Tenant-Aware SignalR Features

You are an Orchard Core expert. Generate accurate hub, route, resource, and client code for SignalR in CrestApps Orchard Core 3.0. SignalR hosting now lives in the Orchard Core framework (`OrchardCore.SignalR`); CrestApps contributes only routing and tenant-isolation helpers that every CrestApps hub follows.

### Guidelines

- Enable `OrchardCore.SignalR` for new work. The CrestApps SignalR features are deprecated compatibility shims.
- CrestApps modules that use SignalR (AI Chat, Chat Interactions, Contact Center, Omnichannel Messaging, Telephony, Reports) depend on `OrchardCore.SignalR` directly.
- Map hubs with `routes.MapHub<T>(SignalRHubRoutes.GetHubPath<T>())`. `HubRouteManager` was removed in 3.0.
- Build browser URLs with `Html.SignalRHubUrl<T>()` (adds the request path base), never a hard-coded path or tenant prefix.
- Use the framework `signalr` script resource as a dependency instead of copying the client library.
- Send per-user or per-group messages to `TenantSignalRGroupName` destinations, never to a bare `Clients.User(userId)` or an unqualified group name.
- Pass `HubConnectionWork.MustComplete` to hub work that changes durable state or group membership.
- Enable at most one backplane per tenant. `OrchardCore.SignalR.Azure` and `OrchardCore.SignalR.Redis` cannot be enabled together.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Item | Value |
|---|---|
| Hosting feature | `OrchardCore.SignalR` (package `OrchardCore.SignalR`) |
| Redis backplane | `OrchardCore.SignalR.Redis` (depends on `OrchardCore.Redis`) |
| Azure backplane | `OrchardCore.SignalR.Azure` (Azure SignalR Service) |
| Deprecated compatibility features | `CrestApps.OrchardCore.SignalR`, `CrestApps.OrchardCore.SignalR.Redis`, `CrestApps.OrchardCore.SignalR.Azure` |
| Route and URL helpers | `SignalRHubRoutes`, `SignalRHtmlHelperExtensions` in `CrestApps.OrchardCore.Core` (namespace `CrestApps.OrchardCore`) |
| Tenant destinations | `TenantSignalRGroupName`, `HubConnectionWork` in `CrestApps.OrchardCore.SignalR.Core` |
| Hub path convention | `/Communication/Hub/{HubTypeName}` |
| Script resource | `signalr` (registered by `OrchardCore.SignalR`, `@microsoft/signalr` 10.0.0) |
| JSON protocol | camel case, with Orchard `JOptions.KnownConverters` |

### Enable SignalR

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.SignalR"
      ],
      "disable": []
    }
  ]
}
```

## Define and Map a Hub

Reference `CrestApps.OrchardCore.Core` for the routing helpers. Install third-party module packages in the web/startup project.

```csharp
using CrestApps.OrchardCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Modules;

namespace MyCompany.OrchardCore.Live;

public sealed class NotificationsHub : Hub
{
    public Task PingAsync()
    {
        return Clients.Caller.SendAsync("pong");
    }
}

public sealed class Startup : StartupBase
{
    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider)
    {
        routes.MapHub<NotificationsHub>(SignalRHubRoutes.GetHubPath<NotificationsHub>());
    }
}
```

Declare `OrchardCore.SignalR` as a dependency of the feature that maps the hub so the SignalR services are present.

Configure a specific hub only when it needs non-default timing:

```csharp
services.Configure<HubOptions<NotificationsHub>>(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

## Connect from a View

```cshtml
@using CrestApps.OrchardCore
@{
    var hubUrl = Html.SignalRHubUrl<NotificationsHub>();
}

<script at="Foot" depends-on="signalr">
    document.addEventListener("DOMContentLoaded", () => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("@hubUrl")
            .withAutomaticReconnect()
            .build();

        connection.on("pong", () => console.log("Connected."));
        connection.start().catch(error => console.error("SignalR connection failed.", error));
    });
</script>
```

Outside a view, call `SignalRHubRoutes.GetTenantAwareHubUrl<T>(httpContext)`. Built scripts are bundled per file in 3.0, so a script that exposes an API must assign it to `window`.

## Tenant-Isolated Destinations

A backplane is shared infrastructure while user ids and group names are tenant-local. Qualify every destination with the shell name:

```csharp
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

public sealed class AlertsHub : Hub
{
    private readonly ShellSettings _shellSettings;

    public AlertsHub(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();

            return;
        }

        // Group membership is durable state, so never abandon it half-way on ConnectionAborted.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            TenantSignalRGroupName.ForUser(_shellSettings.Name, userId),
            HubConnectionWork.MustComplete);

        await base.OnConnectedAsync();
    }
}
```

Publish from a service through `IHubContext<T>` to the same name:

```csharp
await hubContext.Clients
    .Group(TenantSignalRGroupName.ForUser(shellSettings.Name, userId))
    .SendAsync("notify", payload);
```

- `ForUser(tenantName, userId)` and `ForGroup(tenantName, logicalGroupName)` build length-prefixed names (`tenant:{len}:{tenant}:user:{len}:{id}`), so equal ids in different shells never collide and a crafted name cannot spoof another tenant.
- Add only authorized connections to a group. Do not trust caller-supplied tenant, user, or group names.
- Work that only returns a value to the caller may honour `Context.ConnectionAborted`; work that changes durable state or group membership uses `HubConnectionWork.MustComplete`.

## Scale-Out

| Backplane | Feature | Configuration |
|---|---|---|
| Redis | `OrchardCore.SignalR.Redis` | Uses Orchard Core Redis (`OrchardCore.Redis`) options; the channel prefix is qualified with the Redis instance prefix and the shell name. |
| Azure SignalR Service | `OrchardCore.SignalR.Azure` | Shell configuration `SignalR:Azure:ConnectionString` (in `appsettings.json` under `OrchardCore`); the application name is derived per tenant. A warning is logged when the connection string is missing. |

```json
{
  "OrchardCore": {
    "SignalR": {
      "Azure": {
        "ConnectionString": "Endpoint=https://contoso.service.signalr.net;AccessKey=...;Version=1.0;"
      }
    }
  }
}
```

The Contact Center topology check still looks for the deprecated `CrestApps.OrchardCore.SignalR.Redis` feature ID when a topology profile (for example `single-node-distributed`) requires a Redis backplane. Enable that compatibility feature on those sites; it turns on `OrchardCore.SignalR.Redis`. See `orchardcore-contact-center`.

## Upgrading from 2.x

- Keep the deprecated `CrestApps.OrchardCore.SignalR` module installed until the upgrade runs. Its features now depend on `OrchardCore.SignalR`, `OrchardCore.SignalR.Redis`, or `OrchardCore.SignalR.Azure`, so the matching framework feature is enabled automatically. Afterwards remove the CrestApps package and keep the framework features, or enable the framework features yourself and remove the package immediately.
- Replace `CrestApps.OrchardCore.SignalR.Redis` with `OrchardCore.SignalR.Redis` in recipes (except where the Contact Center topology check needs it).
- Replace `HubRouteManager.MapHub<T>(routes)` with `routes.MapHub<T>(SignalRHubRoutes.GetHubPath<T>())`.
- Replace `HubRouteManager.GetUriByHub<T>(HttpContext)` with `Html.SignalRHubUrl<T>()` or `SignalRHubRoutes.GetTenantAwareHubUrl<T>(HttpContext)`.
- Replace any hand-built tenant group names with `TenantSignalRGroupName.ForUser` or `ForGroup`; the encoding changed to length-prefixed names.

## Common Failures

| Symptom | Check |
|---|---|
| 404 on hub negotiation | Enable `OrchardCore.SignalR` and map the hub with `SignalRHubRoutes.GetHubPath<T>()` |
| URL misses the tenant prefix | Generate it with `Html.SignalRHubUrl<T>()` |
| `signalR` is undefined | Add `depends-on="signalr"` and render footer scripts |
| Payload casing differs | The JSON protocol is camel case |
| Messages reach users of another tenant | Use `TenantSignalRGroupName` destinations instead of `Clients.User` |
| Agent connected but receives no queue events | Group membership was cancelled half-way; use `HubConnectionWork.MustComplete` |
| Tenant fails to start with both backplanes | Enable only one of `OrchardCore.SignalR.Redis` and `OrchardCore.SignalR.Azure` |
| Multi-node clients miss messages | Enable a backplane and configure Redis or Azure SignalR |

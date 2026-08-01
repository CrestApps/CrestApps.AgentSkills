---
name: orchardcore-signalr
description: Skill for tenant-aware SignalR integration in CrestApps Orchard Core. Covers hub registration, HubRouteManager URLs, SignalR JSON settings, resource registration, typed hubs, and client connections. Use this skill when requests mention Orchard Core SignalR, tenant-aware hubs, HubRouteManager, real-time notifications, or SignalR JavaScript resources. Strong matches include work with CrestApps.OrchardCore.SignalR, HubRouteManager, AddSignalR, ResourceManagementOptionsConfiguration, JOptions.KnownConverters, and HubOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SignalR - Prompt Templates

## Build Tenant-Aware SignalR Features

You are an Orchard Core expert. Generate accurate real-time hub, route, resource, and client code for the CrestApps SignalR module. It configures SignalR for the Orchard tenant and provides `HubRouteManager` so hubs and generated URLs respect the shell request prefix and site base URL.

### Guidelines

- Install `CrestApps.OrchardCore.SignalR` in the web/startup project.
- Enable `CrestApps.OrchardCore.SignalR`.
- Map every module hub through `HubRouteManager.MapHub<T>` rather than `MapHub<T>` with a hard-coded path.
- Generate browser URLs with `HubRouteManager.GetUriByHub<T>(HttpContext)`.
- Use the registered `signalr` resource as a script dependency instead of copying the client library into a theme.
- Keep hub authorization explicit with normal ASP.NET Core or Orchard Core authorization patterns.
- Configure `HubOptions<T>` for a specific hub when its work requires non-default connection timing.
- The module configures camel-case SignalR JSON and registers `JOptions.KnownConverters`.
- This module does not add a Redis backplane feature or configure a distributed backplane itself.
- Configure any backplane at the hosting application level only after verifying the deployed SignalR package and topology.
- Do not hard-code tenant prefixes, host names, or base URLs in hub JavaScript.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Item | Value |
|---|---|
| Package | `CrestApps.OrchardCore.SignalR` |
| Feature ID | `CrestApps.OrchardCore.SignalR` |
| Route service | `HubRouteManager` |
| Script resource | `signalr` |
| JSON naming | camel case |
| Hub dependency | `Microsoft.AspNetCore.SignalR.Core` for modules that define hubs |

### Enable SignalR

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.SignalR"
      ],
      "disable": []
    }
  ]
}
```

## What the Module Registers

The module registers:

- `HubRouteManager` as a scoped service using `ShellSettings.RequestUrlPrefix` and the current site `BaseUrl`.
- ASP.NET Core SignalR through `AddSignalR()`.
- SignalR JSON protocol configuration with `JsonNamingPolicy.CamelCase`.
- Orchard JSON converters from `JOptions.KnownConverters`.
- `ResourceManagementOptionsConfiguration`, which registers the `signalr` script resource.

The resource has local debug and minified URLs under the module and a CDN definition for `@microsoft/signalr@10.0.0`. Use the resource manager rather than picking a URL manually.

## Define and Map a Hub

Install `Microsoft.AspNetCore.SignalR.Core` in the web/startup project or module that defines the hub. Keep hub behavior in the consuming module; the CrestApps SignalR module provides the host integration.

```csharp
using CrestApps.Core.SignalR.Services;
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
        HubRouteManager.MapHub<NotificationsHub>(routes);
    }
}
```

`HubRouteManager.MapHub<T>` uses the hub type to establish its route. Avoid declaring the same hub with an additional hard-coded route because tenant URL prefixes and generated links can then diverge.

## Configure Hub Options

Configure a specific hub if it has long-running operations or distinct connection requirements:

```csharp
using Microsoft.AspNetCore.SignalR;

services.Configure<HubOptions<NotificationsHub>>(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

Choose values based on expected network and workload behavior. Do not use excessively long timeouts to mask failing hub methods or blocked background work.

## Generate a Tenant-Aware URL

Inject `HubRouteManager` into a Razor view or service:

```cshtml
@inject CrestApps.Core.SignalR.Services.HubRouteManager HubRouteManager

@{
    var hubUrl = HubRouteManager.GetUriByHub<NotificationsHub>(ViewContext.HttpContext);
}
```

The generated URL incorporates the shell request prefix and tenant base URL. This is essential for a tenant mounted below a path, reverse proxies, and non-root deployments.

## Connect from JavaScript

Declare the resource dependency and use the generated URL:

```cshtml
<script at="Foot" depends-on="signalr">
    document.addEventListener("DOMContentLoaded", () => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("@hubUrl")
            .build();

        connection.on("pong", () => console.log("Connected to notifications."));

        connection.start().catch(error => {
            console.error("SignalR connection failed.", error);
        });
    });
</script>
```

Method and payload names are camel case on the wire because the module configures camel-case JSON. Keep client handlers aligned with the names actually sent by the hub.

## Typed Hubs and Services

Use `Hub<TClient>` when the application benefits from compile-time checking of client contracts:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace MyCompany.OrchardCore.Live;

public interface INotificationsClient
{
    Task ReceiveNotification(string message);
}

public sealed class TypedNotificationsHub : Hub<INotificationsClient>
{
    public Task NotifyCallerAsync(string message)
    {
        return Clients.Caller.ReceiveNotification(message);
    }
}
```

Authorize hub methods and groups according to tenant permissions. Do not trust a caller-supplied tenant id, user id, or group name to decide authorization.

## Multi-Tenant Behavior

`HubRouteManager` is the tenancy-sensitive integration point. It is scoped because it uses the active shell settings and site settings. Resolve it in the active request or shell scope; do not cache it in a singleton.

Each hub method runs under normal tenant service resolution. Use scoped services for tenant data, and do not retain a `HubCallerContext` or scoped service after a hub call completes.

## Scale-Out and Redis

The module source registers SignalR, route management, JSON configuration, and the browser resource only. It contains no `AddStackExchangeRedis`, Azure SignalR, or other backplane registration.

For multiple application instances, determine the required hosting-level SignalR scale-out design separately. Add a provider only in the startup host after installing and configuring the corresponding Microsoft package. Preserve `HubRouteManager` for routing regardless of the backplane choice.

## Common Failures

| Symptom | Check |
|---|---|
| 404 on hub negotiation | Enable the SignalR feature and map the hub with `HubRouteManager.MapHub<T>` |
| URL misses tenant prefix | Generate it through `GetUriByHub<T>(HttpContext)` |
| `signalR` is undefined | Add `depends-on="signalr"` and ensure the resource manager renders footer scripts |
| Payload casing differs | Account for the configured camel-case JSON protocol |
| Hub services resolve from the wrong tenant | Avoid singleton caching and resolve services in the active shell scope |
| Multi-node clients miss messages | Configure a host-level scale-out provider; this module does not add one |

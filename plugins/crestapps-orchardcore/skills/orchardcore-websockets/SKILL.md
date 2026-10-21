---
name: orchardcore-websockets
description: Skill for the CrestApps Orchard Core WebSockets infrastructure module. Covers the dependency-only CrestApps.OrchardCore.WebSockets feature that adds the ASP.NET Core WebSocket middleware to the tenant pipeline, the CrestApps WebSockets configuration section bound to WebSocketOptions (KeepAliveInterval, KeepAliveTimeout, AllowedOrigins), hosting raw WebSocket endpoints, the IWebSocketConnectionRegistry and WebSocketRendezvous contracts for provider callbacks that dial back, in-memory and Redis-backed registries, and host affinity across nodes. Use this skill when requests mention raw WebSockets in Orchard Core, refused WebSocket upgrades, UseWebSockets per tenant, provider media streams that connect back such as Telnyx, WebSocket keep-alive or allowed origins, or callbacks landing on the wrong node. Strong matches include CrestApps.OrchardCore.WebSockets, WebSocketsConstants, IWebSocketConnectionRegistry, WebSocketRendezvous, DistributedWebSocketConnectionRegistry, and WebSocketOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core WebSockets - Prompt Templates

## Host Raw WebSocket Endpoints

You are an Orchard Core expert. Generate accurate code and configuration for features that accept raw WebSocket upgrades (`HttpContext.WebSockets.AcceptWebSocketAsync()`) in a CrestApps Orchard Core tenant.

### Guidelines

- The Orchard Core host does not run `app.UseWebSockets()`. Without it a raw upgrade is refused. SignalR does not need it (see `orchardcore-signalr`).
- `CrestApps.OrchardCore.WebSockets` is the single place the middleware is added. It is `EnabledByDependencyOnly`, so it never shows as a toggle; declare it as a dependency of your feature instead of enabling it in a recipe.
- Depend on the contracts in `CrestApps.OrchardCore.WebSockets.Abstractions` (`IWebSocketConnectionRegistry`, `WebSocketRendezvous`), not on the module's host wiring.
- Use unguessable, single-use keys for every rendezvous, and remove the key when the starter times out.
- A live socket cannot move between nodes. Route provider callbacks back to the node that started them.
- Install third-party module packages in the web/startup project.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Feature Overview

| Item | Value |
|---|---|
| Module package | `CrestApps.OrchardCore.WebSockets` (category Infrastructure) |
| Feature ID | `CrestApps.OrchardCore.WebSockets` (`WebSocketsConstants.Feature.Area`) |
| Enabled by | Dependency only (for example the `CrestApps.OrchardCore.Telnyx` feature) |
| Contracts package | `CrestApps.OrchardCore.WebSockets.Abstractions` (namespace `CrestApps.OrchardCore.WebSockets`) |
| Configuration section | `CrestApps:WebSockets` (`WebSocketsConstants.ConfigurationSectionPath`), bound to ASP.NET Core `WebSocketOptions` |
| Default registry | `InMemoryWebSocketConnectionRegistry` (per node) |
| Distributed registry | `DistributedWebSocketConnectionRegistry`, active when `OrchardCore.Redis` is enabled |

## Depend on the Feature

```csharp
using CrestApps.OrchardCore.WebSockets;
using OrchardCore.Modules.Manifest;

[assembly: Feature(
    Id = "MyCompany.MediaBridge",
    Name = "Media Bridge",
    Category = "Telephony",
    Dependencies =
    [
        WebSocketsConstants.Feature.Area,
    ]
)]
```

## Configure the Middleware

Every key is optional; omitted keys keep the ASP.NET Core defaults (two-minute keep-alive, no origin restriction).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "WebSockets": {
        "KeepAliveInterval": "00:00:30",
        "KeepAliveTimeout": "00:00:10",
        "AllowedOrigins": [ "https://app.example.com" ]
      }
    }
  }
}
```

| Key | Meaning |
|---|---|
| `KeepAliveInterval` | Interval for server ping frames on an idle socket. |
| `KeepAliveTimeout` | Time to wait for a pong before aborting. Omit to disable. |
| `AllowedOrigins` | Accepted `Origin` header values. Empty accepts all. Only browser handshakes send `Origin`, so this does not restrict server-to-server provider callbacks. |

Environment variables use `OrchardCore__CrestApps__WebSockets__KeepAliveInterval`. A tenant's own `App_Data/Sites/{TenantName}/appsettings.json` starts at `CrestApps` without the `OrchardCore` wrapper. The options are read once when the pipeline is built, so a change needs a tenant reload.

## Correlate a Provider Callback

A provider (for example Telnyx media streaming) is asked to dial back to a WebSocket endpoint. The request that asked waits for that socket; the endpoint that accepts it hands it over through the registry.

```csharp
using System.Security.Cryptography;
using CrestApps.OrchardCore.WebSockets;
using Microsoft.AspNetCore.WebUtilities;

public sealed class MediaBridgeStarter
{
    private readonly IWebSocketConnectionRegistry _registry;

    public MediaBridgeStarter(IWebSocketConnectionRegistry registry)
    {
        _registry = registry;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var rendezvous = await _registry.RegisterAsync(key, cancellationToken);

        try
        {
            // Ask the provider to connect to /api/my-bridge/media?key={key} here.
            var socket = await rendezvous.ConnectedTask.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);

            try
            {
                // Read and write media frames on the socket.
            }
            finally
            {
                rendezvous.Release();
            }
        }
        catch
        {
            rendezvous.Abort();
            await _registry.RemoveAsync(key, CancellationToken.None);

            throw;
        }
    }
}
```

The endpoint the provider dials:

```csharp
routes.Map("/api/my-bridge/media", async (HttpContext httpContext, IWebSocketConnectionRegistry registry) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var rendezvous = await registry.TryClaimAsync(httpContext.Request.Query["key"], httpContext.RequestAborted);

    if (rendezvous is null)
    {
        // Unknown, already claimed, expired, or registered on another node.
        return Results.NotFound();
    }

    var socket = await httpContext.WebSockets.AcceptWebSocketAsync();

    rendezvous.TryComplete(socket);

    // Keep the request, and so the socket, alive until the consumer releases it.
    await rendezvous.ReleasedTask;

    return Results.Empty;
});
```

`RegisterAsync` throws on a duplicate key. `TryClaimAsync` atomically claims and removes the rendezvous, so a key works once.

## Multi-Node Deployments

| Registry | When | Behavior |
|---|---|---|
| In-memory | Default | A key is known only on the node that registered it. A callback on another node looks like an unknown key. |
| Distributed | `OrchardCore.Redis` enabled for the tenant | The rendezvous still lives on the registering node, which records ownership of the key in Redis for 10 minutes. A misrouted callback is still refused, but the log names the owning node and the rendezvous stays intact for the provider's retry. If Redis is unreachable, registration falls back to node-local behavior. |

- Single node works out of the box.
- On several nodes, give each node its own public base URL (host affinity) so the provider calls back the node that started the flow, and enable `OrchardCore.Redis` so misrouting shows in the log.
- `IWebSocketConnectionRegistry` is replaceable; register another implementation with `services.Replace(...)` to add a relay.

## Common Failures

| Symptom | Check |
|---|---|
| Upgrade refused or the request is not a WebSocket request | Your feature does not depend on `CrestApps.OrchardCore.WebSockets`, so `UseWebSockets` is not in the pipeline |
| Callback is refused with an unknown key | The callback reached another node, the key expired, or it was already claimed |
| Browser handshake rejected | The page origin is missing from `AllowedOrigins` |
| Idle sockets drop behind a proxy | Lower `KeepAliveInterval` below the proxy idle timeout |

## Related Skills

- `orchardcore-telephony-telnyx` for the Telnyx media stream endpoint that uses this feature.
- `orchardcore-contact-center` for voice media and live call topology.
- `orchardcore-signalr` for hub-based real-time messaging.

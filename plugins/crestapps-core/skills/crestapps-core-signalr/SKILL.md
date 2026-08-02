---
name: crestapps-core-signalr
description: Skill for CrestApps.Core SignalR registration, instance route management, and real-time hub mapping.
---

# CrestApps.Core SignalR - Prompt Templates

## Add SignalR Support

Register SignalR through the AI-suite builder when it participates in CrestApps composition. The builder overload can also add the store-committer hub filter.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddSignalR(
            pathPrefix: "/tenant-a",
            addStoreCommitterFilter: true)
    )
);
```

For a host that only needs route management, `builder.Services.AddCoreSignalR("/tenant-a")` returns the standard `ISignalRServerBuilder`.

## Map a Hub with Minimal Hosting

Resolve the registered `HubRouteManager` instance and map the hub with its generated path. Do not use `StartupBase` or a static mapper in a minimal host.

```csharp
var app = builder.Build();

var hubRouteManager = app.Services.GetRequiredService<HubRouteManager>();
app.MapHub<NotificationHub>(
    hubRouteManager.GetPathByHub<NotificationHub>());
```

The default hub path is `/Communication/Hub/{HubTypeName}`. The configured prefix is included by `GetPathByHub<T>()`.

## Generate a Client URL

Use the same route-manager instance when generating an absolute URL for a request:

```csharp
public sealed class ChatEndpoint(HubRouteManager hubRouteManager)
{
    public string GetHubUrl(HttpContext httpContext)
        => hubRouteManager.GetUriByHub<AIChatHub>(httpContext);
}
```

The route manager uses the current request by default and can use its configured site-base URL resolver when one is supplied to the constructor. Configure a SignalR backplane separately for multi-server deployment.

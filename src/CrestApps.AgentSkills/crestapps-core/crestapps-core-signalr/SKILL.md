---
name: crestapps-core-signalr
description: Skill for centralized SignalR hub path management and real-time chat URL generation in CrestApps.Core.
---

# CrestApps.Core SignalR - Prompt Templates

## Add SignalR Support

You are a CrestApps.Core expert. Generate code and guidance for SignalR hub registration and URL generation in CrestApps.Core.

### Guidelines

- Use `AddCoreSignalR()` when the host needs centralized hub route construction.
- Generate hub URLs through `HubRouteManager` instead of hardcoding paths.
- Use the shared `/Communication/Hub/{HubName}` route pattern.
- Add a Redis backplane in multi-server deployments.

### Registration

```csharp
builder.Services.AddCoreSignalR();
```

### Map a Hub

```csharp
public sealed class Startup : StartupBase
{
    public override void Configure(
        IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        HubRouteManager.MapHub<NotificationHub>(routes);
    }
}
```

### Generate the Client URL

```csharp
public sealed class ChatController(HubRouteManager hubRouteManager)
{
    public IActionResult Index()
    {
        var hubUrl = hubRouteManager.GetUriByHub<AIChatHub>(HttpContext);
        ViewBag.ChatHubUrl = hubUrl;
        return View();
    }
}
```

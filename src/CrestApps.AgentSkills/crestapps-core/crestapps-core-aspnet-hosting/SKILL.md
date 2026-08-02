---
name: crestapps-core-aspnet-hosting
description: Skill for composing CrestApps.Core into ASP.NET Core MVC Blazor SignalR and Minimal API hosts.
---

# CrestApps.Core ASP.NET Hosting - Prompt Templates

## Compose an ASP.NET Core Host

You are a CrestApps.Core expert. Put host services first, compose CrestApps through `AddCrestAppsCore(...)`, then map only the endpoints and hubs for enabled features.

### MVC

```csharp
builder.Services.AddControllersWithViews()
    .AddCrestAppsStoreCommitterFilter();

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions()
        .AddSignalR(addStoreCommitterFilter: true))
);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

### Blazor Web App

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddOpenAI()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

### Minimal APIs

`StoreCommitterEndpointFilter` is an `IEndpointFilter`; add it to a group whose handlers write through a staged store.

```csharp
var api = app.MapGroup("/api")
    .AddEndpointFilter<StoreCommitterEndpointFilter>();

api.MapPost("/chat", () => Results.Accepted());
```

Do not add `StoreCommitterEndpointFilter` when the selected store persists each write immediately.

## Endpoint and Protocol Composition

Register protocol services before mapping their endpoints. The MVC and Blazor sample hosts map SignalR hubs with `MapHub`, MCP with `MapMcp("mcp")`, A2A with `MapA2AHost()`, and document/chat endpoints with `AddChatApiEndpoints()` plus the applicable `Add*Endpoint()` extensions.

Use `AddCrestAppsStoreCommitterFilter()` for controllers. For hub writes, use the `ISignalRServerBuilder` overload named `AddCrestAppsStoreCommitterFilter()`; the AI suite exposes this through `.AddSignalR(addStoreCommitterFilter: true)`.

## Startup Composition

The sample hosts use `AddSharedSampleHostDefaults()` for NLog, App_Data, data-protection key persistence, and site-settings option bridges. These are sample-host helpers in `CrestApps.Core.Startup.Shared`, not required framework registration. Use `AddSharedSiteSettings(...)`, `AddSharedArticleServices()`, and `AddSharedTemplateProviders()` only when adopting those sample patterns.

Use the MVC sample for full MVC, storage, MCP, A2A, document, SignalR, and search composition. Use the Blazor sample for `AddRazorComponents`, server interactivity, and the same CrestApps service composition. Keep credentials in `CrestApps:AI:Connections` and deployment selection in `CrestApps:AI:Deployments`.

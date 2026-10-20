---
name: orchardcore-startup-code
description: Skill for running initialization code on tenant startup in Orchard Core. Covers IModularTenantEvents and ModularTenantEvents, the ActivatingAsync, ActivatedAsync, TerminatingAsync, TerminatedAsync and RemovingAsync hooks, tenant lazy-loading and the BackgroundService ShellWarmup setting, uninitialized tenant checks, preloading data for synchronous access, RequiredStartup components, and module initialization best practices. Use this skill when requests mention Orchard Core Startup Code, Run Code on Tenant Startup, Registering a Startup Task, Seeding Default Data on Tenant Start, warming up tenants, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.Modules, IModularTenantEvents, ModularTenantEvents, ShellSettings, IsUninitialized, ShellScope, IContentManager, ILogger.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Startup Code - Prompt Templates

## Run Code on Tenant Startup

You are an Orchard Core expert. Generate code for running initialization tasks when a tenant starts, using the `IModularTenantEvents` interface and `ModularTenantEvents` base class.

### Guidelines

- Implement `IModularTenantEvents` or inherit from `ModularTenantEvents` to run code on tenant activation.
- `ActivatingAsync` is called when a tenant is first hit (lazy-loaded) — use it for initialization logic.
- `ActivatedAsync` is called after all `ActivatingAsync` handlers have completed — use it for post-initialization logic.
- Tenants are **lazy-loaded**: event handlers are NOT invoked at application start — they run on the first request to the tenant, and again each time the shell is rebuilt (after a feature change or a shell release). Set `OrchardCore:BackgroundService:ShellWarmup` to `true` to build shells at startup (the 3.x section name was `OrchardCore_BackgroundService`).
- Handlers also run for **uninitialized** tenants (not set up yet), where `ISession`, `ISiteService` and content services cannot be used; check `ShellSettings.IsUninitialized()` first.
- `ActivatingAsync` events are invoked in module dependency order (based on the module dependency graph).
- `ActivatedAsync` events are invoked in **reverse** module dependency order.
- Register implementations in `Startup.ConfigureServices` using `services.AddScoped<IModularTenantEvents, T>()`.
- Do not use the `Startup` class itself for initialization that requires scoped services — use `IModularTenantEvents` instead.
- Prefer data migrations or recipes for one-time data seeding; tenant events run on every activation, so seeding code must be idempotent.
- To run work after the activation scope commits, schedule it with `ShellScope.AddDeferredTask` (see `orchardcore-deferred-tasks`).
- Keep startup tasks fast to avoid slow first-request times.
- All C# classes must use the `sealed` modifier.

### Basic Startup Task

Inherit from `ModularTenantEvents` and override `ActivatingAsync` to run code on tenant activation:

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace MyModule;

public sealed class MyStartupTaskService : ModularTenantEvents
{
    private readonly ILogger<MyStartupTaskService> _logger;

    public MyStartupTaskService(ILogger<MyStartupTaskService> logger)
    {
        _logger = logger;
    }

    public override Task ActivatingAsync()
    {
        _logger.LogInformation("Tenant has been activated.");

        return Task.CompletedTask;
    }
}
```

### Registering the Startup Task

Register the startup task in the module's `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IModularTenantEvents, MyStartupTaskService>();
    }
}
```

### Using Both ActivatingAsync and ActivatedAsync

Use `ActivatingAsync` for pre-initialization and `ActivatedAsync` for post-initialization logic:

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace MyModule;

public sealed class TenantLifecycleHandler : ModularTenantEvents
{
    private readonly ILogger<TenantLifecycleHandler> _logger;

    public TenantLifecycleHandler(ILogger<TenantLifecycleHandler> logger)
    {
        _logger = logger;
    }

    public override Task ActivatingAsync()
    {
        _logger.LogInformation("Tenant is activating — running pre-initialization.");

        return Task.CompletedTask;
    }

    public override Task ActivatedAsync()
    {
        _logger.LogInformation("Tenant has activated — all modules initialized.");

        return Task.CompletedTask;
    }
}
```

### Seeding Default Data on Tenant Start

Use `IModularTenantEvents` to seed default data when a tenant is first activated:

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace MyModule;

public sealed class DefaultDataSeeder : ModularTenantEvents
{
    private readonly ShellSettings _shellSettings;
    private readonly IContentManager _contentManager;
    private readonly ILogger<DefaultDataSeeder> _logger;

    public DefaultDataSeeder(
        ShellSettings shellSettings,
        IContentManager contentManager,
        ILogger<DefaultDataSeeder> logger)
    {
        _shellSettings = shellSettings;
        _contentManager = contentManager;
        _logger = logger;
    }

    public override async Task ActivatingAsync()
    {
        // No database session exists before the tenant is set up.
        if (_shellSettings.IsUninitialized())
        {
            return;
        }

        var existing = await _contentManager.GetAsync("default-settings", VersionOptions.Latest);

        if (existing is not null)
        {
            return;
        }

        var contentItem = await _contentManager.NewAsync("SiteConfiguration");
        contentItem.ContentItemId = "default-settings";
        contentItem.DisplayText = "Default Configuration";

        await _contentManager.CreateAsync(contentItem, VersionOptions.Published);

        _logger.LogInformation("Default site configuration has been seeded.");
    }
}
```

`ShellSettings` comes from `OrchardCore.Environment.Shell`.

### Preloading Data for Synchronous Access

Orchard Core itself preloads site settings in `ActivatedAsync`, so code paths that cannot be async (for example `IConfigureOptions<T>.Configure()`) can read them with `ISiteService.GetSettings<T>()` without blocking on the database:

```csharp
public sealed class PreloadMyDocumentTenantEvents : ModularTenantEvents
{
    private readonly ShellSettings _shellSettings;
    private readonly IMyDocumentManager _documentManager;

    public PreloadMyDocumentTenantEvents(ShellSettings shellSettings, IMyDocumentManager documentManager)
    {
        _shellSettings = shellSettings;
        _documentManager = documentManager;
    }

    public override async Task ActivatedAsync()
    {
        if (_shellSettings.IsUninitialized())
        {
            return;
        }

        _ = await _documentManager.GetDocumentAsync();
    }
}
```

### Initializing External Service Connections

Use startup tasks to initialize connections to external services or warm up caches:

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace MyModule;

public sealed class ExternalServiceInitializer : ModularTenantEvents
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalServiceInitializer> _logger;

    public ExternalServiceInitializer(
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalServiceInitializer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public override async Task ActivatingAsync()
    {
        var client = _httpClientFactory.CreateClient("ExternalApi");

        try
        {
            var response = await client.GetAsync("/health");
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("External API health check passed.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "External API is not available at startup.");
        }
    }
}
```

### Registering Multiple Startup Tasks

Multiple `IModularTenantEvents` implementations can be registered. They execute in module dependency order for `ActivatingAsync` and in reverse order for `ActivatedAsync`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IModularTenantEvents, DatabaseMigrationTask>();
        services.AddScoped<IModularTenantEvents, CacheWarmupTask>();
        services.AddScoped<IModularTenantEvents, NotificationInitializer>();
    }
}
```

### Tenant Lifecycle Summary

| Event | When Invoked | Order | Use Case |
|-------|-------------|-------|----------|
| `ActivatingAsync` | On first request to the tenant | Module dependency order | Initialize services, seed data, run migrations |
| `ActivatedAsync` | After all `ActivatingAsync` handlers complete | Reverse module dependency order | Post-initialization logic, preloading caches |
| `TerminatingAsync` / `TerminatedAsync` | When the shell is released or the app stops | | Flush buffers, close connections |
| `RemovingAsync(ShellRemovingContext)` | When a tenant is being removed | | Delete external resources owned by the tenant |

### Key Behavior Notes

- **Lazy loading**: Tenants are not activated at application start. The first HTTP request to a tenant triggers activation.
- **Scoped services**: `IModularTenantEvents` implementations are registered as scoped services and can inject any scoped or singleton service.
- **Execution order**: The order of `ActivatingAsync` calls depends on the module dependency graph, not the order of registration.
- **Error handling**: If an `ActivatingAsync` handler throws, the tenant activation fails and the request returns an error.
- **Every tenant, every feature state**: a handler registered from a startup marked `[RequiredStartup]` runs for every tenant even when its own feature is disabled (see `orchardcore-modules`).

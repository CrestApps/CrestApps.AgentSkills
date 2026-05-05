---
name: orchardcore-redis
description: Skill for configuring Redis in Orchard Core. Covers Redis cache, message bus, distributed locking, data protection, health checks, configuration options, key storage structure, and multi-instance deployment considerations. Use this skill when requests mention Orchard Core Redis, Configure Redis Integration, Enabling Redis Features, Redis Features Summary, Redis Configuration, Production Configuration Example, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Redis, OrchardCore.Redis.Cache, OrchardCore.Redis.Bus, OrchardCore.Redis.Lock, OrchardCore.Redis.DataProtection, OrchardCore.Environment.Cache, OrchardCore.Locking.Distributed, OrchardCore.HealthChecks, IDistributedCache, ISignal, CachedProductService. It also helps with Redis Configuration, Production Configuration Example, Key Storage Structure, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Redis - Prompt Templates

## Configure Redis Integration

You are an Orchard Core expert. Generate code and configuration for Redis integration including Redis cache, message bus, distributed locking, data protection, health checks, configuration, key storage, and multi-instance deployment.

### Guidelines

- Enable `OrchardCore.Redis` as the base feature for Redis configuration support.
- Enable `OrchardCore.Redis.Cache` for distributed caching using Redis as the `IDistributedCache` backend.
- Enable `OrchardCore.Redis.Bus` for distributed `ISignal` service, making cache invalidation work across multiple application instances.
- Enable `OrchardCore.Redis.Lock` for distributed locking using Redis.
- Enable `OrchardCore.Redis.DataProtection` for storing ASP.NET Core Data Protection keys in Redis.
- Configure the Redis connection string via `OrchardCore_Redis:Configuration` in `appsettings.json`.
- Use `InstancePrefix` to namespace all Redis keys and prevent collisions between environments or applications.
- Data Protection keys must be stored in durable storage; ensure Redis persistence (AOF or RDB) is enabled.
- The Redis module provides a health check for monitoring Redis server status.
- For multi-instance deployments, enable Redis Bus so that `ISignal` cache invalidation propagates across all nodes.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Redis Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Redis",
        "OrchardCore.Redis.Cache",
        "OrchardCore.Redis.Bus",
        "OrchardCore.Redis.Lock",
        "OrchardCore.Redis.DataProtection"
      ],
      "disable": []
    }
  ]
}
```

### Redis Features Summary

| Feature | Module ID | Description |
|---------|-----------|-------------|
| Redis | `OrchardCore.Redis` | Base Redis configuration support |
| Redis Cache | `OrchardCore.Redis.Cache` | Distributed cache using Redis as `IDistributedCache` backend |
| Redis Bus | `OrchardCore.Redis.Bus` | Distributed `ISignal` service for cross-instance cache invalidation |
| Redis Lock | `OrchardCore.Redis.Lock` | Distributed locking for coordinating work across instances |
| Redis DataProtection | `OrchardCore.Redis.DataProtection` | Stores ASP.NET Core Data Protection keys in Redis |

### Redis Configuration

Configure the Redis connection in `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "localhost:6379,abortConnect=false,connectTimeout=5000",
      "InstancePrefix": "MyApp:",
      "AllowAdmin": true
    }
  }
}
```

#### Configuration Options

| Option | Description | Required |
|--------|-------------|----------|
| `Configuration` | The Redis connection string | Yes |
| `InstancePrefix` | A prefix added to all Redis keys to prevent collisions | No |
| `AllowAdmin` | Whether to allow admin commands (enables persistence check) | No (defaults to `false`) |

### Production Configuration Example

For a production multi-instance deployment with SSL:

```json
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "myredis.example.com:6380,password=MySecurePassword,ssl=true,abortConnect=false,connectTimeout=5000,syncTimeout=5000",
      "InstancePrefix": "Production:",
      "AllowAdmin": true
    }
  }
}
```

### Key Storage Structure

Redis Data Protection stores keys using the following pattern:

```
{InstancePrefix}{TenantName}:DataProtection-Keys
```

Examples:

| InstancePrefix | Tenant | Redis Key |
|---------------|--------|-----------|
| `MyApp:` | `Default` | `MyApp:Default:DataProtection-Keys` |
| `MyApp:` | `Tenant1` | `MyApp:Tenant1:DataProtection-Keys` |
| *(none)* | `Default` | `Default:DataProtection-Keys` |

### Data Protection

The `OrchardCore.Redis.DataProtection` feature stores ASP.NET Core Data Protection keys in Redis. This is essential for:

- **Load-balanced environments**: All instances share the same data protection keys.
- **Multi-tenant deployments**: Each tenant gets its own key ring in Redis.
- **Protecting sensitive data**: Authentication cookies, anti-forgery tokens, and persisted secrets (e.g., SMTP passwords).

#### Prerequisites

1. A running Redis instance with persistence enabled.
2. `OrchardCore.Redis` feature enabled and configured.
3. `OrchardCore.Redis.DataProtection` feature enabled.

#### Persistence Warning

Data protection keyrings are not cache files and must be kept in durable storage. Ensure Redis has a backup strategy:

- **AOF (Append-Only File)**: Logs every write operation for point-in-time recovery.
- **RDB (Redis Database)**: Creates periodic snapshots.

The module automatically checks if persistence is enabled (when `AllowAdmin` is `true`) and logs a warning if it is not configured.

### Redis Cache

When `OrchardCore.Redis.Cache` is enabled, Redis replaces the default in-memory `IDistributedCache`. All Orchard Core features that use `IDistributedCache` automatically benefit from distributed caching.

Use `IDistributedCache` to store and retrieve cached data:

```csharp
using Microsoft.Extensions.Caching.Distributed;

public sealed class CachedProductService
{
    private readonly IDistributedCache _cache;

    public CachedProductService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string?> GetProductAsync(string productId)
    {
        return await _cache.GetStringAsync($"product:{productId}");
    }

    public async Task SetProductAsync(string productId, string data)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            SlidingExpiration = TimeSpan.FromMinutes(10),
        };

        await _cache.SetStringAsync($"product:{productId}", data, options);
    }
}
```

### Redis Bus (Distributed Signal)

When `OrchardCore.Redis.Bus` is enabled, the `ISignal` service becomes distributed. Cache invalidation signals propagate across all application instances via Redis Pub/Sub.

This is critical for multi-instance deployments. Without Redis Bus, signaling cache invalidation on one instance does not affect other instances.

```csharp
using OrchardCore.Environment.Cache;

public sealed class ProductCacheInvalidator
{
    private readonly ISignal _signal;

    public ProductCacheInvalidator(ISignal signal)
    {
        _signal = signal;
    }

    public async Task InvalidateAsync()
    {
        // This signal propagates to ALL instances via Redis Bus.
        await _signal.SignalTokenAsync("productcatalog");
    }
}
```

### Redis Lock (Distributed Locking)

When `OrchardCore.Redis.Lock` is enabled, Orchard Core uses Redis for distributed locking via `IDistributedLock`. This ensures only one instance executes a critical section at a time:

```csharp
using OrchardCore.Locking.Distributed;

public sealed class ImportService
{
    private readonly IDistributedLock _distributedLock;

    public ImportService(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    public async Task RunImportAsync()
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            "IMPORT_LOCK",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30));

        if (!locked)
        {
            return; // Another instance is already running the import.
        }

        await using var _ = locker;

        // Perform the import safely knowing no other instance is doing it.
        await ExecuteImportAsync();
    }

    private Task ExecuteImportAsync()
    {
        // Import logic here.
        return Task.CompletedTask;
    }
}
```

### Health Checks

The Redis module provides a health check to report the status of the Redis server. This integrates with ASP.NET Core health checks and can be used for load balancer probes.

Enable the health check feature:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Redis",
        "OrchardCore.HealthChecks"
      ],
      "disable": []
    }
  ]
}
```

### Multi-Instance Deployment Checklist

For load-balanced or multi-instance Orchard Core deployments using Redis:

1. **Enable `OrchardCore.Redis`** and configure the connection string.
2. **Enable `OrchardCore.Redis.Cache`** so all instances share the same distributed cache.
3. **Enable `OrchardCore.Redis.Bus`** so `ISignal` cache invalidation propagates across all nodes.
4. **Enable `OrchardCore.Redis.Lock`** to coordinate background tasks and prevent duplicate work.
5. **Enable `OrchardCore.Redis.DataProtection`** so all instances share data protection keys.
6. **Set `InstancePrefix`** to namespace keys and prevent collisions with other applications sharing the same Redis instance.
7. **Enable Redis persistence** (AOF or RDB) to prevent data loss on Redis restart.
8. **Set `AllowAdmin: true`** in configuration so the module can verify persistence is enabled.

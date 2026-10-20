---
name: orchardcore-caching
description: Skill for configuring and managing caching in Orchard Core. Covers response compression (OrchardCore.ResponseCompression), dynamic cache, shape caching, cache tag helpers, ISignal-based invalidation, ITagCache, IDynamicCacheService, IDistributedCache with the OrchardCore.Redis.Cache feature and the OrchardCore Redis configuration section, cache profiles and Cache-Control headers, caching content items with JConvert, and CacheContext dependencies. Use this skill when requests mention Orchard Core Caching, Enabling Caching Features, Response Compression, Dynamic Cache (Shape-Level Caching), Shape Cache Tag Helper (Razor), Liquid Cache Block, distributed cache, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.ResponseCompression, OrchardCore.DynamicCache, OrchardCore.Redis, OrchardCore.Environment.Cache, OrchardCore.ContentManagement.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Caching - Prompt Templates

## Configure and Manage Caching

You are an Orchard Core expert. Generate code and configuration for caching strategies including response compression, dynamic cache, shape-level caching, cache invalidation, and distributed cache.

### Guidelines

- Enable `OrchardCore.ResponseCompression` to compress HTTP responses with gzip or Brotli.
- Enable `OrchardCore.DynamicCache` for shape-level output caching with dependency tracking.
- Use `ISignal` to invalidate cached entries when underlying data changes.
- Use `IDistributedCache` for storing serialized data across multiple servers.
- Use `IDynamicCacheService` to programmatically manage dynamic cache entries.
- Use `CacheContext` to declare cache dependencies, vary-by keys, and expiration policies.
- Cache tag helpers in Razor and `{% cache %}` blocks in Liquid provide declarative shape caching.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Caching Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ResponseCompression",
        "OrchardCore.DynamicCache"
      ],
      "disable": []
    }
  ]
}
```

### Response Compression

Enable `OrchardCore.ResponseCompression` to compress HTTP responses for the tenant. The module calls `AddResponseCompression(options => options.EnableForHttps = true)` with the ASP.NET Core default providers and adds `UseResponseCompression()` early in the pipeline (`Startup.Order = -5`); no code is required.

- Prefer compression at the reverse proxy, web server, or CDN when one is in front of the application; use this module when the app is exposed directly.
- Because it compresses HTTPS responses, evaluate the BREACH risk for pages that mix secrets (antiforgery tokens, personal data) with attacker-controlled input before enabling it.

Tune the default providers from a startup class if needed:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.SmallestSize;
        });
    }
}
```

### Dynamic Cache (Shape-Level Caching)

`OrchardCore.DynamicCache` caches the rendered HTML output of shapes. Each cached shape tracks dependencies so it can be evicted when related content changes. Dependencies use the format `contentitemid:{id}` or custom signal names. See `orchardcore-dynamic-cache` for full vary-by contexts, nested cache blocks, programmatic caching, and custom cache context providers.

### Shape Cache Tag Helper (Razor)

Use the Dynamic Cache `<dynamic-cache>` tag helper in Razor views:

```html
<dynamic-cache cache-id="recent-posts"
               vary-by="route"
               dependencies="contentitemid:@Model.ContentItem.ContentItemId"
               expires-after="00:10:00">
    @await DisplayAsync(Model.Content)
</dynamic-cache>
```

The tag helper accepts `cache-id`, `vary-by`, `dependencies`, `expires-after`, `expires-sliding`, and `expires-on`. `vary-by` and `dependencies` accept comma- or space-separated values.

### Liquid Cache Block

In Liquid templates, use the `{% cache %}` tag for shape-level caching:

```liquid
{% cache "my-cache-key", vary_by: "query:page", expires_after: "00:10:00" %}
    {{ Model.Content | shape_render }}
{% endcache %}
```

### Using CacheContext for Shape Dependencies

Shapes can declare caching behavior through `CacheContext`. In a shape display driver, configure cache parameters:

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Cache;

public sealed class RecentPostsDisplayDriver : DisplayDriver<RecentPostsViewModel>
{
    public override IDisplayResult Display(RecentPostsViewModel model, BuildDisplayContext context)
    {
        return View("RecentPosts", model)
            .Location("Detail", "Content:5")
            .Cache("recentposts", cache => cache
                .AddTag("contenttype:BlogPost")
                .AddContext("user")
                .WithExpiryAfter(TimeSpan.FromMinutes(15))
            );
    }
}
```

Common `CacheContext` methods:
- `AddContext(string)` - Vary by the named context (e.g., `"user"`, `"route"`).
- `AddTag(string)` - Associate tags used for invalidation.
- `WithExpiryOn(DateTimeOffset)` - Set a fixed expiration instant.
- `WithExpiryAfter(TimeSpan)` - Set absolute expiration.
- `WithExpirySliding(TimeSpan)` - Set sliding expiration.

### Cache Signals and Dynamic Cache Tags

Use `ISignal.SignalTokenAsync` to invalidate consumers that registered `ISignal.GetToken` for the same key. Dynamic Cache entries use `CacheContext.AddTag` and are invalidated through `ITagCache.RemoveTagAsync`.

```csharp
using OrchardCore.Environment.Cache;

public sealed class ProductService
{
    private readonly ISignal _signal;
    private readonly ITagCache _tagCache;

    public ProductService(ISignal signal, ITagCache tagCache)
    {
        _signal = signal;
        _tagCache = tagCache;
    }

    public async Task InvalidateProductCacheAsync()
    {
        await _signal.SignalTokenAsync("productcatalog");
        await _tagCache.RemoveTagAsync("productcatalog");
    }
}
```

Use the same `"productcatalog"` key when obtaining the change token. The tag removal invalidates Dynamic Cache entries that called `AddTag("productcatalog")`.

### Using IDistributedCache

`IDistributedCache` stores serialized data in a shared cache backend (memory, SQL Server, or Redis). Inject and use it directly:

```csharp
using Microsoft.Extensions.Caching.Distributed;

public sealed class CatalogCacheService
{
    private readonly IDistributedCache _distributedCache;

    public CatalogCacheService(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task<string?> GetCachedCatalogAsync(string key)
    {
        return await _distributedCache.GetStringAsync(key);
    }

    public async Task SetCachedCatalogAsync(string key, string value)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            SlidingExpiration = TimeSpan.FromMinutes(10),
        };

        await _distributedCache.SetStringAsync(key, value, options);
    }

    public async Task RemoveCachedCatalogAsync(string key)
    {
        await _distributedCache.RemoveAsync(key);
    }
}
```

### Using IDynamicCacheService

`IDynamicCacheService` provides programmatic access to the dynamic cache for storing and evicting pre-rendered HTML:

```csharp
using OrchardCore.DynamicCache;

public sealed class WidgetCacheManager
{
    private readonly IDynamicCacheService _dynamicCacheService;
    private readonly ITagCache _tagCache;

    public WidgetCacheManager(
        IDynamicCacheService dynamicCacheService,
        ITagCache tagCache)
    {
        _dynamicCacheService = dynamicCacheService;
        _tagCache = tagCache;
    }

    public async Task<string?> GetCachedWidgetAsync(CacheContext context)
    {
        return await _dynamicCacheService.GetCachedValueAsync(context);
    }

    public async Task SetCachedWidgetAsync(CacheContext context, string htmlContent)
    {
        context.AddTag("widget-sidebar");
        await _dynamicCacheService.SetCachedValueAsync(context, htmlContent);
    }

    public async Task InvalidateWidgetAsync()
    {
        await _tagCache.RemoveTagAsync("widget-sidebar");
    }
}
```

### Redis Distributed Cache Configuration

Enable the `OrchardCore.Redis` and `OrchardCore.Redis.Cache` features (package `OrchardCore.Redis`, installed in the web project when not using the full CMS targets). `OrchardCore.Redis.Cache` replaces the default in-memory `IDistributedCache` for the tenant, so dynamic cache entries and every `IDistributedCache` consumer are shared across instances. Add `OrchardCore.Redis.Bus` to make `ISignal` distributed.

```json
{
  "OrchardCore": {
    "Redis": {
      "Configuration": "localhost:6379,abortConnect=false,connectTimeout=5000",
      "InstancePrefix": "MyApp:"
    }
  }
}
```

Environment variable form: `OrchardCore__Redis__Configuration`. The former `OrchardCore_Redis` section name still works but is deprecated. See `orchardcore-redis`.

### Cache Profiles and Cache-Control Headers

Configure response cache profiles to set `Cache-Control` headers for HTTP responses:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<MvcOptions>(options =>
        {
            options.CacheProfiles.Add("Default", new CacheProfile
            {
                Duration = 300,
                Location = ResponseCacheLocation.Any,
                VaryByHeader = "Accept-Encoding",
            });

            options.CacheProfiles.Add("NoCache", new CacheProfile
            {
                Duration = 0,
                Location = ResponseCacheLocation.None,
                NoStore = true,
            });
        });
    }
}
```

`[ResponseCache]` only emits `Cache-Control` headers. Server-side response caching additionally needs `services.AddResponseCaching()` and `app.UseResponseCaching()`; never cache personalized or authenticated responses that way.

Apply a cache profile to a controller or action:

```csharp
[ResponseCache(CacheProfileName = "Default")]
public sealed class CatalogController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(CacheProfileName = "NoCache")]
    public IActionResult Checkout()
    {
        return View();
    }
}
```

### Caching Content Queries

Combine `IDistributedCache` with content queries to avoid repeated database calls:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.ContentManagement;

public sealed class CachedArticleService
{
    private readonly IContentManager _contentManager;
    private readonly IDistributedCache _distributedCache;

    public CachedArticleService(
        IContentManager contentManager,
        IDistributedCache distributedCache)
    {
        _contentManager = contentManager;
        _distributedCache = distributedCache;
    }

    public async Task<ContentItem?> GetPublishedArticleAsync(string contentItemId)
    {
        var cacheKey = $"published-article:{contentItemId}";
        var cached = await _distributedCache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            // JConvert uses Orchard Core's JSON options, required for ContentItem.
            return JConvert.DeserializeObject<ContentItem>(cached);
        }

        var article = await _contentManager.GetAsync(
            contentItemId,
            VersionOptions.Published);

        if (article is null)
        {
            return null;
        }

        await _distributedCache.SetStringAsync(
            cacheKey,
            JConvert.SerializeObject(article),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            });

        return article;
    }

    // Call from a content handler (PublishedAsync, UnpublishedAsync, RemovedAsync).
    public Task InvalidateArticleAsync(string contentItemId)
        => _distributedCache.RemoveAsync($"published-article:{contentItemId}");
}
```

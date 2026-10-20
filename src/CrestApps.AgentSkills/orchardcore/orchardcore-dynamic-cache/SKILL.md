---
name: orchardcore-dynamic-cache
description: Skill for configuring dynamic caching in Orchard Core. Covers shape-level caching, the dynamic-cache and cache-dependency Razor tag helpers, shape and menu tag helper cache attributes, Liquid cache blocks and shape cache arguments, cache dependencies, cache contexts (query, user, role, route, features), invalidation with ITagCache and ISignal, ShapeResult.Cache in display drivers, custom ICacheContextProvider, cache modes in General Settings, and the OrchardCore DynamicCache configuration section (FailoverRetryLatency). Use this skill when requests mention Orchard Core Dynamic Cache, Configure Dynamic Caching, Well-Known Cache Dependencies, Available Cache Contexts (Vary-By), Shape Tag Helper Attributes, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.DynamicCache, OrchardCore.Environment.Cache, CacheContext, ITagCache, ISignal, IDynamicCache, IDynamicCacheService, IDistributedCache.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Dynamic Cache - Prompt Templates

## Configure Dynamic Caching

You are an Orchard Core expert. Generate code and configuration for dynamic caching including shape-level caching, tag helpers, Liquid cache blocks, cache contexts, dependencies, variations, invalidation, and performance optimization. For response compression, distributed cache providers, and cache-control/response headers, see `orchardcore-caching`.

### Guidelines

- Enable `OrchardCore.DynamicCache` for shape-level output caching with dependency tracking.
- Dynamic Cache caches rendered HTML markup at the shape level, not at the page level.
- Cached sections can be nested; each section can have its own cache policy.
- Cached values are stored via `IDynamicCache`, backed by `IDistributedCache` (defaults to `IMemoryCache`).
- The Razor `<dynamic-cache>` tag helper defaults to a 30-second sliding window when no expiration is set. Programmatic and Liquid cache entries use the dynamic-cache service default.
- Invalidating a child cache block also invalidates all parent blocks.
- Use `contentitemid:{ContentItemId}` and `alias:{Alias}` dependencies to auto-invalidate when content changes.
- Create custom dependencies with `ITagCache.RemoveTagAsync()`.
- Contexts are hierarchical: `user` is more specialized than `user.roles`, so if both are specified, only `user` is used.
- Use `<dynamic-cache>` tag helper in Razor (not `<cache>`, which is the ASP.NET Core built-in).
- Use `{% cache %}` blocks in Liquid for declarative caching.
- Use cache scope tags (`cache_dependency`, `cache_expires_after`, etc.) to alter cache policies from within nested content.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Dynamic Cache

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DynamicCache"
      ],
      "disable": []
    }
  ]
}
```

### Well-Known Cache Dependencies

| Dependency | Description |
|------------|-------------|
| `contentitemid:{ContentItemId}` | Invalidated when the content item is Published, Unpublished, or Removed |
| `alias:{Alias}` | Invalidated when the content item with the specified alias is Published, Unpublished, or Removed |

Create custom dependencies by calling `ITagCache.RemoveTagAsync()` in response to events.

### Available Cache Contexts (Vary-By)

| Context | Description |
|---------|-------------|
| `features` | The list of enabled features |
| `features:{featureName}` | A specific feature name |
| `query` | All query string values |
| `query:{queryName}` | A specific query string parameter |
| `user` | The current user |
| `user.roles` | The roles of the current user |
| `route` | The current request path |

Contexts are hierarchical. For example, `user` is more specific than `user.roles`. If both are declared, only `user` is used.

### Dynamic Cache Razor Tag Helper Attributes

| Razor Attribute | Description | Required |
|-----------------|-------------|----------|
| `cache-id` | The identifier of the cached section | Yes |
| `vary-by` | Space/comma-separated cache contexts | No |
| `dependencies` | Space/comma-separated invalidation tags | No |
| `expires-on` | Fixed `DateTimeOffset` expiration | No |
| `expires-after` | Relative expiration | No |
| `expires-sliding` | Sliding expiration (30 seconds when no expiration is set) | No |

```cshtml
@addTagHelper *, OrchardCore.DynamicCache

<dynamic-cache cache-id="recent-posts" vary-by="user.roles" dependencies="alias:recent-posts" expires-after="00:10:00">
    @foreach (var post in Model.Posts)
    {
        <cache-dependency dependency="contentitemid:@post.ContentItemId" />
        <a display-for="@post">@post.DisplayText</a>
    }
</dynamic-cache>
```

`<cache-dependency dependency="..." />` adds a dependency to the enclosing cache scope while it renders, like the Liquid `cache_dependency` tag.

### Shape and Menu Tag Helper Cache Attributes

`<shape>`, `<menu>`, `<contentitem>`, and other shape tag helpers accept `cache-id`, `cache-context`, `cache-tag`, `cache-fixed-duration`, and `cache-sliding-duration`:

```cshtml
<menu alias="alias:main-menu" cache-id="main-menu" cache-fixed-duration="00:05:00" cache-tag="alias:main-menu" cache-context="user.roles" />
```

### Caching Shapes from Liquid

Liquid `shape` and `contentitem` tags cache a shape with `cache_id`, `cache_context`, `cache_dependency`, `cache_tag`, `cache_fixed_duration`, and `cache_sliding_duration`. `shape_cache` sets the same arguments on an existing shape (`{% shape_cache my_shape cache_id: "my-shape", cache_fixed_duration: "00:05:00" %}`):

```liquid
{% shape "menu", alias: "alias:main-menu", cache_id: "main-menu", cache_fixed_duration: "00:05:00", cache_tag: "alias:main-menu" %}
```

Cache a content item shape:

```liquid
{% contentitem alias: "alias:main-menu", cache_id: "main-menu", cache_fixed_duration: "00:05:00", cache_tag: "alias:main-menu" %}
```

### Caching a Shape Programmatically

Use `ShapeMetadata.Cache()` to mark a shape as cached in a display driver:

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class RecentPostsDisplayDriver : DisplayDriver<RecentPostsWidget>
{
    public override IDisplayResult Display(RecentPostsWidget model, BuildDisplayContext context)
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

#### CacheContext Methods

| Method | Description |
|--------|-------------|
| `WithExpiryOn(DateTimeOffset)` | Cache until a fixed point in time |
| `WithExpiryAfter(TimeSpan)` | Cache for a fixed duration |
| `WithExpirySliding(TimeSpan)` | Cache with a sliding window |
| `AddContext(params string[])` | Vary cached content by the specified context values |
| `RemoveContext(string)` | Remove a context |
| `AddTag(params string[])` | Add invalidation tags |
| `RemoveTag(string)` | Remove a tag |

### Liquid Cache Block

Use `{% cache %}` blocks in Liquid templates for declarative caching. Blocks can be nested:

#### Arguments

| Argument | Description | Required |
|----------|-------------|----------|
| `id` (first positional) | The cache block identifier | Yes |
| `vary_by` | Space/comma-separated context values, or a custom value such as a content item id | No |
| `dependencies` | Space/comma-separated dependency values | No |
| `expires_after` | Fixed duration (e.g., `"00:05:00"`) | No |
| `expires_sliding` | Sliding duration (e.g., `"00:05:00"`) | No |

The block reads `vary_by`; the `contexts` name shown in some older docs is not recognized.

#### Simple Cache Block

```liquid
{% cache "my-cache-block" %}
    <p>This content is cached.</p>
{% endcache %}
```

#### Cache Block with Vary-By

```liquid
{% cache "blog-post-summary", vary_by: Model.ContentItem.ContentItemId %}
    <h2>{{ Model.ContentItem | display_text }}</h2>
    <p>{{ Model.ContentItem.Content.HtmlBodyPart.Html | sanitize_html | raw }}</p>
{% endcache %}
```

#### Nested Cache Blocks

```liquid
{% cache "a" %}
    A {{ "now" | date: "%T" }} (No Duration) <br />
    {% cache "a1", dependencies: "a1", vary_by: "user", expires_after: "0:5:0" %}
        A1 {{ "now" | date: "%T" }} (5 Minutes) <br />
    {% endcache %}
    {% cache "a2", dependencies: "a2", expires_after: "0:0:1" %}
        A2 {{ "now" | date: "%T" }} (1 Second) <br />
        {% cache "a2a", dependencies: "a2a", vary_by: "route", expires_sliding: "0:0:5" %}
            A2A {{ "now" | date: "%T" }} (5 Seconds) <br />
        {% endcache %}
    {% endcache %}
{% endcache %}
```

### Altering Cache Scope from Within

Use these Liquid tags to alter the current cache scope from inside a cache block. These tags are safe to use even if you are not inside a cache block:

| Tag | Description | Example |
|-----|-------------|---------|
| `cache_dependency` | Add a dependency to the current scope | `{% cache_dependency "alias:my-alias" %}` |
| `cache_expires_on` | Set a fixed expiration date/time | `{% cache_expires_on someDate %}` |
| `cache_expires_after` | Set a relative expiration duration | `{% cache_expires_after "01:00:00" %}` |
| `cache_expires_sliding` | Set a sliding window expiration | `{% cache_expires_sliding "00:01:00" %}` |

#### Dynamic Dependencies from Query Results

```liquid
{% cache "recent-blog-posts" %}
    {% assign recentBlogPosts = Queries.RecentBlogPosts | query %}
    {% for item in recentBlogPosts %}
        {{ item | display_text }}

        {% assign cacheDependency = "contentitemid:" | append: item.ContentItemId %}
        {% cache_dependency cacheDependency %}
    {% endfor %}
{% endcache %}
```

### Cache Signals and Tag Invalidation

Use `ISignal.SignalTokenAsync` for consumers that registered `ISignal.GetToken` for the same key. To invalidate Dynamic Cache entries tagged with `AddTag`, use `ITagCache.RemoveTagAsync`.

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

The signal API remains useful for signal-token consumers. The tag removal invalidates Dynamic Cache entries that called `AddTag("productcatalog")`.

### Custom Cache Context Provider

Implement `ICacheContextProvider` to create custom context values for cache variation:

```csharp
using OrchardCore.Environment.Cache;

public sealed class TenantCacheContextProvider : ICacheContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantCacheContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task PopulateContextEntriesAsync(IEnumerable<string> contexts, List<CacheContextEntry> entries)
    {
        if (contexts.Any(ctx => string.Equals(ctx, "tenant", StringComparison.OrdinalIgnoreCase)))
        {
            var tenantName = _httpContextAccessor.HttpContext?.Request.Host.Value ?? "default";
            entries.Add(new CacheContextEntry("tenant", tenantName));
        }

        return Task.CompletedTask;
    }
}
```

### Configuration (appsettings.json)

```json
{
  "OrchardCore": {
    "DynamicCache": {
      "FailoverRetryLatency": "00:00:30"
    }
  }
}
```

`FailoverRetryLatency` is how long the dynamic cache stops using the distributed cache after a read or write failure (for example a Redis outage) before trying again. The former `OrchardCore_DynamicCache` section name still works but is deprecated.

### Cache Options

Dynamic Cache has four modes configurable in **General Settings**:

- **From environment** (default): Enables in `Production` ASP.NET Core environment only.
- **Enabled**: Always enabled.
- **Disabled**: Always disabled.
- **Enabled with cache debug mode**: Enabled with HTML comments logging cache context metadata for troubleshooting.

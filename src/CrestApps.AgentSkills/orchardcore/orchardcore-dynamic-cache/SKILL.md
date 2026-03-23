---
name: orchardcore-dynamic-cache
description: Skill for configuring dynamic caching in Orchard Core. Covers shape-level caching, cache tag helpers, Liquid cache blocks, cache context dependencies, cache variations (query, cookie, user, role), cache invalidation with ISignal, and performance optimization patterns.
---

# Orchard Core Dynamic Cache - Prompt Templates

## Configure Dynamic Caching

You are an Orchard Core expert. Generate code and configuration for dynamic caching including shape-level caching, tag helpers, Liquid cache blocks, cache contexts, dependencies, variations, invalidation, and performance optimization.

### Guidelines

- Enable `OrchardCore.DynamicCache` for shape-level output caching with dependency tracking.
- Dynamic Cache caches rendered HTML markup at the shape level, not at the page level.
- Cached sections can be nested; each section can have its own cache policy.
- Cached values are stored via `IDynamicCache`, backed by `IDistributedCache` (defaults to `IMemoryCache`).
- If no expiration is set, a default sliding window of one minute is used.
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

### Shape Tag Helper Attributes

| Razor Attribute | Liquid Attribute | Description | Required |
|-----------------|-----------------|-------------|----------|
| `cache-id` | `cache_id` | The identifier of the cached shape | Yes |
| `cache-context` | `cache_context` | Space/comma-separated context values | No |
| `cache-dependency` | `cache_dependency` | Space/comma-separated dependency values | No |
| `cache-tag` | `cache_tag` | Space/comma-separated tag values | No |
| `cache-fixed-duration` | `cache_fixed_duration` | Fixed cache duration (e.g., `"00:05:00"`) | No |
| `cache-sliding-duration` | `cache_sliding_duration` | Sliding cache duration (e.g., `"00:05:00"`) | No |

### Caching Shapes via Tag Helpers

Cache a shape in Liquid with shape tag helper attributes:

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
                .AddDependency("contenttype:BlogPost")
                .AddContext("user")
                .WithExpiryAfter(TimeSpan.FromMinutes(15))
            );
    }
}
```

#### CacheContext Methods

| Method | Description |
|--------|-------------|
| `WithDuration(TimeSpan)` | Cache for a fixed amount of time |
| `WithSlidingExpiration(TimeSpan)` | Cache with a sliding window |
| `AddContext(params string[])` | Vary cached content by the specified context values |
| `RemoveContext(string)` | Remove a context |
| `AddDependency(params string[])` | Define values that will invalidate the cache entry |
| `RemoveDependency(string)` | Remove a dependency |
| `AddTag(string)` | Add a tag for manual invalidation |
| `RemoveTag(string)` | Remove a tag |

### Liquid Cache Block

Use `{% cache %}` blocks in Liquid templates for declarative caching. Blocks can be nested:

#### Arguments

| Argument | Description | Required |
|----------|-------------|----------|
| `id` (first positional) | The cache block identifier | Yes |
| `contexts` | Space/comma-separated context values | No |
| `dependencies` | Space/comma-separated dependency values | No |
| `expires_after` | Fixed duration (e.g., `"00:05:00"`) | No |
| `expires_sliding` | Sliding duration (e.g., `"00:05:00"`) | No |
| `vary_by` | A custom value to vary the cache entry | No |

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
    <p>{{ Model.ContentItem.Content.BodyPart.Body }}</p>
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
        {% cache "a2a", dependencies: "a2a", contexts: "route", expires_sliding: "0:0:5" %}
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

        {% assign cacheDependency = "contentitemid:" | append: Model.ContentItem.ContentItemId %}
        {% cache_dependency cacheDependency %}
    {% endfor %}
{% endcache %}
```

### Cache Invalidation with ISignal

Use `ISignal` to programmatically invalidate cached entries by signaling a named dependency:

```csharp
using OrchardCore.Environment.Cache;

public sealed class ProductService
{
    private readonly ISignal _signal;

    public ProductService(ISignal signal)
    {
        _signal = signal;
    }

    public async Task InvalidateProductCacheAsync()
    {
        await _signal.SignalTokenAsync("productcatalog");
    }
}
```

Any cache entry with a `productcatalog` dependency is evicted when the signal fires. Content item changes automatically signal `contentitemid:{ContentItemId}`.

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

### Cache Options

Dynamic Cache has four modes configurable in **General Settings**:

- **From environment**: Enables in `Production` ASP.NET Core environment only.
- **Enabled**: Always enabled.
- **Disabled**: Always disabled.
- **Enabled with cache debug mode**: Enabled with HTML comments logging cache context metadata for troubleshooting.

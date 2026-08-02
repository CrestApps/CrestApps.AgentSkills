# Caching Examples

## Example 1: Dynamic Cache for a Featured Products Widget

A shape that caches featured products and invalidates when any Product content item changes.

### Shape Template (Razor)

```html
<dynamic-cache cache-id="featured-products"
               vary-by="query:category user"
               dependencies="contenttype:Product"
               expires-after="00:20:00">
    <div class="featured-products">
        @foreach (var product in Model.Products)
        {
            <div class="product-card">
                <h3>@product.DisplayText</h3>
                <p>@product.Content.ProductPart.Price.Value</p>
            </div>
        }
    </div>
</dynamic-cache>
```

### Shape Template (Liquid)

```liquid
{% cache "featured-products", vary_by: "query:category", dependencies: "contenttype:Product", expires_after: "00:20:00" %}
    <div class="featured-products">
        {% for product in Model.Products %}
            <div class="product-card">
                <h3>{{ product.DisplayText }}</h3>
                <p>{{ product.Content.ProductPart.Price.Value }}</p>
            </div>
        {% endfor %}
    </div>
{% endcache %}
```

### Display Driver with Cache Tags

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class FeaturedProductsDisplayDriver : DisplayDriver<FeaturedProductsViewModel>
{
    public override IDisplayResult Display(FeaturedProductsViewModel model, BuildDisplayContext context)
    {
        return View("FeaturedProducts", model)
            .Location("Content", "Content:5")
            .Cache("featured-products", cache => cache
                .AddTag("contenttype:Product")
                .AddContext("query")
                .WithExpiryAfter(TimeSpan.FromMinutes(20))
                .WithExpirySliding(TimeSpan.FromMinutes(5))
            );
    }
}
```

## Example 2: Content Event Handler with Cache Tag Invalidation

Automatically invalidate caches when content is published or removed.

```csharp
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Cache;

public sealed class ProductCacheInvalidationHandler : ContentHandlerBase
{
    private readonly ITagCache _tagCache;

    public ProductCacheInvalidationHandler(ITagCache tagCache)
    {
        _tagCache = tagCache;
    }

    public override Task PublishedAsync(PublishContentContext context)
    {
        return InvalidateIfProductAsync(context.ContentItem.ContentType);
    }

    public override Task RemovedAsync(RemoveContentContext context)
    {
        return InvalidateIfProductAsync(context.ContentItem.ContentType);
    }

    private async Task InvalidateIfProductAsync(string contentType)
    {
        if (string.Equals(contentType, "Product", StringComparison.OrdinalIgnoreCase))
        {
            await _tagCache.RemoveTagAsync("contenttype:Product");
        }
    }
}
```

Register the handler:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentHandler<ProductCacheInvalidationHandler>();
    }
}
```

## Example 3: Distributed Cache with JSON Serialization

Cache the results of an expensive query using `IDistributedCache` with manual serialization.

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Queries;

public sealed class CachedQueryService
{
    private readonly IQueryManager _queryManager;
    private readonly IDistributedCache _distributedCache;

    public CachedQueryService(
        IQueryManager queryManager,
        IDistributedCache distributedCache)
    {
        _queryManager = queryManager;
        _distributedCache = distributedCache;
    }

    public async Task<IEnumerable<object>> ExecuteCachedQueryAsync(
        string queryName,
        IDictionary<string, object> parameters)
    {
        var cacheKey = $"query-{queryName}-{string.Join("-", parameters.Values)}";
        var cached = await _distributedCache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<IEnumerable<object>>(cached) ?? [];
        }

        var query = await _queryManager.GetQueryAsync(queryName);

        if (query is null)
        {
            return [];
        }

        var result = await _queryManager.ExecuteQueryAsync(query, parameters);
        var items = result.Items.ToList();

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            SlidingExpiration = TimeSpan.FromMinutes(5),
        };

        await _distributedCache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(items),
            options);

        return items;
    }
}
```

## Example 4: IDynamicCacheService for Pre-rendered HTML Fragments

Store and retrieve pre-rendered HTML using `IDynamicCacheService` directly.

```csharp
using OrchardCore.DynamicCache;
using OrchardCore.Environment.Cache;

public sealed class NavigationCacheService
{
    private readonly IDynamicCacheService _dynamicCacheService;
    private readonly ITagCache _tagCache;

    public NavigationCacheService(
        IDynamicCacheService dynamicCacheService,
        ITagCache tagCache)
    {
        _dynamicCacheService = dynamicCacheService;
        _tagCache = tagCache;
    }

    public async Task<string?> GetCachedNavigationAsync()
    {
        var context = new CacheContext("main-navigation")
            .AddTag("contenttype:Menu")
            .AddContext("user")
            .WithExpiryAfter(TimeSpan.FromHours(1));

        return await _dynamicCacheService.GetCachedValueAsync(context);
    }

    public async Task SetCachedNavigationAsync(string html)
    {
        var context = new CacheContext("main-navigation")
            .AddTag("contenttype:Menu")
            .AddContext("user")
            .WithExpiryAfter(TimeSpan.FromHours(1));

        await _dynamicCacheService.SetCachedValueAsync(context, html);
    }

    public async Task InvalidateNavigationCacheAsync()
    {
        await _tagCache.RemoveTagAsync("contenttype:Menu");
    }
}
```

## Example 5: Multiple Cache Profiles for Different Response Types

Define multiple cache profiles and apply them to different endpoints.

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResponseCaching();

        services.AddMvc(options =>
        {
            options.CacheProfiles.Add("Static", new CacheProfile
            {
                Duration = 86400,
                Location = ResponseCacheLocation.Any,
                VaryByHeader = "Accept-Encoding",
            });

            options.CacheProfiles.Add("Personalized", new CacheProfile
            {
                Duration = 60,
                Location = ResponseCacheLocation.Client,
                VaryByHeader = "Cookie",
            });

            options.CacheProfiles.Add("ApiResponse", new CacheProfile
            {
                Duration = 120,
                Location = ResponseCacheLocation.Any,
                VaryByQueryKeys = ["page", "pageSize", "sort"],
            });

            options.CacheProfiles.Add("NeverCache", new CacheProfile
            {
                Duration = 0,
                Location = ResponseCacheLocation.None,
                NoStore = true,
            });
        });
    }
}
```

Apply cache profiles to controllers and actions:

```csharp
[ResponseCache(CacheProfileName = "ApiResponse")]
public sealed class ProductApiController : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20)
    {
        // Return paginated product list.
        return Ok(results);
    }

    [HttpGet("{id}")]
    [ResponseCache(CacheProfileName = "Static")]
    public async Task<IActionResult> GetById(string id)
    {
        // Return individual product.
        return Ok(product);
    }

    [HttpPost]
    [ResponseCache(CacheProfileName = "NeverCache")]
    public async Task<IActionResult> Create([FromBody] ProductDto dto)
    {
        // Create product, never cache POST responses.
        return CreatedAtAction(nameof(GetById), new { id = product.ContentItemId }, product);
    }
}
```

## Example 6: Enabling Redis and Dynamic Cache via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DynamicCache",
        "OrchardCore.ResponseCompression",
        "OrchardCore.Redis",
        "OrchardCore.Redis.Cache"
      ],
      "disable": []
    }
  ]
}
```

Configure the Redis connection in `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "localhost:6379,abortConnect=false,connectTimeout=5000"
    }
  }
}
```

When `OrchardCore.Redis.Cache` is enabled, it replaces the default in-memory distributed cache with Redis, allowing all `IDistributedCache` consumers and dynamic cache entries to be shared across multiple application instances.

## Example 7: Razor Dynamic Cache Variations

```html
<!-- Cache a user-specific dashboard widget -->
<dynamic-cache cache-id="dashboard-widget"
               vary-by="user route"
               expires-after="00:05:00">
    @await Component.InvokeAsync("DashboardWidget")
</dynamic-cache>

<!-- Cache a public page fragment with query and header variation -->
<dynamic-cache cache-id="search-results"
               vary-by="query:page query:sort query:filter"
               expires-sliding="00:30:00">
    @await DisplayAsync(Model.SearchResults)
</dynamic-cache>

<!-- Cache with a custom composite key -->
<dynamic-cache cache-id="category-products"
               vary-by="@($"{Model.TenantName}-{Model.CategoryId}")"
               expires-after="01:00:00">
    @await DisplayAsync(Model.CategoryProducts)
</dynamic-cache>
```

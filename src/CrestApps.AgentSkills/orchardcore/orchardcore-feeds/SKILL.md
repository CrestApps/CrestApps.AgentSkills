---
name: orchardcore-feeds
description: Skill for exposing Orchard Core content through extensible XML feeds. Covers RSS feed selection, the list RSS route and auto-discovery link, IFeedQueryProvider and IFeedQuery implementations, IFeedItemBuilder population, FeedContext response construction and contextualizers, Lists feed metadata, custom feed routes, and custom feed format builders such as Atom. Use this skill when requests mention Orchard Core Feeds, RSS feeds, Atom feeds, IFeedItemBuilder, feed queries, list feeds, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Feeds, OrchardCore.Feeds.Core, AddFeeds, IFeedBuilder, IFeedBuilderProvider, IFeedQueryProvider, IFeedQuery, IFeedItemBuilder, FeedContext, FeedMetadata, CommonFeedItemBuilder, and ListFeedQuery. It also helps with feature recipes, RSS URLs, custom format providers, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Feeds - Prompt Templates

## Build Extensible Content Feeds

You are an Orchard Core expert. Generate feed configuration and provider code using the Orchard Core feeds abstractions.

### Guidelines

- Enable the `OrchardCore.Feeds` feature. The module has no declared feature dependencies and exposes no feed on its own; other modules (most commonly Lists) describe what to syndicate.
- `services.AddFeeds()` (in `OrchardCore.Feeds.Core`) registers the RSS 2.0 builder for the `rss` format. There is no built-in Atom provider.
- Use `IFeedQueryProvider` to match a request and `IFeedQuery` to add source items and channel metadata to `FeedContext`.
- `FeedController` resolves a single `IFeedItemBuilder` (the last registered one). `OrchardCore.Contents` registers `CommonFeedItemBuilder` for `ContentItem` feed items; registering your own `IFeedItemBuilder` replaces it, so only do that deliberately.
- Providers return a priority. The controller selects the matching format provider and query provider with the highest priority (built-in RSS and list query use `-5`). If either is missing, the controller returns `404`.
- `FeedController.Index(string format)` is in the `OrchardCore.Feeds` area; the format comes from the route or the `format` query value. The response is `text/xml`.
- `OrchardCore.Lists` integrates when both Lists and Feeds are enabled. `ListFeedQuery` supplies list items, and lists are served at `/Contents/Lists/{contentItemId}/rss`.
- `FeedMetadata.DisableRssFeed` and `FeedMetadata.FeedProxyUrl` are supplied by the Lists feed handler for list content.
- Use `IFeedBuilderProvider` and `IFeedBuilder` to add Atom or another format rather than modifying the built-in RSS provider.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Feeds

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Feeds",
        "OrchardCore.Lists"
      ],
      "disable": []
    }
  ]
}
```

Enable `OrchardCore.Lists` only when list content should produce feeds. `OrchardCore.Feeds` alone provides the controller and RSS format infrastructure.

### Built-In RSS Behavior

The RSS provider matches only `format=rss`. It constructs:

```xml
<rss version="2.0">
  <channel>
    <!-- channel metadata and item elements -->
  </channel>
</rss>
```

For a `ContentItem`, `CommonFeedItemBuilder` supplies `title`, `link`, CDATA `description` (from `BodyAspect`), `pubDate`, and a permanent-link `guid`. Links are generated in contextualizers so they use the current scheme and host. The author is intentionally left empty.

### List Feeds

- URL: `/Contents/Lists/{contentItemId}/rss` (route `ListFeed`, area `OrchardCore.Feeds`, default `format = rss`).
- On the list's detail page, the `ListPartFeed` shape registers `<link rel="alternate" type="application/rss+xml">` for auto-discovery, pointing at the proxy URL when one is set.
- The list editor's RSS settings are stored under `ListPart`:

| Field | Effect |
|---|---|
| `DisableRssFeed` | Prevents `ListFeedQuery` from matching and removes the auto-discovery link. |
| `FeedProxyUrl` | Used as the auto-discovery link instead of the local feed URL (e.g., a FeedBurner proxy). |
| `FeedItemsCount` | Number of published list items in the feed, newest first. Default `20`; `0` yields no items. |

Configure them from the list feed editor rather than duplicating feed logic in a display driver.

### Implementing a Custom Feed Query

Map a route to the feed controller with `format = rss` so the built-in RSS builder matches, then match your query on a route value.

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Feeds;
using OrchardCore.Feeds.Models;
using YesSql;

namespace MyModule.Feeds;

public sealed class FeaturedFeedQuery : IFeedQueryProvider, IFeedQuery
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISession _session;

    public FeaturedFeedQuery(IHttpContextAccessor httpContextAccessor, ISession session)
    {
        _httpContextAccessor = httpContextAccessor;
        _session = session;
    }

    public Task<FeedQueryMatch> MatchAsync(FeedContext context)
    {
        var feed = _httpContextAccessor.HttpContext?.GetRouteValue("feed") as string;

        if (!string.Equals(feed, "featured", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<FeedQueryMatch>(null);
        }

        return Task.FromResult(new FeedQueryMatch
        {
            Priority = 10,
            FeedQuery = this,
        });
    }

    public async Task ExecuteAsync(FeedContext context)
    {
        context.Builder.AddProperty(context, null, "title", "Featured products");
        context.Builder.AddProperty(context, null, "description", "Current featured products.");

        var items = await _session.Query<ContentItem, ContentItemIndex>(x =>
                x.ContentType == "Product" && x.Published)
            .OrderByDescending(x => x.PublishedUtc)
            .Take(20)
            .ListAsync();

        foreach (var item in items)
        {
            // CommonFeedItemBuilder fills title, link, description, pubDate, and guid.
            context.Builder.AddItem(context, item);
        }
    }
}
```

The query must add feed items with `context.Builder.AddItem(context, item)`; the item builder populates them afterwards.

### Registering a Query Provider and Route

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Feeds;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IFeedQueryProvider, FeaturedFeedQuery>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "FeaturedFeed",
            areaName: "OrchardCore.Feeds",
            pattern: "featured/rss",
            defaults: new { controller = "Feed", action = "Index", format = "rss", feed = "featured" });
    }
}
```

### Adding a Custom Atom Format

The built-in provider is RSS-only. An Atom implementation provides an `IFeedBuilderProvider` that matches a distinct format, then an `IFeedBuilder` that creates the Atom document and its entries.

```csharp
using OrchardCore.Feeds;
using OrchardCore.Feeds.Models;

namespace MyModule.Feeds;

public sealed class AtomFeedBuilderProvider : IFeedBuilderProvider
{
    public FeedBuilderMatch Match(FeedContext context)
    {
        if (!string.Equals(context.Format, "atom", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new FeedBuilderMatch
        {
            FeedBuilder = new AtomFeedBuilder(),
            Priority = 10,
        };
    }
}
```

Register the provider as a singleton because builder providers are stateless:

```csharp
services.AddSingleton<IFeedBuilderProvider, AtomFeedBuilderProvider>();
```

`IFeedBuilder` members:

- `Task<XDocument> ProcessAsync(FeedContext context, Func<Task> populate)` owns the root XML document and must call `populate`.
- `FeedItem<TItem> AddItem<TItem>(FeedContext context, TItem item)` creates an entry.
- `void AddProperty(FeedContext context, FeedItem feedItem, XElement element)` appends a feed-level property when `feedItem` is `null`, or an item-level one otherwise. The `AddProperty(context, feedItem, name, value)` extension builds the `XElement` for you.

Built-in item builders and list queries special-case `context.Format == "rss"`; for other formats they fall back to `AddProperty` calls, so make your builder handle `title`, `link`, `description`, and `pubDate` properties.

### Feed Pipeline

1. `FeedController` creates a `FeedContext` (with the request `IUpdateModel` and `format`).
2. It chooses the best `IFeedBuilderProvider`, or returns `404`.
3. It chooses the best `IFeedQueryProvider`, or returns `404`.
4. The builder's `ProcessAsync` runs the selected query (channel data and items), then the `IFeedItemBuilder`.
5. Registered contextualizers (`context.Response.Contextualize(...)`) resolve request-dependent URLs with the MVC `Url` helper.
6. The generated `XDocument` is returned as `text/xml`.

Avoid building absolute URLs before contextualization. Add a `FeedResponse.Contextualize` callback when the URL depends on the current request scheme or host.

### Troubleshooting

| Symptom | Check |
|---|---|
| Feed returns `404` | Verify a builder matches the `format` (route default or query string) and a query provider matches the request. |
| Atom is not available | Atom is not built in; register a custom builder provider. |
| RSS has no items | Ensure the query calls `AddItem` with `ContentItem` items and that `OrchardCore.Contents` is enabled for `CommonFeedItemBuilder`. |
| Items lost titles and links | A custom `IFeedItemBuilder` registration replaced `CommonFeedItemBuilder`. |
| Links are empty or incorrect | Generate them in a response contextualizer using the request-aware URL helper. |
| List feed missing | Check `DisableRssFeed` on the list and that both Lists and Feeds are enabled. |

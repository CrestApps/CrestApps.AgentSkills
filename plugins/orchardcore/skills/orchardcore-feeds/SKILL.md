---
name: orchardcore-feeds
description: Skill for exposing Orchard Core content through extensible XML feeds. Covers RSS feed selection, IFeedQueryProvider and IFeedQuery implementations, IFeedItemBuilder population, FeedContext response construction, Lists feed metadata, and custom feed format builders. Use this skill when requests mention Orchard Core Feeds, RSS feeds, Atom feeds, IFeedItemBuilder, feed queries, list feeds, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Feeds, IFeedBuilder, IFeedBuilderProvider, IFeedQueryProvider, IFeedQuery, IFeedItemBuilder, FeedContext, FeedMetadata, and ListFeedQuery. It also helps with feature recipes, RSS URLs, custom format providers, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Feeds - Prompt Templates

## Build Extensible Content Feeds

You are an Orchard Core expert. Generate feed configuration and provider code using the Orchard Core feeds abstractions.

### Guidelines

- Enable the `OrchardCore.Feeds` feature. The module has no declared feature dependencies.
- The release/3.0 module registers an RSS 2.0 builder for the `rss` format. It does not include a built-in Atom provider.
- Use `IFeedQueryProvider` to match a request and `IFeedQuery` to add source items and channel metadata to `FeedContext`.
- Use `IFeedItemBuilder` to populate common item elements after the selected query has added feed items.
- A provider returns a priority. The controller selects the matching format provider and query provider with the highest priority.
- `FeedController` returns XML and routes requests through the normal Orchard Core module routing conventions. The requested format is supplied as `format`.
- `OrchardCore.Lists` integrates when both Lists and Feeds are enabled. `ListFeedQuery` supplies list items and `CommonFeedItemBuilder` creates standard RSS item properties.
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

For a `ContentItem`, `CommonFeedItemBuilder` supplies `title`, `link`, CDATA `description`, `pubDate`, and a permanent-link `guid`. It generates links through MVC URL generation after the feed has been populated.

### List Feed Metadata

On a list content item, the Lists integration stores these fields under `ListPart`:

| Field | Effect |
|---|---|
| `DisableRssFeed` | Prevents `ListFeedQuery` from serving the list as RSS. |
| `FeedProxyUrl` | Supplies a proxy URL through `FeedMetadata` for a feed-aware consumer. |
| `FeedItemsCount` | Limits the number of list items included by the Lists feed query. |

Configure them from the list feed editor rather than duplicating feed logic in a display driver.

### Implementing a Custom Feed Query

Implement both interfaces in one scoped service when a custom URL should select a particular set of content.

```csharp
using OrchardCore.Feeds;
using OrchardCore.Feeds.Models;

namespace MyModule.Feeds;

public sealed class FeaturedFeedQuery : IFeedQueryProvider, IFeedQuery
{
    public Task<FeedQueryMatch> MatchAsync(FeedContext context)
    {
        if (!string.Equals(context.Format, "featured-rss", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<FeedQueryMatch>(null);
        }

        return Task.FromResult(new FeedQueryMatch
        {
            Priority = 10,
            FeedQuery = this,
        });
    }

    public Task ExecuteAsync(FeedContext context)
    {
        context.Builder.AddProperty(context, null, "title", "Featured products");
        context.Builder.AddProperty(context, null, "description", "Current featured products.");

        return Task.CompletedTask;
    }
}
```

The query must add feed items with `context.Builder.AddItem(context, item)` before `IFeedItemBuilder.PopulateAsync` runs.

### Registering a Query Provider

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Feeds;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IFeedQueryProvider, FeaturedFeedQuery>();
    }
}
```

### Adding a Custom Atom Format

The built-in provider is RSS-only. An Atom implementation should provide an `IFeedBuilderProvider` that matches a distinct format, then an `IFeedBuilder` that creates the Atom document and its entries.

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

`IFeedBuilder.ProcessAsync` owns the root XML document, `AddItem<TItem>` creates an entry, and `AddProperty` appends either a feed-level property when the item is `null` or an item-level property otherwise.

### Feed Pipeline

1. `FeedController` creates a `FeedContext` from the requested `format`.
2. It chooses the best `IFeedBuilderProvider`.
3. It chooses the best `IFeedQueryProvider`.
4. The selected query adds channel data and source items.
5. Every registered `IFeedItemBuilder` populates items.
6. Registered contextualizers resolve request-dependent URLs.
7. The generated `XDocument` is returned as `text/xml`.

Avoid building absolute URLs before contextualization. Add a `FeedResponse.Contextualize` callback when the URL depends on the current request scheme or host.

### Troubleshooting

| Symptom | Check |
|---|---|
| Feed returns `404` | Verify a builder and query provider both match the requested format. |
| Atom is not available | Atom is not built in for release/3.0; register a custom builder provider. |
| RSS has no items | Ensure the query calls `AddItem` and that an item builder is registered for its item type. |
| Links are empty or incorrect | Generate them in a response contextualizer using the request-aware URL helper. |

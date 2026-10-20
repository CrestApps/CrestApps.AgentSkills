---
name: orchardcore-sitemaps
description: Skill for configuring sitemaps in Orchard Core. Covers sitemaps with content type and custom path sources, SitemapPart per-item control, localized hreflang sitemaps, sitemap indexes, the Sitemaps recipe and deployment step with type discriminators, caching and the cache cleanup task, robots.txt integration with SitemapsRobotsSettings, the ManageSitemaps permission, IRouteableContentTypeProvider, and decoupled Razor Pages. Use this skill when requests mention Orchard Core Sitemaps, sitemap.xml, sitemap indexes, Skip and Take sitemap splitting, change frequency, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Sitemaps, OrchardCore.Sitemaps.RazorPages, OrchardCore.Sitemaps.Cleanup, OrchardCore.ContentLocalization.Sitemaps, SitemapsPermissions, ContentTypesSitemapSource, CustomPathSitemapSource, SitemapIndexSource, SitemapPart, and SitemapsRazorPagesOptions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Sitemaps - Prompt Templates

## Configure Sitemaps and Sitemap Indexes

You are an Orchard Core expert. Generate sitemap configurations, sitemap index setups, and SitemapPart configurations for Orchard Core.

### Guidelines

- Enable `OrchardCore.Sitemaps` (depends on `OrchardCore.Contents`) for XML sitemap generation. Manage them under **Tools > Search Engine Optimization > Sitemaps / Sitemap Indexes / Sitemaps Cache**; this requires the `ManageSitemaps` permission (`SitemapsPermissions.ManageSitemaps`).
- Sitemaps are configured by creating a Sitemap and adding **Sitemap Sources**: **Content Types** (`ContentTypesSitemapSource`) or **Custom Path** (`CustomPathSitemapSource`) for a fixed URL.
- Sitemap paths must end in `.xml` (e.g., `sitemap.xml`) and are unique; the admin validates this, recipes do not, so always use `.xml` in recipes too.
- Only content types with `AutoroutePart` are listed for inclusion by default. To include content routed another way, implement `IRouteableContentTypeProvider`.
- Add `SitemapPart` to a content type for per-item control (override priority, change frequency, or exclude items). It is optional.
- Priority is stored as an integer `0`-`10` (`5` renders as `0.5`).
- Google and Bing limit sitemaps to 50,000 items or 10 MB, whichever comes first. Use **Limit items** with **Skip** and **Take**, then combine the sitemaps in a sitemap index.
- Enable `OrchardCore.ContentLocalization.Sitemaps` (**Localized Content Item Sitemaps**) for `hreflang` alternates of localized items.
- Enable `OrchardCore.Sitemaps.RazorPages` (**Sitemaps for Decoupled Razor Pages**) for Razor Page routes.
- Sitemaps are cached per tenant in `wwwroot/{tenant}/sm-cache` and auto-cleared on publish. `OrchardCore.Sitemaps.Cleanup` adds the **Sitemap Cache Cleaner** background task (every 5 minutes) that removes stale cache files.
- When both `OrchardCore.Seo` and `OrchardCore.Sitemaps` are enabled, sitemap and sitemap index URLs are added to `robots.txt`.
- Sitemaps are served through a dynamic controller route (`/{**sitemap}`). In Orchard Core 4.0 dynamic routes no longer break named link generation for `[EndpointName]` API actions.
- Always seal classes.

### Enabling Sitemap Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sitemaps"
      ],
      "disable": []
    }
  ]
}
```

### Enabling Localized Sitemaps

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Sitemaps",
        "OrchardCore.ContentLocalization",
        "OrchardCore.ContentLocalization.Sitemaps"
      ],
      "disable": []
    }
  ]
}
```

### The `Sitemaps` Recipe Step

The `Sitemaps` step (also produced by the **All Sitemaps** deployment step) takes a `data` array of polymorphic sitemap documents. Every sitemap and every source needs a `$type` discriminator:

| Object | `$type` |
|---|---|
| Sitemap | `OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions` |
| Sitemap index | `OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps` |
| Content types source | `OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions` |
| Custom path source | `OrchardCore.Sitemaps.Models.CustomPathSitemapSource, OrchardCore.Sitemaps.Abstractions` |
| Index source | `OrchardCore.Sitemaps.Models.SitemapIndexSource, OrchardCore.Sitemaps` |

The easiest way to get exact JSON is to configure sitemaps in the admin and export them with a deployment plan.

### Creating a Sitemap via Recipe

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "data": [
        {
          "$type": "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
          "SitemapId": "pagessitemap",
          "Name": "Pages",
          "Path": "sitemap-pages.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "$type": "OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions",
              "Id": "pagessource",
              "IndexAll": false,
              "LimitItems": false,
              "ChangeFrequency": "Daily",
              "Priority": 5,
              "ContentTypes": [
                {
                  "ContentTypeName": "Page",
                  "ChangeFrequency": "Weekly",
                  "Priority": 8
                },
                {
                  "ContentTypeName": "BlogPost",
                  "ChangeFrequency": "Daily",
                  "Priority": 6
                }
              ]
            },
            {
              "$type": "OrchardCore.Sitemaps.Models.CustomPathSitemapSource, OrchardCore.Sitemaps.Abstractions",
              "Id": "contactsource",
              "Path": "contact",
              "ChangeFrequency": "Monthly",
              "Priority": 4
            }
          ]
        }
      ]
    }
  ]
}
```

`ContentTypesSitemapSource` properties: `IndexAll` (default `true`, uses the source-level `ChangeFrequency`/`Priority` for all routable types), `ContentTypes` (per-type entries when `IndexAll` is `false`), `LimitItems`, and `LimitedContentType` (`ContentTypeName`, `ChangeFrequency`, `Priority`, `Skip`, `Take`) when `LimitItems` is `true`.

### Creating a Sitemap Index via Recipe

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "data": [
        {
          "$type": "OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps",
          "SitemapId": "mainindex",
          "Name": "Sitemap Index",
          "Path": "sitemap.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "$type": "OrchardCore.Sitemaps.Models.SitemapIndexSource, OrchardCore.Sitemaps",
              "Id": "mainindexsource",
              "ContainedSitemapIds": [
                "pagessitemap",
                "articles1",
                "articles2"
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

### Splitting Large Sitemaps with Skip and Take

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "data": [
        {
          "$type": "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
          "SitemapId": "articles1",
          "Name": "Articles 1",
          "Path": "sitemap-articles-1.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "$type": "OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions",
              "Id": "articles1source",
              "IndexAll": false,
              "LimitItems": true,
              "LimitedContentType": {
                "ContentTypeName": "Article",
                "ChangeFrequency": "Weekly",
                "Priority": 5,
                "Skip": 0,
                "Take": 50000
              }
            }
          ]
        },
        {
          "$type": "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
          "SitemapId": "articles2",
          "Name": "Articles 2",
          "Path": "sitemap-articles-2.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "$type": "OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions",
              "Id": "articles2source",
              "IndexAll": false,
              "LimitItems": true,
              "LimitedContentType": {
                "ContentTypeName": "Article",
                "ChangeFrequency": "Weekly",
                "Priority": 5,
                "Skip": 50000,
                "Take": 50000
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Sitemap Change Frequency Values

`ChangeFrequency` is an enum serialized as a string: `Always`, `Hourly`, `Daily`, `Weekly`, `Monthly`, `Yearly`, `Never`. It is written lowercase in the XML (`<changefreq>daily</changefreq>`).

### SitemapPart

Attach `SitemapPart` to a content type for per-item sitemap control. Its stored properties:

| Property | Description |
|---------|-------------|
| `OverrideSitemapConfig` | Enable per-item overrides. |
| `Exclude` | Exclude the content item from the sitemap entirely. |
| `Priority` | Per-item priority (`0`-`10`, default `5`). |
| `ChangeFrequency` | Per-item change frequency (default `Daily`). |

### Attaching SitemapPart via Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class SitemapMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SitemapMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentTypeName}}", type => type
            .WithPart("SitemapPart", part => part
                .WithPosition("10")
            )
        );

        return 1;
    }
}
```

### Attaching SitemapPart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "{{ContentTypeName}}",
          "DisplayName": "{{DisplayName}}",
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "SitemapPart",
              "Name": "SitemapPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "10"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Including Content Routed Without Autoroute

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Sitemaps.Builders;
using OrchardCore.Sitemaps.Services;

namespace MyModule.Sitemaps;

public sealed class ProductRouteableContentTypeProvider : IRouteableContentTypeProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductRouteableContentTypeProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<ContentTypeDefinition>> ListRoutableTypeDefinitionsAsync()
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync("Product");

        return definition is null ? [] : [definition];
    }

    public Task<string> GetRouteAsync(SitemapBuilderContext context, ContentItem contentItem)
    {
        if (contentItem.ContentType != "Product")
        {
            return Task.FromResult<string>(null);
        }

        return Task.FromResult(context.HostPrefix + context.UrlHelper.Content($"~/products/{contentItem.ContentItemId}"));
    }
}
```

Register it with `services.AddScoped<IRouteableContentTypeProvider, ProductRouteableContentTypeProvider>();`.

### Configuring Sitemaps for Decoupled Razor Pages

Enable `OrchardCore.Sitemaps.RazorPages`, then configure routes in `Program.cs`:

```csharp
builder.Services.Configure<SitemapsRazorPagesOptions>(options =>
{
    options.ConfigureContentType("{{ContentTypeName}}", o =>
    {
        o.PageName = "{{RazorPageName}}";
        o.RouteValues = (contentItem) => new
        {
            area = "OrchardCore.Sitemaps",
            slug = contentItem.ContentItemId
        };
    });
});
```

Always include the `area` in the route values.

### Robots.txt Integration

When both `OrchardCore.Seo` and `OrchardCore.Sitemaps` are enabled and no static `robots.txt` exists on the filesystem, sitemap index and sitemap URLs are added to the generated `robots.txt`. Toggle this at **Settings > Search > Search Engine Optimization > Robots** or with the `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "SitemapsRobotsSettings": {
        "IncludeSitemaps": true
      }
    }
  ]
}
```

### Sitemap Caching

- Sitemaps are cached in `wwwroot/{tenant}/sm-cache` and cleared automatically when content items are published or sitemaps change.
- Manually clear the cache at **Tools > Search Engine Optimization > Sitemaps Cache**, or enable `OrchardCore.Sitemaps.Cleanup` to remove orphaned cache files periodically.

### Upgrading from 3.x

- `OrchardCore.Sitemaps.Permissions.ManageSitemaps` was removed. Use `OrchardCore.Sitemaps.SitemapsPermissions.ManageSitemaps` (in `OrchardCore.Sitemaps.Abstractions`). Role claims are unchanged.
- The sitemap and sitemap index admin lists use the shared `AdminList` shape (List/Grid layouts, page size selection) and breadcrumbs; re-check overridden admin list views.

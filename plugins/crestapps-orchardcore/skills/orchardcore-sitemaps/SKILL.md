---
name: orchardcore-sitemaps
description: Skill for configuring sitemaps in Orchard Core. Covers sitemap creation, content type sources, SitemapPart for per-item control, localized sitemaps, sitemap index generation, caching, robots.txt integration, and sitemap configuration for decoupled Razor Pages. Use this skill when requests mention Orchard Core Sitemaps, Configure Sitemaps and Sitemap Indexes, Enabling Sitemap Features, Enabling Localized Sitemaps, Creating a Sitemap via Recipe, Creating a Sitemap Index via Recipe, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Sitemaps, OrchardCore.ContentManagement.Metadata, OrchardCore.Data.Migration, OrchardCore.Seo, SitemapPart, AutoroutePart, IRouteableContentTypeProvider. It also helps with Creating a Sitemap via Recipe, Creating a Sitemap Index via Recipe, Sitemap Change Frequency Values, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Sitemaps - Prompt Templates

## Configure Sitemaps and Sitemap Indexes

You are an Orchard Core expert. Generate sitemap configurations, sitemap index setups, and SitemapPart configurations for Orchard Core.

### Guidelines

- Enable `OrchardCore.Sitemaps` for XML sitemap generation.
- Sitemaps are configured by creating a Sitemap and adding **Sitemap Sources**.
- Sitemap paths must end in `.xml` (e.g., `sitemap.xml`).
- Only content types with `AutoroutePart` are listed for inclusion by default.
- To include content without `AutoroutePart`, implement `IRouteableContentTypeProvider`.
- Use **Content Types Source** to generate sitemap entries for content items.
- Add `SitemapPart` to a content type for per-item control (override priority, change frequency, or exclude items).
- `SitemapPart` is optional; content types can appear in sitemaps without it.
- Google and Bing limit sitemaps to 50,000 items or 10 MB, whichever comes first.
- Use **Skip** and **Take** with sitemap indexes to split large sitemaps.
- Enable `Localized Content Items Sitemap` feature for `hreflang` support.
- Enable `Sitemaps for Decoupled Razor Pages` for Razor Page routes.
- Sitemaps are cached per-tenant in `wwwroot/sm-cache` and auto-cleared on publish.
- When both `SEO` and `Sitemaps` features are enabled, sitemaps are automatically added to `robots.txt`.
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
        "OrchardCore.Sitemaps.LocalizedContentItems"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Sitemap via Recipe

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "Sitemaps": [
        {
          "SitemapId": "{{UniqueSitemapId}}",
          "Name": "{{SitemapName}}",
          "Path": "sitemap.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "Type": "SitemapSource",
              "ContentTypes": [
                {
                  "ContentTypeName": "{{ContentTypeName}}",
                  "ChangeFrequency": "daily",
                  "Priority": 5
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

### Creating a Sitemap Index via Recipe

Use a sitemap index to aggregate multiple sitemaps:

```json
{
  "steps": [
    {
      "name": "SitemapIndexes",
      "SitemapIndexes": [
        {
          "SitemapIndexId": "{{UniqueIndexId}}",
          "Name": "Sitemap Index",
          "Path": "sitemap.xml",
          "Enabled": true,
          "ContainedSitemapIds": [
            "{{SitemapId1}}",
            "{{SitemapId2}}"
          ]
        }
      ]
    }
  ]
}
```

### Sitemap Change Frequency Values

| Value | Description |
|-------|-------------|
| `always` | Content changes on every access. |
| `hourly` | Updated every hour. |
| `daily` | Updated once a day. |
| `weekly` | Updated once a week. |
| `monthly` | Updated once a month. |
| `yearly` | Updated once a year. |
| `never` | Archived content that will not change. |

### SitemapPart

Attach `SitemapPart` to a content type for per-item sitemap control:

| Setting | Description |
|---------|-------------|
| Override Sitemap Config | Check to enable per-item overrides. |
| Exclude | Exclude the content item from the sitemap entirely. |
| Priority | Override the default priority for this specific item. |
| Change Frequency | Override the default change frequency for this specific item. |

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

### Splitting Large Sitemaps with Skip and Take

For content types with more than 50,000 items, split across multiple sitemaps and combine them in an index:

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "Sitemaps": [
        {
          "SitemapId": "articles-part1",
          "Name": "Articles Part 1",
          "Path": "sitemap-articles-1.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "Type": "SitemapSource",
              "LimitItems": true,
              "ContentTypes": [
                {
                  "ContentTypeName": "Article",
                  "ChangeFrequency": "weekly",
                  "Priority": 5
                }
              ],
              "Skip": 0,
              "Take": 50000
            }
          ]
        },
        {
          "SitemapId": "articles-part2",
          "Name": "Articles Part 2",
          "Path": "sitemap-articles-2.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "Type": "SitemapSource",
              "LimitItems": true,
              "ContentTypes": [
                {
                  "ContentTypeName": "Article",
                  "ChangeFrequency": "weekly",
                  "Priority": 5
                }
              ],
              "Skip": 50000,
              "Take": 50000
            }
          ]
        }
      ]
    },
    {
      "name": "SitemapIndexes",
      "SitemapIndexes": [
        {
          "SitemapIndexId": "main-index",
          "Name": "Main Sitemap Index",
          "Path": "sitemap.xml",
          "Enabled": true,
          "ContainedSitemapIds": [
            "articles-part1",
            "articles-part2"
          ]
        }
      ]
    }
  ]
}
```

### Configuring Sitemaps for Decoupled Razor Pages

Enable `Sitemaps for Decoupled Razor Pages`, then configure routes in `Program.cs`:

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

### Robots.txt Integration

When both `OrchardCore.Seo` and `OrchardCore.Sitemaps` are enabled and no static `robots.txt` exists on the filesystem, sitemap URLs are automatically added to the dynamically generated `robots.txt`. Configure this at **Settings** → **Search** → **Search Engine Optimization** → **Robots**.

### Sitemap Caching

- Sitemaps are cached in `wwwroot/sm-cache` per tenant.
- Cache is automatically cleared when content items are published.
- Manually clear cache at **Tools** → **Search Engine Optimization** → **Sitemaps Cache**.

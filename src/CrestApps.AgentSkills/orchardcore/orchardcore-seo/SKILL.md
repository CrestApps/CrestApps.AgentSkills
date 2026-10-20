---
name: orchardcore-seo
description: Skill for configuring SEO in Orchard Core. Covers SeoMetaPart for meta tags and social sharing, SeoMetaPartSettings, SeoAspect fallbacks, SocialMetaSettings site defaults, canonical URLs, absolute and CDN social image URLs, robots.txt with RobotsSettings and IRobotsProvider, the ManageSeoSettings permission, JSON-LD structured data, page titles, and SEO-friendly URL patterns, plus how sitemaps fit in. Use this skill when requests mention Orchard Core SEO, SeoMetaPart, meta descriptions, Open Graph, Twitter cards, Google schema, canonical URLs, robots.txt, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Seo, OrchardCore.Seo.Models, SeoPermissions, SeoAspect, RobotsSettings, IRobotsProvider, OrchardCore.Sitemaps, and OrchardCore.Autoroute, plus migrations, recipes, and examples.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core SEO - Prompt Templates

## Configure SEO Features

You are an Orchard Core expert. Generate code and configuration for SEO optimization in Orchard Core.

### Guidelines

- The `OrchardCore.Seo` module provides `SeoMetaPart` for managing the page title, meta tags, canonical URL, Open Graph, Twitter Card, and Google schema (JSON-LD) per content item, plus a settings-driven `robots.txt`.
- The `OrchardCore.Sitemaps` module handles XML sitemap generation and sitemap indexes; see `orchardcore-sitemaps` for its recipe step format.
- Attach `SeoMetaPart` to any routable content type that needs meta tag control, alongside `AutoroutePart` for SEO-friendly URLs.
- SEO tags are rendered only for the primary content item displayed with the `Detail` display type, and never for widgets (stereotyped items).
- SEO settings (Robots) require the `ManageSeoSettings` permission (`OrchardCore.Seo.SeoPermissions.ManageSeoSettings`, granted to Administrator).
- Use recipe steps to provision SEO settings and sitemaps across environments.
- Always seal classes.

### Enabling SEO and Sitemap Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Seo",
        "OrchardCore.Sitemaps",
        "OrchardCore.Autoroute"
      ],
      "disable": []
    }
  ]
}
```

## SeoMetaPart Configuration

### Attaching SeoMetaPart to a Content Type via Migration

```csharp
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Seo.Models;

namespace MyModule;

public sealed class SeoMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SeoMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentTypeName}}", type => type
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ Model.ContentItem | display_text | slugify }}",
                })
            )
            .WithPart("SeoMetaPart", part => part
                .WithPosition("5")
                .WithSettings(new SeoMetaPartSettings
                {
                    DisplayKeywords = true,
                    DisplayCustomMetaTags = true,
                    DisplayOpenGraph = true,
                    DisplayTwitter = true,
                    DisplayGoogleSchema = true,
                })
            )
        );

        return 1;
    }
}
```

### Attaching SeoMetaPart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "{{ContentTypeName}}",
          "DisplayName": "{{DisplayName}}",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "AutoroutePart",
              "Name": "AutoroutePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                },
                "AutoroutePartSettings": {
                  "AllowCustomPath": true,
                  "Pattern": "{{ Model.ContentItem | display_text | slugify }}"
                }
              }
            },
            {
              "PartName": "SeoMetaPart",
              "Name": "SeoMetaPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "5"
                },
                "SeoMetaPartSettings": {
                  "DisplayKeywords": true,
                  "DisplayCustomMetaTags": true,
                  "DisplayOpenGraph": true,
                  "DisplayTwitter": true,
                  "DisplayGoogleSchema": true
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

### SeoMetaPartSettings

The `Display*` settings (`DisplayKeywords`, `DisplayCustomMetaTags`, `DisplayOpenGraph`, `DisplayTwitter`, `DisplayGoogleSchema`) only control which editor tabs/fields are shown; they default to `false`.

### SeoMetaPart Fields

| Field | Description |
|-------|-------------|
| `Render` | Default `true`. Set to `false` to suppress all SEO output for the item. |
| `PageTitle` | Replaces the whole `<title>` (fixed title, shortcodes processed). |
| `MetaDescription` | `<meta name="description">`. |
| `MetaKeywords` | `<meta name="keywords">`. |
| `Canonical` | Custom `<link rel="canonical">`. |
| `MetaRobots` | Robots directives (e.g., `noindex, nofollow`). |
| `CustomMetaTags` | Array of `MetaEntry` (`Name`, `Property`, `Content`, `HttpEquiv`, `Charset`). |
| `DefaultSocialImage` | `MediaField` used for both Open Graph and Twitter when their own image is empty. |
| `OpenGraphType` | `og:type` (e.g., `website`, `article`). |
| `OpenGraphTitle` / `OpenGraphDescription` | `og:title` / `og:description`. |
| `OpenGraphImage` | `MediaField` for `og:image` (alt text from the media text). |
| `TwitterCard` | `twitter:card` (`summary`, `summary_large_image`). |
| `TwitterTitle` / `TwitterDescription` | `twitter:title` / `twitter:description`. |
| `TwitterImage` | `MediaField` for `twitter:image`. |
| `TwitterCreator` / `TwitterSite` | `twitter:creator` / `twitter:site` handles. |
| `GoogleSchema` | JSON-LD string (shortcodes processed, must be valid JSON). |

## Fallbacks and Site Defaults

`SeoMetaPartHandler` builds a `SeoAspect` from the part, then a final handler fills missing values:

- Canonical: when empty, the absolute display URL of the item (from its route, e.g. `AutoroutePart`).
- Open Graph and Twitter title/description fall back to `PageTitle` and `MetaDescription`.
- Images fall back to `DefaultSocialImage`. Image URLs are mapped through the media store and made absolute; when media is served from a CDN (`MapPathToPublicUrl` already returns `https://...`), the CDN URL is used as-is (fixed in 4.0, it used to be prefixed with the site URL).
- Site-wide defaults are read from a `SocialMetaSettings` custom settings content item when one exists (create it with the Custom Settings feature, a `SocialMetaSettings` part with fields such as `DefaultSocialImage`, `OpenGraphImage`, `TwitterImage`, `DefaultMetaDescription`, `OpenGraphType`, `OpenGraphSiteName`, `OpenGraphAppId`, `TwitterCard`, `TwitterCreator`, `TwitterSite`, and `GoogleSchema`).

Custom code can read the computed values with `await contentManager.PopulateAspectAsync<SeoAspect>(contentItem)`.

## Canonical URLs

- Leave `SeoMetaPart.Canonical` empty to use the item's own absolute URL.
- Set a custom canonical URL only when multiple URLs resolve to the same content (e.g., syndicated copies).
- Use absolute URLs (scheme and host).

## Social Media Meta Tags

### Open Graph Configuration

Open Graph tags control how content appears when shared on Facebook, LinkedIn, and other platforms. `og:url`, `og:site_name`, and `og:locale` are emitted from the item URL, site defaults, and current culture.

### Twitter Card Configuration

Set `TwitterCard` to `summary` (small thumbnail) or `summary_large_image` (large banner). Empty Twitter title and description fall back to the page title and meta description (not to the Open Graph values).

## Structured Data with JSON-LD

The `GoogleSchema` field accepts raw JSON-LD (an object or an array). It is rendered in a `<script type="application/ld+json">` block. Invalid JSON is not rendered.

```json
{
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "{{ArticleTitle}}",
  "description": "{{ArticleDescription}}",
  "author": {
    "@type": "Person",
    "name": "{{AuthorName}}"
  },
  "datePublished": "{{PublishedDate}}",
  "image": "{{ImageUrl}}"
}
```

## Robots.txt

`OrchardCore.Seo` serves a dynamic `/robots.txt` through endpoint routing (so the `OrchardCore.RateLimits` limiter applies). Edit it at **Settings > Search > Search Engine Optimization > Robots** or with the `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "RobotsSettings": {
        "AllowAllAgents": true,
        "DisallowAdmin": true,
        "AdditionalRules": "Disallow: /private/"
      }
    }
  ]
}
```

| Property | Purpose |
|---|---|
| `AllowAllAgents` | Writes `User-agent: *` (default `true`). |
| `DisallowAdmin` | Adds `Disallow: /{AdminUrlPrefix}` (default `true`). |
| `AdditionalRules` | Appended verbatim. |

A physical `robots.txt` in the tenant's static files takes precedence. With Sitemaps enabled, sitemap URLs are added by its robots provider (`SitemapsRobotsSettings.IncludeSitemaps`). Add your own lines by implementing `IRobotsProvider`:

```csharp
using OrchardCore.Seo;

namespace MyModule.Seo;

public sealed class AiCrawlersRobotsProvider : IRobotsProvider
{
    public Task<string> GetContentAsync()
        => Task.FromResult("User-agent: GPTBot\nDisallow: /");
}
```

```csharp
services.AddTransient<IRobotsProvider, AiCrawlersRobotsProvider>();
```

## Sitemaps

Sitemaps are created under **Tools > Search Engine Optimization > Sitemaps** or with the `Sitemaps` recipe step, which uses `data` with `$type` discriminators. See `orchardcore-sitemaps` for complete recipes, sitemap indexes, and Skip/Take splitting.

## SEO-Friendly URL Patterns with AutoroutePart

| Pattern | Example Output |
|---------|---------------|
| `{{ Model.ContentItem \| display_text \| slugify }}` | `my-blog-post` |
| `blog/{{ Model.ContentItem \| display_text \| slugify }}` | `blog/my-blog-post` |
| `{{ Model.ContentItem.CreatedUtc \| date: '%Y/%m' }}/{{ Model.ContentItem \| display_text \| slugify }}` | `2025/01/my-blog-post` |
| `products/{{ Model.ContentItem.Content.ProductPart.Category.Text \| slugify }}/{{ Model.ContentItem \| display_text \| slugify }}` | `products/electronics/wireless-headphones` |

See `orchardcore-autoroute` for settings, permalink numbering, and the home route.

## Page Title Customization

`SeoMetaPart.PageTitle` sets a fixed title that replaces all title segments. When empty, the layout renders the title segments (item display text plus site name). Control the format in the theme layout:

```cshtml
<title>@RenderTitleSegments(Site.SiteName, "before")</title>
```

```liquid
<title>{% page_title Site.SiteName, position: "before", separator: " - " %}</title>
```

## Custom Meta Tags in a Content Item Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "{{ContentTypeName}}",
          "ContentItemId": "{{ContentItemId}}",
          "DisplayText": "{{PageTitle}}",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "{{PageTitle}}"
          },
          "SeoMetaPart": {
            "Render": true,
            "PageTitle": "{{CustomBrowserTitle}}",
            "MetaDescription": "{{Description}}",
            "MetaKeywords": "{{Keywords}}",
            "Canonical": "https://{{YourDomain}}/{{CanonicalPath}}",
            "MetaRobots": "index, follow",
            "OpenGraphType": "article",
            "OpenGraphTitle": "{{OgTitle}}",
            "OpenGraphDescription": "{{OgDescription}}",
            "OpenGraphImage": {
              "Paths": ["{{OgImagePath}}"]
            },
            "TwitterCard": "summary_large_image",
            "TwitterTitle": "{{TwitterTitle}}",
            "TwitterDescription": "{{TwitterDescription}}",
            "TwitterImage": {
              "Paths": ["{{TwitterImagePath}}"]
            },
            "TwitterCreator": "@{{TwitterHandle}}",
            "TwitterSite": "@{{SiteTwitterHandle}}",
            "GoogleSchema": "{\"@context\":\"https://schema.org\",\"@type\":\"WebPage\",\"name\":\"{{PageTitle}}\"}",
            "CustomMetaTags": [
              {
                "Name": "author",
                "Content": "{{AuthorName}}"
              },
              {
                "Name": "theme-color",
                "Content": "#{{HexColor}}"
              }
            ]
          }
        }
      ]
    }
  ]
}
```

## Upgrading from 3.x

- `OrchardCore.Seo.SeoPermissionProvider.ManageSeoSettings` was removed. Use `OrchardCore.Seo.SeoPermissions.ManageSeoSettings` (in `OrchardCore.Seo.Abstractions`). Role claims are unchanged.
- Social image URLs served from a CDN are now emitted as-is instead of being concatenated with the site base URL.

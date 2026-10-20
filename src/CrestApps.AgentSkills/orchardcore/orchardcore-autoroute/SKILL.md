---
name: orchardcore-autoroute
description: Skill for configuring AutoroutePart and the home route in Orchard Core. Covers Liquid URL patterns, AutoroutePartSettings, SEO-friendly slugs, permalink validation and numbering, container and contained item routing, the slug content handle, home page routing with the HomeRoute module and SetHomepage permission, route conflicts, and programmatic lookups with IAutorouteEntries. Use this skill when requests mention Orchard Core AutoroutePart, permalinks, slugs, slug handles, Set as homepage, the HomeRoute site setting, AutoroutePartSettings, container routing, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Autoroute, OrchardCore.HomeRoute, OrchardCore.ContentManagement.Routing, AutoroutePart, AutoroutePartSettings, AutoroutePartIndex, AutoroutePermissions, IAutorouteEntries, and TitlePart, plus migrations, recipes, and Liquid URL patterns.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AutoroutePart - Prompt Templates

## Configure AutoroutePart

You are an Orchard Core expert. Generate code and configuration for AutoroutePart URL routing.

### Guidelines

- AutoroutePart generates SEO-friendly URLs (permalinks) for content items based on Liquid patterns.
- The default URL pattern is `{{ ContentItem.DisplayText | slugify }}` (equivalent to `{{ ContentItem | display_text | slugify }}`), which derives the slug from the content item's display text.
- AutoroutePart requires `TitlePart` (or another display text source) to be attached to the content type for the display text to resolve.
- Enable the `OrchardCore.Autoroute` feature before using AutoroutePart. It depends on `OrchardCore.ContentTypes` and `OrchardCore.HomeRoute`.
- The pattern is evaluated only when the path is empty (on create or update), in the culture of the content item. Changing the title does not change an existing permalink unless the editor clears it or uses **Update path** (`AllowUpdatePath`).
- Set `AllowCustomPath = true` to let editors override the generated URL with a custom path.
- Set `ShowHomepageOption = true` to allow editors to designate a content item as the site home page. The checkbox is only honored for users with the `SetHomepage` permission (`AutoroutePermissions.SetHomepage`, granted to Administrator by default).
- Permalinks are validated: they cannot be `/`, cannot contain spaces, `//`, or any of `: ? # [ ] @ ! $ & ' ( ) * + , . ; = < > \ | %`, and are limited to `AutoroutePart.MaxPathLength` (1024) characters. A permalink already used by another item fails validation with "Your permalink is already in use."
- When a generated slug conflicts with an existing route, or an item is cloned, Orchard Core appends a number (e.g., `my-post-1`, `my-post-2`). The free number is found with a single query against `AutoroutePartIndex`. A cloned item never keeps the homepage flag.
- Liquid patterns have access to `ContentItem` properties including `ContentType`, `DisplayText`, `CreatedUtc`, and all attached part and field data.
- Use the async migration APIs (`AlterTypeDefinitionAsync` in `CreateAsync()`); the synchronous `AlterTypeDefinition` overloads no longer exist.
- Use `IAutorouteEntries` (namespace `OrchardCore.ContentManagement.Routing`) for path lookups instead of querying the index directly.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### AutoroutePartSettings Properties

| Property | Type | Description |
|---|---|---|
| `Pattern` | `string` | Liquid template that generates the URL path. Default `{{ ContentItem.DisplayText \| slugify }}`. |
| `AllowCustomPath` | `bool` | When `true`, editors can manually enter a custom URL. |
| `ShowHomepageOption` | `bool` | When `true`, a checkbox appears to set the item as the site home page. |
| `AllowUpdatePath` | `bool` | When `true`, editors can request a regenerated path (clears the path so the pattern runs again). |
| `AllowDisabled` | `bool` | When `true`, editors can disable route generation for individual (typically contained) items. |
| `AllowRouteContainedItems` | `bool` | On a container type, lets editors turn on **Route Contained Items** (BagPart items, taxonomy terms). |
| `ManageContainedItemRoutes` | `bool` | On a contained type, lets its AutoroutePart control the contained item's route. |
| `AllowAbsolutePath` | `bool` | When `true`, a contained item's path can be absolute instead of relative to the container. |

`AutoroutePartSettings` lives in `OrchardCore.Autoroute.Models`. The part (`AutoroutePart`) stores `Path`, `SetHomepage`, `Disabled`, `RouteContainedItems`, and `Absolute`.

### Migration Pattern

```csharp
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace MyModule;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{ContentTypeName}}", type => type
            .DisplayedAs("{{DisplayName}}")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    AllowUpdatePath = true,
                    ShowHomepageOption = true,
                    Pattern = "{{ ContentItem | display_text | slugify }}",
                })
            )
        );

        return 1;
    }
}
```

### Enabling AutoroutePart via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Autoroute",
        "OrchardCore.Contents"
      ],
      "disable": []
    }
  ]
}
```

### Content Type Definition with AutoroutePart via Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Article",
          "DisplayName": "Article",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
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
                  "AllowUpdatePath": true,
                  "ShowHomepageOption": true,
                  "Pattern": "{{ ContentItem | display_text | slugify }}"
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

### Common Liquid URL Patterns

```text
# Simple slug from display text
{{ ContentItem | display_text | slugify }}

# Prefixed with content type
{{ ContentItem.ContentType | downcase }}/{{ ContentItem | display_text | slugify }}

# Date-based blog pattern
{{ ContentItem.CreatedUtc | date: '%Y' }}/{{ ContentItem.CreatedUtc | date: '%m' }}/{{ ContentItem | display_text | slugify }}

# ListPart child (e.g., BlogPost in a Blog) prefixed by the container
{{ ContentItem | container | display_text | slugify }}/{{ ContentItem | display_text | slugify }}

# A text field value
{{ ContentItem.Content.Product.Color.Text | slugify }}

# Category prefix from a taxonomy field
{% assign terms = ContentItem.Content.Article.Category.TermContentItemIds | terms %}
{% for term in terms %}{{ term | display_text | slugify }}/{% endfor %}{{ ContentItem | display_text | slugify }}
```

### Container and Contained Item Routing

AutoroutePart can route items stored inside a container document: `BagPart` items and taxonomy `Terms`.

1. Add AutoroutePart to the container type and enable `AllowRouteContainedItems`.
2. On the container item, check **Route Contained Items** (`RouteContainedItems`).
3. Optionally add AutoroutePart to the contained type with `ManageContainedItemRoutes = true` to give contained items friendly slugs (otherwise the path is the container path, then the contained `ContentItemId` and display text, e.g. `categories/47twnxzx9hs5k3dyn9j1mc5rny-travel`).
4. Use `AllowAbsolutePath` to let a contained item opt out of the container-relative path, and `AllowDisabled` when only some contained items (e.g., one of two BagParts) should be routed.

Contained paths are kept unique within the container by numbering. For BagPart routing, set the bag's display type to `Summary` and provide `Summary` and `Detail` templates.

### Slug Handle and Lookups

Items with an Autoroute can be resolved by the `slug:` content handle (`AutorouteConstants.SlugPrefix`) anywhere content handles are accepted.

```liquid
{% assign my_content = Content["slug:my-blog/my-blog-post"] %}
{% assign same_content = Content.Slug["my-blog/my-blog-post"] %}
```

```cshtml
@{
    var id = await Orchard.GetContentItemIdBySlugAsync("my-blog/my-blog-post");
    var item = await Orchard.GetContentItemBySlugAsync("my-blog/my-blog-post", latest: false);
}
```

Publishing, unpublishing, or removing an item evicts the `slug:{path}` cache tag. With `OrchardCore.Contents`/`OrchardCore.Widgets` enabled, items get `Content__Slug__{slug}` and `Widget__Slug__{slug}` shape alternates.

### Programmatic Route Management

Use `IAutorouteEntries` to look up route entries programmatically:

```csharp
using OrchardCore.ContentManagement.Routing;

namespace MyModule.Services;

public sealed class RouteService
{
    private readonly IAutorouteEntries _autorouteEntries;

    public RouteService(IAutorouteEntries autorouteEntries)
    {
        _autorouteEntries = autorouteEntries;
    }

    public async Task<string> GetContentItemIdByPathAsync(string path)
    {
        (var found, var entry) = await _autorouteEntries.TryGetEntryByPathAsync(path);

        return found ? entry.ContentItemId : null;
    }

    public async Task<string> GetPathAsync(string contentItemId)
    {
        (var found, var entry) = await _autorouteEntries.TryGetEntryByContentItemIdAsync(contentItemId);

        return found ? entry.Path : null;
    }
}
```

`AutorouteEntry` exposes `ContentItemId`, `Path`, `ContainedContentItemId`, and `JsonPath` (for contained items).

### Home Page Routing (`OrchardCore.HomeRoute`)

The `OrchardCore.HomeRoute` feature (an Autoroute dependency, category Infrastructure) maps a dynamic controller route on `/` that is resolved at request time from the `HomeRoute` site setting (a route value dictionary). Autoroute maps its own dynamic route on `/{any}/{**slug}`, so the home page never hits the Autoroute transformer.

- When `ShowHomepageOption` is enabled and a user with `SetHomepage` checks **Set as homepage**, publishing the item writes its route values into `HomeRoute`. Only one route can be the home page; setting a new one replaces the previous one. A permalink of `/` is rejected; use the homepage option instead.
- A module or theme can point the home route at any controller action by writing its route values.

Set it from a recipe with the `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "HomeRoute": {
        "Area": "OrchardCore.Contents",
        "Controller": "Item",
        "Action": "Display",
        "ContentItemId": "<content-item-id>"
      }
    }
  ]
}
```

Dynamic routes mapped by Autoroute, HomeRoute, and Sitemaps no longer break named link generation (`LinkGenerator.GetPathByName()`) for `[EndpointName]` API actions; Orchard Core 4.0 strips endpoint names from the non-routable placeholder endpoints.

### Upgrading from 3.x

- `OrchardCore.Autoroute.Permissions.SetHomepage` was removed. Use `OrchardCore.Autoroute.AutoroutePermissions.SetHomepage` (in `OrchardCore.Autoroute.Core`). Role claims are unchanged.
- Permalink numbering of a path with a dash that isn't followed by a number now starts at `1`: the first copy of `my-post` is `my-post-1` (it used to be `my-post-0`). Cloning items whose permalink is used thousands of times is no longer slow.

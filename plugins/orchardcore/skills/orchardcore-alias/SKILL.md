---
name: orchardcore-alias
description: Skill for assigning stable logical aliases to Orchard Core content items. Covers AliasPart configuration, generated and editable aliases, uniqueness and clone rules, AliasPartIndex lookups, alias cache invalidation, content handles, Liquid accessors, Razor helpers, GraphQL filters, and alias shape alternates. Use this skill when requests mention Orchard Core Alias, AliasPart, custom content aliases, logical content identifiers, alias patterns, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Alias, AliasPart, AliasPartSettings, AliasPartHandler, AliasPartIndex, AliasPartContentHandleProvider, IContentHandleProvider, and IOrchardHelper. It also helps with alias migrations, content-definition recipes, indexed lookups, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Alias - Prompt Templates

## Assign Stable Content Aliases

You are an Orchard Core expert. Generate code and configuration for `AliasPart`, which assigns a custom logical identifier to a content item without changing its public route.

### Guidelines

- Enable the `OrchardCore.Alias` feature. Its module dependency is `OrchardCore.ContentTypes`.
- Attach `AliasPart` to a type before assigning aliases. It is an attachable part rather than a route provider.
- `AliasPart.Alias` is indexed case-insensitively in `AliasPartIndex`; aliases are limited to `AliasPart.MaxAliasLength` which is 735 characters.
- The default `AliasPartSettings.Pattern` is `{{ Model.ContentItem.DisplayText | slugify }}`.
- Use `AliasPartOptions.Editable` to allow editors to change the generated alias, or `GeneratedDisabled` to hide editing and generate it from the pattern.
- Alias generation runs when a newly created or updated part has no alias. The handler appends `-1`, `-2`, and so on to make duplicate generated aliases unique (trimming the base to stay within the length limit). Cloning an item also generates a suffixed alias for the clone.
- Editor-entered and imported aliases are NOT trimmed or slugified. Apply `slugify` in the pattern when a slug-like value is needed. Generated aliases have line endings removed.
- Non-empty aliases are unique tenant-wide across all content types (case-insensitive). Uniqueness is validated through `AliasPartIndex`, not a database constraint, so concurrent creators should still handle validation failures.
- Resolve items through the content-handle system using the `alias:` prefix (`IContentHandleManager`), the `Content` Liquid accessors, or the supplied `IOrchardHelper` Razor extensions.
- The module defines no permission of its own. Configuring the part needs **Edit content types**; editing an alias follows the item's edit permissions.
- Do not confuse aliases with `AutoroutePart`. An alias is a logical handle and does not create a public URL.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Alias

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Alias"
      ],
      "disable": []
    }
  ]
}
```

### Attaching AliasPart with a Migration

Configure a Liquid pattern for generated aliases. The pattern has access to `Model` and `ContentItem`.

```csharp
using OrchardCore.Alias.Models;
using OrchardCore.Alias.Settings;
using OrchardCore.ContentManagement.Metadata;
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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Product", type => type
            .Creatable()
            .Listable()
            .WithPart("TitlePart")
            .WithPart(nameof(AliasPart), part => part
                .WithPosition("2")
                .WithSettings(new AliasPartSettings
                {
                    Pattern = "product-{{ ContentItem.DisplayText | slugify }}",
                    Options = AliasPartOptions.Editable,
                })));

        return 1;
    }
}
```

### AliasPart Settings

| Setting | Type | Behavior |
|---|---|---|
| `Pattern` | `string` | Liquid template used only when the part alias is empty. |
| `Options` | `AliasPartOptions` | `Editable` exposes the alias editor. `GeneratedDisabled` keeps generation but hides editing. |

Changing a pattern does not retroactively replace non-empty aliases. Clear or change an item's alias deliberately if it must be regenerated.

### Content Definition Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Product",
          "DisplayName": "Product",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart"
            },
            {
              "PartName": "AliasPart",
              "Name": "AliasPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "2"
                },
                "AliasPartSettings": {
                  "Pattern": "product-{{ ContentItem.DisplayText | slugify }}",
                  "Options": "Editable"
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

### Creating an Item with an Explicit Alias

An explicit alias is retained by `AliasPartHandler`; it is still validated for uniqueness.

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "Product",
          "DisplayText": "Trail Backpack",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Trail Backpack"
          },
          "AliasPart": {
            "Alias": "featured-backpack"
          }
        }
      ]
    }
  ]
}
```

### Looking Up an Alias in Liquid

```liquid
{% assign menu = Content["alias:main-menu"] %}          {% comment %} published {% endcomment %}
{% assign draft = Content.Latest["alias:main-menu"] %}  {% comment %} latest, including drafts {% endcomment %}
{% assign menu2 = Content.Alias["main-menu"] %}         {% comment %} published-only convenience accessor {% endcomment %}

{% contentitem handle: "alias:main-menu", display_type: "Summary" %}
```

These return `nil` when nothing matches. There is no alias-specific JavaScript scripting function.

### Looking Up an Alias in Razor

The module adds helpers to `IOrchardHelper`. Pass either `featured-backpack` or the backwards-compatible `alias:featured-backpack` form. `GetContentItemByAliasAsync(alias, latest: true)` loads the latest version instead of the published one.

```cshtml
@using OrchardCore.ContentManagement

@{
    ContentItem? product = await Orchard.GetContentItemByAliasAsync("featured-backpack");
}

@if (product is not null)
{
    <span>@product.DisplayText</span>
}
```

To retrieve only the stable content item ID:

```cshtml
@{
    string productId = await Orchard.GetContentItemIdByAliasAsync("alias:featured-backpack");
}
```

### Looking Up an Alias from a Service

Prefer `IContentHandleManager`, which composes with every registered `IContentHandleProvider`, then load the version you need:

```csharp
using OrchardCore.ContentManagement;

namespace MyModule.Services;

public sealed class MenuProvider
{
    private readonly IContentHandleManager _contentHandleManager;
    private readonly IContentManager _contentManager;

    public MenuProvider(IContentHandleManager contentHandleManager, IContentManager contentManager)
    {
        _contentHandleManager = contentHandleManager;
        _contentManager = contentManager;
    }

    public async Task<ContentItem?> GetMainMenuAsync()
    {
        var contentItemId = await _contentHandleManager.GetContentItemIdAsync("alias:main-menu");

        return contentItemId is null
            ? null
            : await _contentManager.GetAsync(contentItemId, VersionOptions.Published);
    }
}
```

Query `AliasPartIndex` directly only when you need index-level filtering. The stored index value is lower-cased, so normalize the lookup value and filter `Published` or `Latest` explicitly.

```csharp
using OrchardCore.Alias.Indexes;
using YesSql;

namespace MyModule.Services;

public sealed class ProductAliasLookup
{
    private readonly ISession _session;

    public ProductAliasLookup(ISession session)
    {
        _session = session;
    }

    public async Task<string?> FindContentItemIdAsync(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        var index = await _session
            .QueryIndex<AliasPartIndex>(x => x.Alias == alias.ToLowerInvariant() && x.Published)
            .FirstOrDefaultAsync();

        return index?.ContentItemId;
    }
}
```

### Content Handles and Caching

`AliasPartContentHandleProvider` registers the `alias:` content handle. Published, unpublished, and removed aliases invalidate the `alias:<value>` cache tag. When caching output resolved through an alias, depend on that tag so the cache is invalidated when the alias changes.

### Indexing Behavior

`AliasPartIndexProvider` records `Alias`, `ContentItemId`, `Latest`, and `Published`; both the latest and the published version can contribute rows. It removes index records for soft-deleted items, and when `AliasPart` is removed from the type the stale part data and index row are removed on the next update. Prefer the supplied index and handle provider instead of maintaining a duplicate alias table.

`AliasPart` also contributes its value to the content indexing pipeline (Lucene, Elasticsearch, Azure AI Search) as a stored keyword, so it is not tokenized as full text.

### GraphQL

With GraphQL enabled, types containing `AliasPart` expose an `aliasPart` field and an indexed `where` filter (`alias`, `alias_in`, `alias_contains`, `alias_starts_with`, and negated forms). Use lowercase filter values.

```graphql
query {
  article(where: { aliasPart: { alias: "support" } }) {
    contentItemId
    displayText
    aliasPart { alias }
  }
}
```

### Shape Alternates

With `OrchardCore.Contents` enabled, content shapes get `Content__Alias__{alias}` and `Content_Summary__Alias__{alias}` alternates; with `OrchardCore.Widgets`, `Widget__Alias__{alias}` and `Widget_Summary__Alias__{alias}`. Dashes are encoded as `__`, so `main-menu` gives `Content__Alias__main__menu` (template files `Content-Alias-main-menu.cshtml` and `Content-Alias-main-menu.Summary.cshtml`).

### Custom Handle Providers

Implement `IContentHandleProvider` (`Order` plus `GetContentItemIdAsync(handle)`, returning `null` when the prefix does not match) and register it with `services.AddScoped<IContentHandleProvider, MyHandleProvider>()`. Providers run in ascending `Order` and stop at the first non-empty ID; use a distinct prefix to avoid clashing with `alias:` or `slug:`.

### Troubleshooting

- Lookup returns nothing: check that the feature is enabled on the tenant, the part is attached, the alias is non-empty, and use `Content.Latest[...]` / `VersionOptions.Latest` for drafts (`Content.Alias[...]` is published-only).
- Generated alias does not change: generation only runs while the alias is empty; clear it and save.
- "Already in use": aliases are tenant-wide, include drafts and other content types, and compare case-insensitively.

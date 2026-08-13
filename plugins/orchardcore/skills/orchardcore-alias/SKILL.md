---
name: orchardcore-alias
description: Skill for assigning stable logical aliases to Orchard Core content items. Covers AliasPart configuration, generated and editable aliases, AliasPartIndex lookups, alias cache invalidation, content handles, and Razor helpers. Use this skill when requests mention Orchard Core Alias, AliasPart, custom content aliases, logical content identifiers, alias patterns, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Alias, AliasPart, AliasPartSettings, AliasPartHandler, AliasPartIndex, AliasPartContentHandleProvider, IContentHandleProvider, and IOrchardHelper. It also helps with alias migrations, content-definition recipes, indexed lookups, and the code patterns captured in this skill.
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
- Alias generation runs when a newly created or updated part has no alias. The handler appends `-1`, `-2`, and so on to make duplicate generated aliases unique.
- Resolve items through the content-handle system using the `alias:` prefix, or use the supplied `IOrchardHelper` Razor extensions.
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

### Looking Up an Alias in Razor

The module adds helpers to `IOrchardHelper`. Pass either `featured-backpack` or the backwards-compatible `alias:featured-backpack` form.

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

Query `AliasPartIndex` when a service needs the ID. The stored index value is lower-cased, so normalize the lookup value.

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

`AliasPartIndexProvider` records `Alias`, `ContentItemId`, `Latest`, and `Published`. It removes index records for soft-deleted items and avoids indexing a part no longer attached to the content type. Prefer the supplied index and handle provider instead of maintaining a duplicate alias table.

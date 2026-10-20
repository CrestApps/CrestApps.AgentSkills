---
name: orchardcore-taxonomies
description: Skill for creating and managing taxonomies in Orchard Core. Covers taxonomies and hierarchical terms, TaxonomyField settings, the Tags editor and TagNames, Term and TermItem shapes, routable taxonomies and term listings, TaxonomyIndex queries, Orchard helpers, Liquid filters, admin contents list taxonomy filters, the ManageTaxonomy permission, and IContentsTaxonomyListFilter. Use this skill when requests mention Orchard Core Taxonomies, categories, tags, terms, TaxonomyField, taxonomy_terms, inherited_terms, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Taxonomies, OrchardCore.Taxonomies.ContentsAdminList, TaxonomyField, TaxonomyPart, TermPart, TaxonomyIndex, TaxonomyFieldSettings, AliasPart, and QueryCategorizedContentItemsAsync, plus migrations, recipes, and examples.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Taxonomies - Prompt Templates

## Create and Manage Taxonomies

You are an Orchard Core expert. Generate taxonomy definitions, terms, and taxonomy field configurations.

### Guidelines

- Enable `OrchardCore.Taxonomies` to use taxonomy features.
- A Taxonomy is a content item of type `Taxonomy` that contains terms.
- Terms are hierarchical content items within a taxonomy.
- Use `TaxonomyField` on content types to allow categorization.
- Taxonomies can be flat (tags) or hierarchical (categories).
- Terms can have custom content parts and fields.
- `TaxonomyPart` (`TermContentType`, `Terms`) is added to taxonomies and `TermPart` (`TaxonomyContentItemId`) is welded onto terms automatically; do not attach them manually.
- Terms are stored inside the taxonomy document (not as separate content items); child terms are nested in a term's `Terms` array.
- Managing taxonomies requires the `ManageTaxonomy` permission (`OrchardCore.Taxonomies.Permissions.ManageTaxonomies`, granted to Administrator) plus the usual content permissions.
- Front-end term listings (a term displayed through container routing) use the full `Pager`, so the 4.0 **Page size selection** (`pageSize` query string) applies.
- Enable `OrchardCore.Taxonomies.ContentsAdminList` to filter the admin content list by taxonomy terms.

### Enabling Taxonomy Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Taxonomies"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Taxonomy via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "{{TaxonomyId}}",
          "ContentType": "Taxonomy",
          "DisplayText": "{{TaxonomyName}}",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "{{TaxonomyName}}"
          },
          "AliasPart": {
            "Alias": "{{taxonomy-alias}}"
          },
          "TaxonomyPart": {
            "Terms": [
              {
                "ContentItemId": "{{TermId1}}",
                "ContentType": "{{TermContentType}}",
                "DisplayText": "{{TermName1}}",
                "TitlePart": {
                  "Title": "{{TermName1}}"
                }
              },
              {
                "ContentItemId": "{{TermId2}}",
                "ContentType": "{{TermContentType}}",
                "DisplayText": "{{TermName2}}",
                "TitlePart": {
                  "Title": "{{TermName2}}"
                }
              }
            ],
            "TermContentType": "{{TermContentType}}"
          }
        }
      ]
    }
  ]
}
```

### Hierarchical Terms (Nested)

```json
{
  "ContentItemId": "term-parent",
  "ContentType": "Category",
  "DisplayText": "Parent Category",
  "TitlePart": {
    "Title": "Parent Category"
  },
  "Terms": [
    {
      "ContentItemId": "term-child-1",
      "ContentType": "Category",
      "DisplayText": "Child Category 1",
      "TitlePart": {
        "Title": "Child Category 1"
      }
    },
    {
      "ContentItemId": "term-child-2",
      "ContentType": "Category",
      "DisplayText": "Child Category 2",
      "TitlePart": {
        "Title": "Child Category 2"
      }
    }
  ]
}
```

### Attaching TaxonomyField to a Content Type

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("BlogPost", part => part
    .WithField("Categories", field => field
        .OfType("TaxonomyField")
        .WithDisplayName("Categories")
        .WithSettings(new TaxonomyFieldSettings
        {
            TaxonomyContentItemId = "{{TaxonomyContentItemId}}",
            Unique = false,
            LeavesOnly = false
        })
        .WithPosition("0")
    )
    .WithField("Tags", field => field
        .OfType("TaxonomyField")
        .WithDisplayName("Tags")
        .WithEditor("Tags")
        .WithSettings(new TaxonomyFieldSettings
        {
            TaxonomyContentItemId = "{{TagsTaxonomyContentItemId}}"
        })
        .WithPosition("1")
    )
);
```

### TaxonomyField Settings

| Setting | Description |
|---|---|
| `TaxonomyContentItemId` | The taxonomy whose terms can be selected. |
| `Unique` | Only one term can be selected (radio buttons). |
| `LeavesOnly` | Only terms without children can be selected. |
| `Open` | Editors can create new terms from the field editor (the `Tags` editor defaults this to `true` through `TaxonomyFieldTagsEditorSettings`). |
| `Placeholder` | Placeholder of the tags input. |
| `Required` / `Hint` | Standard field settings. |

The field stores `TaxonomyContentItemId` and `TermContentItemIds`. With the `Tags` editor and display mode, the term display texts are also stored as `TagNames`:

```liquid
{% for tagName in Model.ContentItem.Content.BlogPost.Tags.TagNames %}
    <span class="badge text-bg-secondary">{{ tagName }}</span>
{% endfor %}
```

Republish categorized items after renaming a tag, because `TagNames` is a stored copy.

### TaxonomyIndex

`TaxonomyIndex` has one row per selected term per field: `TaxonomyContentItemId`, `ContentItemId`, `ContentType`, `ContentPart`, `ContentField`, `TermContentItemId`, `Published`, and `Latest`.

### Querying Categorized Content in Razor

```cshtml
@using YesSql.Services
@{
    var termIds = Model.TermContentItemIds;
    var related = await Orchard.QueryCategorizedContentItemsAsync(query => query
        .Where(index => index.TermContentItemId.IsIn(termIds) &&
            index.ContentItemId != Model.Part.ContentItem.ContentItemId));
}
```

`Orchard.GetTaxonomyTermAsync(taxonomyId, termId)` returns a term, and `Orchard.GetInheritedTermsAsync(taxonomyId, termId)` returns the term with its parents.

### Querying by Taxonomy Terms in Liquid

Create a SQL query named `ContentItemsByTaxonomyTerm` with **Return Content Items** disabled:

```sql
SELECT DISTINCT ContentItemId
FROM TaxonomyIndex
WHERE TermContentItemId = @termContentItemId:''
  AND Published = true
```

Execute the query, then load its returned content item ids with the `content_item_id` filter:

```liquid
{% assign rows = Queries.ContentItemsByTaxonomyTerm | query: termContentItemId: termId %}
{% assign contentItemIds = rows | map: "ContentItemId" %}
{% assign items = contentItemIds | content_item_id %}

{% for item in items %}
    {{ item | display_text }}
{% endfor %}
```

### Routable Taxonomies and Term Listings

Add `AutoroutePart` to the `Taxonomy` type with `AllowRouteContainedItems`, then enable **Route Contained Items** on the taxonomy item. The taxonomy renders through the `TaxonomyPart` shape and each term route renders `TermPart`, which lists the content items categorized with that term (and its children) with a full pager.

### Filtering Taxonomy Content Listings

Implement `IContentsTaxonomyListFilter` (namespace `OrchardCore.Taxonomies.Core`) to add restrictions to the query used by a term listing, or replace `IContentsTaxonomyListQueryService` (`QueryAsync(TermPart, Pager)`) for full control. Keep filters focused on query constraints and let the built-in listing handle paging.

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Taxonomies.Core;
using OrchardCore.Taxonomies.Models;
using YesSql;

namespace MyModule.Taxonomies;

public sealed class ArticlesOnlyTaxonomyListFilter : IContentsTaxonomyListFilter
{
    public Task FilterAsync(IQuery<ContentItem> query, TermPart termPart)
    {
        query.With<ContentItemIndex>(x => x.ContentType == "Article");

        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<IContentsTaxonomyListFilter, ArticlesOnlyTaxonomyListFilter>();`.

### Rendering a Term Hierarchy (Term Shape)

```liquid
{% shape "term", alias: "alias:Categories" %}
{% shape "term", TaxonomyContentItemId: taxonomyId, TermContentItemId: termId %}
```

```cshtml
<shape type="Term" alias="alias:Categories" />
```

The `Term` shape holds `TermItem` shapes (`Level`, `TermContentItem`, `Items`, `Differentiator`); each item is morphed into `TermContentItem` for rendering. Alternates include `Term-Categories.cshtml`, `TermItem-Category-level-2.cshtml`, and `TermContentItem-Categories.cshtml`.

### Admin Contents List Filters

With `OrchardCore.Taxonomies.ContentsAdminList` enabled, choose which taxonomies appear as filters in the admin content list:

```json
{
  "steps": [
    {
      "name": "settings",
      "TaxonomyContentsAdminListSettings": {
        "TaxonomyContentItemIds": [
          "{{TaxonomyContentItemId}}"
        ]
      }
    }
  ]
}
```

### Displaying Taxonomy Terms

```liquid
{% assign terms = Model.ContentItem.Content.BlogPost.Categories | taxonomy_terms %}
{% for term in terms %}
    <span class="badge">{{ term.DisplayText }}</span>
{% endfor %}

{% assign taxonomyId = Model.ContentItem.Content.BlogPost.Categories.TaxonomyContentItemId %}
{% for termId in Model.ContentItem.Content.BlogPost.Categories.TermContentItemIds %}
    {% assign path = termId | inherited_terms: taxonomyId %}
    {% for t in path %}{{ t.DisplayText }} / {% endfor %}
{% endfor %}
```

### Custom Term Content Type

Create a custom term type with additional fields:

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("Category", type => type
    .DisplayedAs("Category")
    .WithPart("TitlePart")
    .WithPart("Category", part => part
        .WithPosition("1")
    )
);

await _contentDefinitionManager.AlterPartDefinitionAsync("Category", part => part
    .WithField("Icon", field => field
        .OfType("TextField")
        .WithDisplayName("Icon CSS Class")
        .WithPosition("0")
    )
    .WithField("Description", field => field
        .OfType("TextField")
        .WithDisplayName("Description")
        .WithEditor("TextArea")
        .WithPosition("1")
    )
);
```

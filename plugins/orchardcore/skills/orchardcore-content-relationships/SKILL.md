---
name: orchardcore-content-relationships
description: Skill for modeling content relationships in Orchard Core. Covers five relationship strategies including BagPart for embedded items, ContentPickerField for many-to-many references, FlowPart for layout widgets, ListPart for hierarchical collections, and Taxonomies for categorization and tagging, plus ContentPickerField settings, DisplayedStereotypes, TitlePattern, and reverse lookups with ContentPickerFieldIndex. Use this skill when requests mention Orchard Core Content Relationships, a Relationship Strategy Decision Matrix, embedded items, related content, many-to-many references, parent-child content, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include BagPart, ContentPickerField, ContentPickerFieldSettings, ContentPickerFieldIndex, LocalizationSetContentPickerField, FlowPart, ListPart, TaxonomyField, and DataMigration code patterns and recipes.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Relationships - Prompt Templates

## Model Content Relationships

You are an Orchard Core expert. Guide the selection and implementation of content relationship strategies — connecting content items using built-in parts and fields.

### Guidelines

- Orchard Core provides five built-in strategies for relating content items: `BagPart`, `ContentPickerField`, `FlowPart`, `ListPart`, and `Taxonomies`.
- Choose the strategy based on ownership semantics, editing experience, and query requirements.
- `BagPart` and `FlowPart` store child items **embedded** within the parent (inline editing, fast retrieval).
- `ListPart` stores child items **independently** but linked to a parent (separate URLs, separate editing).
- `ContentPickerField` creates **references** between independently managed items (many-to-many).
- `Taxonomies` provide **categorization** with hierarchical terms and indirect many-to-many relationships.
- Query relationship data through the index supplied by its owning feature when one exists; add a custom `MapIndex` only for fields and relationship projections your application filters or sorts frequently.
- Enable the owning feature before defining its relationship: `OrchardCore.Flows` for `BagPart` and `FlowPart`, `OrchardCore.Lists` for `ListPart`, `OrchardCore.ContentFields` for `ContentPickerField`, and `OrchardCore.Taxonomies` for taxonomy fields.
- All C# classes must use the `sealed` modifier.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.

### Relationship Strategy Decision Matrix

| Strategy             | Ownership   | Child Editing     | Child Has Own URL | Relationship | Best For                          |
|----------------------|-------------|-------------------|-------------------|--------------|-----------------------------------|
| `BagPart`            | Embedded    | Inline with parent| No                | One-to-many  | FAQ items, feature lists          |
| `ContentPickerField` | Referenced  | Independent       | Yes               | Many-to-many | Related posts, product bundles    |
| `FlowPart`           | Embedded    | Inline with parent| No                | One-to-many  | Page layouts, widget canvases     |
| `ListPart`           | Hierarchical| Independent       | Yes               | One-to-many  | Blog → Blog Posts, Forums         |
| `Taxonomies`         | Referenced  | Via taxonomy editor| No (terms)       | Many-to-many | Tags, categories, classifications |

### BagPart — Embedded Items within a Parent

`BagPart` stores child content items directly inside a parent. Items are edited inline and cannot exist independently.

#### When to Use

- Child items have no meaning outside the parent (e.g., FAQ question-answer pairs).
- You want all items edited on the same page.
- Fast retrieval without extra database queries is important.

#### Defining BagPart in Code

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // Define the child content type
        await _contentDefinitionManager.AlterTypeDefinitionAsync("FaqItem", type => type
            .DisplayedAs("FAQ Item")
            .Stereotype("Widget")
            .WithPart("FaqItem", part => part.WithPosition("0"))
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("FaqItem", part => part
            .WithField("Question", field => field
                .OfType("TextField")
                .WithDisplayName("Question")
                .WithPosition("0")
            )
            .WithField("Answer", field => field
                .OfType("HtmlField")
                .WithDisplayName("Answer")
                .WithPosition("1")
                .WithEditor("Wysiwyg")
            )
        );

        // Attach BagPart to the parent
        await _contentDefinitionManager.AlterTypeDefinitionAsync("FaqPage", type => type
            .DisplayedAs("FAQ Page")
            .Creatable()
            .Listable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("BagPart", part => part
                .WithPosition("1")
                .WithDisplayName("FAQ Items")
                .WithDescription("Add question-answer pairs.")
                .WithSettings(new BagPartSettings
                {
                    ContainedContentTypes = ["FaqItem"],
                })
            )
        );

        return 1;
    }
}
```

#### BagPart via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "FaqPage",
          "DisplayText": "Frequently Asked Questions",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Frequently Asked Questions"
          },
          "BagPart": {
            "ContentItems": [
              {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "FaqItem",
                "DisplayText": "What is Orchard Core?",
                "FaqItem": {
                  "Question": { "Text": "What is Orchard Core?" },
                  "Answer": { "Html": "<p>Orchard Core is an open-source CMS built on ASP.NET Core.</p>" }
                }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

### ContentPickerField — Many-to-Many References

`ContentPickerField` selects existing content items by reference. Items remain independently managed.

#### When to Use

- You need many-to-many relationships (e.g., related articles, product bundles).
- Referenced items have their own URLs and lifecycle.
- You want to link to items that already exist.

#### Defining ContentPickerField in Code

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("BlogPost", part => part
            .WithField("RelatedPosts", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Related Posts")
                .WithPosition("5")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = true,
                    DisplayedContentTypes = ["BlogPost"],
                })
            )
        );

        return 1;
    }
}
```

#### ContentPickerField Settings

| Setting | Description |
|---|---|
| `Required` / `Hint` | Standard field settings (at least one item, editor hint). |
| `Multiple` | Allow selecting several items. |
| `DisplayAllContentTypes` | Allow any content type. |
| `DisplayedContentTypes` | Selectable types when `DisplayAllContentTypes` is `false`. |
| `DisplayedStereotypes` | Restrict to types with one of these stereotypes; takes precedence over `DisplayedContentTypes`. |
| `Placeholder` | Placeholder text of the picker input. |
| `TitlePattern` | Liquid pattern for each item's title in the picker, default `{{ Model.ContentItem \| display_text }}`. With `LocalizationPart`, use `{{ Model.ContentItem \| display_text }} ({{ Model.ContentItem.Content.LocalizationPart.Culture }})`. |

`ContentPickerFieldSettings` is in `OrchardCore.ContentFields.Settings`. The field stores `string[] ContentItemIds`.

#### Accessing ContentPickerField in Liquid and Razor

```liquid
{% assign related = Model.ContentItem.Content.BlogPost.RelatedPosts.ContentItemIds | content_item_id %}
{% for item in related %}
    <a href="{{ item | display_url }}">{{ item.DisplayText }}</a>
    {{ item | shape_build_display: "Summary" | shape_render }}
{% endfor %}
```

```cshtml
@foreach (var item in await Orchard.GetContentItemsByIdAsync(Model.ContentItemIds))
{
    @await Orchard.DisplayAsync(item, "Summary")
}
```

#### Reverse Lookups (Which Items Reference This One?)

A picker stores IDs only on the referencing side. Enable `OrchardCore.ContentFields.Indexing.SQL` and query `ContentPickerFieldIndex` to find referencing items:

```csharp
using OrchardCore.ContentFields.Indexing.SQL;
using OrchardCore.ContentManagement;
using YesSql;

namespace MyModule.Services;

public sealed class RelatedPostsService
{
    private readonly ISession _session;

    public RelatedPostsService(ISession session)
    {
        _session = session;
    }

    public Task<IEnumerable<ContentItem>> GetPostsReferencingAsync(string contentItemId)
        => _session
            .Query<ContentItem, ContentPickerFieldIndex>(x =>
                x.ContentType == "BlogPost" &&
                x.ContentField == "RelatedPosts" &&
                x.SelectedContentItemId == contentItemId &&
                x.Published)
            .ListAsync();
}
```

For localized sites use `LocalizationSetContentPickerField`, which stores localization sets instead of item IDs so the picked content resolves to the current culture.

### FlowPart — Structured Layout with Widgets

`FlowPart` allows building structured, responsive page layouts with embedded widgets. Similar to `BagPart` but adds layout positioning.

#### When to Use

- Building flexible, ad-hoc pages with complex layouts (about, contact, portfolio pages).
- You need widgets positioned in columns and rows.
- Content editors need a visual canvas for arranging content blocks.

Widgets inside FlowPart and BagPart are sorted by drag and drop; with `OrchardCore.ContentPreview` enabled, every drop refreshes the live preview. See `orchardcore-flow-bagpart` for settings and templates.

#### Defining FlowPart in Code

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Page", type => type
            .DisplayedAs("Page")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("FlowPart", part => part.WithPosition("1"))
        );

        return 1;
    }
}
```

### ListPart — Hierarchical One-to-Many Collections

`ListPart` connects independently managed content items in a parent-child hierarchy. Children have their own URLs and are edited separately. See `orchardcore-lists` for settings, `ContainedPartIndex` queries, and page size selection.

#### When to Use

- Classic parent-child relationships where children are independent (e.g., Blog → Blog Posts).
- Children need their own URLs, editing pages, and publishing lifecycle.
- You want paginated lists of children under a parent.

#### Defining ListPart in Code

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Blog", type => type
            .DisplayedAs("Blog")
            .Creatable()
            .Listable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("ListPart", part => part
                .WithPosition("1")
                .WithSettings(new ListPartSettings
                {
                    ContainedContentTypes = ["BlogPost"],
                    PageSize = 10,
                })
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlogPost", type => type
            .DisplayedAs("Blog Post")
            .Creatable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("MarkdownBodyPart", part => part.WithPosition("1"))
        );

        return 1;
    }
}
```

### Taxonomies — Categorization and Tagging

Taxonomies provide categorization and tagging of content items using Terms. Terms can be hierarchical (categories with sub-categories) or flat (tags).

#### When to Use

- Classifying content with shared tags or categories (e.g., blog post tags, product categories).
- You need to list all content items with a given term.
- Terms can form hierarchies (e.g., Electronics → Phones → Smartphones).

#### Defining a Taxonomy Field in Code

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("BlogPost", part => part
            .WithField("Tags", field => field
                .OfType("TaxonomyField")
                .WithDisplayName("Tags")
                .WithPosition("3")
                .WithEditor("Tags")
                .WithSettings(new TaxonomyFieldSettings
                {
                    TaxonomyContentItemId = "{{tagsTermTaxonomyContentItemId}}",
                    Unique = false,
                })
            )
            .WithField("Category", field => field
                .OfType("TaxonomyField")
                .WithDisplayName("Category")
                .WithPosition("4")
                .WithSettings(new TaxonomyFieldSettings
                {
                    TaxonomyContentItemId = "{{categoryTaxonomyContentItemId}}",
                    Unique = true,
                })
            )
        );

        return 1;
    }
}
```

#### Taxonomy via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "tags-taxonomy",
          "ContentType": "Taxonomy",
          "DisplayText": "Tags",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Tags"
          },
          "TaxonomyPart": {
            "TermContentType": "Tag",
            "Terms": [
              {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "Tag",
                "DisplayText": "Technology",
                "TitlePart": { "Title": "Technology" }
              },
              {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "Tag",
                "DisplayText": "Lifestyle",
                "TitlePart": { "Title": "Lifestyle" }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

### Choosing the Right Strategy

1. **Embedded child items that are only meaningful within a parent** → Use `BagPart`.
2. **Flexible page layouts with drag-and-drop widget positioning** → Use `FlowPart`.
3. **Independent child items with their own URLs under a parent** → Use `ListPart`.
4. **Referencing existing items in many-to-many relationships** → Use `ContentPickerField`.
5. **Categorizing or tagging content for filtering and grouping** → Use `Taxonomies`.

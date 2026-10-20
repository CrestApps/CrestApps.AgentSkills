---
name: orchardcore-lists
description: Skill for working with ListPart in Orchard Core. Covers list content types, ListPart shapes, list item rendering, PagerSlim cursor paging and the full pager, page size selection with the pageSize query string, ShowHeader, admin list ordering with List or Grid layouts, ContainedPartIndex queries, IContainerService, list RSS feeds, GraphQL, Liquid filters, Razor templates, and blog or collection patterns. Use this skill when requests mention Orchard Core Lists, ListPart, ContainedPart, blogs with posts, ListPartSettings, ListPartViewModel, list_items, list_count, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Lists, ListPart, ContainedPart, ContainedPartIndex, ListPartSettings, PagerSlim, IContainerService, QueryListItemsAsync, and AutoroutePart container patterns, plus migrations and recipes.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Lists - Prompt Templates

## Working with ListPart

You are an Orchard Core expert. Generate code and configuration for list-based content types using ListPart in Orchard Core.

### Guidelines

- ListPart is provided by the `OrchardCore.Lists` module and associates child content items with a parent container.
- A classic example is a Blog (parent) containing BlogPost items (children).
- ListPart stores child items as separate content items linked via `ContainedPart`, unlike BagPart which embeds items inline.
- Configure `ContainedContentTypes` in `ListPartSettings` to restrict which content types can be added to the list.
- Enable ordering via `EnableOrdering` in `ListPartSettings` for drag-and-drop reordering.
- Set `PageSize` in `ListPartSettings` (default `10`) to control pagination. By default the front-end list uses the cursor-based `PagerSlim` (newer/older links, `before`/`after` query values); set `ShowFullPager = true` for a numbered `Pager` (`page` query value).
- When **Page size selection** is enabled in the General site settings, visitors can pick an allowed size with the `pageSize` query string; any other value falls back to the list's `PageSize`.
- Use the `list_items` and `list_count` Liquid filters to load list items in templates.
- Every contained item has a `ContainedPart` with `ListContentItemId`, `ListContentType`, and `Order`, indexed in `ContainedPartIndex`.
- Always wrap recipe JSON in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier except View Models.
- Use file-scoped namespaces in C# examples.

### Enabling List Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Lists"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Blog with ListPart

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
            .Creatable()
            .Listable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    Pattern = "{{ Model.ContentItem | display_text | slugify }}",
                })
            )
            .WithPart("ListPart", part => part
                .WithPosition("2")
                .WithSettings(new ListPartSettings
                {
                    PageSize = 10,
                    EnableOrdering = true,
                    ContainedContentTypes = ["BlogPost"],
                })
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlogPost", type => type
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    Pattern = "{{ Model.ContentItem | container | display_text | slugify }}/{{ Model.ContentItem | display_text | slugify }}",
                })
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Trumbowyg")
            )
        );

        return 1;
    }
}
```

### ListPartViewModel Properties

| Property                          | Type                                 | Description                             |
|-----------------------------------|--------------------------------------|-----------------------------------------|
| `ListPart`                        | `ListPart`                           | The `ListPart` instance                 |
| `ContentItems`                    | `IEnumerable<ContentItem>`           | The content items the list contains     |
| `ContainedContentTypeDefinitions` | `IEnumerable<ContentTypeDefinition>` | The content types the list accepts      |
| `Context`                         | `BuildPartDisplayContext`            | The current display context             |
| `Pager`                           | `dynamic`                            | The pager shape for pagination          |

`ListPartViewModel` also exposes `EnableOrdering`, `IsOrdering`, `ListPartFilterViewModel`, and (in the admin) the `List` `AdminListContext`.

### ListPartSettings Properties

| Property                | Type       | Description                                              |
|-------------------------|------------|----------------------------------------------------------|
| `PageSize`              | `int`      | Number of content items per page (default `10`)          |
| `EnableOrdering`        | `bool`     | Enable drag-and-drop ordering of items                   |
| `ContainedContentTypes` | `string[]` | Content types that may be contained by this list         |
| `ShowHeader`            | `bool`     | Show a header for the owning (container) content in the admin list |
| `ShowFullPager`         | `bool`     | Use the numbered `Pager` instead of `PagerSlim`          |

### Paging and Page Size Selection

- `PagerSlim` (default) renders **Newer/Older** links for cursor-based paging, like the Blog recipe's post list.
- With `ShowFullPager`, the shape is a numbered `Pager` built from the total item count.
- Both pagers render the `Pager_PageSizeSelector` shape when **Page size selection** is enabled (General settings, allowed sizes default `10, 25, 50, 100`). The selected `pageSize` is carried in pager links. Style it by overriding `Pager_PageSizeSelector` (model has `PageSizes` and `CurrentPageSize`).
- Custom code can reuse the same validation: `pagerOptions.GetPageSize(pagerParameters.PageSize, settings.PageSize)`, and `PagerSlimParameters.PageSize` now carries the requested size.

### Admin List

The list's contained items in the admin are rendered through the shared `AdminList` shape, so they follow the site's **List** or **Grid** layout. With `EnableOrdering`, an unfiltered admin list of the latest versions with more than one item forces the `List` layout and hides the layout selector; drag a row's handle to reorder (saved immediately). Searching or filtering disables drag ordering and restores normal layout selection. The **List Items** button names the contained content type.

### Liquid Tags for Lists

#### `list_items` - Load Published List Items

The `list_items` filter loads published content items for a given content item or content item ID:

```liquid
{% assign blogPosts = Model.ContentItem | list_items %}
{% for post in blogPosts %}
    {{ post | shape_build_display: "Summary" | shape_render }}
{% endfor %}
```

Load by content item ID:

```liquid
{% assign blogPosts = "4j42fxryjcpqkkacmg6cwz3pr5" | list_items %}
{% for post in blogPosts %}
    <h2>{{ post.DisplayText }}</h2>
{% endfor %}
```

#### `list_count` - Count Published List Items

```liquid
{% assign count = Model.ContentItem | list_count %}
<p>Total posts: {{ count }}</p>
```

### Templating a ListPart with Liquid

Override the list rendering for a specific content type by creating a template named `Blog-ListPart.liquid`:

```liquid
{% for item in Model.ContentItems %}
    {{ item | shape_build_display: "Summary" | shape_render }}
{% endfor %}

{% assign previousText = "Newer Posts" | t %}
{% assign nextText = "Older Posts" | t %}
{% assign previousClass = "previous" %}
{% assign nextClass = "next" %}

{% shape_pager Model.Pager previous_text: previousText, next_text: nextText,
    previous_class: previousClass, next_class: nextClass %}

{{ Model.Pager | shape_render }}
```

### Templating a ListPart with Razor

Create a file named `Blog-ListPart.cshtml`:

```html
@model OrchardCore.Lists.ViewModels.ListPartViewModel

@foreach (var item in Model.ContentItems)
{
    var shape = await ContentItemDisplayManager.BuildDisplayAsync(item, Model.Context.Updater, "Summary");
    @await DisplayAsync(shape)
}

@await DisplayAsync(Model.Pager)
```

### Shape Alternates for ListPart

| Template Name                | Filename                        | Description                                  |
|------------------------------|---------------------------------|----------------------------------------------|
| `ListPart`                   | `ListPart.cshtml`               | Default ListPart shape                       |
| `Blog__ListPart`             | `Blog-ListPart.cshtml`          | ListPart for Blog content type only          |
| `Blog_Summary__ListPart`     | `Blog-ListPart.Summary.cshtml`  | ListPart for Blog in Summary display type    |
| `ListPartFeed`               | `ListPartFeed.cshtml`           | RSS feed shape for ListPart                  |

### Razor Helpers

The `OrchardCore.Lists` module provides `IOrchardHelper` helpers for querying list items programmatically:

| Method | Description |
|---|---|
| `QueryListItemsCountAsync(listContentItemId, itemPredicate = null)` | Count of contained items matching an optional `ContentItemIndex` predicate |
| `QueryListItemsAsync(listContentItemId, itemPredicate = null)` | Contained items matching an optional `ContentItemIndex` predicate |

```cshtml
@{
    var recent = await Orchard.QueryListItemsAsync(blogId, x => x.Published && x.CreatedUtc > DateTime.UtcNow.AddDays(-30));
}
```

Liquid also provides the `container` filter (`{{ Model.ContentItem | container }}`) to load the parent list of a contained item.

### Querying Contained Items in Code

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Lists.Indexes;
using YesSql;

var posts = await _session.Query<ContentItem>()
    .With<ContainedPartIndex>(x => x.ListContentItemId == blogId)
    .With<ContentItemIndex>(x => x.Published)
    .OrderByDescending(x => x.CreatedUtc)
    .Take(10)
    .ListAsync();
```

`ContainedPartIndex` stores `ListContentItemId`, `ListContentType`, `ContentItemId`, `Order`, `Published`, `Latest`, and `DisplayText`. `IContainerService` (`OrchardCore.Lists.Services`) provides `GetItemCountAsync(listContentItemId, ContainedItemOptions)`, `QueryContainedItemsAsync(...)` with a `Pager` or `PagerSlim`, `GetNextOrderNumberAsync`, and `UpdateContentItemOrdersAsync`. `ContainedItemOptions.Status` is `Published` (default), `Latest`, `Draft`, or `Owner`.

### Feeds and GraphQL

- With `OrchardCore.Feeds` enabled, each list exposes an RSS feed at `/Contents/Lists/{contentItemId}/rss`; see `orchardcore-feeds` for `DisableRssFeed`, `FeedProxyUrl`, and `FeedItemsCount`.
- With GraphQL enabled, `listPart { contentItems(first: 10, skip: 0) { ... } }` returns published contained items, and `containedPart: { listContentItemId: "..." }` can be used in `where` filters of the contained type.

### ContainedPart Indexing

Every content item contained in a list has a `ContainedPart` with indexable properties:

| Index Field                                        | Description                   |
|----------------------------------------------------|-------------------------------|
| `Content.ContentItem.ContainedPart.ListContentItemId` | The parent list content item ID |
| `Content.ContentItem.ContainedPart.Order`              | The display order of the item   |

These are the field names in full-text indexes (Lucene, Elasticsearch, Azure AI Search).

### Recipe: Creating a Blog with Posts

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "4j42fxryjcpqkkacmg6cwz3pr5",
          "ContentType": "Blog",
          "DisplayText": "My Blog",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "My Blog"
          },
          "AutoroutePart": {
            "Path": "blog"
          }
        },
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "BlogPost",
          "DisplayText": "First Post",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "First Post"
          },
          "HtmlBodyPart": {
            "Html": "<p>Welcome to my first blog post!</p>"
          },
          "ContainedPart": {
            "ListContentItemId": "4j42fxryjcpqkkacmg6cwz3pr5",
            "Order": 0
          }
        }
      ]
    }
  ]
}
```

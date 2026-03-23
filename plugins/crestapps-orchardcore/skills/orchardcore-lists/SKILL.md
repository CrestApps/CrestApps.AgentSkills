---
name: orchardcore-lists
description: Skill for working with ListPart in Orchard Core. Covers list content types, ListPart shapes, list item rendering, paging, Liquid tags, Razor templates, and blog/collection patterns.
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
- Set `PageSize` in `ListPartSettings` to control pagination.
- Use the `list_items` and `list_count` Liquid filters to load list items in templates.
- Every contained item has a `ContainedPart` with `ListContentItemId` and `Order` properties for indexing.
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
                    AllowRouteContainedItems = true,
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

### ListPartSettings Properties

| Property                | Type       | Description                                              |
|-------------------------|------------|----------------------------------------------------------|
| `PageSize`              | `int`      | Number of content items per page                         |
| `EnableOrdering`        | `bool`     | Enable drag-and-drop ordering of items                   |
| `ContainedContentTypes` | `string[]` | Content types that may be contained by this list         |

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
{% assign blogPosts = "4abcdef123456" | list_items %}
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

The `OrchardCore.Lists` module provides helpers for querying list items programmatically:

| Method                      | Description                                                      |
|-----------------------------|------------------------------------------------------------------|
| `QueryListItemsCountAsync`  | Returns the count of content items matching a predicate          |
| `QueryListItemsAsync`       | Returns content items matching a predicate                       |

### ContainedPart Indexing

Every content item contained in a list has a `ContainedPart` with indexable properties:

| Index Field                                        | Description                   |
|----------------------------------------------------|-------------------------------|
| `Content.ContentItem.ContainedPart.ListContentItemId` | The parent list content item ID |
| `Content.ContentItem.ContainedPart.Order`              | The display order of the item   |

### Recipe: Creating a Blog with Posts

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
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
            "ListContentItemId": "{{blog-content-item-id}}",
            "Order": 0
          }
        }
      ]
    }
  ]
}
```

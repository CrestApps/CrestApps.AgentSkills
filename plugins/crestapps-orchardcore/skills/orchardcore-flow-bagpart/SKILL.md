---
name: orchardcore-flow-bagpart
description: Skill for building page layouts with FlowPart and BagPart in Orchard Core. Covers flow widgets, BagPart for named containers, Blocks Editor configuration, content type setup, Liquid and Razor templating, shape alternates, and layout building patterns.
---

# Orchard Core Flow & BagPart - Prompt Templates

## Building Layouts with FlowPart and BagPart

You are an Orchard Core expert. Generate code and configuration for composing page layouts using FlowPart and BagPart.

### Guidelines

- FlowPart and BagPart are provided by the `OrchardCore.Flows` module.
- **FlowPart** allows embedding arbitrary widget content items inline within a content item (e.g., a page).
- **BagPart** is similar but lets you restrict which content types can be contained within it via its settings.
- BagPart can be added as a **named part**, allowing multiple BagParts on a single content type (e.g., `Services`, `Portfolio`, `About`).
- BagPart items are stored as a single document in the database for efficient retrieval.
- Empty flows render with shape name `FlowPart_Empty`; empty bags render as `BagPart_Empty`.
- Use placement to hide empty parts by placing `FlowPart_Empty` or `BagPart_Empty` to `"-"`.
- The **Blocks Editor** provides a modal-based content type picker as an alternative to the default dropdown. Enable it by setting the editor to `Blocks`.
- Always wrap recipe JSON in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier except View Models.
- Use file-scoped namespaces in C# examples.

### Enabling Flow Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Flows",
        "OrchardCore.Widgets"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Content Type with FlowPart

Use a data migration to create a content type (e.g., `LandingPage`) that contains a FlowPart for embedding widgets:

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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("LandingPage", type => type
            .Creatable()
            .Listable()
            .Draftable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("FlowPart", part => part.WithPosition("1"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("2")
                .WithSettings(new AutoroutePartSettings
                {
                    Pattern = "{{ Model.ContentItem | display_text | slugify }}",
                    AllowRouteContainedItems = true,
                })
            )
        );

        return 1;
    }
}
```

### Creating a Content Type with Named BagParts

Named BagParts allow multiple containers on a single content type, each restricted to specific child content types:

```csharp
public sealed class Migrations : DataMigation
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("AgencyPage", type => type
            .Creatable()
            .Listable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("Services", "BagPart", part => part
                .WithPosition("1")
                .WithDisplayName("Services")
                .WithDescription("Services section")
                .WithSettings(new BagPartSettings
                {
                    ContainedContentTypes = ["ServiceWidget"],
                })
            )
            .WithPart("Portfolio", "BagPart", part => part
                .WithPosition("2")
                .WithDisplayName("Portfolio")
                .WithDescription("Portfolio section")
                .WithSettings(new BagPartSettings
                {
                    ContainedContentTypes = ["PortfolioWidget"],
                })
            )
        );

        return 1;
    }
}
```

### Enabling the Blocks Editor

Set the editor to `Blocks` for a modal-based content type picker UI:

#### Via Migration

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("LandingPage", type => type
    .WithPart("FlowPart", part => part
        .WithEditor("Blocks")
    )
);
```

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("AgencyPage", type => type
    .WithPart("Services", "BagPart", part => part
        .WithEditor("Blocks")
    )
);
```

#### Via Admin UI

1. Navigate to **Content Definition** → **Content Types**.
2. Edit the content type containing the FlowPart or BagPart.
3. Click **Edit** on the FlowPart or BagPart.
4. Set the **Editor** field to `Blocks`.

### Blocks Editor Settings (BagPart)

When the Blocks Editor is enabled, the following additional settings are available:

| Setting          | Description                                                           |
|------------------|-----------------------------------------------------------------------|
| Add Button Text  | Custom text for the "Add" button. Defaults to "Add Block".            |
| Modal Title Text | Custom title for the content type picker modal. Defaults to "Select Block". |

### Hiding Empty Flows and Bags

Use placement to suppress empty containers:

```json
{
    "FlowPart_Empty": [
        {
            "place": "-"
        }
    ],
    "BagPart_Empty": [
        {
            "place": "-"
        }
    ]
}
```

To render empty containers with the same template as populated ones:

```json
{
    "FlowPart_Empty": [
        {
            "shape": "FlowPart"
        }
    ]
}
```

### Templating BagPart - Decoupled Approach

Access content items directly through the named BagPart, bypassing display management:

#### Liquid

```liquid
{% for service in Model.ContentItem.Content.Services.ContentItems %}
    <h4 class="service-heading">{{ service.DisplayText }}</h4>
    <p class="text-muted">{{ service.HtmlBodyPart.Html | raw }}</p>
{% endfor %}
```

#### Razor

```html
@foreach (var item in Model.ContentItem.Content.Services.ContentItems)
{
    <h4 class="service-heading">@item.DisplayText</h4>
    <p class="text-muted">@Html.Raw(item.Content.HtmlBodyPart.Html)</p>
}
```

### Templating BagPart - Display Management Approach

Use display management to build and render child shapes with proper alternate resolution:

#### Liquid

```liquid
<section class="flow">
    {% for item in Model.ContentItems %}
        {{ item | shape_build_display: "Detail" | shape_render }}
    {% endfor %}
</section>
```

#### Razor

```html
@using OrchardCore.Flows.ViewModels

@model BagPartViewModel
@inject OrchardCore.ContentManagement.Display.IContentItemDisplayManager ContentItemDisplayManager

<section class="flow">
    @foreach (var item in Model.BagPart.ContentItems)
    {
        var itemContent = await ContentItemDisplayManager.BuildDisplayAsync(
            item,
            Model.BuildPartDisplayContext.Updater,
            Model.Settings.DisplayType ?? "Detail",
            Model.BuildPartDisplayContext.GroupId);

        @await DisplayAsync(itemContent)
    }
</section>
```

### Template Alternates

BagPart supports standard shape alternates. For named BagParts, include the part name in the alternate:

| Alternate Template File         | Description                                            |
|---------------------------------|--------------------------------------------------------|
| `MyBag-BagPart.liquid`          | Alternate for content type `MyBag` and `BagPart`       |
| `MyBag-MyNamedBagPart.liquid`   | Alternate for content type `MyBag` and named part `MyNamedBagPart` |

Use `ConsoleLog` (Razor) or `console_log` (Liquid) to inspect all available alternates for a shape.

### Placement Differentiator

Use the named BagPart name as the differentiator in `placement.json`:

```json
{
    "BagPart": [
        {
            "differentiator": "Services",
            "place": "Content:1"
        }
    ]
}
```

### Recipe: Creating a Page with FlowPart Content

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "[js:uuid()]",
          "ContentType": "LandingPage",
          "DisplayText": "Welcome",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Welcome"
          },
          "FlowPart": {
            "Widgets": [
              {
                "ContentItemId": "[js:uuid()]",
                "ContentType": "Paragraph",
                "DisplayText": "Intro",
                "TitlePart": {
                  "Title": "Intro"
                },
                "HtmlBodyPart": {
                  "Html": "<p>Welcome to our site!</p>"
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

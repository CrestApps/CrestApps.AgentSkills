---
name: orchardcore-widgets
description: Skill for creating and managing widgets in Orchard Core. Covers widget content types, zones, layers, layer rules, widget placement configuration, Widget shape templates and wrappers, safe rendering of widget HTML, and ManageLiquidTemplates for Liquid widgets. Use this skill when requests mention Orchard Core Widgets, Create and Manage Widgets, Enabling Widget Features, Creating a Widget Content Type, Layer Configuration via Recipe, Layer Rule Expressions, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Widgets, OrchardCore.Layers, OrchardCore.Flows, FlowPart, DataMigration, IContentDefinitionManager, WithPart, WithField, LayerRule, Paragraph, RawHtml, Blockquote, Image, Container. It also helps with widget examples, Layer Configuration via Recipe, Layer Rule Expressions, Placing Widgets in Zones, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Widgets - Prompt Templates

## Create and Manage Widgets

You are an Orchard Core expert. Generate widget definitions, layer configurations, and zone placement for Orchard Core.

### Guidelines

- Widgets are content types with the `Widget` stereotype.
- Widgets are placed in zones defined by the theme layout.
- Layers control when widgets are visible using rule expressions.
- Enable `OrchardCore.Widgets` and `OrchardCore.Layers` for widget support.
- Each widget is a content item that can be edited from the admin panel.
- Layer rules are composed of conditions (URL, role, culture, homepage, authentication, content type, JavaScript, Boolean, All/Any groups); see `orchardcore-layers` and `orchardcore-rules`.
- Widgets that contain a `LiquidPart`, `LiquidField`, or `FacebookPluginPart` can only be edited or published by users with `ManageLiquidTemplates`, and so can the pages that embed them in a `FlowPart`, `BagPart`, or `WidgetsListPart`.
- HTML and Markdown in widgets are stored unsanitized; render the part or field shapes in widget templates rather than writing stored `Html` with `raw`.
- Use `FlowPart` to allow widgets inside content items.
- `WidgetsListPart` editor wrapper placement uses `ContentPart_Edit` with differentiator `{ContentType}-{PartName}`.
- `WidgetsListPart` does not render its own front-end display shape; it injects widgets directly into layout zones.

### Enabling Widget Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Widgets",
        "OrchardCore.Layers",
        "OrchardCore.Flows"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Widget Content Type

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
        await _contentDefinitionManager.AlterTypeDefinitionAsync("{{WidgetName}}", type => type
            .DisplayedAs("{{WidgetDisplayName}}")
            .Stereotype("Widget")
            .WithPart("{{WidgetName}}", part => part
                .WithPosition("0")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("{{WidgetName}}", part => part
            .WithField("{{FieldName}}", field => field
                .OfType("{{FieldType}}")
                .WithDisplayName("{{FieldDisplayName}}")
                .WithPosition("0")
            )
        );

        return 1;
    }
}
```

### Layer Configuration via Recipe

```json
{
  "steps": [
    {
      "name": "Layers",
      "Layers": [
        {
          "Name": "Always",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "BooleanCondition",
                "Value": true
              }
            ]
          },
          "Description": "Widgets on this layer are always displayed."
        },
        {
          "Name": "Homepage",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "HomepageCondition",
                "Value": true
              }
            ]
          },
          "Description": "Widgets on this layer are displayed on the homepage."
        },
        {
          "Name": "Authenticated",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "IsAuthenticatedCondition",
                "Value": true
              }
            ]
          },
          "Description": "Widgets on this layer are displayed for authenticated users."
        }
      ]
    }
  ]
}
```

### Layer Rule Expressions

Common layer rule conditions:

- `UrlCondition` - Match by URL pattern (regex): `^/$` (homepage), `^/blog/.*` (blog section).
- `IsAuthenticatedCondition` - Visible for authenticated users.
- `IsAnonymousCondition` - Visible for anonymous users.
- `RoleCondition` - Visible for specific roles.
- `CultureCondition` - Visible for specific cultures.
- `BooleanCondition` - Always true/false.
- `HomepageCondition` - Visible when the request targets the homepage.
- `JavascriptCondition` - Custom JavaScript expression.

### Placing Widgets in Zones

Widgets are assigned to zones and layers via the admin UI or recipes:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "widget-hero-banner",
          "ContentType": "HeroBanner",
          "DisplayText": "Welcome Banner",
          "Latest": true,
          "Published": true,
          "LayerMetadata": {
            "Layer": "Homepage",
            "Zone": "Header",
            "Position": 0
          },
          "HeroBanner": {
            "Heading": {
              "Text": "Welcome to Our Site"
            },
            "Description": {
              "Html": "<p>Your one-stop solution.</p>"
            }
          }
        }
      ]
    }
  ]
}
```

### Widget Template (Liquid)

The `Widget` shape exposes `Model.ContentItem`, the `Model.Content` zone with
the shapes of its parts and fields, and `Model.Header`, `Model.Meta`,
`Model.Footer`, `Model.Classes`. Create `Views/Widget-{{WidgetName}}.liquid`
(`Widget__{{WidgetName}}`):

```liquid
<div class="{{ Model.Classes | join: " " }}">
    <h3>{{ Model.ContentItem.Content.{{WidgetName}}.Heading.Text }}</h3>
    <div class="widget-body">
        {{ Model.Content["{{WidgetName}}-Body"] | shape_render }}
    </div>
</div>
```

Rendering the `{PartName}-{FieldName}` field shape processes shortcodes and
sanitizes the HTML. The built-in themes render `Paragraph` and `RawHtml` the
same way (`{{ Model.Content.Paragraph-Content | shape_render }}`).

### Widget Template (Razor)

Create a template at `Views/Widget-{{WidgetName}}.cshtml`:

```cshtml
<div class="@string.Join(" ", Model.Classes)">
    <h3>@Model.ContentItem.Content.{{WidgetName}}.Heading.Text</h3>
    <div class="widget-body">
        @await DisplayAsync(Model.Content.Named("{{WidgetName}}-Body"))
    </div>
</div>
```

### Widget Wrapper

Widgets rendered by Layers are wrapped by `Widget_Wrapper`
(`Widget.Wrapper.cshtml`) that adds the title container. Override it, or the
`Widget-{{WidgetName}}.Wrapper` / `Widget-Zone-Footer.Wrapper` alternates, to
remove the wrapper markup:

```liquid
{{ Model.Content | shape_render }}
```

### FlowPart for Inline Widgets

Allow widgets inside content items using `FlowPart`:

```csharp
await _contentDefinitionManager.AlterTypeDefinitionAsync("Page", type => type
    .WithPart("FlowPart")
);
```

FlowPart allows editors to add widgets inline within content item editing.

### WidgetsListPart Editor Placement

To hide or move the whole `WidgetsListPart` editor row in the admin UI:

```json
{
  "ContentPart_Edit": [
    {
      "differentiator": "PlacementTest-WidgetsListPart",
      "place": "-"
    }
  ]
}
```

If the part is attached as a named part, replace `WidgetsListPart` with the part name.

### Built-in Widget Content Types

The default recipes (Blog, Agency) create widget types such as:

- `Paragraph` (an `HtmlField` named `Content`)
- `RawHtml` (an `HtmlField` named `Content`)
- `Blockquote`
- `MediaWidget` / `Image`
- `Container`

### Widget Best Practices

- Use the `Widget` stereotype for all widget content types.
- Keep widget templates small and focused.
- Use layer rules to control visibility instead of conditional logic in templates.
- Test widgets across different zones and screen sizes.
- Use `FlowPart` for content-embedded widgets.

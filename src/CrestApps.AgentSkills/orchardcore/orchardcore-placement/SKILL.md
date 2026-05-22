---
name: orchardcore-placement
description: Skill for configuring Orchard Core placement, including placement.json, tabs, cards, columns, alternates, wrappers, dynamic placement providers, and fluent location syntax in display drivers. Use this skill when requests mention Orchard Core Placement, Configure Orchard Core placement, Placement string format, Segment meanings, Important ordering rules, Valid examples, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with PlacementLocationBuilder, IShapePlacementProvider, MyPart, TextField, MyShape, SecretShape, MyPlacementProvider, IPlacementInfoResolver, IBuildShapeContext, ShapePlacementContext, IServiceCollection, placement.json. It also helps with placement examples, Important ordering rules, Valid examples, placement.json examples, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.2"
---

# Orchard Core Placement - Prompt Templates

## Configure Orchard Core placement

You are an Orchard Core expert. Generate correct placement for shapes, editor shapes, tabs, cards, columns, and fluent display-driver locations.

### Guidelines

- `placement.json` is the standard way to place shapes in modules and themes.
- Display drivers can also place editor/display shapes with `.Location("...")` or the fluent `PlacementLocationBuilder`.
- Placement can target a zone only, or a zone plus editor groupings such as tabs, cards, and columns.
- For Orchard Core editors that use tabs/cards/columns, the view must render Orchard's grouped output, typically with `@await DisplayAsync(Model.Content)`.
- Use tabs, cards, and columns only when Orchard Core is responsible for rendering the grouped editor UI.
- Column names are arbitrary labels used for CSS classes; they do not imply left/right behavior.
- Prefer neutral column names such as `Col`, `Main`, `Sidebar`, or `Details` unless a name is intentionally tied to custom CSS.
- Use `-` to hide a shape.
- Use `alternates` to swap templates and `wrappers` to wrap a shape in additional markup.
- Use a custom `IShapePlacementProvider` only when placement must be computed dynamically.
- Shape type and differentiator are calculated from the rendered shape, not from the content part or field name alone.
- Use `ContentPart_Edit` to hide or move a whole content part editor row in the admin UI. Its differentiator is `{ContentType}-{PartName}`.
- Inner part editor shapes such as `BagPart_Edit`, `FlowPart_Edit`, or `TitlePart_Edit` use `{PartName}` as differentiator and only affect the inner editor content, not the wrapper.
- Standard field shapes use `{PartName}-{FieldName}` as differentiator.
- Field display-mode shapes use `{PartName}-{FieldName}-{FieldType}_Display__{DisplayMode}` as differentiator.
- Always seal classes.

## Placement string format

The placement format supports these segments:

```text
Zone:position#TabName;tabPosition%CardName;cardPosition|ColumnName[_width][;columnPosition]
```

Every segment after `Zone:position` is optional.

### Segment meanings

- `Zone` - target zone such as `Content`, `Header`, `Meta`, `Actions`, or `Parts`
- `:position` - position within the zone or within the current grouping
- `#TabName;tabPosition` - editor tab name and the tab's ordering position
- `%CardName;cardPosition` - editor card name and the card's ordering position
- `|ColumnName[_width][;columnPosition]` - editor column name, optional Bootstrap width modifier, and optional column ordering position

### Important ordering rules

- The separators must appear in this order when combined: `#`, then `%`, then `|`.
- Use `;` before the group position for tabs, cards, and columns.
- For columns, place the optional width after the name using `_`, as in `|Col_4;2` or `|Sidebar_lg-3;2`.
- Do not use `:` after a tab, card, or column name. `#General:1` creates the literal tab name `General:1`, which is wrong.

### Valid examples

| Placement | Meaning |
| --- | --- |
| `Content:5` | Place the shape in the `Content` zone at position 5 |
| `Content:1#General;1` | Place in the `General` tab |
| `Content:4%Interaction;1` | Place in the `Interaction` card inside `Content` |
| `Content:4#Capabilities;8%Tools;3` | Place in the `Capabilities` tab, then the `Tools` card |
| `Content:4#Capabilities;8%Tools;3|Col_4;2` | Place in a 4-wide column, ordered second, inside the `Tools` card inside the `Capabilities` tab |

## Differentiator patterns

| Shape type | Typical usage | Differentiator pattern | Example |
| --- | --- | --- | --- |
| `BagPart`, `FlowPart`, `TitlePart` | Part display shape or inner `XxxPart_Edit` shape from a part driver | `{PartName}` | `Services`, `FlowPart`, `TitlePart` |
| `ContentPart` | Display wrapper for parts without a dedicated display driver | `{PartName}` | `GalleryPart` |
| `ContentPart_Edit` | Whole content part editor row in admin | `{ContentType}-{PartName}` | `PlacementTest-BagPart`, `LandingPage-Services`, `Article-TitlePart` |
| `TextField`, `HtmlField`, `ContentPickerField`, etc. | Standard field display/editor shape | `{PartName}-{FieldName}` | `Article-Subtitle`, `Address-City` |
| `TextField_Display`, `HtmlField_Display`, etc. | Field display-mode shape | `{PartName}-{FieldName}-{FieldType}_Display__{DisplayMode}` | `Blog-Subtitle-TextField_Display__Header` |

`PartName` is the attached part name. For non-named parts it is usually the part type, such as `BagPart`, `FlowPart`, `WidgetsListPart`, or `TitlePart`. For named parts, use the custom name such as `Services`.

## placement.json examples

### Basic placement

```json
{
  "TextField_Edit": [
    {
      "place": "Content:2"
    }
  ],
  "MyPart_Edit": [
    {
      "place": "Content:5"
    }
  ]
}
```

### Cards inside the content editor

```json
{
  "AIProfileGeneralFields_Edit": [
    {
      "place": "Content:1%General;1"
    }
  ],
  "AIProfileDeployment_Edit": [
    {
      "place": "Content:2%Deployments;1"
    }
  ],
  "AIProfileInteractionFields_Edit": [
    {
      "place": "Content:3%Interactions;2"
    }
  ]
}
```

### Tabs and cards together

```json
{
  "MyTools_Edit": [
    {
      "place": "Content:7#Capabilities;8%Tools;3"
    }
  ],
  "MyAgents_Edit": [
    {
      "place": "Content:5#Capabilities;8%Agents;2"
    }
  ]
}
```

### Cards and columns together

```json
{
  "MyFieldA_Edit": [
    {
      "place": "Content:1%Layout;1|Col_4;1"
    }
  ],
  "MyFieldB_Edit": [
    {
      "place": "Content:1%Layout;1|Col_4;2"
    }
  ],
  "MyFieldC_Edit": [
    {
      "place": "Content:1%Layout;1|Col_4;3"
    }
  ],
  "MyLargeField_Edit": [
    {
      "place": "Content:2%Layout;1"
    }
  ]
}
```

This places `MyFieldA_Edit`, `MyFieldB_Edit`, and `MyFieldC_Edit` in three separate `col-md-4` wrappers on the same row, then renders `MyLargeField_Edit` full-width below the row in the same card.

### Content type and display type filters

```json
{
  "MyPart": [
    {
      "contentType": ["BlogPost"],
      "displayType": "Detail",
      "place": "Content:5"
    },
    {
      "contentType": ["Article"],
      "displayType": "Summary",
      "place": "Meta:2"
    }
  ]
}
```

### Differentiator, alternates, wrappers, and hide

```json
{
  "TextField": [
    {
      "differentiator": "BlogPost-Subtitle",
      "place": "Content:2"
    }
  ],
  "MyShape": [
    {
      "alternates": ["MyShape__BlogPost"],
      "wrappers": ["MyShape_Wrapper"],
      "place": "Content:5"
    }
  ],
  "SecretShape": [
    {
      "place": "-"
    }
  ]
}
```

### Hiding whole part editor wrappers

```json
{
  "ContentPart_Edit": [
    {
      "differentiator": "PlacementTest-BagPart",
      "place": "-"
    },
    {
      "differentiator": "PlacementTest-FlowPart",
      "place": "-"
    },
    {
      "differentiator": "PlacementTest-WidgetsListPart",
      "place": "-"
    },
    {
      "differentiator": "PlacementTest-TitlePart",
      "place": "-"
    }
  ]
}
```

Use this pattern when you need to hide or move the whole editor row. Do not use `BagPart_Edit`, `FlowPart_Edit`, `WidgetsListPart_Edit`, or `TitlePart_Edit` for this unless you intentionally want to target only the inner editor shape.

## Display driver placement

You can use either the string form or the fluent builder form.

### String form

```csharp
return Initialize<MyViewModel>("MyShape_Edit", model =>
{
    model.Value = value;
})
.Location("Content:4%Interaction;1");
```

### Fluent card placement

```csharp
return Initialize<MyViewModel>("MyShape_Edit", model =>
{
    model.Value = value;
})
.Location(c => c.Zone("Content", "4").Card("Interaction", "1"));
```

### Fluent tab, card, and column placement

```csharp
return Initialize<MyViewModel>("MyShape_Edit", model =>
{
    model.Value = value;
})
.Location(c => c
    .Zone("Content", "4")
    .Tab("Capabilities", "8")
    .Card("Tools", "3")
    .Column("Col", "2", "4"));
```

This produces a `col-md-4` column ordered second within the card.

### Fluent layout zone placement

```csharp
return Initialize<MyViewModel>("MyShape", model =>
{
    model.Value = value;
})
.Location(c => c.Zone("Content", "5").AsLayoutZone());
```

Use `.AsLayoutZone()` when the placement should be treated as a layout zone rather than an editor grouping.

## Custom placement provider

Use an `IShapePlacementProvider` when placement depends on runtime conditions.

```csharp
using OrchardCore.DisplayManagement.Descriptors.ShapePlacementStrategy;

public sealed class MyPlacementProvider : IShapePlacementProvider
{
    public Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        return Task.FromResult<IPlacementInfoResolver>(new Resolver());
    }

    private sealed class Resolver : IPlacementInfoResolver
    {
        public PlacementInfo ResolvePlacement(ShapePlacementContext placementContext)
        {
            if (placementContext.ShapeType == "MyShape")
            {
                return new PlacementInfo
                {
                    Location = "Content:5#General;1%Details;1",
                };
            }

            return null;
        }
    }
}
```

### Registering the provider

```csharp
using OrchardCore.DisplayManagement.Descriptors.ShapePlacementStrategy;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IShapePlacementProvider, MyPlacementProvider>();
    }
}
```

## Orchard Core editor grouping guidance

- Use tabs when you need top-level editor sections.
- Use cards when you need visually grouped fields inside a zone or tab.
- Use columns when a card needs multi-column layout.
- Each shape with a column modifier gets its own column wrapper inside the row.
- Shapes without a column modifier render full-width outside the row, before or after the column row based on placement order.
- Use the column width modifier to control Bootstrap sizing, for example `_4` for `col-md-4` or `_lg-3` for `col-lg-3`.
- If the width is omitted, Orchard Core uses an equal-width Bootstrap column (`col-md`).
- When placing display-driver editor shapes into cards, prefer keeping everything inside the `Content` zone unless Orchard specifically expects another zone.
- In CrestApps-style editors, a card-only placement such as `Content:4%Interaction;1` is valid and preferred over inventing custom zones like `Interaction:10`.

## Common zones

- `Content`
- `Content:before`
- `Content:after`
- `Header`
- `Navigation`
- `Sidebar`
- `Meta`
- `Tags`
- `Actions`
- `Footer`

---
name: orchardcore-placement
description: Skill for configuring Orchard Core placement, including placement.json, tabs, cards, columns, groups, alternates, wrappers, positions with before, after, start and end sentinels, placement precedence, dynamic placement providers, and fluent location syntax in display drivers. Use this skill when requests mention Orchard Core Placement, Configure Orchard Core placement, Placement string format, Segment meanings, Important ordering rules, Valid examples, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with PlacementLocationBuilder, IShapePlacementProvider, IPlacementInfoResolver, PlacementInfo, IBuildShapeContext, ShapePlacementContext, ContentPart_Edit, TextField, differentiators, placement.json. It also helps with placement examples, editor grouping, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
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
- `@group` - a group identifier used to filter shapes (for example `OnGroup` site settings sections)

### Position values

Positions sort numerically and hierarchically, never alphabetically:

```text
start  <  before  <  0 (empty)  <  1  <  1.1  <  2  <  10  <  after  <  free-text  <  end
```

- `before` and `after` are anchors inside the numeric range. Non-numeric positions (for example `PrefixPosition()` values that alphabetize admin menu items) sort after `after`.
- Use `start` or `end` when a shape must be truly first or last, regardless of any other position. Sub-ordering works the same way: `end` < `end.50` < `end.100`.
- Shapes with the same position keep their registration order. The module or theme name never affects order.

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
| `Content:end` | Place truly last in the `Content` zone |
| `/Footer:1` | Place in the `Footer` layout zone instead of a local content zone |
| `Parameters:1@search` | Place with the `search` group identifier (fluent `.Group("search")`) |

## Differentiator patterns

| Shape type | Typical usage | Differentiator pattern | Example |
| --- | --- | --- | --- |
| `BagPart`, `FlowPart`, `TitlePart` | Part display shape or inner `XxxPart_Edit` shape from a part driver | `{PartName}` | `Services`, `FlowPart`, `TitlePart` |
| `ContentPart` | Display wrapper for parts without a dedicated display driver | `{PartName}` | `GalleryPart` |
| `ContentPart_Edit` | Whole content part editor row in admin | `{ContentType}-{PartName}` | `PlacementTest-BagPart`, `LandingPage-Services`, `Article-TitlePart` |
| `TextField`, `HtmlField`, `ContentPickerField`, etc. | Standard field display/editor shape | `{PartName}-{FieldName}` | `Article-Subtitle`, `Address-City` |
| `TextField_Display`, `HtmlField_Display`, etc. | Field display-mode shape | `{PartName}-{FieldName}-{FieldType}_Display__{DisplayMode}` | `Blog-Subtitle-TextField_Display__Header` |

`PartName` is the attached part name. For non-named parts it is usually the part type, such as `BagPart`, `FlowPart`, `WidgetsListPart`, or `TitlePart`. For named parts, use the custom name such as `Services`.

## placement.json rules

`placement.json` sits at the **root** of a module or theme. Each property is a shape type; its value is an array of rules. A rule combines filters (which shapes) with placement info (what to do). Every filter is optional.

| Filter | Matches |
| --- | --- |
| *(property name)* | The shape's original type, such as `TextField`, `ContentPart`, `ContentPart_Edit`, `Parts_Contents_Publish` |
| `displayType` | `Detail`, `Summary`, `SummaryAdmin`, `SummaryAdminGrid`, `DetailAdmin`, `Edit`, or a custom display type |
| `differentiator` | One instance of a reused shape (see the table above) |
| `contentType` | A content type or stereotype, or an array; `Art*` matches every type starting with `Art` |
| `contentPart` | A part name, or an array, that the content item must contain |
| `path` | A request path, or an array |

| Placement info | Effect |
| --- | --- |
| `place` | `Zone:position` plus optional groupings; `-` hides the shape; a leading `/` targets a layout zone |
| `alternates` | Adds alternate shape types (more specific templates) |
| `wrappers` | Wraps the shape in other shapes |
| `shape` | Substitutes another shape type |

Custom filters implement `IPlacementNodeFilterProvider` (`Key` is the JSON property name, `IsMatch(ShapePlacementContext, object expression)` evaluates it) and are registered as scoped services.

A layout zone target (`/Sidebar:5`) is only visible if the theme layout renders it with `RenderSectionAsync("Sidebar", required: false)` or `{% render_section "Sidebar", required: false %}`.

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

### Dynamic parts in a custom zone

A part defined only in the content definition (no driver) renders as `ContentPart` in `Detail` and `ContentPart_Summary` in `Summary`, with the part name as differentiator. A rule keyed on the part name (`GalleryPart`) never matches.

```json
{
  "ContentPart": [
    {
      "place": "MyGalleryZone",
      "differentiator": "GalleryPart"
    }
  ],
  "ContentPart_Summary": [
    {
      "place": "MyGalleryZone",
      "differentiator": "GalleryPart"
    }
  ]
}
```

Render the custom zone where it belongs in the content template, for example in `Content-Product.cshtml`:

```cshtml
@await DisplayAsync(Model.MyGalleryZone)
```

The part itself still renders with `GalleryPart.cshtml` (or `GalleryPart.Summary.cshtml`).

### Field display modes

A field rendered with a display mode uses the shape type `{FieldType}_Display` and the full differentiator:

```json
{
  "TextField_Display": [
    {
      "place": "Content:1",
      "differentiator": "Blog-MyField-TextField_Display__Header"
    }
  ]
}
```

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

## Placement precedence

1. Tenant rules from the `OrchardCore.Placements` module (admin UI or `OrchardCore.Placements.FileStorage`) override everything below.
2. The startup project, which acts as a super-theme.
3. The active theme for the request (front-end or admin) and its base themes.
4. Modules in dependency order.
5. Driver defaults from `.Location(...)`.

Display types do not inherit placement: rules for `SummaryAdmin` do not reach `SummaryAdminGrid` (admin Grid layout). See `orchardcore-admin-lists`.

## Custom placement provider

Use an `IShapePlacementProvider` when placement depends on runtime conditions.

```csharp
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;

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
                // PlacementInfo is immutable; FromLocation parses and caches the location string.
                return PlacementInfo.FromLocation("Content:5#General;1%Details;1");
            }

            return null;
        }
    }
}
```

### Registering the provider

```csharp
using OrchardCore.DisplayManagement.Descriptors;

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
- Widths above 12 wrap to the next line (standard Bootstrap behavior).
- All column shapes at the same grouping level (same tab or card) share one row. For two separate rows of columns, put them in two cards.
- Shapes without any grouping fall into a default `Content` group as soon as other shapes in the zone use groupings.
- The shape position (`:N`) orders shapes inside a group; the group position (`;N`) orders the tabs, cards, or columns themselves. In `Parts:2#Settings;1`, the shape is at position 2 and the `Settings` tab is at group position 1.
- When placing display-driver editor shapes into cards, prefer keeping everything inside the `Content` zone unless Orchard specifically expects another zone.
- In CrestApps-style editors, a card-only placement such as `Content:4%Interaction;1` is valid and preferred over inventing custom zones like `Interaction:10`.

## Common zones

- `Content` (use `Content:before`, `Content:after`, `Content:start`, or `Content:end` for positions)
- `Header`
- `Navigation`
- `Sidebar`
- `Meta`
- `Tags`
- `Actions`
- `Footer`

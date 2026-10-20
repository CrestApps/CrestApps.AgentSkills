---
name: orchardcore-shapes
description: Skill for working with shapes in Orchard Core's display management system. Covers shape creation, Liquid and Razor shape templates, template file naming and canonical shape names, alternates and their priority, wrappers and ChildContent, shape metadata, ad-hoc and strongly typed shapes, IShapeFactory, IShapeTableProvider, IShapeAttributeProvider code bindings, layouts and zones, item positions including start and end sentinels, DisplayAsAsync, IShapeHtmlRenderer, and rendering shapes in Liquid. Use this skill when requests mention Orchard Core Shapes, Working with Shapes, How Shapes Work, Shape Metadata, Creating Shape Templates, Liquid Shape Template (Views/Content.liquid), alternates not being picked, wrappers, zones, or related troubleshooting. Strong matches include OrchardCore.DisplayManagement, IShapeFactory, IShapeTableProvider, IDisplayHelper, IShapeHtmlRenderer, ShapeMetadata, IDisplayManager, IDisplayResult, TextField, TitlePart, HtmlBodyPart.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Shapes - Prompt Templates

## Working with Shapes

You are an Orchard Core expert. Generate shape templates, shape providers, and shape-related code for Orchard Core.

### Guidelines

- Shapes are the fundamental rendering unit in Orchard Core's display management.
- Every piece of visible output is rendered through a shape.
- Shapes have a type name (e.g., `Content`, `Widget`, `TextField`), metadata, and properties.
- Shape templates can be written in Liquid (`.liquid`) or Razor (`.cshtml`). Both use the same file naming rules.
- Use canonical names (`_` and `__`) in C#, `placement.json`, and shape metadata. Use dots and hyphens only in template file names.
- For `DisplayDriver<T>.Edit()` flows, remember the root editor shape for the model type as well as any nested field/editor shapes. If `BuildEditorAsync()` is used for `TableauExportPipeline`, Orchard will resolve a root `TableauExportPipeline_Edit` shape in addition to shapes like `TableauExportPipelineFields_Edit`.
- For ALL admin editor Razor shapes (`*.Edit.cshtml` including site settings editors), shape templates should use the `ocat-*` (Orchard Core Admin Theme) CSS classes for consistent field layout: `ocat-wrapper` for the outer row, `ocat-label` for labels, `ocat-end` for the input column, `ocat-end-offset` for checkbox-only layouts/headings/buttons, `ocat-limited-wrapper` + `ocat-limited` for narrower inputs.
- The former `@Orchard.GetWrapperClasses()`, `@Orchard.GetLabelClasses()`, `@Orchard.GetEndClasses()` helper methods and `TheAdminThemeOptions` class have been removed. Use the static `ocat-*` CSS classes directly. Do NOT use these classes in frontend views or Admin Menu node editing.
- `IShapeFactory` creates shapes dynamically from code; `IDisplayHelper` renders them inside a view; `IShapeHtmlRenderer` renders them to a string outside of a view.
- `IShapeTableProvider` customizes shape behavior (alternates, wrappers, bindings).
- `IDisplayManager<T>` orchestrates building and rendering shapes for content.
- Always seal classes.

## How Shapes Work

Shapes are objects that carry data and metadata for rendering. The rendering pipeline:

1. A **display driver** returns an `IDisplayResult` (usually a `ShapeResult`).
2. **Placement** rules are resolved first; a hidden shape is never created.
3. The **display manager** builds the shape using `IShapeFactory` and adds it to the selected zone.
4. When rendered, `Displaying` handlers run, then the **binding** is resolved by checking alternates from highest to lowest priority, then the shape type and its `__` fallbacks.
5. The binding (Razor, Liquid, or a `[Shape]` method) renders the shape, then **wrappers** render around its `ChildContent`.

If no binding can be resolved, rendering throws an `InvalidOperationException` naming the missing shape type and active theme.

### Shape Metadata

Every shape has a `Metadata` property (`ShapeMetadata`) with:

- `Type` — The logical shape type used for binding resolution (not the CLR type).
- `DisplayType` — The display context (e.g., `Detail`, `Summary`, `Edit`).
- `Alternates` — Ordered alternate binding names; **the last added has the highest priority**.
- `Wrappers` — Shape types rendered around this shape; the first added is the innermost.
- `Name` / `Differentiator` — Identify the shape inside its parent and among reused shape types.
- `Position` — The position within a zone.
- `Prefix` — HTML field prefix used while rendering editors.
- `TabGrouping`, `CardGrouping`, `ColumnGrouping` — Editor grouping set by placement.
- `PlacementSource` — Where the placement rule came from.
- `ChildContent` — The rendered output so far (set by the binding, cache, or wrappers). Do not use it as input data.

## Template File Names

| Canonical binding name | Razor or Liquid file name |
|---|---|
| `Content` | `Content.cshtml` / `Content.liquid` |
| `Content__BlogPost` | `Content-BlogPost.cshtml` / `.liquid` |
| `Content_Summary__BlogPost` | `Content-BlogPost.Summary.cshtml` / `.liquid` |
| `Content_Summary` | `Content.Summary.cshtml` / `.liquid` |
| `Widget__HeroBanner` | `Widget-HeroBanner.cshtml` / `.liquid` |
| `Fields_Common_Text__FirstName` | `Views/Fields/Common.Text-FirstName.cshtml` / `.liquid` |

Rules:

- `-` becomes `__` (breaking separator, creates fallback levels); `.` becomes `_`.
- When the final dot appears after the final hyphen, the suffix is a display type inserted before the first `__`: `Content-BlogPost.Summary` → `Content_Summary__BlogPost`.
- Templates are harvested from `Views`, `Views/Items`, `Views/Parts`, `Views/Fields`, and `Views/Elements`; the last three add `Parts_`, `Fields_`, or `Elements_` prefixes.
- A file named `Content__BlogPost__Summary.liquid` does **not** match the summary alternate; use `Content-BlogPost.Summary.liquid`.

## Creating Shape Templates

### Liquid Shape Template (Views/Content.liquid)

```liquid
<article>
    <header>
        {{ Model.Header | shape_render }}
    </header>

    {{ Model.Content | shape_render }}

    <footer>
        {{ Model.Footer | shape_render }}
    </footer>
</article>
```

### Razor Shape Template (Views/Content.cshtml)

```cshtml
<article>
    <header>
        @await DisplayAsync(Model.Header)
    </header>

    @await DisplayAsync(Model.Content)

    <footer>
        @await DisplayAsync(Model.Footer)
    </footer>
</article>
```

### Root Editor Wrapper Template (Views/TableauExportPipeline_Edit.cshtml)

When a `DisplayDriver<T>` editor is built through `IDisplayManager<T>.BuildEditorAsync()` or `UpdateEditorAsync()`, Orchard also resolves a root editor shape for the model type. That root template usually just renders the editor zones populated by nested shapes:

```cshtml
@await DisplayAsync(Model.Content)
```

Without this wrapper, Orchard throws an `InvalidOperationException` saying the root shape type (for example `TableauExportPipeline_Edit`) was not found.

## Shape Alternates

Alternates let more specific templates override generic ones. Alternates added later win; do not add the base type itself as an alternate.

For a `Content` shape displaying a `BlogPost` content type in `Summary` display type, the resolver checks (most specific first):

| Alternate | File Name |
|---|---|
| `Content_Summary__BlogPost` | `Content-BlogPost.Summary.cshtml` / `.liquid` |
| `Content__BlogPost` | `Content-BlogPost.cshtml` / `.liquid` |
| `Content_Summary` | `Content.Summary.cshtml` / `.liquid` |
| `Content` (base) | `Content.cshtml` / `.liquid` |

Other content alternates include `Content__Alias__[Alias]`, `Content__Slug__[Slug]`, and their display-type variants. See `orchardcore-templates` for the full alternate catalog.

Shape types containing `__` also fall back by removing the last segment: `Car__Toyota__Hybrid` → `Car__Toyota` → `Car`.

## Rendering Shapes in Liquid Templates

### Basic Shape Rendering

```liquid
<!-- Create and render a shape by type, with properties -->
{% shape "ShapeName", prop1: "value1", prop2: "value2" %}

<!-- Create, populate, then render -->
{% assign car = "Car" | shape_new %}
{% shape_add_properties car brand: "Toyota", color: "Blue" %}
{{ car | shape_render }}

<!-- Render a shape object or a zone of the current shape -->
{{ Model.Content | shape_render }}
{{ Model.Header | shape_render }}

<!-- Render a content item with a display type -->
{{ Model.ContentItem | shape_build_display: "Summary" | shape_render }}
```

In a Liquid **layout**, render layout zones with `{% render_section "Header", required: false %}` and the body with `{% render_body %}`. The `{% zone "Footer" %}...{% endzone %}` block adds markup **to** a layout zone; it does not render it.

### Rendering Content Part Shapes in Liquid

```liquid
<!-- Render all shapes in the Content zone -->
{{ Model.Content | shape_render }}

<!-- Render a specific part's shape -->
{{ Model.Content.TitlePart | shape_render }}
{{ Model.Content.HtmlBodyPart | shape_render }}

<!-- Remove one shape by name before rendering the zone -->
{% shape_remove_item Model.Content "HtmlBodyPart" %}
{{ Model.Content | shape_render }}
```

### Iterating Over Shapes in a Zone

```liquid
{% for item in Model.Content %}
    {{ item | shape_render }}
{% endfor %}
```

### Conditional Shape Rendering in Liquid

```liquid
{% if Model.Content.TitlePart %}
    <h1>{{ Model.Content.TitlePart | shape_render }}</h1>
{% endif %}

{% if Model.Footer %}
    <footer>{{ Model.Footer | shape_render }}</footer>
{% endif %}
```

## Rendering Shapes in Razor Templates

```cshtml
@* Render a zone of the current shape *@
@await DisplayAsync(Model.Content)
@await DisplayAsync(Model.Header)

@* Render a specific part shape *@
@await DisplayAsync(Model.Content.TitlePart)

@* Create and render an ad-hoc shape *@
@{
    var shape = await New.MyCustomShape(Property1: "value1");
}
@await DisplayAsync(shape)

@* Tag helper form *@
<shape type="MyCustomShape" prop-property1="value1" />
```

Use `DisplayAsAsync` to render an existing shape as another logical type. It clears alternates by default and changes `Metadata.Type` on the same instance:

```cshtml
@await DisplayAsAsync(Model, "CompactCar")
@await DisplayAsAsync(Model, "CompactCar", clearAlternates: false)
```

## Creating Shapes from Code

### Using IShapeFactory

```csharp
using OrchardCore.DisplayManagement;

public sealed class MyService
{
    private readonly IShapeFactory _shapeFactory;
    private readonly IShapeHtmlRenderer _shapeHtmlRenderer;

    public MyService(IShapeFactory shapeFactory, IShapeHtmlRenderer shapeHtmlRenderer)
    {
        _shapeFactory = shapeFactory;
        _shapeHtmlRenderer = shapeHtmlRenderer;
    }

    public async Task<IShape> CreateCustomShapeAsync()
    {
        // Dynamic shape with copied properties, rendered by "MyCustomShape" templates.
        return await _shapeFactory.CreateAsync("MyCustomShape", Arguments.From(new
        {
            Title = "Hello",
            Description = "World",
        }));
    }

    public async Task<IShape> CreateTypedShapeAsync()
    {
        // Strongly typed shape: the model must be a non-sealed class (a proxy derives from it).
        return await _shapeFactory.CreateAsync<CarViewModel>("Car", car =>
        {
            car.Brand = "Toyota";
            car.Color = "Blue";
        });
    }

    // Outside of a view (API endpoint, background-rendered fragment, JSON response).
    public async Task<string> RenderCustomShapeAsync(CancellationToken cancellationToken)
        => await _shapeHtmlRenderer.RenderAsync(await CreateCustomShapeAsync(), cancellationToken);
}
```

Inside a view, render with `@await DisplayAsync(shape)`. Never call `IDisplayHelper` from an endpoint without a view; use `IShapeHtmlRenderer` instead (`RenderAsync` or `RenderZoneAsync(shape, "Content")`).

### Using Initialize in Display Drivers

Display drivers create shapes using `Initialize<TModel>`:

```csharp
public sealed class MyPartDisplayDriver : ContentPartDisplayDriver<MyPart>
{
    public override IDisplayResult Display(MyPart part, BuildPartDisplayContext context)
    {
        return Initialize<MyPartViewModel, MyPart>("MyPart", static (model, part) =>
        {
            model.Text = part.Text;
            model.ContentItem = part.ContentItem;
        }, part)
        .Location("Detail", "Content:5")
        .Location("Summary", "Content:5");
    }
}
```

See `orchardcore-display-management` for every `ShapeResult` helper (`Initialize`, `Dynamic`, `Copy`, `View`, `Factory`, `Combine`) and the 4.0 typed-state and `ValueTask` overloads.

### Root + Nested Editor Shape Pattern

If your driver returns nested editor shapes like `Initialize<MyViewModel>("MyTypeFields_Edit", ...)`, also add the root editor template for the model type used by `BuildEditorAsync()`:

```csharp
public sealed class MyTypeDisplayDriver : DisplayDriver<MyType>
{
    public override IDisplayResult Edit(MyType model, BuildEditorContext context)
    {
        return Initialize<MyTypeViewModel>("MyTypeFields_Edit", viewModel =>
        {
            viewModel.Name = model.Name;
        })
        .Location("Content:1");
    }
}
```

```cshtml
@* Views/MyType_Edit.cshtml *@
@await DisplayAsync(Model.Content)
```

```cshtml
@* Views/MyTypeFields.Edit.cshtml *@
@model MyTypeViewModel

<input asp-for="Name" class="form-control" />
```

This split keeps the model-level wrapper and the field editor template aligned with Orchard's shape resolution rules.

### Combining Multiple Shapes

```csharp
public override IDisplayResult Display(MyPart part, BuildPartDisplayContext context)
{
    return Combine(
        Initialize<MyPartViewModel>("MyPart", model =>
        {
            model.Text = part.Text;
        }).Location("Detail", "Content:5"),

        Initialize<MyPartSummaryViewModel>("MyPart_Summary", model =>
        {
            model.Summary = part.Summary;
        }).Location("Summary", "Content:5"),

        Dynamic("MyPart_Actions")
            .Location("SummaryAdmin", "Actions:5")
    );
}
```

### Ad-Hoc (Dynamic) Shapes

Create shapes without a specific view model:

```csharp
public override IDisplayResult Display(MyPart part, BuildPartDisplayContext context)
{
    return Dynamic("MyPart_Badge", shape =>
    {
        shape.Text = part.BadgeText;
        shape.CssClass = part.IsActive ? "active" : "inactive";
    })
    .Location("Detail", "Meta:5");
}
```

## Shape Table Providers

`IShapeTableProvider` lets you customize shape behavior globally. The shape table is built once per tenant and theme, so only register behavior (handlers, bindings) there; never resolve request data or register resources inside `DiscoverAsync`.

### Adding Alternates

```csharp
using OrchardCore.DisplayManagement.Descriptors;

public sealed class CarShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Car")
            .OnDisplaying(context =>
            {
                if (context.Shape is CarViewModel car)
                {
                    // Least specific first: the last added alternate wins.
                    context.Shape.Metadata.Alternates.Add($"Car_Color__{car.Color}");
                    context.Shape.Metadata.Alternates.Add($"Car_Brand__{car.Brand}");
                }
            });

        return ValueTask.CompletedTask;
    }
}
```

Values used in binding names should be controlled identifiers, not arbitrary user input.

### Adding Wrappers

```csharp
public sealed class WidgetShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Widget")
            .OnDisplaying(context =>
            {
                // Wrap all widgets with the Widget_CardWrapper template.
                context.Shape.Metadata.Wrappers.Add("Widget_CardWrapper");
            });

        return ValueTask.CompletedTask;
    }
}
```

### Registering a Shape Table Provider

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<CarShapeTableProvider>();
        services.AddShapeTableProvider<WidgetShapeTableProvider>();
    }
}
```

## Shape Wrapper Templates

A wrapper receives the same shape; `Model.Metadata.ChildContent` holds the output rendered so far. Never call `DisplayAsync(Model)` or `shape_render` on `Model` inside a wrapper, which re-renders the shape recursively.

### Liquid Wrapper (Views/Widget.CardWrapper.liquid)

```liquid
<div class="card widget-{{ Model.ContentItem.ContentType | downcase }}">
    <div class="card-body">
        {{ Model.Metadata.ChildContent }}
    </div>
</div>
```

### Razor Wrapper (Views/Widget.CardWrapper.cshtml)

```cshtml
<div class="card">
    <div class="card-body">
        @Model.Metadata.ChildContent
    </div>
</div>
```

Wrappers can also be added from `placement.json` (`"wrappers": [ "Widget_CardWrapper" ]`) or the `<add-wrapper>` metadata tag helper.

## Code Bindings with `[Shape]`

```csharp
using Microsoft.AspNetCore.Html;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;

public sealed class CarShapes : IShapeAttributeProvider
{
    [Shape("Car")]
    public IHtmlContent Car(IShape Shape)
    {
        var car = (CarViewModel)Shape;
        var tagBuilder = Shape.GetTagBuilder("div");
        tagBuilder.InnerHtml.Append($"A {car.Color} {car.Brand} car.");

        return tagBuilder;
    }
}
```

Register with `services.AddShapeAttributes<CarShapes>();`.

## Layouts, Zones, and Item Positions

- `ILayoutAccessor.GetLayoutAsync()` returns the request's `Layout` shape; its zones are `Zone` shapes created on demand.
- Content shapes built by display managers hold `ContentZone` zones, which build tab, card, and column grouping shapes when children carry grouping metadata.
- `await shape.AddAsync(child, "5")` adds a positioned item. Positions sort numerically and hierarchically: `start < before < 0 (empty) < 1 < 1.1 < 2 < 10 < after < free text < end`. Use `start`/`end` when an item must be truly first or last, including relative to free-text positions such as `PrefixPosition()` values.

```csharp
var layout = await _layoutAccessor.GetLayoutAsync();
var menu = await _shapeFactory.CreateAsync("Menu");

await layout.Zones["Header"].AddAsync(menu, "5");
```

## Common Shape Types Reference

| Shape Type | Description | Template |
|---|---|---|
| `Content` | Main content item display | `Content.liquid` |
| `Content_Edit` | Content item editor | `Content.Edit.liquid` |
| `Widget` | Widget display wrapper | `Widget.liquid` |
| `Zone` | Layout zone container | `Zone.liquid` |
| `MenuItem` | Menu item rendering | `MenuItem.liquid` |
| `MenuItemLink` | Menu item with link | `MenuItemLink.liquid` |
| `NavigationItem` | Navigation item | `NavigationItem.liquid` |
| `List` | List part container | `List.liquid` |
| `PagerSlim` | Pagination controls | `PagerSlim.liquid` |
| `Pager` | Full pagination | `Pager.liquid` |
| `TextField` | Text field display | `TextField.liquid` |
| `HtmlField` | HTML field display | `HtmlField.liquid` |
| `BooleanField` | Boolean field display | `BooleanField.liquid` |
| `DateTimeField` | DateTime field display | `DateTimeField.liquid` |
| `NumericField` | Numeric field display | `NumericField.liquid` |
| `MediaField` | Media field display | `MediaField.liquid` |
| `ContentPickerField` | Content picker display | `ContentPickerField.liquid` |
| `TaxonomyField` | Taxonomy field display | `TaxonomyField.liquid` |
| `LinkField` | Link field display | `LinkField.liquid` |

## Overriding Built-In Shape Templates

### Override Content Display (Views/Content.liquid)

```liquid
<article class="content-item content-item-{{ Model.ContentItem.ContentType | downcase }}">
    {% if Model.Header %}
        <header>{{ Model.Header | shape_render }}</header>
    {% endif %}

    {% if Model.Meta %}
        <div class="meta">{{ Model.Meta | shape_render }}</div>
    {% endif %}

    {{ Model.Content | shape_render }}

    {% if Model.Footer %}
        <footer>{{ Model.Footer | shape_render }}</footer>
    {% endif %}
</article>
```

### Override Content for Specific Type (Views/Content-BlogPost.liquid)

```liquid
<article class="blog-post">
    <header>
        <h1>{{ Model.ContentItem.DisplayText }}</h1>
        <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
            {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
        </time>
    </header>

    {{ Model.Content | shape_render }}

    {% if Model.Footer %}
        <footer class="post-footer">
            {{ Model.Footer | shape_render }}
        </footer>
    {% endif %}
</article>
```

### Override Widget (Views/Widget.liquid)

```liquid
<div class="widget widget-{{ Model.ContentItem.ContentType | downcase }}">
    <div class="widget-body">
        {{ Model.Content | shape_render }}
    </div>
</div>
```

### Override Summary Display (Views/Content.Summary.liquid)

```liquid
<article class="content-summary">
    <h3>
        <a href="{{ Model.ContentItem | display_url }}">
            {{ Model.ContentItem.DisplayText }}
        </a>
    </h3>
    {% if Model.Meta %}
        <div class="meta">{{ Model.Meta | shape_render }}</div>
    {% endif %}
    {{ Model.Content | shape_render }}
</article>
```

## Shape Debug Information

Enable **Write shape debug information** in the tenant's **Settings → Debugging** editor. Orchard Core applies that setting to `ShapeRenderingOptions` through `IOptionsMonitor`, so it takes effect without a shell release.

When enabled, each rendered shape is wrapped with comments like:

```html
<!--shape-start type:Content bindings:Content__BlogPost => Views/Content-BlogPost.cshtml (razor) -->
...
<!--shape-end type:Content-->
```

The comment shows the shape type and the binding that won. To find slow shapes rather than template names, collect the `OrchardCore.DisplayManagement` activity source and meter (see `orchardcore-display-management`) or use `orchardcore-mini-profiler`.

When an override is not selected, check: the shape's `Metadata.Type`, `DisplayType`, and ordered `Alternates`; canonical name vs file name form; that the template is under a harvested `Views` folder; whether a later alternate wins; whether placement changed the shape type (which clears alternates and wrappers); and whether the active theme or startup project overrides the module binding.

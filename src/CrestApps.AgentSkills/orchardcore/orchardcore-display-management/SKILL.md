---
name: orchardcore-display-management
description: Skill for using Orchard Core's display management system. Covers display drivers, display managers, ShapeResult helpers (Initialize, Factory, Dynamic, Copy, View, Combine), typed-state ValueTask initializers, shape table providers, display types, placement locations, editor and display mode patterns, IShapeHtmlRenderer for rendering shapes outside of a view, ShapeRenderingOptions and shape debug information, shape rendering telemetry with OpenTelemetry and dotnet-counters, and upgrading display drivers from Orchard Core 3 (obsolete Shape method, Factory overrides). Use this skill when requests mention Orchard Core Display Management, ContentPartDisplayDriver, ContentFieldDisplayDriver, SiteDisplayDriver, IDisplayResult, wrapper templates for DisplayDriver models, rendering a shape to an HTML string, or related troubleshooting. Strong matches include OrchardCore.DisplayManagement, OrchardCore.DisplayManagement.Views, OrchardCore.ContentManagement.Display, DisplayManagementDiagnostics.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Display Management - Prompt Templates

## Create Display Drivers and Shapes

You are an Orchard Core expert. Generate display drivers, shapes, and display management code for Orchard Core.

### Guidelines

- Every content part that needs custom rendering requires a `DisplayDriver`.
- Content part drivers inherit from `ContentPartDisplayDriver<TPart>`, field drivers from `ContentFieldDisplayDriver<TField>`, site settings drivers from `SiteDisplayDriver<TSettings>`, and any other model from `DisplayDriver<TModel>`.
- Drivers handle three operations: `Display`, `Edit`, and `Update` (sync or `...Async` variants).
- Each operation returns `IDisplayResult` (shapes to render). `ShapeResult` resolves placement first and only creates the shape when it is actually displayed.
- View models are used to pass data between drivers and views.
- Shape names follow the convention `{PartName}` for display and `{PartName}_Edit` for editor.
- Use `Initialize<TModel>` to create shapes with a view model. Use `Factory()` when you build the `IShape` yourself. Never use the obsolete `Shape()` method (see Upgrading from 3.x).
- Prefer `static` lambdas with the typed-state overloads on hot paths (drivers that run for every content item in a list) to avoid closure allocations.
- Register drivers in `Startup.cs` using `services.AddContentPart<TPart>().UseDisplayDriver<TDriver>()`.
- For non-content-item models rendered through `DisplayDriver<TModel>`, the root shape still needs its own wrapper template for each display type you build (for example, `CampaignAction.Edit.cshtml` for `CampaignAction_Edit` and `CampaignAction.SummaryAdmin.cshtml` for `CampaignAction_SummaryAdmin`).
- When driver results are placed into zones with `.Location("Content:1")`, `.Location("Actions:5")`, or similar, the wrapper template must render those zones (`Model.Content`, `Model.Actions`, `Model.Meta`, etc.) or the child shapes will never appear.
- For ALL admin editor views (`*.Edit.cshtml` including `*Settings.Edit.cshtml`), use the `ocat-*` (Orchard Core Admin Theme) CSS classes instead of raw `mb-3`, `form-label`, or hard-coded grid classes. Use `ocat-wrapper` for the outer row, `ocat-label` for labels, `ocat-end` for the input column, `ocat-end-offset` for checkbox-only rows/headings/buttons with no left label, `ocat-limited-wrapper` + `ocat-limited` for narrow-width fields. Do NOT apply these classes to frontend views (Login, Register, etc.) or Admin Menu node editing (needs full width).
- The former `@Orchard.GetWrapperClasses()`, `@Orchard.GetLabelClasses()`, `@Orchard.GetEndClasses()` helper methods and `TheAdminThemeOptions` class have been removed. Use the static `ocat-*` CSS classes directly.
- Always seal classes.

### Content Part Display Driver Pattern

```csharp
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;

public sealed class {{PartName}}DisplayDriver : ContentPartDisplayDriver<{{PartName}}>
{
    public override IDisplayResult Display({{PartName}} part, BuildPartDisplayContext context)
    {
        return Initialize<{{PartName}}ViewModel>("{{PartName}}", model =>
        {
            model.{{PropertyName}} = part.{{PropertyName}};
            model.ContentItem = part.ContentItem;
        })
        .Location("Detail", "Content:5")
        .Location("Summary", "Content:5");
    }

    public override IDisplayResult Edit({{PartName}} part, BuildPartEditorContext context)
    {
        return Initialize<{{PartName}}ViewModel>("{{PartName}}_Edit", model =>
        {
            model.{{PropertyName}} = part.{{PropertyName}};
            model.ContentItem = part.ContentItem;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync({{PartName}} part, UpdatePartEditorContext context)
    {
        var model = new {{PartName}}ViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.{{PropertyName}} = model.{{PropertyName}};

        return Edit(part, context);
    }
}
```

### View Model Pattern

```csharp
using OrchardCore.ContentManagement;

public class {{PartName}}ViewModel
{
    public string {{PropertyName}} { get; set; }
    public ContentItem ContentItem { get; set; }
}
```

### Display Shape View (Views/{{PartName}}.cshtml)

```cshtml
@model {{Namespace}}.ViewModels.{{PartName}}ViewModel

<p>@Model.{{PropertyName}}</p>
```

### Editor Shape View (Views/{{PartName}}_Edit.cshtml)

```cshtml
@model {{Namespace}}.ViewModels.{{PartName}}ViewModel

<div class="ocat-wrapper" asp-validation-class-for="{{PropertyName}}">
    <label asp-for="{{PropertyName}}" class="ocat-label">{{DisplayLabel}}</label>
    <div class="ocat-end">
        <input asp-for="{{PropertyName}}" class="form-control" />
        <span asp-validation-for="{{PropertyName}}"></span>
    </div>
</div>
```

### Registering a Display Driver

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Content part driver.
        services.AddContentPart<{{PartName}}>()
            .UseDisplayDriver<{{PartName}}DisplayDriver>();

        // Driver for any other model, e.g. DisplayDriver<CampaignAction>.
        services.AddDisplayDriver<CampaignAction, CampaignActionDisplayDriver>();

        // Site settings driver (SiteDisplayDriver<TSettings>).
        services.AddSiteDisplayDriver<MySettingsDisplayDriver>();
    }
}
```

### Content Part with Handler Pattern

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

public sealed class {{PartName}}Handler : ContentPartHandler<{{PartName}}>
{
    public override Task InitializingAsync(InitializingContentContext context, {{PartName}} part)
    {
        part.{{PropertyName}} = "default value";
        return Task.CompletedTask;
    }
}
```

`IContentPartHandlerResolver.GetHandlers()`, `IContentFieldHandlerResolver.GetHandlers()`, `IContentPartDisplayDriverResolver.GetDisplayModeDrivers()`, and `IContentFieldDisplayDriverResolver.GetDisplayModeDrivers()` resolve once per scope and return the same list for the rest of the scope. Copy the list before adding or removing items.

## ShapeResult Helpers

| Method | Purpose |
|---|---|
| `Initialize<TModel>(shapeType, initialize)` | Create a strongly typed shape and initialize it lazily. Sync (`Action<TModel>`) or async (`Func<TModel, ValueTask>`). |
| `Initialize<TModel, TState...>(shapeType, initialize, state...)` | Same, with up to five typed state values passed to a `static` lambda (sync or `ValueTask`). |
| `Dynamic(shapeType, initialize)` / `Dynamic<TState>(...)` | Create a dynamic `Shape`; initializer is `Action<dynamic>` or `Func<dynamic, ValueTask>`, optionally with typed state. |
| `Copy(shapeType, model)` | Copy the public properties of an object into a dynamic shape. |
| `View(shapeType, model)` | Wrap an existing model in `ShapeViewModel<TModel>`. |
| `Factory(shapeType, builder)` / `Factory<TState>(...)` | Build the `IShape` yourself, lazily. Builders can be sync or return `ValueTask<IShape>`. |
| `Factory<TBuilderState, TInitState>(...)` | Virtual extension point with separate builder and initializer state. |
| `Combine(...)` / `CombineAsync(...)` | Return several display results. |
| `ShapeResult.Create<TBuilderState>()` / `Create<TBuilderState, TInitState>()` | Create a `ShapeResult` with typed state outside a driver. |

`ShapeResult` fluent members include `Location(...)`, `Displaying(...)`, `Processing(...)`, `Name(...)`, `Differentiator(...)`, `OnGroup(...)`, `Cache(cacheId, cache => ...)`, `Prefix(...)`, and `RenderWhen(...)` (also with a typed state overload).

### Typed-State Initializers (No Closures)

```csharp
public override IDisplayResult Display(SubtitlePart part, BuildPartDisplayContext context)
{
    return Initialize<SubtitlePartViewModel, SubtitlePart>("SubtitlePart", static (model, part) =>
    {
        model.Subtitle = part.Subtitle;
        model.ContentItem = part.ContentItem;
    }, part)
    .Location("Detail", "Content:3");
}
```

Two or more states:

```csharp
public override IDisplayResult Display(RatingField field, BuildFieldDisplayContext context)
{
    return Initialize<RatingFieldViewModel, RatingField, BuildFieldDisplayContext>(
        GetDisplayShapeType(context),
        static (model, field, context) =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        },
        field,
        context)
        .Location("Detail", "Content")
        .Location("Summary", "Content");
}
```

### Async Initializer

```csharp
public override IDisplayResult Display(RecentPostsPart part, BuildPartDisplayContext context)
{
    return Initialize<RecentPostsViewModel>("RecentPostsPart", async model =>
    {
        model.Posts = await _postService.GetRecentAsync(part.Count);
    })
    .Location("Detail", "Content:10");
}
```

Async initializers return `ValueTask`. They only run if the shape is placed and displayed, so expensive work is skipped for hidden shapes.

### Factory with Typed State

```csharp
public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
{
    return Factory("ProductBadge", static (buildContext, part) =>
        buildContext.ShapeFactory.CreateAsync<ProductBadgeViewModel, ProductPart>(
            "ProductBadge",
            static (model, part) => model.Label = part.BadgeText,
            part),
        part)
        .Location("Detail", "Meta:5");
}
```

### Overriding the Factory Extension Point

Derived base drivers that intercept every shape override the five-parameter generic `Factory`:

```csharp
public override ShapeResult Factory<TBuilderState, TInitState>(
    string shapeType,
    Func<IBuildShapeContext, TBuilderState, ValueTask<IShape>> shapeBuilder,
    TBuilderState shapeBuilderState,
    Func<IShape, TInitState, ValueTask> initializeAsync,
    TInitState initializeState)
{
    return base.Factory(shapeType, shapeBuilder, shapeBuilderState, initializeAsync, initializeState)
        .OnGroup(MyGroupId);
}
```

### Display Types

Orchard Core uses display types to differentiate how content is rendered:

- `Detail` — Full content display (e.g., a blog post page).
- `Summary` — Abbreviated display (e.g., in a list).
- `SummaryAdmin` — Admin list row in the List layout.
- `SummaryAdminGrid` — Admin list card in the Grid layout. Placement and driver locations for `SummaryAdmin` do not apply to it; see `orchardcore-admin-lists`.
- `DetailAdmin` — Admin detail rendering (for example the admin navbar).
- `Edit` — Editor form for the content part.

### Wrapper Templates for `DisplayDriver<TModel>`

**CRITICAL**: When you use `DisplayDriver<TModel>` (not `ContentPartDisplayDriver`), Orchard Core resolves a **root shape** for each display type. If this root shape template does not exist, you get `InvalidOperationException: The shape type '{ModelName}_Edit' is not found`. You **must** create wrapper templates for every display type the driver builds shapes for.

**Required wrapper templates per display type:**

| Display type built by driver | Required wrapper template file |
|-----|------|
| Edit (via `Edit()` / `BuildEditorAsync`) | `Views/{ModelName}.Edit.cshtml` |
| SummaryAdmin (via `DisplayAsync` + `Location("Content:1")`) | `Views/{ModelName}.SummaryAdmin.cshtml` |
| Detail | `Views/{ModelName}.cshtml` |
| Summary | `Views/{ModelName}.Summary.cshtml` |

**The wrapper template renders the zones that child shapes are placed into.** If the wrapper doesn't render a zone (e.g., `Model.Content`, `Model.Actions`, `Model.Meta`), the child shapes placed in those zones will be silently dropped.

Example driver:

```csharp
internal sealed class CampaignActionDisplayDriver : DisplayDriver<CampaignAction>
{
    public override Task<IDisplayResult> DisplayAsync(CampaignAction model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("CampaignAction_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("CampaignAction_Buttons_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
        );
    }

    public override IDisplayResult Edit(CampaignAction model, BuildEditorContext context)
        => Initialize<CampaignActionViewModel, CampaignAction>("CampaignActionFields_Edit", static (viewModel, model) =>
        {
            viewModel.Name = model.Name;
        }, model)
        .Location("Content:1");
}
```

Required wrapper templates:

- `Views/CampaignAction.Edit.cshtml` for the root `CampaignAction_Edit` shape.
- `Views/CampaignAction.SummaryAdmin.cshtml` for the root `CampaignAction_SummaryAdmin` shape.

Minimal edit wrapper:

```cshtml
@if (Model.Content != null)
{
    @await DisplayAsync(Model.Content)
}
```

Summary admin wrapper with zones:

```cshtml
<div class="row g-0">
    <div class="col-lg col-12">
        @if (Model.Content != null)
        {
            @await DisplayAsync(Model.Content)
        }

        @if (Model.Meta != null)
        {
            @await DisplayAsync(Model.Meta)
        }
    </div>
    <div class="col-lg-auto col-12">
        @if (Model.Actions != null)
        {
            @await DisplayAsync(Model.Actions)
        }
    </div>
</div>
```

Child shape file-name mapping:

- `CampaignAction_Fields_SummaryAdmin` → `Views/Items/CampaignAction.Fields.SummaryAdmin.cshtml`
- `CampaignAction_Buttons_SummaryAdmin` → `Views/Items/CampaignAction.Buttons.SummaryAdmin.cshtml`
- `CampaignActionFields_Edit` → `Views/CampaignActionFields.Edit.cshtml`

### Placing Shapes in Zones

Use `.Location()` to place shapes in zones with positions:

```csharp
return Initialize<MyViewModel>("MyShape", model => { ... })
    .Location("Detail", "Content:5")      // Detail view, Content zone, position 5
    .Location("Summary", "Meta:5")        // Summary view, Meta zone, position 5
    .Location("SummaryAdmin", "Actions:5"); // Admin summary, Actions zone, position 5
```

Positions also accept `before`, `after`, and the terminal sentinels `start` and `end`. See `orchardcore-placement` for the full syntax, tabs, cards, columns, and the fluent `PlacementLocationBuilder`.

### Content Field Display Driver

```csharp
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;

public sealed class {{FieldName}}FieldDisplayDriver : ContentFieldDisplayDriver<{{FieldName}}Field>
{
    public override IDisplayResult Display({{FieldName}}Field field, BuildFieldDisplayContext context)
    {
        return Initialize<{{FieldName}}FieldViewModel>(
            GetDisplayShapeType(context),
            model =>
            {
                model.Field = field;
                model.Part = context.ContentPart;
                model.PartFieldDefinition = context.PartFieldDefinition;
            })
            .Location("Detail", "Content")
            .Location("Summary", "Content");
    }
}
```

### Shape Table Provider

Override shape rendering behavior:

```csharp
using OrchardCore.DisplayManagement.Descriptors;

public sealed class MyShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Content")
            .OnDisplaying(context =>
            {
                // Add alternates, wrappers, etc.
                context.Shape.Metadata.Alternates.Add("Content__{{ContentType}}");
            });

        return ValueTask.CompletedTask;
    }
}
```

Register it with `services.AddShapeTableProvider<MyShapeTableProvider>();`.

## Render a Shape Outside of a View

From an endpoint or service that has no view (for example, returning shape markup in a JSON response), inject `IShapeHtmlRenderer`. Without it, the first shape template of the request would render as a main page wrapped in the theme layout.

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

public sealed class ContentPreviewEndpoint
{
    public static async Task<IResult> HandleAsync(
        string contentItemId,
        IContentManager contentManager,
        IContentItemDisplayManager displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IShapeHtmlRenderer shapeHtmlRenderer,
        HttpContext httpContext)
    {
        var contentItem = await contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            return Results.NotFound();
        }

        var shape = await displayManager.BuildDisplayAsync(contentItem, updateModelAccessor.ModelUpdater, "Summary");

        // The whole shape, or only one zone without the shape's own template.
        var html = await shapeHtmlRenderer.RenderAsync(shape, httpContext.RequestAborted);
        var content = await shapeHtmlRenderer.RenderZoneAsync(shape, "Content", httpContext.RequestAborted);

        return Results.Json(new { html, content });
    }
}
```

`RenderZoneAsync` returns an empty string when the zone is missing or empty. The cancellation token is checked before rendering starts. Authorize access to the content item before rendering it.

### Shape Debug Information

You can instruct Orchard Core to write HTML comments around rendered shapes. This makes it easier to identify which Razor or Liquid template produced a specific fragment in the page output.

Enable it per tenant from **Settings → Debugging → Write shape debug information** (`DebugSettings.WriteShapeDebugInformation`). The setting feeds `ShapeRenderingOptions.WriteShapeDebugInformation` through the `IOptionsMonitor` refresh pipeline, so toggling it applies without a shell release.

When enabled, rendered shapes are wrapped with HTML comments:

```html
<!--shape-start type:Menu bindings:Menu__Main => Themes/Contoso/Views/Menu-Main.cshtml (razor) -->
...
<!--shape-end type:Menu-->
```

The start comment contains the shape type and the binding that was used. Razor bindings report the `.cshtml` path, while Liquid bindings report a virtual `.liquid` source.

Code that reads the option must use `IOptionsMonitor<ShapeRenderingOptions>.CurrentValue`, not `IOptions<ShapeRenderingOptions>`, to see changes. To force the value from code (for example in a test host), use `services.PostConfigure<ShapeRenderingOptions>(o => o.WriteShapeDebugInformation = true)` so the site setting does not override it.

### Shape Rendering Telemetry

Each rendered shape emits an activity named `Render shape` (tags `orchardcore.shape.type` and `orchardcore.shape.binding`), a `orchardcore.display.shapes_rendered` counter, and a `orchardcore.display.shape.duration` histogram (seconds, including child shapes) from the `OrchardCore.DisplayManagement` activity source and meter. Nothing is collected unless you add them explicitly:

```csharp
using OrchardCore.DisplayManagement;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(DisplayManagementDiagnostics.ActivitySourceName))
    .WithMetrics(metrics => metrics.AddMeter(DisplayManagementDiagnostics.MeterName));
```

```bash
dotnet-counters monitor --name OrchardCore.Cms.Web --counters OrchardCore.DisplayManagement
```

### Liquid Shape Templates

Liquid shape templates are rendered by `LiquidShapeTemplateViewEngine` with Fluid directly (no per-shape Razor `LiquidPage`). `LiquidViewExtensionProvider` is no longer registered and `RazorShapeTemplateViewEngine` no longer handles `.liquid` files. Liquid views returned by controllers still use `LiquidPage`. PO file contexts of localized strings are unchanged.

## Upgrading from 3.x

- `DisplayDriverBase.Shape(shapeType, shape)` is obsolete because it allocates the `IShape` even when the shape is hidden. Replace it with `Factory(shapeType, ctx => shape)` or, better, build the shape inside the factory.
- Overrides of the 3-parameter virtual `Factory(string, Func<IBuildShapeContext, ValueTask<IShape>>, Func<IShape, Task>)` must move to `Factory<TBuilderState, TInitState>(string, Func<IBuildShapeContext, TBuilderState, ValueTask<IShape>>, TBuilderState, Func<IShape, TInitState, ValueTask>, TInitState)`.
- Initializer delegates are `ValueTask`-based: `new ShapeResult(type, builder, Func<IShape, ValueTask>)` and `Dynamic(type, Func<dynamic, ValueTask>)` replace the former `Func<..., Task>` parameters. Async lambdas compile unchanged; explicit `Task`-returning method groups must return `ValueTask`.
- Inject `IOptionsMonitor<ShapeRenderingOptions>` instead of `IOptions<ShapeRenderingOptions>`.
- Lists returned by the content handler and display driver resolvers are shared for the scope; copy them before mutating.

---
name: orchardcore-display-management
description: Skill for using Orchard Core's display management system. Covers display drivers, display managers, shapes, display types, shape table providers, placement, and editor/display mode patterns. Use this skill when requests mention Orchard Core Display Management, Create Display Drivers and Shapes, Content Part Display Driver Pattern, View Model Pattern, Display Shape View (Views/{{PartName}}.cshtml), Editor Shape View (Views/{{PartName}}_Edit.cshtml), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.DisplayManagement.Views, OrchardCore.ContentManagement, OrchardCore.ContentManagement.Handlers, DisplayDriver. It also helps with display management examples, Display Shape View (Views/{{PartName}}.cshtml), Editor Shape View (Views/{{PartName}}_Edit.cshtml), Registering a Display Driver, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
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
- Display drivers inherit from `ContentPartDisplayDriver<TPart>`.
- Drivers handle three operations: `Display`, `Edit`, and `Update`.
- Each operation returns `IDisplayResult` (shapes to render).
- View models are used to pass data between drivers and views.
- Shape names follow the convention `{PartName}` for display and `{PartName}_Edit` for editor.
- Use `Initialize<TModel>` to create shapes with a view model.
- Register drivers in `Startup.cs` using `services.AddContentPart<TPart>().UseDisplayDriver<TDriver>()`.
- For non-content-item models rendered through `DisplayDriver<TModel>`, the root shape still needs its own wrapper template for each display type you build (for example, `CampaignAction.Edit.cshtml` for `CampaignAction_Edit` and `CampaignAction.SummaryAdmin.cshtml` for `CampaignAction_SummaryAdmin`).
- When driver results are placed into zones with `.Location("Content:1")`, `.Location("Actions:5")`, or similar, the wrapper template must render those zones (`Model.Content`, `Model.Actions`, `Model.Meta`, etc.) or the child shapes will never appear.
- For ALL admin editor views (`*.Edit.cshtml` including `*Settings.Edit.cshtml`), use the Orchard admin helper wrappers instead of raw `mb-3`, `form-label`, or hard-coded grid classes: `@Orchard.GetWrapperClasses(...)`, `@Orchard.GetLabelClasses(...)`, `@Orchard.GetEndClasses(...)`, `@Orchard.GetLimitedWidthWrapperClasses(...)`, and `@Orchard.GetLimitedWidthClasses(...)`.
- These helpers accept arbitrary class names, so preserve custom CSS by passing classes into the helper arguments, for example `@Orchard.GetWrapperClasses("class1", "class2", "class3")`. Use `@Orchard.GetEndClasses(true)` for checkbox-only rows, section headings, and buttons that should align with the input column. Use `@Orchard.GetLimitedWidthWrapperClasses("mb-3")` with `@Orchard.GetLimitedWidthClasses()` for settings fields that intentionally use narrower input columns (label MUST be placed outside the LimitedWidthClasses div). Do NOT apply helpers to frontend views (Login, Register, etc.) or Admin Menu node editing (needs full width).
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

<div class="@Orchard.GetWrapperClasses()">
    <label asp-for="{{PropertyName}}" class="@Orchard.GetLabelClasses()">{{DisplayLabel}}</label>
    <div class="@Orchard.GetEndClasses()">
        <input asp-for="{{PropertyName}}" class="form-control" />
        <span asp-validation-for="{{PropertyName}}" class="text-danger"></span>
    </div>
</div>
```

### Registering a Display Driver

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<{{PartName}}>()
            .UseDisplayDriver<{{PartName}}DisplayDriver>();
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

### Display Types

Orchard Core uses display types to differentiate how content is rendered:

- `Detail` — Full content display (e.g., a blog post page).
- `Summary` — Abbreviated display (e.g., in a list).
- `SummaryAdmin` — Admin-specific summary view.
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
        => Initialize<CampaignActionViewModel>("CampaignActionFields_Edit", m => { })
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

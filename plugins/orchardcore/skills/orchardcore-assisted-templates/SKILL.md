---
name: orchardcore-assisted-templates
description: Skill for the Orchard Core 4.0 Assisted Templates and Shape Tracing features of the Templates module. Covers OrchardCore.Templates.Assisted and OrchardCore.Templates.ShapeTracing, the Theme Templates and Feature Templates lists, viewing, cloning, resetting and deleting theme templates, the cloned, edited, overridden and source changed statuses, Create override, the Alternates list, Inspect Content and the Inspect templates action, and extending them with ITemplateSource, SourceTemplate, ShapeTemplateFiles and ITemplateAlternatesProvider. Use this skill when requests mention Orchard Core assisted templates, theme templates, feature templates, cloning a theme template, shape tracing, finding which template renders a shape, or alternates for a content type. Strong matches include OrchardCore.Templates.Assisted, OrchardCore.Templates.ShapeTracing, ITemplateSource, ITemplateAlternatesProvider, TemplateAlternate, SourceTemplate, and TemplateInspector.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Assisted Templates and Shape Tracing

You are an Orchard Core expert. Help users find, view and clone the templates of themes and features, discover alternates, and inspect the shapes a content item renders.

## Guidelines

- Both features are off by default. Enable them in **Tools → Features** (category **Development**) or with a `Feature` recipe step.
- They add buttons to **Design → Templates**, and to **Design → Admin Templates** when `OrchardCore.AdminTemplates` is enabled. Local templates are still Liquid templates of `OrchardCore.Templates` (see `orchardcore-templates`).
- Only Liquid templates of the **active** theme can be cloned. Razor (`.cshtml`) templates and templates of inactive themes can only be viewed; use **Create override** to write a Liquid template with the same name.
- Every action requires `ManageLiquidTemplates` plus `ManageTemplates` (front end) or `ManageAdminTemplates` (admin), like the template editors.
- Clone records are tenant data next to the templates but not part of the `Templates` recipe or deployment steps, so imported templates show as **Overridden**.

## Features

| Feature | ID | Depends on |
|---|---|---|
| Assisted Templates | `OrchardCore.Templates.Assisted` | `OrchardCore.Templates` |
| Shape Tracing | `OrchardCore.Templates.ShapeTracing` | `OrchardCore.Templates.Assisted`, `OrchardCore.Contents`, `OrchardCore.ContentFields` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Templates.Assisted",
        "OrchardCore.Templates.ShapeTracing"
      ]
    }
  ]
}
```

## Theme Templates

**Theme Templates** (admin route `TemplateSources/Theme`) lists the templates of the site themes on **Templates**, of the admin themes on **Admin Templates**. A dropdown picks the theme (the active one by default, marked "(active)"); a theme's templates include those of its base themes. Each row shows the source file (for example `TheBlogTheme` `Views/Content-BlogPost.liquid`) and the template name that overrides it (`Content__BlogPost`).

On the active theme:

| Action | Effect |
|---|---|
| **View** | Shows the template content (any language, any theme). |
| **Clone** | Copies a Liquid template as a local template with the same name, which then overrides it; **Edit** it like any template. |
| **Reset** | Replaces a local template's content with the theme template. |
| **Delete** | Deletes the local template so the theme template renders again. |

Clone, Reset and Delete are also bulk actions; bulk **Reset** only applies to templates cloned from this list.

| Status | Meaning |
|---|---|
| Not overridden | The theme template renders. |
| Cloned | The local template equals the template it was cloned from. |
| Edited | The local template was edited since it was cloned. |
| Overridden | A local template overrides it but was not cloned from this list, so it is not monitored. |
| Cloned from _theme_ | Cloned from another theme (for example the previously active one); review, reset, or delete it. |
| Source changed | The theme template changed since it was cloned (for example after a theme update). |

Filter by these statuses and by language (**Liquid**, **Razor**). Local templates are not tied to a theme: after switching the active theme, templates cloned from the old theme keep overriding the new one and show as **Cloned from**.

## Feature Templates

**Feature Templates** (`TemplateSources/Feature`) lists the templates of enabled features, all of them (**All features**) or one feature picked in the dropdown. They are mostly Razor, so use them as a reference and **Create override** shows the shape name to override.

- A template that does not render its shape shows **Overridden by** the extension whose template does (the active theme, or another feature by shape table order). Such templates cannot be cloned.
- Features do not declare which templates render the admin, so the list guesses from names (`TextField_Edit`, `Content_SummaryAdmin`, workflow activities ending in `_Design`). The **Renders** filter defaults to the front end on **Templates** and to the admin on **Admin Templates**.

## Alternates

The **Alternates** button of a template (in both lists and on the page viewing a template) lists the alternates of its shape for the site's content types, parts and fields: `Detail` and `Summary` display types on **Templates**, `DetailAdmin` and `SummaryAdmin` on **Admin Templates**. Content item alternates use `Content` or the type's stereotype (for example `Widget`). Each alternate shows whether a local template already has its name and whether the theme or a feature renders it:

- **Create** opens the editor of a new template with that name.
- **Clone** (when the listed template is a Liquid template of the active theme) creates it with that content as a starting point.
- **View** shows the theme template that already renders the alternate, to clone it instead.

The content alternates come from Shape Tracing (`ContentTemplateAlternatesProvider`); without it the button appears only for alternates other providers suggest.

## Inspect Content

With Shape Tracing, **Inspect Content** (on **Templates** and **Admin Templates**, admin route `TemplateInspector`) and the **Inspect templates** item of a content item's actions menu in **Content → Content Items** (`TemplateInspectorAction_SummaryAdmin`, `ActionsMenu:20`) show the shapes the item renders in a display type:

- Each shape is indented under its parent, with the template that renders it and every alternate, most specific first, including item-only ones such as `Content__Alias__[Alias]` and `Content__Slug__[Slug]`. The bound alternate is checked; clicking an alternate opens the editor of a local template with that name.
- **View** and **Clone** work as in the lists for theme and feature templates; a Razor template is view-only.
- The picker lists the 50 most recently modified items the user can view (`ViewContent`) and searches the rest by display text. Published versions are inspected; a never-published draft also requires `PreviewContent`.
- Resolution matches the front end (active site theme, Templates) or the admin (active admin theme, Admin Templates). The item renders on its own, without the page layout, so layout shapes such as widgets are not listed.

## Extending

Both services live in the `OrchardCore.Templates` module assembly (`OrchardCore.Templates.Services`, models in `OrchardCore.Templates.Models`); reference the module package from the module that registers them and gate the startup with `[RequireFeatures("OrchardCore.Templates.Assisted")]`.

### A Template Source

Each `ITemplateSource` is a button above the template lists, at `/Admin/TemplateSources/{Name}`. Scopes fill the dropdown (the first is the default); return none to list everything at once.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Templates.Models;
using OrchardCore.Templates.Services;

namespace MyModule.Services;

public sealed class MyTemplateSource : ITemplateSource
{
    private readonly ShapeTemplateFiles _shapeTemplateFiles;

    public MyTemplateSource(ShapeTemplateFiles shapeTemplateFiles, IStringLocalizer<MyTemplateSource> stringLocalizer)
    {
        _shapeTemplateFiles = shapeTemplateFiles;
        DisplayName = stringLocalizer["My Templates"];
    }

    public string Name => "My";

    public LocalizedString DisplayName { get; }

    public LocalizedString GetDescription(bool adminTemplates) => DisplayName;

    public Task<IEnumerable<TemplateSourceScope>> GetScopesAsync(bool adminTemplates)
        => Task.FromResult(Enumerable.Empty<TemplateSourceScope>());

    public async Task<IEnumerable<SourceTemplate>> GetTemplatesAsync(bool adminTemplates, string scope)
    {
        // Templates rendering the shapes of a theme, from the theme or an enabled module.
        var templates = await _shapeTemplateFiles.GetTemplatesAsync("TheTheme");

        return templates.Where(template => template.ExtensionId == "My.Module");
    }
}
```

```csharp
[RequireFeatures("OrchardCore.Templates.Assisted")]
public sealed class AssistedTemplatesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITemplateSource, MyTemplateSource>();
    }
}
```

`SourceTemplate` has `Name` (the template name that overrides it), `ExtensionId`, `RelativePath`, `FilePath`, `IsInUse`, `OverriddenBy`, `IsAdmin` (`null` when unknown), and computed `IsLiquid` and `CanClone` (Liquid, in use, not overridden by another extension). `TemplateSourceScope` has `Name`, `DisplayName` and `IsInUse`.

### Suggesting Alternates

```csharp
using OrchardCore.Templates.Models;
using OrchardCore.Templates.Services;

namespace MyModule.Services;

public sealed class CampaignTemplateAlternatesProvider : ITemplateAlternatesProvider
{
    public Task<IEnumerable<TemplateAlternate>> GetAlternatesAsync(bool adminTemplates)
    {
        if (!adminTemplates)
        {
            return Task.FromResult(Enumerable.Empty<TemplateAlternate>());
        }

        IEnumerable<TemplateAlternate> alternates =
        [
            new TemplateAlternate
            {
                Name = "CampaignAction_SummaryAdmin__Email",
                ShapeType = "CampaignAction",
                DisplayType = "SummaryAdmin",
            },
        ];

        return Task.FromResult(alternates);
    }
}
```

Register it with `services.AddScoped<ITemplateAlternatesProvider, CampaignTemplateAlternatesProvider>();`. `ContentType` and `Element` (`HtmlBodyPart`, or `HtmlBodyPart.Description` for a field) are optional and describe what the alternate targets.

## Lists

The template source and alternates pages are admin lists named `TemplateSources` (rows `SourceTemplateEntry`) and `TemplateAlternates` (rows `TemplateAlternateEntry`); their columns can be extended with `IAdminListColumnsProvider` (see `orchardcore-admin-lists`).

## Troubleshooting

| Symptom | Cause |
|---|---|
| No **Theme Templates** button | `OrchardCore.Templates.Assisted` disabled, or the user lacks `ManageLiquidTemplates`. |
| No **Inspect Content** or content alternates | `OrchardCore.Templates.ShapeTracing` disabled. |
| **Clone** missing | Razor template, inactive theme, or a template overridden by another extension. |
| Cloned templates show **Overridden** on another tenant | Clone records are not exported with the templates. |
| Widgets missing from Inspect Content | The item is rendered without the page layout. |

## Related Skills

- `orchardcore-templates` for local templates, naming and recipes.
- `orchardcore-theming` and `orchardcore-shape-discovery` for how shapes bind to theme and module templates.
- `orchardcore-placement` for where shapes render.

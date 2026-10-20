---
name: orchardcore-theming
description: Skill for creating and customizing Orchard Core themes. Covers theme scaffolding, Liquid and Razor layouts (render_section, render_body, RenderSectionAsync, RenderBodyAsync), zones, shape templates, alternates, placement rules, assets, resource manifests, theme settings, admin themes based on TheAdmin (bootstrap-select styles, admin menu color CSS custom properties), extra Feature attributes in theme manifests, and the jQuery and Vue 3 changes of Orchard Core 4. Use this skill when requests mention Orchard Core Theming, Create a Theme, theme layouts, shape overrides, template discovery, admin theme customization, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.DisplayManagement.Manifest, OrchardCore.Theme.Targets, OrchardCore.ResourceManagement, IOrchardHelper, BagPart, FlowPart, ListPart, ResourceManifest, TheTheme, TheAdmin.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Theming

Use this skill for Orchard Core theming tasks including theme creation, shape overrides, template discovery, content rendering, placement rules, and asset management.

## How to Use

- **Task match**: Scan the Tasks list below. If the request matches, go directly to the referenced files.
- **Exploration**: Use the section cues to pick a reference, then choose the relevant detail.
- Determine Razor vs Liquid early. Check the active theme for `.cshtml` (Razor) or `.liquid` files. If unclear, default to Liquid and confirm with the user.
- Open only the necessary reference files; prefer examples and ready-to-copy patterns.

## Evidence Rules

- Prefer repo evidence over assumptions: check the active theme, base theme, `placement.json`, and existing templates.
- Confirm unknown part/field properties in `ContentDefinition.json` or the database.
- Ask for missing identifiers (content type, part name, field name, display type) instead of inventing them.
- Do not invent recipe steps or feature IDs; consult the recipes skill for valid steps.

## Guidelines

- Theme names should be PascalCase and may be prefixed with the organization name (e.g., `CrestApps.MyTheme`).
- Themes must have a `Manifest.cs` declaring the theme and its base theme (if any).
- Prefer shapes over MVC partials for UI composition.
- `Views/Layout.cshtml` is treated as the site layout automatically; do not set `Layout = null` inside it.
- Override base theme views in the child theme rather than editing the base theme.
- Render layout zones with `{% render_section "Name", required: false %}` (Liquid) or `@await RenderSectionAsync("Name", required: false)` (Razor), and the main content with `{% render_body %}` / `@await RenderBodyAsync()`. The `{% zone %}` block and `<zone>` tag helper **add** content to a zone; they do not render it.
- Front-end themes must require every script they use. Orchard Core 4.0 admin scripts no longer pull in jQuery; require `jQuery` explicitly if your theme needs it.
- Asset pipelines can be managed through `wwwroot/` and resource manifests.
- The `TheAdmin` theme is used for the admin panel and can be extended.

## Reference Cues

Use these cues to decide which reference file to open:

- Use `references/theming-examples.md` for end-to-end theme creation examples (manifests, layouts, shape overrides).
- Use `references/shape-alternates.md` to find shape names, alternates, and naming rules for template overrides.
- Use `references/placement-rules.md` for `placement.json` patterns, filters, differentiators, and editor grouping.
- Use `references/shape-workflow.md` for a step-by-step checklist to build or override a shape safely.
- Use `references/assets-resources.md` for resource manifests, requiring scripts/styles, and built-in Orchard Core resources.

## Tasks

- Create a new theme from scratch.
- Override a content item shape template.
- Find shape alternates and placement rules.
- Add or override placement rules in `placement.json`.
- Add scripts/styles and include them in the layout.
- Create or customize the admin theme.
- Render BagPart/FlowPart/ListPart items in templates.
- Update a shape after adding fields.
- Work on theme structure or layout.
- Determine template language (Razor vs Liquid).

## Create a Theme

### Manifest Pattern

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "{{ThemeName}}",
    Author = "{{Author}}",
    Website = "{{Website}}",
    Version = "1.0.0",
    Description = "{{Description}}",
    BaseTheme = "{{BaseTheme}}"
)]
```

### Project File Pattern

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.DisplayManagement" Version="3.*" />
    <PackageReference Include="OrchardCore.Theme.Targets" Version="3.*" />
  </ItemGroup>

</Project>
```

### Theme Folder Structure

```
MyTheme/
├── Manifest.cs
├── MyTheme.csproj
├── Views/
│   ├── Layout.liquid (or Layout.cshtml)
│   ├── _ViewImports.cshtml
│   └── Items/
│       └── Content-BlogPost.liquid
├── wwwroot/
│   ├── css/
│   │   └── site.css
│   └── js/
│       └── site.js
├── placement.json (optional)
└── ResourceManagementOptionsConfiguration.cs (optional)
```

### _ViewImports for Razor Themes

If a theme uses Razor, `_ViewImports.cshtml` should exist with at least:

```cshtml
@inherits OrchardCore.DisplayManagement.Razor.RazorPage<TModel>
```

If it's missing, it can cause build errors.

### Liquid Layout Template

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}" dir="{{ Culture.Dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{% page_title Site.SiteName, position: "before" %}</title>
    {% style name: "bootstrap", version: "5", at: "Head" %}
    {% style name: "{{ThemeName}}", at: "Head" %}
    {% script name: "bootstrap", version: "5", at: "Foot" %}
    {% script name: "{{ThemeName}}", at: "Foot" %}
    {% resources type: "Header" %}
    {% render_section "HeadMeta", required: false %}
</head>
<body>
    {% render_section "Header", required: false %}

    <main role="main" class="container">
        {% render_section "Messages", required: false %}
        {% render_section "BeforeContent", required: false %}
        {% render_body %}
        {% render_section "AfterContent", required: false %}
    </main>

    {% render_section "Footer", required: false %}

    {% resources type: "FootScript" %}
</body>
</html>
```

### Razor Layout Template

```cshtml
<!DOCTYPE html>
<html lang="@Orchard.CultureName()" dir="@Orchard.CultureDir()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName, "before")</title>
    <style asp-name="bootstrap" version="5" at="Head"></style>
    <style asp-name="{{ThemeName}}" at="Head"></style>
    <script asp-name="bootstrap" version="5" at="Foot"></script>
    <script asp-name="{{ThemeName}}" at="Foot"></script>
    <resources type="Header" />
    @await RenderSectionAsync("HeadMeta", required: false)
</head>
<body>
    @await RenderSectionAsync("Header", required: false)

    <main role="main" class="container">
        @await RenderSectionAsync("Messages", required: false)
        @await RenderBodyAsync()
    </main>

    @await RenderSectionAsync("Footer", required: false)

    <resources type="FootScript" />
</body>
</html>
```

`Orchard`, `Site`, and `ThemeLayout` are provided by `RazorPage<TModel>`; do not `@inject` another `Orchard` property. Shapes that register resources must render before `<resources type="Header" />`, so pre-render them into a variable at the top of the layout when they appear later in the markup.

### Shape Templates

Override shape rendering by creating templates in the `Views/` folder:

- `Content.liquid` — Default content item display.
- `Content-BlogPost.liquid` — Content display for BlogPost type.
- `Content-BlogPost.Summary.liquid` — Summary display mode for BlogPost type.
- `Widget.liquid` — Default widget wrapper.
- `MenuItem.liquid` — Menu item rendering.

File naming convention: `__` in shape type maps to `-` in file names; `_DisplayType` maps to `.DisplayType` suffix.

### Resource Manifest

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest = CreateManifest();

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }

    private static ResourceManifest CreateManifest()
    {
        var manifest = new ResourceManifest();

        manifest
            .DefineStyle("{{ThemeName}}")
            .SetUrl("~/{{ThemeName}}/css/site.min.css", "~/{{ThemeName}}/css/site.css");

        manifest
            .DefineScript("{{ThemeName}}")
            .SetUrl("~/{{ThemeName}}/js/site.min.js", "~/{{ThemeName}}/js/site.js")
            .SetDependencies("bootstrap:5");

        return manifest;
    }
}
```

Choose head or foot when requiring the script (`at="Foot"`); `SetPosition(ResourcePosition.First | Last | ByDependency)` only changes ordering.

Register the configuration from the theme or module startup:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
```

### Zones

Common zones used in Orchard Core themes:

| Zone | Purpose |
|------|---------|
| `Header` | Top of the page, navigation bar area |
| `Content` | Main content area |
| `Footer` | Bottom of the page |
| `BeforeContent` | Before the main content |
| `AfterContent` | After the main content |
| `Navigation` | Primary navigation area |
| `Sidebar` | Sidebar content area |
| `AsideFirst` / `AsideSecond` | Multi-column sidebars |

### Zone Alternates

Zone shapes support alternates for per-zone template overrides:

- `Zone__ZoneName` → file `Zone-Footer.cshtml` (or `.liquid`)

## Additional Features in a Theme Manifest

A theme assembly can declare extra `[Feature]` attributes next to `[Theme]`. The theme stays listed on the Themes page, and the extra features appear in **Configuration → Features** with their category and Enable button:

```csharp
using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Name = "Contoso Theme",
    Author = "Contoso",
    Version = "1.0.0",
    Description = "Contoso front-end theme.",
    BaseTheme = "TheTheme"
)]

[assembly: Feature(
    Id = "ContosoTheme.Widgets",
    Name = "Contoso Theme Widgets",
    Description = "Widget content types used by the Contoso theme.",
    Category = "Theming"
)]
```

Attach code that belongs to the extra feature with `[Feature("ContosoTheme.Widgets")]` on its `Startup` class. In Orchard Core 3.x such themes disappeared from the Themes page; that is fixed in 4.0.

## Admin Themes Based on TheAdmin

- `bootstrap-select` styles are no longer included automatically. Admin views that use it must add `<style asp-name="bootstrap-select" at="Head"></style>`.
- Admin scripts no longer use jQuery or jQuery UI, and admin Vue apps use Vue 3. Do not assume `$` exists in admin views; require `jQuery` explicitly if a custom admin script still needs it.
- The admin menu palette is driven by CSS custom properties. Rebrand it from a theme that depends on TheAdmin:

```css
:root {
    --oc-admin-accent-rgb: 73, 80, 87;
    --oc-admin-accent-strong-color: #343a40;
    --oc-admin-menu-bg-color: #f1f3f5;
    --oc-admin-menu-border-color: #dee2e6;
}
```

Other menu variables include `--oc-admin-accent-color`, `--oc-admin-menu-group-font-color`, `--oc-admin-menu-separator-color`, `--oc-admin-menu-item-hover-bg-color`, `--oc-admin-menu-item-active-bg-color`, and `--oc-selected-indicator-color`. The menu markup was redesigned in 4.0 (groups, `nav-group`, `current`, off-canvas below `md`); re-check overrides of `Navigation-admin.cshtml`, `NavigationItem-admin.cshtml`, or `NavigationItemLink-admin.cshtml`. See `orchardcore-admin` and `orchardcore-admin-menu` for the menu itself.

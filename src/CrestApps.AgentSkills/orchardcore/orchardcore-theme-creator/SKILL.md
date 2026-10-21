---
name: orchardcore-theme-creator
description: Creates new Orchard Core themes in a solution that consumes Orchard Core packages, with the octheme project template, theme manifest and base themes, project file, Razor and Liquid layouts using RenderBodyAsync, RenderSectionAsync, render_body and render_section, resource tag helpers and manifests, the theme thumbnail, Assets.json asset builds, extra features declared in a theme manifest, and activation through the admin or the themes recipe step. Use when the user needs to create a new theme, scaffold a layout, derive from TheTheme or TheAdmin, or set up theme frontend assets. Use this skill when requests mention Orchard Core Theme Creator, dotnet new octheme, Create Manifest.cs, theme project file, theme assets, or closely related Orchard Core work. Strong matches include OrchardCore.Theme.Targets, OrchardCore.DisplayManagement.Manifest, ThemeAttribute, BaseTheme, OrchardCore.ProjectTemplates, TheTheme, TheBlogTheme, TheAgencyTheme, TheComingSoonTheme, TheAdmin, SafeMode, Assets.json, Theme.png.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Theme Creator

Step-by-step creation of a theme project for an Orchard Core site built on NuGet packages. For shape overrides, alternates, placement, and admin theme customization once the theme exists, use `orchardcore-theming`.

## Prerequisites

- .NET SDK 10.0+.
- An Orchard Core web project (startup project) that references `OrchardCore.Application.Cms.Targets` (see `orchardcore-setup`).
- Node.js and Yarn only when the theme compiles SCSS or scripts (see `orchardcore-asset-manager`).

## Theme Creation Workflow

### Step 1: Scaffold the project

Use the Orchard Core project templates:

```bash
dotnet new install OrchardCore.ProjectTemplates
dotnet new octheme -n Contoso.Theme -o src/Themes/Contoso.Theme
dotnet sln add src/Themes/Contoso.Theme/Contoso.Theme.csproj
dotnet add src/Contoso.Web/Contoso.Web.csproj reference src/Themes/Contoso.Theme/Contoso.Theme.csproj
```

`octheme` creates `Manifest.cs`, the project file, and a `Views/Layout.liquid` (pass `--AddLiquid false` to skip the Liquid files and `--OrchardVersion` to pick the Orchard Core package version). Create the theme folder **next to** the web project, not inside it, and reference it **from the web project**; a theme that is not referenced by the startup project is never discovered.

### Step 2: Project file

The theme project references `OrchardCore.Theme.Targets`, which marks the assembly as a theme. Use the Razor SDK when the theme has `.cshtml` files:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.Theme.Targets" Version="3.*" />
    <PackageReference Include="OrchardCore.ContentManagement" Version="3.*" />
    <PackageReference Include="OrchardCore.DisplayManagement" Version="3.*" />
    <PackageReference Include="OrchardCore.ResourceManagement" Version="3.*" />
  </ItemGroup>

</Project>
```

A Liquid-only theme can use `Microsoft.NET.Sdk`; switch to `Microsoft.NET.Sdk.Razor` as soon as you add Razor views.

### Step 3: Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "Contoso Theme",
    Author = "Contoso",
    Website = "https://contoso.com",
    Version = "1.0.0",
    Description = "The Contoso front-end theme.",
    BaseTheme = "TheTheme",
    Tags = ["Bootstrap"]
)]
```

- The theme ID is the assembly name (`Contoso.Theme`); `Name` is only the display name.
- `BaseTheme` takes the **ID** of another theme (`TheTheme`, `TheBlogTheme`, `TheAgencyTheme`, `TheComingSoonTheme`, `TheAdmin`, `SafeMode`, or one of yours). Templates and `placement.json` rules of the base theme apply unless your theme overrides them; static files keep their own URL (`~/TheTheme/...`). Omit it for a standalone theme.
- Add the `Admin` tag (`ManifestConstants.AdminTag`) to `Tags` for an admin theme so it is offered as an admin theme, and usually set `BaseTheme = "TheAdmin"`.
- `Dependencies = ["OrchardCore.Menu"]` enables features the theme needs.
- `ManifestConstants.OrchardCoreTeam`, `OrchardCoreWebsite`, and `OrchardCoreVersion` describe Orchard Core's own themes; use your own author, website, and version.

**Shipping extra features with the theme:** a theme assembly can also declare `[assembly: Feature(Id = "Contoso.Theme.Widgets", Name = "...", Category = "Theming")]` (from `OrchardCore.Modules.Manifest`). In Orchard Core 4.0 the theme still appears on the Themes page and the extra features appear on the Features page; bind their services with `[Feature("Contoso.Theme.Widgets")]` on a `Startup` class.

### Step 4: Views/_ViewImports.cshtml (Razor themes)

```cshtml
@inherits OrchardCore.DisplayManagement.Razor.RazorPage<TModel>
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, OrchardCore.DisplayManagement
@addTagHelper *, OrchardCore.ResourceManagement
```

Add `@addTagHelper *, OrchardCore.Menu` when the layout uses the `<menu>` tag helper (and reference the `OrchardCore.Menu` package). A missing `@inherits` line causes build errors in Razor views.

### Step 5: Create the layout

`Views/Layout.cshtml` (or `Views/Layout.liquid`) is the site layout:

```cshtml
<!DOCTYPE html>
<html lang="@Orchard.CultureName()" dir="@Orchard.CultureDir()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@RenderTitleSegments(Site.SiteName, "before")</title>
    <style asp-name="bootstrap" version="5" at="Head"></style>
    <style asp-name="Contoso.Theme" asp-src="~/Contoso.Theme/css/site.min.css" debug-src="~/Contoso.Theme/css/site.css" depends-on="bootstrap:5" at="Head"></style>
    <script asp-name="bootstrap" version="5" at="Foot"></script>
    <script asp-name="Contoso.Theme" asp-src="~/Contoso.Theme/js/site.min.js" debug-src="~/Contoso.Theme/js/site.js" at="Foot"></script>
    <resources type="Header" />
    @await RenderSectionAsync("HeadMeta", required: false)
</head>
<body>
    @await RenderSectionAsync("Header", required: false)
    <main class="container">
        @await RenderSectionAsync("Messages", required: false)
        @await RenderBodyAsync()
    </main>
    @if (IsSectionDefined("Footer"))
    {
        <footer class="container">
            @await RenderSectionAsync("Footer", required: false)
        </footer>
    }
    <resources type="FootScript" />
</body>
</html>
```

The Liquid equivalent:

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}" dir="{{ Culture.Dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{% page_title Site.SiteName, position: "before" %}</title>
    {% style name: "bootstrap", version: "5", at: "Head" %}
    {% script name: "bootstrap", version: "5", at: "Foot" %}
    {% resources type: "Header" %}
    {% render_section "HeadMeta", required: false %}
</head>
<body>
    {% render_section "Header", required: false %}
    <main class="container">
        {% render_section "Messages", required: false %}
        {% render_body %}
    </main>
    {% render_section "Footer", required: false %}
    {% resources type: "FootScript" %}
</body>
</html>
```

- `RenderBodyAsync()` / `{% render_body %}` renders the `Content` zone; `RenderSectionAsync(name, required: false)` / `{% render_section %}` renders any other zone. `<zone name="...">` and `{% zone %}` only **add** content to a zone, they never render one.
- `<resources type="Header" />` renders the meta, link, stylesheet, and head script resources (`Meta`, `HeadLink`, `Stylesheet`, `HeadScript` can also be rendered separately); `<resources type="FootScript" />` renders the foot scripts.
- Shapes that register resources must render before `<resources type="Header" />`; pre-render such shapes into a variable at the top of the layout.
- Require every script the theme uses. Orchard Core 4.0 no longer loads jQuery through admin or module scripts; add `<script asp-name="jQuery" at="Foot"></script>` only if the theme needs it.

### Step 6: Static files and thumbnail

- Files under the theme's `wwwroot/` are served at `~/{ThemeId}/...`, for example `~/Contoso.Theme/css/site.css`.
- Add `wwwroot/Theme.png` to show a thumbnail on **Design → Themes**.
- To reference resources by name everywhere, define them in a resource manifest registered from a theme `Startup` with `services.AddResourceConfiguration<T>()` (see `orchardcore-theming`).

### Step 7: Add assets (optional)

```
Assets/
├── scss/site.scss
├── js/site.js
└── package.json
Assets.json
```

See `references/assets.md` for the files and `orchardcore-asset-manager` for which parts of the asset pipeline work outside the Orchard Core repository.

### Step 8: Build and activate

```bash
dotnet build
dotnet run --project src/Contoso.Web
```

Enable and select the theme on **Design → Themes**, or from a recipe:

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": ["Contoso.Theme"]
    },
    {
      "name": "themes",
      "site": "Contoso.Theme",
      "admin": "TheAdmin"
    }
  ]
}
```

## Quick Reference

### Common Zones

| Zone | Purpose |
|------|---------|
| `HeadMeta` | Meta/link tags rendered inside `<head>` |
| `Header` | Site header |
| `Navigation` | Main menu |
| `Messages` | Alerts/notifications |
| `BeforeContent` / `AfterContent` | Around the main content |
| `Content` | Main content (rendered by `RenderBodyAsync`) |
| `Sidebar` | Side widgets |
| `Footer` | Site footer |

A layout zone is filled by widgets (Layers), `placement.json` rules whose `place` starts with `/`, or `<zone>` blocks in views. For widgets, also list the zone names on **Design → Settings → Zones** (see `orchardcore-layers`).

### Built-in Base Themes

| Theme ID | Description |
|----------|-------------|
| `TheTheme` | Default Bootstrap front-end theme |
| `TheBlogTheme` | Blog theme (Liquid) |
| `TheAgencyTheme` | Agency/business theme |
| `TheComingSoonTheme` | Coming soon page |
| `TheAdmin` | Admin theme |
| `SafeMode` | Minimal fallback theme |

### Naming Conventions

| Item | Convention |
|------|------------|
| Theme project and ID | `Company.ThemeName` (PascalCase) |
| Static file URL | `~/Company.ThemeName/...` |
| Resource names | Theme ID or `Company.ThemeName.Part` |
| CSS / JS entry | `site.css` / `site.js` |

### Inside the Orchard Core repository

Only when contributing a theme to Orchard Core itself: put it under `src/OrchardCore.Themes/`, use `ProjectReference`s to `src/OrchardCore/OrchardCore.Theme.Targets` instead of packages, use `ManifestConstants` in the manifest, and build assets with the repository's `yarn build -n <name>`.

## References

- `references/theme-structure.md` - Directory layout and templates
- `references/assets.md` - SCSS, JS, and asset pipeline

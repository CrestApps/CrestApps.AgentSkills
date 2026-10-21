# Theme Structure Reference

## Solution Layout

```
Contoso.sln
├── src/Contoso.Web/                 # Startup project (OrchardCore.Application.Cms.Targets)
│   └── Contoso.Web.csproj           # References the theme project
└── src/Themes/Contoso.Theme/
    ├── Manifest.cs                  # [assembly: Theme(...)]
    ├── Contoso.Theme.csproj         # References OrchardCore.Theme.Targets
    ├── Startup.cs                   # Optional: resource manifest, services
    ├── ResourceManagementOptionsConfiguration.cs  # Optional
    ├── placement.json               # Optional: placement rules
    ├── Views/
    │   ├── Layout.cshtml            # Or Layout.liquid
    │   ├── _ViewImports.cshtml      # Razor themes only
    │   ├── Content-Article.cshtml   # Shape overrides
    │   └── Widget-HeroBanner.liquid
    ├── Assets/                      # Optional sources
    │   ├── scss/site.scss
    │   ├── js/site.js
    │   └── package.json
    ├── Assets.json                  # Optional asset build actions
    └── wwwroot/                     # Served at ~/Contoso.Theme/...
        ├── Theme.png                # Thumbnail on Design → Themes
        ├── css/
        ├── js/
        └── images/
```

## Required Files

### Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "Contoso Theme",
    Author = "Contoso",
    Website = "https://contoso.com",
    Version = "1.0.0",
    Description = "The Contoso front-end theme.",
    Tags = ["Bootstrap"]
)]
```

### Theme with a Base Theme

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "Contoso Admin",
    Author = "Contoso",
    Website = "https://contoso.com",
    Version = "1.0.0",
    Description = "Contoso branding for the admin.",
    BaseTheme = "TheAdmin",
    Tags = ["Admin"]
)]
```

`BaseTheme` is the base theme's ID (its assembly name). The `Admin` tag makes the theme selectable as the admin theme.

### Project File (.csproj)

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

Reference the theme from the web project:

```xml
<ItemGroup>
  <ProjectReference Include="..\Themes\Contoso.Theme\Contoso.Theme.csproj" />
</ItemGroup>
```

A theme distributed as a NuGet package is installed with a `PackageReference` in the web project instead.

## Views

### Views/Layout.cshtml

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
    @await RenderSectionAsync("Footer", required: false)
    <resources type="FootScript" />
</body>
</html>
```

- `RenderBodyAsync()` renders the `Content` zone; `RenderSectionAsync(name, required: false)` renders any other zone; `IsSectionDefined(name)` tests whether a zone has content. `<zone name="...">` only **adds** content to a zone, it never renders one.
- `<resources type="Header" />` renders meta, link, stylesheet, and head script resources; `<resources type="FootScript" />` renders foot scripts.
- Require every script the theme uses. Orchard Core 4.0 no longer loads jQuery through admin or module scripts; add `<script asp-name="jQuery" at="Foot"></script>` only if the theme needs it.
- `Orchard`, `Site`, and `ThemeLayout` come from `RazorPage<TModel>`; do not set `Layout = null` in the layout.

### Rendering the Main Menu

```cshtml
<menu alias="alias:main-menu" cache-id="main-menu" cache-fixed-duration="00:05:00" cache-tag="alias:main-menu" cache-context="user.roles" />
```

Requires the `OrchardCore.Menu` feature, a menu content item with the `main-menu` alias, and `@addTagHelper *, OrchardCore.Menu`. In Liquid use `{% shape "menu", alias: "alias:main-menu" %}`.

### Views/_ViewImports.cshtml

```cshtml
@inherits OrchardCore.DisplayManagement.Razor.RazorPage<TModel>
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, OrchardCore.DisplayManagement
@addTagHelper *, OrchardCore.ResourceManagement
@addTagHelper *, OrchardCore.Menu

@using OrchardCore.DisplayManagement
@using OrchardCore.DisplayManagement.Shapes
@using OrchardCore.DisplayManagement.Zones
@using OrchardCore.Mvc.Utilities
@using Microsoft.AspNetCore.Mvc.Localization
```

### Theme Startup with a Resource Manifest

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace Contoso.Theme;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest = CreateManifest();

    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);

    private static ResourceManifest CreateManifest()
    {
        var manifest = new ResourceManifest();

        manifest
            .DefineStyle("Contoso.Theme")
            .SetUrl("~/Contoso.Theme/css/site.min.css", "~/Contoso.Theme/css/site.css")
            .SetDependencies("bootstrap:5");

        manifest
            .DefineScript("Contoso.Theme")
            .SetUrl("~/Contoso.Theme/js/site.min.js", "~/Contoso.Theme/js/site.js")
            .SetDependencies("bootstrap:5");

        return manifest;
    }
}
```

With the manifest, the layout requires resources by name only: `<style asp-name="Contoso.Theme" at="Head"></style>`. `OrchardCore.Theme.Targets` references `OrchardCore.Module.Targets`, so a theme can have `Startup` classes like a module.

### Theme with Extra Features

```csharp
using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Name = "Contoso Theme",
    Author = "Contoso",
    Version = "1.0.0",
    Description = "The Contoso front-end theme."
)]

[assembly: Feature(
    Id = "Contoso.Theme.Widgets",
    Name = "Contoso Theme Widgets",
    Description = "Widgets used by the Contoso theme.",
    Category = "Theming"
)]
```

The theme stays on the Themes page and `Contoso.Theme.Widgets` appears on the Features page (fixed in Orchard Core 4.0). Bind code to it with `[Feature("Contoso.Theme.Widgets")]` on a `Startup` class.

## Common Zones

| Zone | Purpose |
|------|---------|
| `HeadMeta` | Meta tags in head |
| `Header` | Site header, navigation |
| `Navigation` | Main menu |
| `Messages` | Alerts, notifications |
| `BeforeContent` | Before main content |
| `Content` | Main page content |
| `AfterContent` | After main content |
| `Sidebar` | Side navigation or widgets |
| `Footer` | Site footer |

The layout decides which zones exist; list the same zone names on **Design → Settings → Zones** (Layers) so editors can add widgets to them.

## Built-in Base Themes

| Theme ID | Description |
|----------|-------------|
| `TheTheme` | Default Bootstrap front-end theme |
| `TheBlogTheme` | Blog theme |
| `TheAgencyTheme` | Agency/business theme |
| `TheComingSoonTheme` | Coming soon page |
| `TheAdmin` | Admin theme |
| `SafeMode` | Minimal fallback theme |

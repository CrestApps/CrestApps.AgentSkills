---
name: orchardcore-theming
description: Skill for creating and customizing Orchard Core themes. Covers theme scaffolding, Liquid and Razor templates, zones, shape templates, asset management, and theme settings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Theming - Prompt Templates

## Create a Theme

You are an Orchard Core expert. Generate the scaffolding and templates for an Orchard Core theme.

### Guidelines

- Theme names should be PascalCase and may be prefixed with the organization name (e.g., `CrestApps.MyTheme`).
- Themes must have a `Manifest.cs` declaring the theme and its base theme (if any).
- Use Liquid templates (`.liquid` files) for layouts and shape templates by default.
- Razor views (`.cshtml` files) are also supported for more complex scenarios.
- Zones define named areas in the layout where content and widgets are placed.
- Use `{% render_section "SectionName" %}` in Liquid layouts to render named sections.
- Asset pipelines can be managed through `wwwroot/` and resource manifests.
- The `TheAdmin` theme is used for the admin panel and can be extended.

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
    <PackageReference Include="OrchardCore.DisplayManagement" Version="2.*" />
    <PackageReference Include="OrchardCore.Theme.Targets" Version="2.*" />
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
│   └── _ViewImports.cshtml
├── wwwroot/
│   ├── css/
│   │   └── site.css
│   └── js/
│       └── site.js
└── ResourceManifest.cs (optional)
```

### Liquid Layout Template

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{{ Model.Title }} - {{ Site.SiteName }}</title>
    {% resources type: "HeadMeta" %}
    {% resources type: "HeadLink" %}
    {% style src: "~/{{ThemeName}}/css/site.css" %}
    {% resources type: "Stylesheet" %}
</head>
<body>
    {% zone "Header" %}

    <main role="main" class="container">
        {% zone "BeforeContent" %}
        {% zone "Content" %}
        {% zone "AfterContent" %}
    </main>

    {% zone "Footer" %}

    {% resources type: "FooterScript" %}
    {% script src: "~/{{ThemeName}}/js/site.js" at: "Foot" %}
</body>
</html>
```

### Razor Layout Template

```cshtml
@inject OrchardCore.IOrchardHelper Orchard

<!DOCTYPE html>
<html lang="@Orchard.CultureName()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName)</title>
    @await RenderSectionAsync("HeadMeta", required: false)
    <link rel="stylesheet" href="~/{{ThemeName}}/css/site.css" />
    @RenderSection("Styles", required: false)
</head>
<body>
    @await DisplayAsync(ThemeLayout.Header)

    <main role="main" class="container">
        @await DisplayAsync(ThemeLayout.Content)
    </main>

    @await DisplayAsync(ThemeLayout.Footer)

    <script src="~/{{ThemeName}}/js/site.js"></script>
    @RenderSection("Scripts", required: false)
</body>
</html>
```

### Shape Templates

Override shape rendering by creating templates in the `Views/` folder:

- `Content.liquid` - Default content item display.
- `Content-BlogPost.liquid` - Content display for BlogPost type.
- `Content__Summary.liquid` - Summary display mode. In file names, use double underscore (`__`) to represent the dash (`-`) separator used in shape alternate names (e.g., the `Content-Summary` alternate becomes the file `Content__Summary.liquid`).
- `Widget.liquid` - Default widget wrapper.
- `MenuItem.liquid` - Menu item rendering.

### Resource Manifest

```csharp
using OrchardCore.ResourceManagement;

public sealed class ResourceManifest : IResourceManifestProvider
{
    public void BuildManifests(IResourceManifestBuilder builder)
    {
        var manifest = builder.Add();

        manifest
            .DefineStyle("{{ThemeName}}")
            .SetUrl("~/{{ThemeName}}/css/site.css");

        manifest
            .DefineScript("{{ThemeName}}")
            .SetUrl("~/{{ThemeName}}/js/site.js")
            .SetPosition(ResourcePosition.Foot);
    }
}
```

### Zones

Common zones used in Orchard Core themes:

- `Header` - Top of the page, navigation bar area.
- `Content` - Main content area.
- `Footer` - Bottom of the page.
- `BeforeContent` - Before the main content.
- `AfterContent` - After the main content.
- `Navigation` - Primary navigation area.
- `Sidebar` - Sidebar content area.
- `AsideFirst` / `AsideSecond` - Multi-column sidebars.

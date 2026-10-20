---
name: orchardcore-theme-creator
description: Creates new Orchard Core themes with proper structure, manifest, layouts using RenderBodyAsync and RenderSectionAsync, resource tag helpers, Assets.json asset builds, and extra features declared in a theme manifest. Use when the user needs to create a new theme, customize layouts, or set up frontend assets. Use this skill when requests mention Orchard Core Theme Creator, Theme Creation Workflow, Create Theme Directory, Create Manifest.cs, theme project file, theme assets, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.Themes, OrchardCore.DisplayManagement.Manifest, OrchardCore.Modules.Manifest, OrchardCore.Theme.Targets, OrchardCore.Cms.Web, TheBlogTheme, TheTheme, TheAdmin, TheAgencyTheme, Assets.json, site.css, site.js.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Theme Creator

This skill guides you through creating new Orchard Core themes following project conventions.

## Prerequisites

- OrchardCore repository (working directory)
- .NET SDK 10.0+ installed
- Node.js 22.x and Yarn 4.x only when the theme includes optional asset compilation

## Theme Creation Workflow

### Step 1: Create Theme Directory

```bash
mkdir src/OrchardCore.Themes/YourTheme
cd src/OrchardCore.Themes/YourTheme
```

### Step 2: Create Required Files

Every theme needs these files:

1. **Manifest.cs** - Theme metadata
2. **YourTheme.csproj** - Project file
3. **Views/Layout.cshtml** - Main layout
4. **Views/_ViewImports.cshtml** - Razor imports

### Step 3: Create Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Name = "Your Theme",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "Your theme description."
)]
```

**Extending a base theme:**
```csharp
[assembly: Theme(
    Name = "Your Theme",
    BaseTheme = "TheBlogTheme",  // Inherit from base
    // ... other properties
)]
```

**Shipping extra features with the theme:** a theme assembly can also declare `[assembly: Feature(Id = "YourTheme.Widgets", Name = "...", Category = "Theming")]`. In Orchard Core 4.0 the theme still appears on the Themes page and the extra features appear on the Features page; bind their services with `[Feature("YourTheme.Widgets")]` on a `Startup` class.

### Step 4: Create Project File

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
    <Title>Your Theme</Title>
    <Description>Your theme description.</Description>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\OrchardCore\OrchardCore.Theme.Targets\OrchardCore.Theme.Targets.csproj" />
  </ItemGroup>
</Project>
```

### Step 5: Create Layout

Create `Views/Layout.cshtml`:

```cshtml
<!DOCTYPE html>
<html lang="@Orchard.CultureName()" dir="@Orchard.CultureDir()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@RenderTitleSegments(Site.SiteName, "before")</title>
    <style asp-name="bootstrap" version="5" at="Head"></style>
    <style asp-name="YourTheme" asp-src="~/YourTheme/css/site.min.css" debug-src="~/YourTheme/css/site.css" depends-on="bootstrap:5" at="Head"></style>
    <script asp-name="bootstrap" version="5" at="Foot"></script>
    <script asp-name="YourTheme" asp-src="~/YourTheme/js/site.min.js" debug-src="~/YourTheme/js/site.js" at="Foot"></script>
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

- `RenderBodyAsync()` renders the `Content` zone; `RenderSectionAsync(name, required: false)` renders any other zone. `<zone name="...">` only **adds** content to a zone, it never renders one.
- `<resources type="Header" />` renders meta, link, stylesheet, and head script resources; `<resources type="FootScript" />` renders foot scripts.
- Require every script the theme uses. Orchard Core 4.0 no longer loads jQuery through admin or module scripts; add `<script asp-name="jQuery" at="Foot"></script>` only if the theme needs it.

### Step 6: Add Assets (Optional)

Create asset structure:
```
Assets/
├── scss/site.scss
├── js/site.js
└── package.json
Assets.json
```

See `references/assets.md` for configuration details.

### Step 7: Build and Activate

```bash
# Build theme
cd /path/to/orchardcore

dotnet build src/OrchardCore.Themes/YourTheme

# Build assets (if added)
yarn && yarn build

# Run application
cd src/OrchardCore.Cms.Web
dotnet run -f net10.0

# Activate theme in Admin → Design → Themes
```

## Quick Reference

### Available Zones

| Zone | Purpose |
|------|---------|
| `Header` | Site header |
| `Navigation` | Main menu |
| `Messages` | Alerts/notifications |
| `HeadMeta` | Meta/link tags rendered inside `<head>` |
| `Content` | Main content |
| `Sidebar` | Side widgets |
| `Footer` | Site footer |

### Base Themes

| Theme | Description |
|-------|-------------|
| `TheBlogTheme` | Blog template |
| `TheAdmin` | Admin UI |
| `TheAgencyTheme` | Business template |

### Naming Conventions

| Item | Convention |
|------|------------|
| Theme folder | `YourThemeName` (PascalCase) |
| Namespace | `YourThemeName` |
| CSS file | `site.css` |
| JS file | `site.js` |

## References

- `references/theme-structure.md` - Directory layout and templates
- `references/assets.md` - SCSS, JS, and asset pipeline
- `AGENTS.md` (repo root) - Build commands

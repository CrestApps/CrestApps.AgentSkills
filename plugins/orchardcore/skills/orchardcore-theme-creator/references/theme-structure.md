# Theme Structure Reference

## Directory Layout

```
src/OrchardCore.Themes/YourTheme/
├── Manifest.cs                 # Theme metadata
├── YourTheme.csproj            # Project file
├── Views/
│   ├── Layout.cshtml           # Main layout
│   ├── _ViewImports.cshtml     # Razor imports
│   └── Shared/                 # Shared partial views
├── Assets/
│   ├── scss/
│   │   └── site.scss           # Main stylesheet
│   ├── js/
│   │   └── site.js             # Main JavaScript
│   └── package.json            # Frontend dependencies
├── wwwroot/
│   ├── css/                    # Compiled CSS output
│   ├── js/                     # Compiled JS output
│   └── images/                 # Static images
└── Assets.json                 # Asset build configuration
```

## Required Files

### Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Name = "Your Theme",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "A custom theme for OrchardCore.",
    Tags = ["Bootstrap"]
)]
```

### Theme with Base Theme

```csharp
[assembly: Theme(
    Name = "Your Theme",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "A custom theme extending TheAdmin.",
    BaseTheme = "TheAdmin",  // Inherit from another theme
    Tags = ["Bootstrap", "Admin"]
)]
```

### Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
    <Title>Your Theme</Title>
    <Description>A custom theme for OrchardCore.</Description>
    <PackageTags>$(PackageTags) OrchardCoreCMS</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\OrchardCore\OrchardCore.Theme.Targets\OrchardCore.Theme.Targets.csproj" />
    <!-- Optional: Reference base theme -->
    <!-- <ProjectReference Include="..\TheAdmin\TheAdmin.csproj" /> -->
  </ItemGroup>
</Project>
```

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

### Views/_ViewImports.cshtml

```razor
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

### Theme with Extra Features

```csharp
using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Name = "Your Theme",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "A custom theme for OrchardCore."
)]

[assembly: Feature(
    Id = "YourTheme.Widgets",
    Name = "Your Theme Widgets",
    Description = "Widgets used by Your Theme.",
    Category = "Theming"
)]
```

The theme stays on the Themes page and `YourTheme.Widgets` appears on the Features page (fixed in Orchard Core 4.0).

## Available Zones

Common zones used in OrchardCore themes:

| Zone | Purpose |
|------|---------|
| `Header` | Site header, navigation |
| `Navigation` | Main menu |
| `Messages` | Alerts, notifications |
| `BeforeContent` | Before main content |
| `Content` | Main page content |
| `AfterContent` | After main content |
| `Sidebar` | Side navigation or widgets |
| `Footer` | Site footer |
| `HeadMeta` | Meta tags in head |

## Base Themes

Available base themes to extend:

| Theme | Description |
|-------|-------------|
| `TheBlogTheme` | Simple blog theme |
| `TheAdmin` | Admin dashboard theme |
| `TheAgencyTheme` | Agency/business theme |
| `SafeMode` | Minimal safe mode theme |

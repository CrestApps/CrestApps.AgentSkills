---
name: orchardcore-razor
description: Skill for building Razor views in Orchard Core themes and modules. Covers _ViewImports.cshtml setup, layouts with RenderBodyAsync, RenderSectionAsync and IsSectionDefined, the zone tag helper, shape tag helpers with caching attributes, resource tag helpers, content link and content item tag helpers, media tag helpers, datetime tag helpers, dynamic cache, IOrchardHelper extensions, and title segments. Use this skill when requests mention Orchard Core Razor Views, _ViewImports.cshtml Setup, Minimum Required Directives, _ViewStart.cshtml Conventions, Layout Rendering Patterns, Minimal Layout Template, Razor helpers, or closely related Orchard Core implementation or troubleshooting work. Strong matches include OrchardCore.DisplayManagement.Razor, OrchardCore.ResourceManagement, OrchardCore.Menu, OrchardCore.Contents.TagHelpers, OrchardCore.Media, OrchardCore.DynamicCache, IOrchardHelper, IOrchardDisplayHelper.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Razor Views - Prompt Templates

## _ViewImports.cshtml Setup

Every Orchard Core theme or module should include a `_ViewImports.cshtml` file at the root of its `Views` folder.

### Minimum Required Directives

```cshtml
@inherits OrchardCore.DisplayManagement.Razor.RazorPage<TModel>
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, OrchardCore.DisplayManagement
@addTagHelper *, OrchardCore.ResourceManagement
@addTagHelper *, OrchardCore.Menu
@addTagHelper *, OrchardCore.Contents.TagHelpers
@addTagHelper *, OrchardCore.Media
@addTagHelper *, OrchardCore.DynamicCache
@using OrchardCore.DisplayManagement
@using OrchardCore.DisplayManagement.Shapes
@using OrchardCore.ContentManagement
@using Microsoft.AspNetCore.Html
```

### Guidelines

- `@inherits OrchardCore.DisplayManagement.Razor.RazorPage<TModel>` gives access to `Orchard` (`IOrchardDisplayHelper`), `New`, `DisplayAsync`, `Site`, `T`, `ThemeLayout`, and the title helpers. Do not `@inject` another `Orchard` property.
- Add only the `@addTagHelper` lines for assemblies your theme or module actually uses, and reference the matching packages (for example `OrchardCore.ResourceManagement` and `OrchardCore.Contents.TagHelpers`).
- `@using OrchardCore.DisplayManagement.Razor` is not needed when `@inherits` already references that namespace.
- No `@using OrchardCore` directive is needed to access content helpers from `IOrchardHelper`.

## _ViewStart.cshtml Conventions

```cshtml
@{
    Layout = "Layout";
}
```

- Place `_ViewStart.cshtml` in the `Views` folder to set the default layout.
- `"Layout"` references the shape named `Layout` which resolves to `Layout.cshtml` (or `Layout.liquid`) in the active theme.
- Modules typically do not set a layout in `_ViewStart.cshtml` because their views render inside the theme's layout automatically.
- Override per-view by setting `Layout = null;` for partial or layoutless pages.

## Layout Rendering Patterns

A theme's `Layout.cshtml` renders the zones of the `Layout` shape with `RenderSectionAsync` and the main content with `RenderBodyAsync`. The `<zone>` tag helper does the opposite: it **adds** content to a zone and renders nothing where it is declared.

### Minimal Layout Template

```cshtml
<!DOCTYPE html>
<html lang="@Orchard.CultureName()" dir="@Orchard.CultureDir()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName, "before")</title>
    <style asp-name="bootstrap" version="5" at="Head"></style>
    <style asp-name="{{ThemeStyleName}}" at="Head"></style>
    <script asp-name="bootstrap" version="5" at="Foot"></script>
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
        <footer>
            @await RenderSectionAsync("Footer", required: false)
        </footer>
    }

    <resources type="FootScript" />
</body>
</html>
```

### Guidelines

- `@await RenderBodyAsync()` renders the `Content` zone of the layout. `@await RenderSectionAsync("Name", required: false)` renders any other zone; `IsSectionDefined("Name")` checks whether it has content.
- Shapes that register resources (navbar, menus, branding) must render **before** `<resources type="Header" />`. Pre-render them into a variable at the top of the layout when they appear after the head, as `TheTheme` does with its navbar.
- `<resources type="Header" />` renders `Meta`, `HeadLink`, `Stylesheet`, and `HeadScript` in one call. You can also render them individually.
- Render `<resources type="FootScript" />` right before `</body>`.
- `RenderTitleSegments(segment, position = "0", separator = null)` adds a title segment and returns the full title; `"before"` places the site name before the page segments.
- Do not reference `jQuery` implicitly. Front-end themes that need it must require it explicitly with `<script asp-name="jQuery" at="Foot"></script>`.

## Shape Tag Helpers

Shapes are the fundamental rendering unit in Orchard Core. Use tag helpers to display and compose shapes.

### Display a Shape

```cshtml
<shape type="{{ShapeType}}" />
```

### Display a Shape with Properties

```cshtml
<shape type="{{ShapeType}}" prop-title="@item.Title" prop-content="@item.Content" />
```

- Prefix shape properties with `prop-` to pass typed values; plain attributes are passed as strings.
- Use `<add-property name="content">...</add-property>` inside `<shape>` to pass Razor markup as an `IHtmlContent` property.
- Use `<metadata display-type="Summary"><add-alternate name="..." /><add-wrapper name="..." /></metadata>` inside a shape tag helper to adjust metadata.

### Display a Shape Object

When you have a shape object (e.g., from a zone or the shape factory), render it with `DisplayAsync`:

```cshtml
@await DisplayAsync(Model)
@await DisplayAsync(Model.Content)
```

### Cached Shapes

```cshtml
<shape type="{{ShapeType}}" cache-id="{{unique-id}}" cache-fixed-duration="00:05:00" cache-tag="alias:main-menu" cache-context="user.roles" />
```

Supported cache attributes are `cache-id`, `cache-context`, `cache-tag`, `cache-fixed-duration`, and `cache-sliding-duration`. They require the `OrchardCore.DynamicCache` feature.

## Zone Tag Helper

`<zone>` adds its child content to a layout zone (optionally at a position). Use it in any view to push markup into the layout:

```cshtml
<zone name="Footer" position="after">
    <p class="small">@T["Rendered from a content template."]</p>
</zone>
```

Render the zone in the layout with `@await RenderSectionAsync("Footer", required: false)`.

Common zone names: `Header`, `Navigation`, `Messages`, `Content`, `Sidebar`, `Footer`, `HeadMeta`, `BeforeContent`, `AfterContent`.

## Link Generation

In Orchard Core views, prefer MVC tag helpers so link generation stays in the Razor template and remains route-aware.

### Preferred View Pattern

```cshtml
<a asp-action="Edit" asp-controller="Admin" asp-area="MyModule" asp-route-id="@Model.ContentItemId">
    @T["Edit"]
</a>
```

### Content Item Links

With `@addTagHelper *, OrchardCore.Contents.TagHelpers`:

```cshtml
<a display-for="@contentItem">@contentItem.DisplayText</a>
<a edit-for="@contentItem">@T["Edit"]</a>
<a admin-for="@contentItem">@T["Admin"]</a>
```

### Guidelines

- Prefer `asp-action`, `asp-controller`, and when needed `asp-area` instead of hard-coded application URLs.
- Use `display-for`/`edit-for` to link to content items instead of building URLs from `AutoroutePart`.
- Do not use `@Url.Content("~/...")` to generate application links in Orchard Core views.
- Continue using normal tag helpers for forms and buttons, such as `<form asp-action="Update" method="post">`.

### URL Generation in Code

If a URL must be generated outside the view, inject `LinkGenerator` and pass the current `HttpContext` so Orchard Core generates the correct route-aware URL.

```csharp
using Microsoft.AspNetCore.Routing;

public sealed class AdminLinkService
{
    private readonly LinkGenerator _linkGenerator;

    public AdminLinkService(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public string? GetEditUrl(HttpContext httpContext, string contentItemId)
    {
        return _linkGenerator.GetPathByAction(
            httpContext,
            action: "Edit",
            controller: "Admin",
            values: new
            {
                area = "MyModule",
                id = contentItemId,
            });
    }
}
```

## Resource Tag Helpers

### Register a Stylesheet

```cshtml
<style asp-name="{{ResourceName}}" at="Head"></style>
```

### Register a Named Resource with a Version

```cshtml
<style asp-name="bootstrap" version="5" at="Head"></style>
```

### Register a Script at the Foot or Head

```cshtml
<script asp-name="{{ResourceName}}" at="Foot"></script>
<script asp-name="{{ResourceName}}" at="Head"></script>
```

### Register an Inline Script

```cshtml
<script at="Foot">
    document.addEventListener('DOMContentLoaded', function () {
        console.log('Page loaded');
    });
</script>
```

### Script Dependencies

```cshtml
<script asp-name="{{ResourceName}}" asp-src="~/{{ThemeName}}/js/site.min.js" debug-src="~/{{ThemeName}}/js/site.js" depends-on="bootstrap:5" at="Foot"></script>
```

### Admin Views Using bootstrap-select

TheAdmin no longer includes `bootstrap-select` styles automatically. Admin views that use it must require both:

```cshtml
<style asp-name="bootstrap-select" at="Head"></style>
<script asp-name="bootstrap-select" at="Foot"></script>
```

### Render All Registered Resources

Place these in the layout to output all resources registered by shapes and modules:

```cshtml
<resources type="Header" />
<resources type="FootScript" />
```

| `type` | Output |
|---|---|
| `Meta`, `HeadLink`, `Stylesheet`, `HeadScript` | Individual head sections |
| `Header` | All four head sections |
| `FootScript` | Scripts registered with `at="Foot"` |

See `orchardcore-resources` for the complete resource manager guidance.

## Media Tag Helpers

### Render an Image from the Media Library

```cshtml
<img asset-src="@imagePath" alt="@altText" />
```

### Resize an Image

```cshtml
<img asset-src="@imagePath" img-width="300" />
<img asset-src="@imagePath" img-width="800" img-height="600" img-resize-mode="Crop" />
```

| Mode | Description |
|------|-------------|
| `Pad` | Resize and pad to fit the target dimensions |
| `BoxPad` | Pad the image to fit within the bounding box |
| `Crop` | Resize and crop to fill the target dimensions |
| `Min` | Resize to the minimum of the target dimensions |
| `Max` | Resize to the maximum of the target dimensions |
| `Stretch` | Stretch the image to fill the target dimensions |

See `orchardcore-media` for the full media tag helper and image processing reference.

## Content Tag Helpers

### Render a Content Item by Handle

```cshtml
<contentitem alias="alias:{{alias}}" display-type="Summary" />
```

### Render a Loaded Content Item

```cshtml
@await Orchard.DisplayAsync(contentItem, "Summary")
```

### Common Display Types

| Display Type | Description |
|--------------|-------------|
| `Detail` | Full detail view |
| `Summary` | Abbreviated summary view |
| `SummaryAdmin` | Admin list row (List layout) |
| `SummaryAdminGrid` | Admin list card (Grid layout) |

## Menu Tag Helper

### Render a Named Menu

```cshtml
<menu alias="alias:main-menu" cache-id="main-menu" cache-fixed-duration="00:05:00" cache-tag="alias:main-menu" cache-context="user.roles" />
```

- The `alias` attribute specifies the alias of the menu content item.
- The menu renders using the menu shape templates, which can be overridden in your theme.

## Caching Markup

Use Orchard's `<dynamic-cache>` tag helper (feature `OrchardCore.DynamicCache`), not ASP.NET Core's `<cache>`, so entries are tenant-aware and invalidated by Orchard dependencies. Require scripts and styles outside the cached block: code inside it does not run on a cache hit.

```cshtml
<dynamic-cache cache-id="recent-posts" vary-by="route" dependencies="alias:recent-posts" expires-after="00:10:00">
    @await DisplayAsync(Model.Content)
</dynamic-cache>
```

| Attribute | Description |
|-----------|-------------|
| `cache-id` | Required identifier of the cached section |
| `vary-by` | Space/comma-separated cache contexts (`user`, `user.roles`, `route`, `query:page`, ...) |
| `dependencies` | Space/comma-separated invalidation tags (`contentitemid:{id}`, `alias:{alias}`) |
| `expires-after` | Relative expiration |
| `expires-on` | Absolute `DateTimeOffset` expiration |
| `expires-sliding` | Sliding expiration (default 30 seconds when nothing is set) |

Use `<cache-dependency dependency="contentitemid:@item.ContentItemId" />` inside a cached block to add dependencies discovered while rendering. See `orchardcore-dynamic-cache`.

## Date-Time Tag Helpers

### Render a UTC Date in the Site's Time Zone

```cshtml
<datetime utc="@Model.ContentItem.CreatedUtc" />
```

### Render a Date with a Custom Format

```cshtml
<datetime utc="@Model.ContentItem.PublishedUtc" format="MMMM dd, yyyy" />
```

### Relative Time

```cshtml
<timespan utc="@Model.ContentItem.CreatedUtc" />
```

## IOrchardHelper Extensions

Access `IOrchardHelper` via the `Orchard` property in Razor views that inherit from `OrchardCore.DisplayManagement.Razor.RazorPage<TModel>`.

Content helpers live in `ContentOrchardHelperExtensions` and are available without adding `@using OrchardCore`.

### Get a Content Item by ID

```cshtml
@{
    var item = await Orchard.GetContentItemByIdAsync("{{contentItemId}}");
}
```

### Get Several Content Items by ID

```cshtml
@{
    var items = await Orchard.GetContentItemsByIdAsync(ids);
}
```

### Get a Content Item by Version ID

```cshtml
@{
    var item = await Orchard.GetContentItemByVersionIdAsync("{{contentItemVersionId}}");
}
```

### Query Content Items

```cshtml
@{
    var items = await Orchard.QueryContentItemsAsync(query =>
        query.Where(index => index.ContentType == "BlogPost" && index.Published));
}
```

### Get Recent Content Items by Content Type

```cshtml
@{
    var items = await Orchard.GetRecentContentItemsByContentTypeAsync("BlogPost", 10);
}
```

### Get a Content Item by Handle

```cshtml
@{
    var item = await Orchard.GetContentItemByHandleAsync("alias:{{alias}}");
}
```

### Site Settings

Access global site settings via `Site`:

```cshtml
<p>@Site.SiteName</p>
<p>@Site.BaseUrl</p>
```

### Log a Value to the Browser Console

```cshtml
@Orchard.ConsoleLog(Model.ContentItem)
```

### Liquid-to-HTML Rendering

```cshtml
@await Orchard.LiquidToHtmlAsync("{{ 'now' | date: '%B %d, %Y' }}")
```

Only render Liquid that comes from trusted code or from users with the `ManageLiquidTemplates` permission. `HtmlBodyPart`, `HtmlField`, `MarkdownBodyPart`, and `MarkdownField` no longer run Liquid in Orchard Core 4.0; use `LiquidPart` or `LiquidField` for authored Liquid.

### Markdown to HTML

```cshtml
@await Orchard.MarkdownToHtmlAsync(markdown)
@await Orchard.MarkdownToHtmlAsync(markdown, sanitize: false)
```

The `renderLiquid` parameter was removed in 4.0.

## Defining Named Resources

Register stylesheets and scripts so they can be referenced by name in Razor views. See `orchardcore-resources` for CDN, integrity, and dependency configuration.

### Resource Configuration Class

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

public sealed class ResourceManagementOptionsConfiguration
    : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest.DefineStyle("{{ThemeStyleName}}")
            .SetUrl("~/{{ModuleOrThemeId}}/css/site.min.css", "~/{{ModuleOrThemeId}}/css/site.css")
            .SetDependencies("bootstrap:5")
            .SetVersion("1.0.0");

        _manifest.DefineScript("{{ThemeScriptName}}")
            .SetUrl("~/{{ModuleOrThemeId}}/js/site.min.js", "~/{{ModuleOrThemeId}}/js/site.js")
            .SetDependencies("bootstrap:5")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
```

### Guidelines

- The first `SetUrl` parameter is the minified version; the second is the debug version.
- Choose where a script renders when you require it (`at="Foot"` / `at="Head"`). `SetPosition(ResourcePosition.First | Last | ByDependency)` only changes ordering, not the head/foot location.
- Use `SetDependencies("name")` or `SetDependencies("name:version")` to declare dependencies.
- Register the manifest class in `Startup.cs`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
```

## Recipe Step for Themes

### Set the Active Theme via Recipe

```json
{
    "steps": [
        {
            "name": "Themes",
            "Site": "{{ThemeId}}",
            "Admin": "TheAdmin"
        }
    ]
}
```

---
name: orchardcore-resources
description: Skill for managing CSS and JavaScript resources in Orchard Core. Covers resource manifest registration, built-in named resources (jQuery, bootstrap, bootstrap-select, font-awesome, vuejs 3, vue-multiselect, vue-draggable, Sortable, codemirror, monaco, list-management), CDN settings, the OrchardCore Resources configuration section, script and style tag helpers, Liquid resource tags, dependencies and versions, inline scripts and styles, meta and link tags, IResourceManager from code, and upgrading from Orchard Core 3 (jQuery no longer pulled in by admin scripts, Vue 2 resources removed, bootstrap-select styles not included by TheAdmin). Use this skill when requests mention Orchard Core Resources, Registering a Resource Manifest, Built-in Named Resources, resource tag helpers, Liquid resource tags, missing jQuery after upgrading, or troubleshooting. Strong matches include OrchardCore.Resources, OrchardCore.ResourceManagement, ResourceManagementOptions, ResourceManifest, IResourceManager, RequireSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Resources - Prompt Templates

## Managing CSS and JavaScript Resources

You are an Orchard Core expert. Generate code and configuration for registering and rendering CSS/JavaScript resources in Orchard Core.

### Guidelines

- **Package requirement:** To use the resource tag helpers (`<script asp-name>`, `<style asp-name>`, `<resources>`, `<link>`, `<meta>`), the `IResourceManager` API, or `IConfigureOptions<ResourceManagementOptions>`, the consuming project **must reference the `OrchardCore.ResourceManagement` NuGet package**. OrchardCore modules and themes (which target `OrchardCore.Module.Targets` / `OrchardCore.Theme.Targets`) usually get it transitively, but standalone MVC, decoupled, or headless projects must add the `<PackageReference Include="OrchardCore.ResourceManagement" />` explicitly. The `@addTagHelper *, OrchardCore.ResourceManagement` directive and the `at="Foot"`/`at="Head"` script placement resolve from that assembly.
- The `OrchardCore.Resources` module provides the Resource Manager for declaring, ordering, and rendering scripts and styles.
- Resources are served from a module or theme's `wwwroot` folder via `StaticFileMiddleware`.
- Resource paths use the `~/ModuleName/` or `~/ThemeName/` prefix convention (tilde represents tenant base path).
- Named resources have a name, type (script/stylesheet), optional version, CDN URLs, and dependencies.
- Register resource manifests by implementing `IConfigureOptions<ResourceManagementOptions>` and calling `services.AddResourceConfiguration<T>()` in `Startup`.
- Use tag helpers (`<script asp-name>`, `<style asp-name>`) in Razor or `{% script %}` / `{% style %}` in Liquid.
- Dependencies ensure correct load order regardless of declaration order.
- `AppendVersion` (enabled by default) appends an SHA256 hash for cache busting.
- `UseCdn` is disabled by default; enable it in **Settings > General** after setup.
- Orchard Core 4.0 admin scripts no longer use jQuery or jQuery UI, and admin Vue apps run on Vue 3. `jQuery` is still a registered resource: any theme or module script that needs it must require it explicitly.
- Always wrap recipe JSON in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier except View Models.
- Use file-scoped namespaces in C# examples.

### Resource Settings (Admin Configuration)

| Setting           | Description                                                                                  |
|-------------------|----------------------------------------------------------------------------------------------|
| `AppendVersion`   | Appends a version hash to local scripts/styles for cache busting. On by default.             |
| `UseCdn`          | Serves scripts/styles from configured CDN URLs instead of local files. Off by default.       |
| `ResourceDebugMode` | Serves non-minified versions and disables CDN/CdnBaseUrl. For development use.            |
| `CdnBaseUrl`      | Prepends this base URL to local resources served via the Resource Manager.                   |

### Built-in Named Resources

Common resources provided by `OrchardCore.Resources` (latest versions; older versions of several remain registered, so `version="4"` or `depends-on="bootstrap:4"` still resolve):

| Name | Type | Version | Dependencies |
|------|------|---------|--------------|
| `jQuery` / `jQuery.slim` | Script | 3.7.1 | - |
| `jQuery-ui` | Script, Style | 1.14.2 | jQuery |
| `jQuery-ui-i18n` | Script | 1.14.2 | jQuery-ui |
| `popperjs` | Script | 2.11.8 | - |
| `bootstrap` | Script | 5.3.8 (also 4.6.1) | popperjs |
| `bootstrap` / `bootstrap-rtl` | Style | 5.3.8 | - |
| `bootstrap-select` | Script, Style | 1.2.6 | bootstrap |
| `font-awesome` | Style, Script | 7.3.1 (also 6.x, 5.x) | - |
| `font-awesome-v4-shims` | Script | 7.3.1 | - |
| `codemirror` (+ `codemirror-mode-*`, `codemirror-addon-*`) | Script, Style | 5.65.7 | - |
| `monaco` | Script, Style | 0.52.2 | - (ES module build, no `monaco-loader`) |
| `trumbowyg` | Script, Style | 2.31.0 | jquery-resizable |
| `nouislider` | Script, Style | 15.7.0 | - |
| `vuejs` | Script | 3.5.13 | - |
| `vue-multiselect` | Script, Style | 3.2.0 | vuejs |
| `Sortable` | Script | 1.15.3 | - |
| `vue-draggable` | Script | 4.1.0 | vuejs, Sortable |
| `js-cookie` | Script | 3.0.5 | - |
| `list-management` | Script | 1.0.0 | - |

`list-management` is a reusable admin helper for searchable lists, bulk selection, and filter auto-submit. Require it with `<script asp-name="list-management" at="Foot"></script>` and opt a container in with `data-list-management`.

### Registering a Resource Manifest

Create a configuration class to define named resources:

```csharp
namespace MyModule;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("MyModule-CustomScript")
            .SetUrl("~/MyModule/js/custom.min.js", "~/MyModule/js/custom.js")
            .SetCdn(
                "https://cdn.example.com/custom.min.js",
                "https://cdn.example.com/custom.js")
            .SetCdnIntegrity(
                "sha384-abc123minifiedhash",
                "sha384-abc123debughash")
            .SetVersion("1.0.0")
            .SetDependencies("bootstrap:5");

        _manifest
            .DefineStyle("MyModule-CustomStyle")
            .SetUrl("~/MyModule/css/custom.min.css", "~/MyModule/css/custom.css")
            .SetVersion("1.0.0")
            .SetDependencies("bootstrap");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
```

Register in `Startup.cs`:

```csharp
namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
```

### Configuration (appsettings.json)

The resource settings can also be provided from configuration, globally or per tenant (`OrchardCore:{TenantName}:Resources`). The site **General** settings override these values.

```json
{
  "OrchardCore": {
    "Resources": {
      "ResourceDebugMode": "FromConfiguration",
      "UseCdn": false,
      "CdnBaseUrl": "https://cdn.example.com",
      "AppendVersion": true
    }
  }
}
```

`ResourceDebugMode` accepts `FromConfiguration` (debug in every environment except Production), `Enabled`, or `Disabled`. The former `OrchardCore_Resources` section name still works but is deprecated.

### Using Tag Helpers (Razor)

Add to `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, OrchardCore.ResourceManagement
```

#### Register a Named Resource

```html
<script asp-name="bootstrap"></script>
<style asp-name="bootstrap"></style>
```

#### Force CDN Usage

```html
<script asp-name="bootstrap" use-cdn="true"></script>
```

#### Specify Version

```html
<script asp-name="bootstrap" version="4"></script>
```

#### Append Version Hash

```html
<script asp-name="bootstrap" asp-append-version="true"></script>
```

#### Specify Location (Head or Foot)

```html
<script asp-name="bootstrap" at="Foot"></script>
```

#### Inline Resource Definition

Define and register a resource directly in a view:

```html
<script asp-name="foo" asp-src="~/MyTheme/js/foo.min.js" debug-src="~/MyTheme/js/foo.js" depends-on="bootstrap:5" version="1.0"></script>
<script asp-name="bar" asp-src="~/MyTheme/js/bar.min.js" debug-src="~/MyTheme/js/bar.js" depends-on="foo:1.0" version="1.0"></script>
```

### Using Liquid Tags

#### Register a Named Resource

```liquid
{% script name:"bootstrap" %}
{% style name:"bootstrap" %}
```

#### Force CDN

```liquid
{% script name:"bootstrap", use_cdn:"true" %}
```

#### Specify Version

```liquid
{% script name:"bootstrap", version:"4" %}
```

#### Append Version Hash

```liquid
{% script name:"bootstrap", append_version:"true" %}
```

#### Specify Location

```liquid
{% script name:"bootstrap", at:"Foot" %}
```

#### Inline Resource Definition

```liquid
{% script name:"foo", src:"~/MyTheme/js/foo.min.js", debug_src:"~/MyTheme/js/foo.js", depends_on:"bootstrap:5", version:"1.0" %}
```

### Custom Inline Scripts

#### Razor

```html
<script at="Foot">
    document.addEventListener('DOMContentLoaded', function() {
        console.log('Page loaded');
    });
</script>
```

Named inline script (rendered only once, with dependencies). A script that uses jQuery must declare it, because nothing else on the page is guaranteed to require it:

```html
<script asp-name="MyInitScript" at="Foot" depends-on="jQuery">
    $(function() { /* initialization */ });
</script>
```

#### Liquid

```liquid
{% scriptblock at: "Foot" %}
    document.addEventListener('DOMContentLoaded', function() {
        console.log('Page loaded');
    });
{% endscriptblock %}
```

Named inline script:

```liquid
{% scriptblock name: "MyInitScript", at: "Foot", depends_on:"jQuery" %}
    $(function() { /* initialization */ });
{% endscriptblock %}
```

### Custom Inline Styles

#### Razor

```html
<style at="Head">
    .hero-section { background: linear-gradient(to right, #667eea, #764ba2); }
</style>
```

Named inline style (rendered only once):

```html
<style asp-name="hero-style" depends-on="bootstrap">
    .hero-section { background: linear-gradient(to right, #667eea, #764ba2); }
</style>
```

#### Liquid

```liquid
{% styleblock at: "Head" %}
    .hero-section { background: linear-gradient(to right, #667eea, #764ba2); }
{% endstyleblock %}
```

Named inline style:

```liquid
{% styleblock name: "hero-style", depends_on:"bootstrap" %}
    .hero-section { background: linear-gradient(to right, #667eea, #764ba2); }
{% endstyleblock %}
```

### Link Tag Helper

Use the link tag for non-stylesheet relationships (favicons, preloads):

#### Razor

```html
<link asp-src="~/MyTheme/favicon/favicon-16x16.png" rel="icon" type="image/png" sizes="16x16" />
```

#### Liquid

```liquid
{% link rel:"icon", type:"image/png", sizes:"16x16", src:"~/MyTheme/favicon/favicon-16x16.png" %}
```

### Meta Tags

#### Razor

```html
<meta asp-name="description" content="My website description" />
```

#### Liquid

```liquid
{% meta name:"description", content:"My website description" %}
```

Meta tag properties:

| Property                     | Description                          |
|------------------------------|--------------------------------------|
| `name` (`asp-name` in Razor) | The `name` attribute                |
| `content`                    | The `content` attribute             |
| `httpequiv`                  | The `http-equiv` attribute          |
| `charset`                    | The `charset` attribute             |
| `separator`                  | Separator for multiple same-name tags|

### Rendering Resources in Layout

Resources must be rendered in the layout to appear on the page.

#### Head Resources (in `<head>`)

**Razor:**

```html
<head>
    <resources type="Meta" />
    <resources type="HeadLink" />
    <resources type="HeadScript" />
    <resources type="Stylesheet" />
</head>
```

**Liquid:**

```liquid
<head>
    {% resources type: "Meta" %}
    {% resources type: "HeadLink" %}
    {% resources type: "HeadScript" %}
    {% resources type: "Stylesheet" %}
</head>
```

`<resources type="Header" />` (Liquid `{% resources type: "Header" %}`) renders `Meta`, `HeadLink`, `Stylesheet`, and `HeadScript` in one call. Render shapes that register resources (menus, navbars) before the head resources, or pre-render them into a variable at the top of the layout.

#### Foot Resources (at end of `<body>`)

**Razor:**

```html
<body>
    ...
    <resources type="FootScript" />
</body>
```

**Liquid:**

```liquid
<body>
    ...
    {% resources type: "FootScript" %}
</body>
```

### Using the IResourceManager API

Inject `IResourceManager` (package `OrchardCore.ResourceManagement.Abstractions`) to register resources from code that runs during the request, such as an MVC filter, a display driver, or a view component. Never register resources from an `IShapeTableProvider`: the shape table is built once and cached.

```csharp
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Filters;
using OrchardCore.Admin;
using OrchardCore.ResourceManagement;

namespace MyModule;

public sealed class AdminAssetsFilter : IAsyncResultFilter
{
    private readonly IResourceManager _resourceManager;

    public AdminAssetsFilter(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.IsViewOrPageResult() && AdminAttribute.IsApplied(context.HttpContext))
        {
            // Register named resources.
            _resourceManager.RegisterResource("stylesheet", "bootstrap-select").AtHead();
            _resourceManager.RegisterResource("script", "bootstrap-select").AtFoot();

            // Register a custom inline script.
            _resourceManager.RegisterFootScript(
                new HtmlString("<script>console.log('Hello');</script>"));

            // Register a meta tag.
            _resourceManager.RegisterMeta(
                new MetaEntry { Content = "Orchard Core", Name = "generator" });
        }

        await next();
    }
}
```

Register the filter with `services.Configure<MvcOptions>(options => options.Filters.Add<AdminAssetsFilter>());`.

`RequireSettings` also supports `UseVersion("5")` (latest matching version, exception when missing), `ShouldAppendVersion(true)`, `UseCdn(true)`, `UseCulture(...)`, and `SetDependencies(...)`. Missing named resources are logged as errors.

### Resource Location Convention

| Location  | Description                                                       |
|-----------|-------------------------------------------------------------------|
| `Head`    | Rendered in `<head>` via `HeadScript` resources tag               |
| `Foot`    | Rendered at end of `<body>` via `FootScript` resources tag        |
| `Inline`  | Rendered at the point of declaration (default for unnamed scripts)|

## Upgrading from 3.x

- TheAdmin no longer includes `bootstrap-select` styles. Views that use `bootstrap-select` must add `<style asp-name="bootstrap-select" at="Head"></style>` (and the script).
- Admin scripts were rewritten without jQuery and jQuery UI. Front-end themes or modules that relied on jQuery being loaded by another resource must require `jQuery` explicitly.
- Vue 2 resources were removed: `vuejs` 2.7.16, `vue-multiselect` 2.1.6, `vue-multiselect-wrapper`, and `vue-draggable` 2.24.3. Use `vuejs` 3, `vue-multiselect` 3, and `vue-draggable` 4, and drop `:2` version pins from `depends-on`.
- `monaco-loader` was removed; `monaco` is now an ES module build with a separate `monaco` stylesheet.
- AJAX-injected field and part editor scripts are ES modules.
- Rename the `OrchardCore_Resources` configuration section to `OrchardCore:Resources`.

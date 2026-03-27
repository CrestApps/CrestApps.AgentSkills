---
name: orchardcore-resources
description: Skill for managing CSS and JavaScript resources in Orchard Core. Covers resource manifest registration, named resources, CDN configuration, script and style tag helpers, resource dependencies, inline scripts, resource versioning, and module asset conventions. Use this skill when requests mention Orchard Core Resources, Managing CSS and JavaScript Resources, Resource Settings (Admin Configuration), Built-in Named Resources, Registering a Resource Manifest, Using Tag Helpers (Razor), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Resources, OrchardCore.ResourceManagement, IConfigureOptions, IServiceCollection, IResourceManager, MyShapeTableProvider, IShapeTableProvider, ShapeTableBuilder. It also helps with Registering a Resource Manifest, Using Tag Helpers (Razor), Using Liquid Tags, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Resources - Prompt Templates

## Managing CSS and JavaScript Resources

You are an Orchard Core expert. Generate code and configuration for registering and rendering CSS/JavaScript resources in Orchard Core.

### Guidelines

- The `OrchardCore.Resources` module provides the Resource Manager for declaring, ordering, and rendering scripts and styles.
- Resources are served from a module or theme's `wwwroot` folder via `StaticFileMiddleware`.
- Resource paths use the `~/ModuleName/` or `~/ThemeName/` prefix convention (tilde represents tenant base path).
- Named resources have a name, type (script/stylesheet), optional version, CDN URLs, and dependencies.
- Register resource manifests by implementing `IConfigureOptions<ResourceManagementOptions>` and calling `services.AddResourceConfiguration<T>()` in `Startup`.
- Use tag helpers (`<script asp-name>`, `<style asp-name>`) in Razor or `{% script %}` / `{% style %}` in Liquid.
- Dependencies ensure correct load order regardless of declaration order.
- `AppendVersion` (enabled by default) appends an SHA256 hash for cache busting.
- `UseCdn` is disabled by default; enable it in **Settings > General** after setup.
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

Common resources provided by `OrchardCore.Resources`:

| Name             | Type   | Version | Dependencies |
|------------------|--------|---------|--------------|
| jQuery           | Script | 3.7.1   | -            |
| jQuery-ui        | Script | 1.12.1  | jQuery       |
| bootstrap        | Script | 5.3.8   | popperjs     |
| bootstrap        | Style  | 5.3.8   | -            |
| font-awesome     | Style  | 6.7.2   | -            |
| codemirror       | Script | 5.65.7  | -            |
| Sortable         | Script | 1.10.2  | -            |
| monaco           | Script | 0.46.0  | monaco-loader|

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
            .SetDependencies("jQuery");

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
<script asp-name="foo" asp-src="~/MyTheme/js/foo.min.js" debug-src="~/MyTheme/js/foo.js" depends-on="jQuery" version="1.0"></script>
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
{% script name:"foo", src:"~/MyTheme/js/foo.min.js", debug_src:"~/MyTheme/js/foo.js", depends_on:"jQuery", version:"1.0" %}
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

Named inline script (rendered only once, with dependencies):

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

Inject `IResourceManager` for programmatic resource registration from code:

```csharp
namespace MyModule;

public sealed class MyShapeTableProvider : IShapeTableProvider
{
    private readonly IResourceManager _resourceManager;

    public MyShapeTableProvider(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        // Register a named resource
        var settings = _resourceManager.RegisterResource("script", "bootstrap");
        settings.AtFoot();
        settings.UseVersion("5");

        // Register a custom inline script
        _resourceManager.RegisterFootScript(
            new HtmlString("<script>console.log('Hello');</script>"));

        // Register a meta tag
        _resourceManager.RegisterMeta(
            new MetaEntry { Content = "Orchard Core", Name = "generator" });

        return ValueTask.CompletedTask;
    }
}
```

### Resource Location Convention

| Location  | Description                                                       |
|-----------|-------------------------------------------------------------------|
| `Head`    | Rendered in `<head>` via `HeadScript` resources tag               |
| `Foot`    | Rendered at end of `<body>` via `FootScript` resources tag        |
| `Inline`  | Rendered at the point of declaration (default for unnamed scripts)|

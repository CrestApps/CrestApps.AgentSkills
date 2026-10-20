# Assets and Resources

How to register and require scripts/styles via Orchard Core's resource manager.

## Define Resources in a Manifest

Create an `IConfigureOptions<ResourceManagementOptions>` implementation in a module or theme:

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
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
            .DefineStyle("MyTheme")
            .SetUrl("~/MyTheme/css/site.min.css", "~/MyTheme/css/site.css")
            .SetVersion("1.0")
            .SetDependencies("bootstrap");

        manifest
            .DefineScript("MyTheme")
            .SetUrl("~/MyTheme/js/site.min.js", "~/MyTheme/js/site.js")
            .SetDependencies("bootstrap:5");

        return manifest;
    }
}
```

Register the configuration in the module or theme startup:

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

## Require Resources in Razor

Using tag helpers:

```cshtml
<!-- From manifest (by name) -->
<style asp-name="MyTheme"></style>
<script asp-name="MyTheme" at="Foot"></script>

<!-- Direct file inclusion -->
<style asp-src="~/MyTheme/css/extra.css" at="Head"></style>
<script asp-src="~/MyTheme/js/extra.js" at="Foot"></script>

<!-- With dependency ordering -->
<style asp-name="MyTheme" depends-on="bootstrap"></style>
```

Placement options: `at="Head"` or `at="Foot"` controls rendering location.

## Require Resources in Liquid

```liquid
{% style name: "MyTheme", at: "Head" %}
{% script name: "MyTheme", at: "Foot" %}
{% style src: "~/MyTheme/css/extra.css", at: "Head" %}
{% script src: "~/MyTheme/js/extra.js", at: "Foot" %}
{% resources type: "Header" %}
{% resources type: "FootScript" %}
```

For simple cases in Liquid, link static assets directly:

```liquid
<link rel="stylesheet" href="{{ '~/MyTheme/css/site.css' | href }}">
```

## Built-In Orchard Core Resources

Common style/script names you can require without adding CDNs yourself (from `OrchardCore.Resources`):

| Resource Name | Type | Description |
|---|---|---|
| `bootstrap` | CSS/JS | Bootstrap 5.3 (4.6 also registered) |
| `bootstrap-rtl` | CSS | Bootstrap RTL support |
| `font-awesome` | CSS/JS | Font Awesome 7 (6 and 5 also registered) |
| `jQuery` | JS | jQuery 3.7.1, only loaded when required explicitly |
| `jQuery-ui` | JS/CSS | jQuery UI 1.14.2 |
| `trumbowyg` | JS/CSS | WYSIWYG editor |
| `codemirror` | JS/CSS | Code editor |
| `monaco` | JS/CSS | Monaco editor (ES module build) |
| `bootstrap-select` | JS/CSS | Bootstrap select component |
| `nouislider` | JS/CSS | Range slider |
| `vuejs` | JS | Vue 3 |
| `vue-multiselect` | JS/CSS | Vue 3 multiselect component |
| `vue-draggable` | JS | Vue 3 draggable lists (depends on `Sortable`) |

Prefer using these names in `asp-name`/`depends-on` instead of adding CDN links manually. In Orchard Core 4.0, admin and module scripts no longer depend on jQuery, so a theme that uses jQuery must require it itself. Vue 2 resources were removed; see `orchardcore-resources`.

## Injecting Resources for Admin Pages

Use an MVC result filter to include resources only on full admin views:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

[RequireFeatures("OrchardCore.Admin")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<AdminResourceFilter>();
        });
    }
}

public sealed class AdminResourceFilter : IAsyncResultFilter
{
    private readonly IResourceManager _resourceManager;

    public AdminResourceFilter(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.IsViewOrPageResult() && AdminAttribute.IsApplied(context.HttpContext))
        {
            _resourceManager.RegisterResource("stylesheet", "MyModule.AdminStyles");
        }

        await next.Invoke();
    }
}
```

## Static File Serving

- Static files live under `wwwroot/` in your theme or module.
- Files are served at `~/ModuleOrThemeName/path/to/file`.
- Orchard Core's module file provider handles routing automatically.
- Theme images are static files: `<img src="~/MyTheme/images/logo.png" asp-append-version="true">`. Use `asset-src` only for media library paths.

## Tips

- Keep resource names stable; use versioning to bust caches.
- Prefer manifest resources so dependencies are tracked and deduplicated.
- Use `at="Head"` for critical CSS; default to `Foot` for scripts.
- Admin views using `bootstrap-select` must require its style explicitly (`<style asp-name="bootstrap-select" at="Head"></style>`); TheAdmin no longer includes it.
- For quick theme prototyping, you can use a CDN (e.g., Tailwind Play CDN) directly in the layout head. Prefer a build pipeline for production.

# Theming Examples

## Example 1: Simple Blog Theme

### Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "CrestApps.BlogTheme",
    Author = "CrestApps",
    Website = "https://crestapps.com",
    Version = "1.0.0",
    Description = "A clean and responsive blog theme.",
    BaseTheme = "TheTheme"
)]
```

### Views/Layout.liquid

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}" dir="{{ Culture.Dir }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{% page_title Site.SiteName, position: "before", separator: " - " %}</title>
    {% style name: "CrestApps.BlogTheme", at: "Head" %}
    {% script name: "CrestApps.BlogTheme", at: "Foot" %}
    {% resources type: "Header" %}
    {% render_section "HeadMeta", required: false %}
</head>
<body>
    <header class="site-header">
        {% shape "menu", alias: "alias:main-menu", cache_id: "main-menu", cache_fixed_duration: "00:05:00", cache_tag: "alias:main-menu", cache_context: "user.roles" %}
        {% render_section "Navigation", required: false %}
        <div class="container">
            <h1 class="site-title">{{ Site.SiteName }}</h1>
        </div>
    </header>

    <main role="main" class="container">
        <div class="row">
            <div class="col-md-8">
                {% render_section "Messages", required: false %}
                {% render_body %}
            </div>
            <aside class="col-md-4">
                {% render_section "Sidebar", required: false %}
            </aside>
        </div>
    </main>

    <footer class="site-footer">
        {% render_section "Footer", required: false %}
        <div class="container">
            <p>&copy; {{ "now" | date: "%Y" }} {{ Site.SiteName }}</p>
        </div>
    </footer>

    {% resources type: "FootScript" %}
</body>
</html>
```

## Example 2: Content Shape Override (Liquid)

### Views/Content-BlogPost.liquid

```liquid
<article class="blog-post">
    <header>
        <h2>{{ Model.ContentItem.DisplayText }}</h2>
        <time datetime="{{ Model.ContentItem.PublishedUtc | date: "%Y-%m-%d" }}">
            {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
        </time>
    </header>

    <div class="post-body">
        {{ Model.Content | shape_render }}
    </div>
</article>
```

## Example 3: Content Shape Override (Razor)

### Views/Content-BlogPost.cshtml

```cshtml
<article class="blog-post">
    <header>
        <h2>@Model.ContentItem.DisplayText</h2>
        <time datetime="@Model.ContentItem.PublishedUtc?.ToString("yyyy-MM-dd")">
            @Model.ContentItem.PublishedUtc?.ToString("MMMM dd, yyyy")
        </time>
    </header>

    <div class="post-body">
        @await DisplayAsync(Model.Content)
    </div>
</article>
```

## Example 4: Summary Display Type Override

### Views/Content-BlogPost.Summary.liquid

```liquid
<article class="blog-summary">
    <h3>
        <a href="{{ Model.ContentItem | display_url }}">
            {{ Model.ContentItem.DisplayText }}
        </a>
    </h3>
    <time datetime="{{ Model.ContentItem.PublishedUtc | date: "%Y-%m-%d" }}">
        {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
    </time>
</article>
```

## Example 5: Media Field Rendering in a Template

### Razor — Image from MediaField

```cshtml
@{
    var imgPath = Model.ContentItem.Content.ArticlePart.HeroImage.Paths?[0]?.ToString();
    var imgAlt = Model.ContentItem.Content.ArticlePart.HeroImage.MediaTexts?[0]?.ToString();
}
@if (!string.IsNullOrEmpty(imgPath))
{
    <figure>
        <img asset-src="@imgPath" asp-append-version="true" alt="@imgAlt" class="img-fluid" />
    </figure>
}
```

### Liquid — Image from MediaField

```liquid
{% assign img = Model.ContentItem.Content.ArticlePart.HeroImage.Paths[0] %}
{% assign alt = Model.ContentItem.Content.ArticlePart.HeroImage.MediaTexts[0] %}
{% if img %}
    <figure>
        <img src="{{ img | asset_url | resize_url: width: 1200 }}" alt="{{ alt }}" class="img-fluid" />
    </figure>
{% endif %}
```

## Example 6: Widget Shape Override

### Views/Widget-HeroBanner.liquid

```liquid
<section class="hero-banner">
    <div class="container">
        {{ Model.Content | shape_render }}
    </div>
</section>
```

## Example 7: Placement.json

A `placement.json` that customizes how Article content type is rendered:

```json
{
    "TitlePart": [
        { "place": "Header:1", "displayType": "Detail", "contentType": "Article" },
        { "place": "Header:1", "displayType": "Summary", "contentType": "Article" }
    ],
    "HtmlBodyPart": [
        { "place": "Content:5", "displayType": "Detail", "contentType": "Article" },
        { "place": "-", "displayType": "Summary", "contentType": "Article" }
    ]
}
```

## Example 8: Resource Manifest

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
            .DefineStyle("CrestApps.BlogTheme")
            .SetUrl("~/CrestApps.BlogTheme/css/site.min.css", "~/CrestApps.BlogTheme/css/site.css")
            .SetVersion("1.0")
            .SetDependencies("bootstrap:5");

        manifest
            .DefineScript("CrestApps.BlogTheme")
            .SetUrl("~/CrestApps.BlogTheme/js/site.min.js", "~/CrestApps.BlogTheme/js/site.js")
            .SetDependencies("bootstrap:5");

        return manifest;
    }
}
```

Register the configuration in the theme startup:

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

## Example 9: Razor Layout with Resource Tag Helpers

### Views/Layout.cshtml

```cshtml
<!DOCTYPE html>
<html lang="@Orchard.CultureName()" dir="@Orchard.CultureDir()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName, "before")</title>
    <style asp-name="CrestApps.BlogTheme" at="Head"></style>
    <script asp-name="CrestApps.BlogTheme" at="Foot"></script>
    <resources type="Header" />
    @await RenderSectionAsync("HeadMeta", required: false)
</head>
<body>
    @await RenderSectionAsync("Header", required: false)

    <div class="container">
        <div class="row">
            <main class="col-md-8">
                @await RenderSectionAsync("Messages", required: false)
                @await RenderBodyAsync()
            </main>
            @if (IsSectionDefined("Sidebar"))
            {
                <aside class="col-md-4">
                    @await RenderSectionAsync("Sidebar", required: false)
                </aside>
            }
        </div>
    </div>

    @await RenderSectionAsync("Footer", required: false)

    <resources type="FootScript" />
</body>
</html>
```

## Example 10: BagPart / FlowPart Rendering

### Liquid — Render FlowPart Items

```liquid
{% for item in Model.ContentItem.Content.FlowPart.Widgets %}
    {{ item | shape_build_display: "Detail" | shape_render }}
{% endfor %}
```

### Razor — Render BagPart Items

```cshtml
@foreach (var item in Model.ContentItem.Content.BagPart.ContentItems)
{
    @await Orchard.DisplayAsync(item, "Detail")
}
```

## Example 11: Admin Theme Child

### Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "CrestApps.CustomAdmin",
    Author = "CrestApps",
    Version = "1.0.0",
    Description = "Custom admin theme with branding.",
    BaseTheme = "TheAdmin",
    Tags = new[] { "Admin" }
)]
```

Override the admin branding (logo area of the top bar) by creating `Views/AdminBranding.cshtml` in your admin theme:

```cshtml
@using Microsoft.Extensions.Options
@using OrchardCore.Admin
@inject IOptions<AdminOptions> AdminOptions

<a class="ta-navbar-brand navbar-brand" href="@Url.Content("~/" + AdminOptions.Value.AdminUrlPrefix)">
    <div class="d-flex align-items-center">
        <img src="~/CrestApps.CustomAdmin/images/logo.svg" alt="@Site.SiteName" height="32" class="pe-2" />
        <span>@Site.SiteName</span>
    </div>
</a>
```

Rebrand the admin menu colors from the admin theme's stylesheet (Orchard Core 4.0):

```css
:root {
    --oc-admin-accent-rgb: 73, 80, 87;
    --oc-admin-accent-strong-color: #343a40;
    --oc-admin-menu-bg-color: #f1f3f5;
    --oc-admin-menu-border-color: #dee2e6;
}
```

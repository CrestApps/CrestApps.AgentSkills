# Razor View Examples

## Example 1: Complete Theme Layout

### Views/_ViewImports.cshtml

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

### Views/_ViewStart.cshtml

```cshtml
@{
    Layout = "Layout";
}
```

### Views/Layout.cshtml

```cshtml
@{
    // Pre-render zones that register resources, so their scripts and styles
    // are known before <resources type="Header" /> runs.
    var navigation = await RenderSectionAsync("Navigation", required: false);
}
<!DOCTYPE html>
<html lang="@Orchard.CultureName()" dir="@Orchard.CultureDir()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName, "before")</title>
    <style asp-name="bootstrap" version="5" at="Head"></style>
    <style asp-name="MyTheme" at="Head"></style>
    <script asp-name="bootstrap" version="5" at="Foot"></script>
    <script asp-name="MyTheme" at="Foot"></script>
    <resources type="Header" />
    @await RenderSectionAsync("HeadMeta", required: false)
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-lg">
            <div class="container">
                <a class="navbar-brand" href="~/">@Site.SiteName</a>
                <menu alias="alias:main-menu" cache-id="main-menu" cache-fixed-duration="00:05:00" cache-tag="alias:main-menu" cache-context="user.roles" />
                @navigation
            </div>
        </nav>
    </header>

    @if (IsSectionDefined("BeforeContent"))
    {
        <div class="before-content">
            @await RenderSectionAsync("BeforeContent", required: false)
        </div>
    }

    <main class="container">
        <div class="row">
            <div class="@(IsSectionDefined("Sidebar") ? "col-md-8" : "col-md-12")">
                @await RenderSectionAsync("Messages", required: false)
                @await RenderBodyAsync()
            </div>

            @if (IsSectionDefined("Sidebar"))
            {
                <aside class="col-md-4">
                    @await RenderSectionAsync("Sidebar", required: false)
                </aside>
            }
        </div>
    </main>

    @if (IsSectionDefined("Footer"))
    {
        <footer class="site-footer">
            <div class="container">
                @await RenderSectionAsync("Footer", required: false)
            </div>
        </footer>
    }

    <resources type="FootScript" />
</body>
</html>
```

### ResourceManagementOptionsConfiguration.cs

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

        _manifest.DefineStyle("MyTheme")
            .SetUrl("~/MyTheme/css/site.min.css", "~/MyTheme/css/site.css")
            .SetDependencies("bootstrap:5")
            .SetVersion("1.0.0");

        _manifest.DefineScript("MyTheme")
            .SetUrl("~/MyTheme/js/site.min.js", "~/MyTheme/js/site.js")
            .SetDependencies("bootstrap:5")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
```

The script's location is chosen when it is required (`at="Foot"` in the layout), not in the manifest.

### Startup.cs

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
```

## Example 2: Blog Post Detail Template

This template overrides the shape for a `BlogPost` content type rendered in `Detail` display type (`Content_Detail__BlogPost`; `Content-BlogPost.cshtml` also matches the detail page).

### Views/Content-BlogPost.Detail.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = (ContentItem)Model.ContentItem;
    var blogPostPart = contentItem.Content.BlogPost;
    var imagePath = blogPostPart?.Image?.Paths?[0]?.ToString();
    var subtitle = blogPostPart?.Subtitle?.Text?.ToString();
}

<article>
    <header>
        <h1>@contentItem.DisplayText</h1>

        @if (!string.IsNullOrEmpty(subtitle))
        {
            <p class="lead">@subtitle</p>
        }

        <div class="meta text-muted">
            <datetime utc="@contentItem.PublishedUtc" format="MMMM dd, yyyy" />
        </div>
    </header>

    @if (!string.IsNullOrEmpty(imagePath))
    {
        <div class="featured-image">
            <img asset-src="@imagePath"
                 img-width="1200"
                 img-height="630"
                 img-resize-mode="Crop"
                 alt="@contentItem.DisplayText"
                 class="img-fluid rounded" />
        </div>
    }

    <div class="body-content">
        @await DisplayAsync(Model.Content)
    </div>
</article>
```

## Example 3: Blog Post Summary Template

### Views/Content-BlogPost.Summary.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = (ContentItem)Model.ContentItem;
    var blogPostPart = contentItem.Content.BlogPost;
    var imagePath = blogPostPart?.Image?.Paths?[0]?.ToString();
    var subtitle = blogPostPart?.Subtitle?.Text?.ToString();
}

<article class="card mb-4">
    @if (!string.IsNullOrEmpty(imagePath))
    {
        <img asset-src="@imagePath"
             img-width="400"
             img-height="250"
             img-resize-mode="Crop"
             class="card-img-top"
             alt="@contentItem.DisplayText" />
    }

    <div class="card-body">
        <h2 class="card-title">
            <a display-for="@contentItem">@contentItem.DisplayText</a>
        </h2>

        @if (!string.IsNullOrEmpty(subtitle))
        {
            <p class="card-text text-muted">@subtitle</p>
        }

        <div class="card-text">
            <small class="text-muted">
                <datetime utc="@contentItem.PublishedUtc" format="MMM dd, yyyy" />
            </small>
        </div>
    </div>
</article>
```

## Example 4: Widget Template with Caching

### Views/Widget-HeroBanner.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = (ContentItem)Model.ContentItem;
    var heroPart = contentItem.Content.HeroBanner;
    var heading = heroPart?.Heading?.Text?.ToString();
    var bgImagePath = heroPart?.BackgroundImage?.Paths?[0]?.ToString();
    var ctaUrl = heroPart?.CallToAction?.Url?.ToString();
    var ctaText = heroPart?.CallToAction?.Text?.ToString();
}

<dynamic-cache cache-id="hero-banner-@contentItem.ContentItemId"
               dependencies="contentitemid:@contentItem.ContentItemId"
               expires-after="00:30:00">
    <section class="hero-banner" style="@(!string.IsNullOrEmpty(bgImagePath) ? $"background-image: url('{Orchard.AssetUrl(bgImagePath)}')" : "")">
        <div class="container text-center">
            @if (!string.IsNullOrEmpty(heading))
            {
                <h1 class="display-4">@heading</h1>
            }

            @await DisplayAsync(Model.Content)

            @if (!string.IsNullOrEmpty(ctaUrl))
            {
                <a href="@ctaUrl" class="btn btn-primary btn-lg">@(ctaText ?? "Learn More")</a>
            }
        </div>
    </section>
</dynamic-cache>
```

The `contentitemid:` dependency invalidates the cached markup when the widget is published, unpublished, or removed.

## Example 5: Querying Content Items in a Razor View

### Views/RecentBlogPosts.cshtml

```cshtml
@{
    var recentPosts = await Orchard.GetRecentContentItemsByContentTypeAsync("BlogPost", 5);
}

@if (recentPosts.Any())
{
    <div class="recent-posts">
        <h3>Recent Posts</h3>
        <ul class="list-unstyled">
            @foreach (var post in recentPosts)
            {
                <li>
                    <a display-for="@post">@post.DisplayText</a>
                    <small class="text-muted">
                        <datetime utc="@post.PublishedUtc" format="MMM dd, yyyy" />
                    </small>
                </li>
            }
        </ul>
    </div>
}
```

## Example 6: Pushing Content into a Layout Zone

From any content template, add markup to a zone rendered by the layout:

```cshtml
<zone name="Sidebar" position="1">
    <div class="card">
        <div class="card-body">@T["Related reading"]</div>
    </div>
</zone>
```

The layout renders it with `@await RenderSectionAsync("Sidebar", required: false)`.

## Example 7: Shape with Inline Resources

### Views/ContactForm.cshtml

```cshtml
<style asp-name="ContactFormStyles" asp-src="~/MyTheme/css/contact.min.css" debug-src="~/MyTheme/css/contact.css" at="Head"></style>

<div class="contact-form">
    <h2>Contact Us</h2>
    <form method="post" asp-action="Submit" asp-controller="Contact">
        <div class="mb-3">
            <label for="name" class="form-label">Name</label>
            <input type="text" class="form-control" id="name" name="Name" required />
        </div>
        <div class="mb-3">
            <label for="email" class="form-label">Email</label>
            <input type="email" class="form-control" id="email" name="Email" required />
        </div>
        <div class="mb-3">
            <label for="message" class="form-label">Message</label>
            <textarea class="form-control" id="message" name="Message" rows="5" required></textarea>
        </div>
        <button type="submit" class="btn btn-primary">Send</button>
    </form>
</div>

<script at="Foot">
    document.querySelector('.contact-form form').addEventListener('submit', function (e) {
        var btn = this.querySelector('button[type="submit"]');
        btn.disabled = true;
        btn.textContent = 'Sending...';
    });
</script>
```

If a script needs jQuery, require it explicitly (`<script asp-name="jQuery" at="Foot"></script>` or `depends-on="jQuery"`); Orchard Core 4.0 admin and theme scripts no longer pull it in.

## Example 8: Content Picker and Related Items

### Views/Content-Article.cshtml (fragment)

```cshtml
@using OrchardCore.ContentFields.Fields
@using OrchardCore.ContentManagement

@{
    var contentItem = (ContentItem)Model.ContentItem;
    var relatedField = contentItem.Get<ContentPart>("Article")?.Get<ContentPickerField>("RelatedArticles");
    var related = relatedField?.ContentItemIds?.Length > 0
        ? await Orchard.GetContentItemsByIdAsync(relatedField.ContentItemIds)
        : Enumerable.Empty<ContentItem>();
}

@if (related.Any())
{
    <aside class="related-articles">
        <h3>Related Articles</h3>
        <div class="row">
            @foreach (var item in related)
            {
                <div class="col-md-4">
                    @await Orchard.DisplayAsync(item, "Summary")
                </div>
            }
        </div>
    </aside>
}
```

## Example 9: Theme Setup Recipe

```json
{
    "steps": [
        {
            "name": "Feature",
            "enable": [
                "OrchardCore.Resources",
                "OrchardCore.Menu",
                "OrchardCore.Media",
                "OrchardCore.DynamicCache"
            ]
        },
        {
            "name": "Themes",
            "Site": "MyCustomTheme",
            "Admin": "TheAdmin"
        }
    ]
}
```

## Example 10: Alternates and Shape Overrides

Orchard Core resolves shape templates using naming conventions. Override specific shapes by creating Razor views with matching names.

### Shape Naming Convention

| Canonical alternate | File Name | Description |
|---|---|---|
| `Content` | `Content.cshtml` | Default content item rendering |
| `Content__BlogPost` | `Content-BlogPost.cshtml` | Content type-specific override |
| `Content_Detail__BlogPost` | `Content-BlogPost.Detail.cshtml` | Content type + `Detail` display type |
| `Content_Summary__BlogPost` | `Content-BlogPost.Summary.cshtml` | Content type + `Summary` display type |
| `Content__Alias__about` | `Content-Alias-about.cshtml` | One content item by alias |
| `Widget` | `Widget.cshtml` | Default widget wrapper |
| `Widget__HeroBanner` | `Widget-HeroBanner.cshtml` | Widget content type override |

### Custom Placement via placement.json

Control where parts and fields render within the content shape zones. Keys are shape types:

```json
{
    "TitlePart": [
        {
            "place": "Header:0",
            "contentType": ["BlogPost"],
            "displayType": "Detail"
        }
    ],
    "HtmlBodyPart": [
        {
            "place": "Content:1",
            "contentType": ["BlogPost"],
            "displayType": "Detail"
        },
        {
            "place": "-",
            "contentType": ["BlogPost"],
            "displayType": "Summary"
        }
    ],
    "TaxonomyField": [
        {
            "place": "Meta:2",
            "contentType": ["BlogPost"],
            "differentiator": "BlogPost-Tags"
        }
    ]
}
```

- `Header:0` places the shape in the `Header` zone of the content shape at position `0`.
- Field shapes are targeted with the `{PartName}-{FieldName}` differentiator.
- `"-"` hides the shape entirely.

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
<html lang="{{ Culture.Name }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{{ Model.Title }} - {{ Site.SiteName }}</title>
    {% resources type: "HeadMeta" %}
    {% resources type: "HeadLink" %}
    {% style src: "~/CrestApps.BlogTheme/css/site.css" %}
    {% resources type: "Stylesheet" %}
</head>
<body>
    <header class="site-header">
        {% zone "Navigation" %}
        <div class="container">
            <h1 class="site-title">{{ Site.SiteName }}</h1>
        </div>
    </header>

    <main role="main" class="container">
        <div class="row">
            <div class="col-md-8">
                {% zone "Content" %}
            </div>
            <aside class="col-md-4">
                {% zone "Sidebar" %}
            </aside>
        </div>
    </main>

    <footer class="site-footer">
        {% zone "Footer" %}
        <div class="container">
            <p>&copy; {{ "now" | date: "%Y" }} {{ Site.SiteName }}</p>
        </div>
    </footer>

    {% resources type: "FooterScript" %}
</body>
</html>
```

## Example 2: Content Shape Override

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

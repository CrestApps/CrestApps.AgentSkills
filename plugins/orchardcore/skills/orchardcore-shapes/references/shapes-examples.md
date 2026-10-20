# Orchard Core Shapes Examples

## Example 1: Blog Theme Shape Overrides

Overriding the Content shape for BlogPost content type with Liquid templates. Liquid files use the same naming as Razor files: `-` for `__` and a trailing `.DisplayType`.

### Views/Content-BlogPost.liquid

```liquid
<article class="blog-post">
    <header class="post-header">
        <h1>{{ Model.ContentItem.DisplayText }}</h1>
        <div class="post-meta">
            <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
            </time>
            <span class="author">by {{ Model.ContentItem.Author }}</span>
        </div>
    </header>

    {% if Model.Content %}
        <div class="post-body">
            {{ Model.Content | shape_render }}
        </div>
    {% endif %}

    {% if Model.Footer %}
        <footer class="post-footer">
            {{ Model.Footer | shape_render }}
        </footer>
    {% endif %}
</article>
```

### Views/Content-BlogPost.Summary.liquid (alternate `Content_Summary__BlogPost`)

```liquid
<article class="blog-summary">
    <h3>
        <a href="{{ Model.ContentItem | display_url }}">
            {{ Model.ContentItem.DisplayText }}
        </a>
    </h3>
    <div class="post-meta">
        <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
            {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
        </time>
    </div>
    {% if Model.Content %}
        {{ Model.Content | shape_render }}
    {% endif %}
</article>
```

## Example 2: Custom Widget Wrapper with Liquid

### Views/Widget.liquid

```liquid
<div class="widget widget-{{ Model.ContentItem.ContentType | downcase }}">
    {% if Model.Header %}
        <div class="widget-header">
            {{ Model.Header | shape_render }}
        </div>
    {% endif %}
    <div class="widget-body">
        {{ Model.Content | shape_render }}
    </div>
</div>
```

### Views/Widget-HtmlWidget.liquid

```liquid
<div class="html-widget">
    {{ Model.Content | shape_render }}
</div>
```

## Example 3: Custom Shape Table Provider for Product Alternates

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;

public sealed class ProductShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Content")
            .OnDisplaying(context =>
            {
                // Content shapes expose the ContentItem and Stereotype properties.
                if (!context.Shape.TryGetProperty("ContentItem", out ContentItem contentItem) ||
                    contentItem.ContentType != "Product")
                {
                    return;
                }

                // Category is a text field on the Product part: Product.Category.Text.
                string category = contentItem.Content.Product?.Category?.Text?.ToString();

                if (!string.IsNullOrEmpty(category))
                {
                    // Matches Views/Content-Product-{category}.cshtml or .liquid.
                    context.Shape.Metadata.Alternates.Add($"Content__Product__{category}");
                }
            });

        // Add a wrapper to all Widget shapes
        builder.Describe("Widget")
            .OnDisplaying(context =>
            {
                context.Shape.Metadata.Wrappers.Add("Widget_CardWrapper");
            });

        return ValueTask.CompletedTask;
    }
}
```

### Views/Widget.CardWrapper.liquid

A wrapper renders the already rendered output from `Model.Metadata.ChildContent`. Rendering `Model` again would recurse.

```liquid
<div class="card">
    <div class="card-body">
        {{ Model.Metadata.ChildContent }}
    </div>
</div>
```

### Registration

```csharp
using OrchardCore.DisplayManagement;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<ProductShapeTableProvider>();
    }
}
```

## Example 4: Creating Shapes from Code in a Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

public sealed class DashboardController : Controller
{
    private readonly IShapeFactory _shapeFactory;
    private readonly IShapeHtmlRenderer _shapeHtmlRenderer;

    public DashboardController(
        IShapeFactory shapeFactory,
        IShapeHtmlRenderer shapeHtmlRenderer)
    {
        _shapeFactory = shapeFactory;
        _shapeHtmlRenderer = shapeHtmlRenderer;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildDashboardAsync());
    }

    // Returns the rendered markup as data, e.g. for client-side refreshes.
    public async Task<IActionResult> Refresh()
    {
        var html = await _shapeHtmlRenderer.RenderAsync(await BuildDashboardAsync(), HttpContext.RequestAborted);

        return Json(new { html });
    }

    private async Task<IShape> BuildDashboardAsync()
    {
        // Create shapes programmatically
        var statsShape = await _shapeFactory.CreateAsync("Dashboard_Stats", Arguments.From(new
        {
            TotalPosts = 42,
            TotalComments = 128,
            TotalUsers = 15
        }));

        var recentPostsShape = await _shapeFactory.CreateAsync("Dashboard_RecentPosts", Arguments.From(new
        {
            Count = 5
        }));

        var model = await _shapeFactory.CreateAsync("Dashboard", Arguments.From(new
        {
            Stats = statsShape,
            RecentPosts = recentPostsShape
        }));

        return model;
    }
}
```

### Views/Dashboard/Index.cshtml

```cshtml
@await DisplayAsync(Model)
```

### Views/Dashboard.liquid

```liquid
<div class="dashboard">
    <div class="row">
        <div class="col-md-4">
            {{ Model.Stats | shape_render }}
        </div>
        <div class="col-md-8">
            {{ Model.RecentPosts | shape_render }}
        </div>
    </div>
</div>
```

### Views/Dashboard_Stats.liquid

```liquid
<div class="stats-card">
    <h3>Site Statistics</h3>
    <ul>
        <li>Posts: {{ Model.TotalPosts }}</li>
        <li>Comments: {{ Model.TotalComments }}</li>
        <li>Users: {{ Model.TotalUsers }}</li>
    </ul>
</div>
```

## Example 5: Field Shape Templates

### Views/TextField.liquid

```liquid
{% if Model.Field.Text and Model.Field.Text != "" %}
    <p class="text-field text-field-{{ Model.PartFieldDefinition.Name | downcase }}">
        {{ Model.Field.Text }}
    </p>
{% endif %}
```

## Example 6: Display Driver Editor Wrapper

When `IDisplayManager<T>.BuildEditorAsync()` renders an editor for a model, Orchard resolves the root `<Type>_Edit` shape first. If the driver only returns nested editor shapes, add a root wrapper template that renders the content zone.

### Driver

```csharp
public sealed class TableauExportPipelineDisplayDriver : DisplayDriver<TableauExportPipeline>
{
    public override IDisplayResult Edit(TableauExportPipeline pipeline, BuildEditorContext context)
    {
        return Initialize<EditTableauExportPipelineViewModel>("TableauExportPipelineFields_Edit", model =>
        {
            model.Name = pipeline.Name;
        })
        .Location("Content:1");
    }
}
```

### Views/TableauExportPipeline_Edit.cshtml

```cshtml
@await DisplayAsync(Model.Content)
```

### Views/TableauExportPipelineFields.Edit.cshtml

```cshtml
@model EditTableauExportPipelineViewModel

<div class="ocat-wrapper" asp-validation-class-for="Name">
    <label asp-for="Name" class="ocat-label"></label>
    <div class="ocat-end">
        <input asp-for="Name" class="form-control" />
    </div>
</div>
```

If the root wrapper is missing, Orchard throws an `InvalidOperationException` for the missing `<Type>_Edit` shape.

### Views/MediaField.liquid

```liquid
{% if Model.Field.Paths and Model.Field.Paths.size > 0 %}
    {% for path in Model.Field.Paths %}
        <img src="{{ path | asset_url }}"
             alt="{{ Model.PartFieldDefinition.DisplayName }}"
             class="img-fluid" />
    {% endfor %}
{% endif %}
```

### Views/ContentPickerField.liquid

```liquid
{% if Model.Field.ContentItemIds and Model.Field.ContentItemIds.size > 0 %}
    <ul class="content-picker-list">
        {% assign items = Model.Field.ContentItemIds | content_item_id %}
        {% for item in items %}
            <li>
                <a href="{{ item | display_url }}">{{ item.DisplayText }}</a>
            </li>
        {% endfor %}
    </ul>
{% endif %}
```

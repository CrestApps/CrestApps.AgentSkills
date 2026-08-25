# Content Items Admin List Filters Examples

## Example 1: End-to-end `product` filter

A single-value `product:` token that filters content items on a custom `ProductIndex.SerialNumber`, documented with a card in the **Available Filters** dialog.

### Index

```csharp
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace MyModule.Indexes;

public sealed class ProductIndex : MapIndex
{
    public string ContentItemId { get; set; }
    public string SerialNumber { get; set; }
}

public sealed class ProductIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<ProductIndex>()
            .Map(contentItem =>
            {
                var part = contentItem.As<ProductPart>();

                if (part is null || string.IsNullOrEmpty(part.SerialNumber))
                {
                    return null;
                }

                return new ProductIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    SerialNumber = part.SerialNumber,
                };
            });
    }
}
```

### Filter provider

```csharp
using MyModule.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql.Filters.Query;

namespace MyModule.Services;

public sealed class ProductContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("product", builder => builder
                .OneCondition((val, query) =>
                    query.With<ProductIndex>(i =>
                        i.SerialNumber != null && i.SerialNumber.Contains(val))));
    }
}
```

### Display driver (filter card)

```csharp
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace MyModule.Drivers;

public sealed class ProductContentsAdminListDisplayDriver : DisplayDriver<ContentOptionsViewModel>
{
    public override IDisplayResult Display(ContentOptionsViewModel model, BuildDisplayContext context)
    {
        return View("ContentsAdminFilters_Thumbnail__Product", model)
            .Location("Thumbnail", "Content:35");
    }
}
```

### View — `Views/Items/ContentsAdminFilters-Product.Thumbnail.cshtml`

```html
@model ShapeViewModel<ContentOptionsViewModel>
@{
    var term = Model.Value.FilterResult.FirstOrDefault(x => x.TermName == "product");
}

<div class="d-flex justify-content-between align-items-center gap-2">
    <h6 class="card-title fw-semibold mb-0">@T["Product"]</h6>
    <span class="text-primary text-nowrap">
        <i class="fa-solid fa-sm fa-minus" title="@T["Accepts a single value"]" aria-hidden="true"></i>
    </span>
</div>
<div class="mt-1"><code class="small text-nowrap">@(term?.ToString() ?? "product:...")</code></div>
<p class="card-text small text-body-secondary mt-1 mb-0">@T["Filters on a product serial number."]</p>
```

### Startup

```csharp
using MyModule.Drivers;
using MyModule.Indexes;
using MyModule.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Index that backs the filter.
        services.AddSingleton<IIndexProvider, ProductIndexProvider>();

        // Filter logic: adds the `product:` token to the admin search box.
        services.AddScoped<IContentsAdminListFilterProvider, ProductContentsAdminListFilterProvider>();

        // Filter card: documents the token in the Available Filters dialog.
        services.AddDisplayDriver<ContentOptionsViewModel, ProductContentsAdminListDisplayDriver>();
    }
}
```

## Example 2: Multiple-value term with logical operators

A `producttext:` token that searches both the content display text and the product serial number and supports `AND`, `OR`, `NOT`, and groups.

```csharp
using MyModule.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents.Services;
using YesSql.Filters.Query;

namespace MyModule.Services;

public sealed class ProductTextContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("producttext", builder => builder
                .ManyCondition(
                    (val, query) => query.Any(
                        q => q.With<ContentItemIndex>(i =>
                            i.DisplayText != null && i.DisplayText.Contains(val)),
                        q => q.With<ProductIndex>(i =>
                            i.SerialNumber != null && i.SerialNumber.Contains(val))),
                    (val, query) => query.All(
                        q => q.With<ContentItemIndex>(i =>
                            i.DisplayText == null || i.DisplayText.NotContains(val)),
                        q => q.With<ProductIndex>(i =>
                            i.SerialNumber == null || i.SerialNumber.NotContains(val)))));
    }
}
```

The matching card uses the `fa-bars` icon to signal **Multiple** capability:

```html
<span class="text-primary text-nowrap">
    <i class="fa-solid fa-sm fa-bars" title="@T["Supports logical operators and groups"]" aria-hidden="true"></i>
</span>
```

## Example 3: Make a term a default term

Allow the `product` term to be entered without its prefix (bare text is routed to it). Register it in `ContentsAdminListFilterOptions` and mark the card with the `fa-check` (**Default**) icon.

```csharp
services.Configure<ContentsAdminListFilterOptions>(options =>
{
    options.DefaultTermNames.Add("product");
});
```

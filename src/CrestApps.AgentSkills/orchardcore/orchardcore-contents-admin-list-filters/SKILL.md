---
name: orchardcore-contents-admin-list-filters
description: Skill for adding custom filters to the Orchard Core content items admin list. Covers implementing IContentsAdminListFilterProvider to add searchable terms to the content admin search box, wiring named and default terms with OneCondition/ManyCondition against YesSql indexes, registering the provider in Startup, and documenting the new filter in the Available Filters dialog with a DisplayDriver<ContentOptionsViewModel> Thumbnail card. Use this skill when requests mention custom content admin list filters, IContentsAdminListFilterProvider, QueryEngineBuilder<ContentItem>, WithNamedTerm, WithDefaultTerm, ContentsAdminListFilterOptions, the content items Filters dropdown or Filter syntax dialog, ContentOptionsViewModel filter cards, ContentsAdminFilters Thumbnail views, or closely related Orchard Core content admin list search work. Strong matches include OrchardCore.Contents, IContentsAdminListFilterProvider, ContentOptionsViewModel, ContentsAdminListFilterOptions.DefaultTermNames, and ContentsAdminFilters-*.Thumbnail.cshtml.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Content Items Admin List Filters

## Add a custom filter to the content items admin list

You are an Orchard Core expert. The content items admin list (**Content → Content Items**) has a search box that parses tokens such as `text:`, `type:`, `status:`, and `sort:`. Add your own token by implementing a filter provider, then document it for end users by adding a card to the **Available Filters** dialog. These are two independent extension points: the filter **logic** and the filter **card**. A filter still works without a card, but users will not discover it; a card without a matching provider documents a filter that does nothing.

### Guidelines

- Enable `OrchardCore.Contents`. The content items admin list ships with this feature.
- Implement `IContentsAdminListFilterProvider` to add searchable terms; register it with `services.AddScoped<IContentsAdminListFilterProvider, T>()`.
- Build terms against `QueryEngineBuilder<ContentItem>`. Filter against a YesSql index (`ContentItemIndex` or a custom index), not against materialized content items.
- Use `WithNamedTerm("token", ...)` for a token the user types as `token:value`. Use `WithDefaultTerm("token", ...)` for the term applied to bare text with no prefix; register at most one default term.
- Use `OneCondition` for a term that accepts a single value (the `fa-minus`/**Single** icon). Use `ManyCondition` for a term that supports the `AND`, `OR`, and `NOT` operators and groups (the `fa-bars`/**Multiple** icon), and supply both the matching and the negated predicate.
- To let an existing named term also be entered without its prefix, add it through `ContentsAdminListFilterOptions.DefaultTermNames`. This documents the `fa-check`/**Default** capability.
- Document the filter by implementing `DisplayDriver<ContentOptionsViewModel>` and returning a `View(...)` in the `Content` zone of the `Thumbnail` display type; register it with `services.AddDisplayDriver<ContentOptionsViewModel, T>()`.
- The shape type `ContentsAdminFilters_Thumbnail__<Name>` resolves to `Views/Items/ContentsAdminFilters-<Name>.Thumbnail.cshtml`. The card wrapper and grid are supplied for you; the view only renders the card's inner content.
- Use the same capability icons the built-in filters use so the shared legend at the bottom of the dialog stays accurate: `fa-check` (**Default**), `fa-minus` (**Single**), `fa-bars` (**Multiple**).
- The same pattern documents the [users admin list](../orchardcore-users-admin-list-filters/SKILL.md) (`UserIndexOptions`) and the audit trail admin list (`AuditTrailIndexOptions`); only the model type differs.

## Register the filter logic

Add named or default terms to the `QueryEngineBuilder<ContentItem>`. This example adds a single-value `product` term that filters on a custom `ProductIndex`.

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql.Filters.Query;

namespace MyModule;

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

For a term that supports logical operators and groups, use `ManyCondition` and provide both the matching and the negated predicate:

```csharp
builder
    .WithNamedTerm("producttext", builder => builder
        .ManyCondition(
            (val, query) => query.With<ProductIndex>(i =>
                i.SerialNumber != null && i.SerialNumber.Contains(val)),
            (val, query) => query.With<ProductIndex>(i =>
                i.SerialNumber == null || i.SerialNumber.NotContains(val))));
```

Register the provider in the module's `Startup`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Contents.Services;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentsAdminListFilterProvider, ProductContentsAdminListFilterProvider>();
    }
}
```

To let the `product` term also be entered as bare text (no `product:` prefix), register it as a default term:

```csharp
services.Configure<ContentsAdminListFilterOptions>(options =>
{
    options.DefaultTermNames.Add("product");
});
```

## Register the filter card

Implement a display driver for `ContentOptionsViewModel`. The position after `Content:` controls where the card appears in the grid.

```csharp
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace MyModule;

public sealed class ProductContentsAdminListDisplayDriver : DisplayDriver<ContentOptionsViewModel>
{
    public override IDisplayResult Display(ContentOptionsViewModel model, BuildDisplayContext context)
    {
        // First argument is the shape type; second is the display type and position.
        return View("ContentsAdminFilters_Thumbnail__Product", model)
            .Location("Thumbnail", "Content:35");
    }
}
```

Register the driver in `Startup`:

```csharp
services.AddDisplayDriver<ContentOptionsViewModel, ProductContentsAdminListDisplayDriver>();
```

## The card template

The shape type `ContentsAdminFilters_Thumbnail__Product` resolves to `Views/Items/ContentsAdminFilters-Product.Thumbnail.cshtml`. Each card is wrapped in a Bootstrap card and laid out in the responsive grid automatically, so the view supplies only the inner content: a title with its capability icons, the filter token, and a short description.

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

Choose the icon that matches the term's capability, and match the `title` text to the shared legend:

| Icon | Font Awesome class | Meaning | Term method |
|------|--------------------|---------|-------------|
| ✓ | `fa-check` | **Default** — may be entered with or without the term name | `WithDefaultTerm` / `DefaultTermNames` |
| − | `fa-minus` | **Single** — accepts a single value | `OneCondition` |
| ☰ | `fa-bars` | **Multiple** — supports `AND`, `OR`, `NOT`, and groups | `ManyCondition` |

## Troubleshooting

- If typing `product:...` returns unfiltered results, confirm the `IContentsAdminListFilterProvider` is registered with `AddScoped` and the term name matches the token exactly (case-insensitive).
- If the term filters but no card appears in the **Available Filters** dialog, register the `DisplayDriver<ContentOptionsViewModel>` with `AddDisplayDriver` and verify the view path is `Views/Items/ContentsAdminFilters-<Name>.Thumbnail.cshtml`.
- If the card renders but the token line is empty, ensure the `TermName` passed to `FilterResult.FirstOrDefault` matches the token used in `WithNamedTerm`/`WithDefaultTerm`.
- If bare text is not matched by your term, register it in `ContentsAdminListFilterOptions.DefaultTermNames` or define it with `WithDefaultTerm`.
- Filter against a persisted YesSql index. Predicates over in-memory content properties are not translated to SQL and will not filter.

See [references/contents-admin-list-filters-examples.md](references/contents-admin-list-filters-examples.md) for a complete end-to-end example.

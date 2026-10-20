---
name: orchardcore-contents-admin-list-filters
description: Skill for adding custom filters to the Orchard Core content items admin list. Covers IContentsAdminListFilterProvider search terms, ContentsAdminListFilterOptions default terms per content type or stereotype and exact match, scoping the list to several content types or stereotypes with the contentTypeId and stereotype query string values, and documenting filters in the redesigned Available Filters dialog with a DisplayDriver<ContentOptionsViewModel> Thumbnail card. Use this skill when requests mention custom content admin list filters, IContentsAdminListFilterProvider, QueryEngineBuilder<ContentItem>, WithNamedTerm, WithDefaultTerm, ContentsAdminListFilterOptions, DefaultTermNames, UseExactMatch, the Filter syntax dialog, ContentOptionsViewModel filter cards, ContentsAdminFilters Thumbnail views, upgrading filter cards to Orchard Core 4.0, or closely related content admin list search work. Strong matches include OrchardCore.Contents, ContentsAdminList, and ContentsAdminFilters-*.Thumbnail.cshtml.
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
- Bare text is routed to the default term, `text` (`ContentsAdminListFilterOptions.DefaultTermName`). To route bare text to another term when the list is scoped to a content type or stereotype, map it in `ContentsAdminListFilterOptions.DefaultTermNames` (a dictionary keyed by content type or stereotype name, valued by term name). Mark that term's card with the `fa-check`/**Default** icon.
- Set `ContentsAdminListFilterOptions.UseExactMatch = true` to quote bare search text as a single exact-match term, unless the text uses `AND` or `OR`.
- Document the filter by implementing `DisplayDriver<ContentOptionsViewModel>` and returning a `View(...)` in the `Content` zone of the `Thumbnail` display type; register it with `services.AddDisplayDriver<ContentOptionsViewModel, T>()`.
- The shape type `ContentsAdminFilters_Thumbnail__<Name>` resolves to `Views/Items/ContentsAdminFilters-<Name>.Thumbnail.cshtml`. The card wrapper and grid are supplied for you; the view only renders the card's inner content.
- Use the same capability icons the built-in filters use so the shared legend at the bottom of the dialog stays accurate: `fa-check` (**Default**), `fa-minus` (**Single**), `fa-bars` (**Multiple**).
- The same pattern documents the users admin list (`UserIndexOptions`, see `orchardcore-users-admin-list-filters`) and the audit trail admin list (`AuditTrailIndexOptions`, see `orchardcore-audit-trail`); only the model type differs.
- In Orchard Core 4.0 the content items list renders through the shared `AdminList` shape under the list name `Contents` (`ContentsAdminList.Name`), so it follows the site's List/Grid layout. To add columns or change how rows render, see `orchardcore-admin-lists`; filters and filter cards are unaffected by the layout.

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

To route bare text (no prefix) to the `product` term while the list is scoped to the `Product` content type (or to a stereotype), map it in `DefaultTermNames`. Other lists keep using the `text` term:

```csharp
using OrchardCore.Contents;

services.Configure<ContentsAdminListFilterOptions>(options =>
{
    // Key: content type or stereotype name. Value: term name used for bare text.
    options.DefaultTermNames.Add("Product", "product");

    // Optional: treat bare text as one quoted, exact-match term.
    options.UseExactMatch = true;
});
```

## Scope the list to content types or stereotypes

The `ListContentItems` action (`/Admin/Contents/ContentItems/{contentTypeId?}`) accepts several content types or stereotypes, as repeated or comma-separated query string values. Use these URLs in admin menu links:

```text
/Admin/Contents/ContentItems/Article
/Admin/Contents/ContentItems?contentTypeId=Article&contentTypeId=BlogPost
/Admin/Contents/ContentItems?contentTypeId=Article,BlogPost
/Admin/Contents/ContentItems?stereotype=Widget,Page
```

A list scoped to a single content type or stereotype uses its `DefaultTermNames` entry for bare text. The content types and stereotypes the list is filtered by are passed to admin list column providers under `ContentsAdminList.ContentTypesKey` and `ContentsAdminList.StereotypesKey`.

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

If you also override the dialog's grid container (`ContentOptionsViewModel.Thumbnail.cshtml`) or the card wrapper (`ContentsAdminFilters-Card.Thumbnail.cshtml`), match the built-in markup, which emits `row row-cols-*` for the grid and `col > card h-100` for each card.

## Upgrading from 3.x

- The **Available Filters** dialog is now a `modal-xl` grid of cards, and a Thumbnail template renders only the card's inner content. Old cards still render but look out of place.
- Before (3.x):

```html
<div class="ocat-wrapper">
    <h4 class="card-title ocat-label">@T["Product"]</h4>
    <div class="ocat-end">
        <pre>@(term?.ToString() ?? "product:...")</pre>
    </div>
</div>
<p>@T["Filters on a product serial number."]</p>
<div class="d-block text-end">
    <span><i class="fa-solid fa-sm fa-minus text-primary" aria-hidden="true"></i></span>
</div>
```

- After (4.0): title and capability icons on the first line, the token in a `<code>` line, and the description last, as shown in [The card template](#the-card-template).
- `contentTypeId` and `stereotype` now accept several values; single-value routes and query strings work as before.
- A theme or module that overrides the content items list page must render the `List` context of `ListContentsViewModel` with the `<admin-list>` tag helper (see `orchardcore-admin-lists`). Row templates such as `Content-BlogPost.SummaryAdmin.cshtml` only apply in the `List` layout.

## Troubleshooting

- If typing `product:...` returns unfiltered results, confirm the `IContentsAdminListFilterProvider` is registered with `AddScoped` and the term name matches the token exactly (case-insensitive).
- If the term filters but no card appears in the **Available Filters** dialog, register the `DisplayDriver<ContentOptionsViewModel>` with `AddDisplayDriver` and verify the view path is `Views/Items/ContentsAdminFilters-<Name>.Thumbnail.cshtml`.
- If the card renders but the token line is empty, ensure the `TermName` passed to `FilterResult.FirstOrDefault` matches the token used in `WithNamedTerm`/`WithDefaultTerm`.
- If bare text is not matched by your term, map the content type or stereotype to it in `ContentsAdminListFilterOptions.DefaultTermNames` and open the list scoped to that type, or replace the default `text` term with `WithDefaultTerm`.
- Filter against a persisted YesSql index. Predicates over in-memory content properties are not translated to SQL and will not filter.

See [references/contents-admin-list-filters-examples.md](references/contents-admin-list-filters-examples.md) for a complete end-to-end example.

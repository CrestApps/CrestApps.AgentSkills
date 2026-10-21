# Product Listing

Feature `CrestApps.OrchardCore.Products.Listing`. Lists products sold online and products that are only listed, with filters, sorts, keyword search, paging, grid and list layouts, and product pages.

## Field listing settings

On a field of a part attached to a type with `ProductPart`:

| Setting | Effect |
| --- | --- |
| Filterable | Visitors filter by the field |
| Sortable | Numbers and dates by value; text and taxonomy by visible name |
| Searchable | Keyword search matches the value |
| Show on product cards | Short chip on the card |
| Show in specifications | Specification table on list rows and the product page |
| Listing label, Number format, Unit | Display |
| Swatch colors | `Midnight = #191970` per line, for text and taxonomy fields |
| Color field on terms | A text field on terms that holds each term's color |

Supported fields: text, multi-text, numeric, boolean, date, date and time, time, link, content picker, user picker, taxonomy; HTML and Markdown are searchable. Dates index as `yyyyMMdd` numbers, times as minutes since midnight. A taxonomy field can expose one tree level as its own filter (Make, then Model). Terms appear in URLs by slug from `ISlugService`. Price, content type, purchasable and featured are always available; variant options add `Option.{Name}` attributes with URL key `option-{name}`.

## The listing item

The `ProductListing` editor sets: listed types (every type or chosen ones), **Show filters**, filters (kind *Checkboxes*, *Radio buttons*, *Range* or *Toggle*; style; ready-made ranges like `-25, 25-75, 75+`; *Shown after* another filter), **Let visitors change the sort**, sorts and default sort, layout, image shape, page sizes, keyword search, **Paging** (`ProductListingPart.Paging`: numbered, Load more, infinite scroll) and **Search index** (`ProductListingPart.IndexProfileName`: database or a `ProductSearch` index).

Every listing offers **Recommended** (by `ProductPart.Position`) and **Featured first**, plus a *Featured* toggle filter. Ties go to featured products, then newest.

## Product pages

Turn on **Show product details on the product page** (`ProductPageSettings.ShowDetails`) on the type's Product part: breadcrumb, gallery from the first media field with images, price, highlights, actions and the specification table. The inquiry button's label and link (with `{id}` and `{title}`) are set on the type.

Breadcrumb trails are named `ProductListing` and `Product`, giving alternates such as `BreadcrumbItem-Product-Title`.

## Theming shapes

| Shape | Alternates |
| --- | --- |
| `ProductListingPart` | Content part alternates |
| `ProductListingBreadcrumb`, `ProductListingFilters`, `ProductListingSearch`, `ProductListingToolbar`, `ProductListingActiveFilters`, `ProductListingPager` | `-{ContentType}` |
| `ProductFacet` | `ProductFacet-{Kind}` (`Checkbox`, `Radio`, `Range`, `Toggle`), `ProductFacet-{Style}`, `ProductFacet-{Key}` |
| `ProductListingResults` | `-Grid`, `-List` |
| `ProductCard` | `ProductCard-{ContentType}`, `ProductCard-List`, `ProductCard-List-{ContentType}` |
| `ProductPrice`, `ProductBreadcrumb`, `ProductSummary`, `ProductSpecifications` | |
| `ProductGallery`, `ProductActions` | `-{ContentType}` |
| `Content` in display type `ProductAction` | `Content-{ContentType}.ProductAction` |

Listing shape models expose `Listing`; product page shapes expose `Product`. View models are registered for Liquid:

```liquid
<article class="product-card card h-100">
  <div class="card-body">
    <h3 class="h6"><a href="{{ Model.Url }}" class="stretched-link">{{ Model.Title }}</a></h3>
    <p class="small text-muted">{{ Model.Chips | join: " · " }}</p>
    {{ Model.Price | shape_render }}
  </div>
</article>
```

CSS custom properties restyle without templates: `--product-accent`, `--product-sale-color`, `--product-success-color`, `--product-border-color`, `--product-muted-color`, `--product-surface`, `--product-surface-alt`, `--product-radius`.

## Adding a filter style

```csharp
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ProductFacetStyleOptions>(options =>
            options.AddStyle("Stars", style =>
            {
                style.DisplayName = S["Star rating"];
                style.Kinds.Add(ProductFacetKind.Checkbox);
                style.Kinds.Add(ProductFacetKind.Radio);
            }));
    }
}
```

Provide `ProductFacet-Stars.cshtml` (or Liquid `ProductFacet-Stars`) taking a `ProductFacetViewModel`. A filter whose style is no longer registered draws as a list.

## Search index and search servers

- One record per published product in the tenant database, refreshed on publish, unpublish, delete and import; rebuilt in the background when product types, listing settings or taxonomy terms change. Results are cached per tenant and filter set (up to five minutes stale on another node).
- Records carry the product's culture; a listing shows the request culture plus non-localized products.
- Search modules register a `ProductSearch` index type; create the index under **Search > Indexes** and pick it on the listing. `ProductSearchIndexer` raises `IProductSearchIndexHandler`, and the same records are written to every `ProductSearch` index profile.
- Shared field names come from `ProductSearchIndexFields` (`a_{attribute}_{hash}` with `_v`, `_f`, `_n`, `_sn`, `_nx`, `_st` suffixes); the shared query plan is `ProductSearchIndexQuery`.
- `ProductSearchRouter` falls back to the database when the named index or provider is missing or the server fails.
- Keyword search on a server matches word starts and ranks by relevance; the database matches any part of a word.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products.Listing",
        "CrestApps.OrchardCore.Products.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

See `orchardcore-elasticsearch`, `orchardcore-lucene` and `orchardcore-azure-ai-search` for the server connection.

## Search engines

Enable `OrchardCore.Seo` and attach the SEO part to the `ProductListing` type and product types. Filtered or keyword states of a listing ask for `noindex, follow` and canonicalize to the listing URL; page, sort, page size and layout don't count as filters.

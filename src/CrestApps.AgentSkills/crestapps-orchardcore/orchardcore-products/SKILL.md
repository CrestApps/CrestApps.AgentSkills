---
name: orchardcore-products
description: Skill for the CrestApps Orchard Core Products module and its search modules. Covers ProductPart and ProductPartSettings (price, currency, SKU, sales mode, price display), the Currencies catalog and recipe step, ProductPricePart for one-time or recurring prices, ProductVariantsPart, IProductSnapshotResolver, IPriceResolver, IBuyerPriceProvider, IProductAvailabilityProvider, merchandising (sales, quantity rules, discounts, add-ons), Product Listing pages with filters, sorts, ProductFacetStyleOptions and shapes, IProductSearchDocumentContributor, IProductListingEntryProvider, IProductSearchIndexRefresher, Product Recommendations, Comparison, Grouped Products, Bundles, Product Feeds, the Catalog API, and product search on Lucene, Elasticsearch and Azure AI Search (CrestApps.OrchardCore.Products.Lucene, Products.Elasticsearch, Products.AzureAI). Use it when requests mention Orchard Core products, a product catalog, product listings, variants, prices, currencies or product search.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Products

Products (new in CrestApps.OrchardCore 3.0) turns any content type into a product by attaching `ProductPart`. It owns prices, currencies, variants, the sellable snapshot, the price seam and the front-end product listing. It never calculates tax or takes payment.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Commerce (`orchardcore-commerce`) | Required shell |
| Carts, Orders (`orchardcore-carts`, `orchardcore-orders`) | Sell products with one-time prices through a cart |
| Subscriptions (`orchardcore-subscriptions`) | Sells products with recurring prices; plan types carry the Product part |
| Inventory (`orchardcore-inventory`) | Implements `IProductAvailabilityProvider` and the In stock filter |
| Markets (`orchardcore-markets`) | Implements `IShopperCurrency` and `IProductSaleRestriction` |
| Shipping (`orchardcore-shipping`) | Adds weights and classes through `ShippingPart` |
| Taxation (`orchardcore-taxation`) | Classifies products through `TaxationPart`; `ProductTaxableItemProvider` |
| Companies (`orchardcore-companies`) | Implements `IBuyerPriceProvider` for price lists |

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Products` | Products | Commerce |
| `CrestApps.OrchardCore.Products.Listing` | Product Listing | Products, `OrchardCore.Contents`, `OrchardCore.Title`, `OrchardCore.Autoroute` |
| `CrestApps.OrchardCore.Products.Recommendations` | Product Recommendations | Product Listing, Resources |
| `CrestApps.OrchardCore.Products.Comparison` | Product Comparison | Product Listing |
| `CrestApps.OrchardCore.Products.Grouped` | Grouped Products | Product Listing, Resources |
| `CrestApps.OrchardCore.Products.Bundles` | Product Bundles | Product Listing, Resources |
| `CrestApps.OrchardCore.Products.Feeds` | Product Feeds | Products |
| `CrestApps.OrchardCore.Products.Api` | Catalog API | Product Listing |
| `CrestApps.OrchardCore.Products.Lucene` | Product Search (Lucene) | Product Listing, `OrchardCore.Lucene` |
| `CrestApps.OrchardCore.Products.Elasticsearch` | Product Search (Elasticsearch) | Product Listing, `OrchardCore.Elasticsearch` |
| `CrestApps.OrchardCore.Products.AzureAI` | Product Search (Azure AI Search) | Product Listing, `OrchardCore.Indexing`, `OrchardCore.AzureAI` |

### Guidelines

- Install `CrestApps.OrchardCore.Products` (and any search module package) in the web/startup project.
- Resolve prices through `IPriceResolver` and sellable data through `IProductSnapshotResolver`; never read `ProductPart.Price` directly to charge.
- Prices are `decimal` and always travel with their currency (`PriceResult`).
- A product owns its currency; only Markets converts into a shopper's currency.
- Sales mode and price rules are enforced on the server: a snapshot is refused for a product that can't be bought.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

## ProductPart

| Property | Notes |
| --- | --- |
| `Price` (`decimal?`) | In the product's own currency; required when it can be bought or its price shown |
| `Currency` | ISO 4217; falls back to `ProductPartSettings.DefaultCurrency` |
| `Sku`, `CompareAtPrice` | Optional; compare-at is shown struck through when higher |
| `IsPurchasable` | Used when the type's sales mode is `PerItem` |
| `PriceDisplay` | Used when the type allows a per-item override |
| `Featured`, `Position` | Merchandising order for every listing |
| `SalePrice`, `SaleStartsUtc`, `SaleEndsUtc` | Scheduled sale |
| `MinQuantity`, `MaxQuantity`, `QuantityStep`, `PriceTiers`, `RoleDiscounts` | Quantity rules and volume or role discounts |
| `AddOns` | `ProductAddOn` text or checkbox extras priced per unit, per person or as a percent |
| `ExternalUrl`, `ExternalButtonText` | External product; can't be bought here |

`ProductPartSettings`: `Type` (`ProductType`: `Undefined`, `Good`, `Service`, `Digital`), `DefaultCurrency`, `SalesMode` (`Purchasable`, `ListingOnly`, `PerItem`), `PriceDisplay` (`Show`, `OnRequest`, `Hidden`), `OnRequestLabel`, `AllowPriceDisplayOverride`. `ProductPart` and its settings live in `CrestApps.OrchardCore.Products.Core` (namespace `CrestApps.OrchardCore.Products.Core.Models`).

```csharp
internal sealed class ProductTypeMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductTypeMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Mug", type => type
            .Creatable()
            .Listable()
            .WithPart("TitlePart")
            .WithPart("ProductPart", part => part
                .WithSettings(new ProductPartSettings
                {
                    Type = ProductType.Good,
                    DefaultCurrency = "USD",
                    SalesMode = ProductSalesMode.Purchasable,
                })));

        return 1;
    }
}
```

The same type in a recipe, with a product item:

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Mug",
          "DisplayName": "Mug",
          "Settings": {
            "ContentTypeSettings": { "Creatable": true, "Listable": true, "Draftable": true }
          },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart", "Settings": {} },
            {
              "PartName": "ProductPart",
              "Name": "ProductPart",
              "Settings": {
                "ProductPartSettings": { "Type": "Good", "DefaultCurrency": "USD", "SalesMode": "Purchasable", "PriceDisplay": "Show" },
                "ProductPageSettings": { "ShowDetails": true }
              }
            }
          ]
        }
      ],
      "ContentParts": []
    },
    {
      "name": "content",
      "data": [
        {
          "ContentItemId": "mug00000000000000000000001",
          "ContentType": "Mug",
          "DisplayText": "Contoso mug",
          "Published": true,
          "Latest": true,
          "TitlePart": { "Title": "Contoso mug" },
          "ProductPart": { "Price": 12, "Currency": "USD", "Sku": "MUG-01", "IsPurchasable": true }
        }
      ]
    }
  ]
}
```

With `CrestApps.OrchardCore.Recipes` enabled, JSON Schema is contributed for `ProductPart` and the `Currencies` step (see `orchardcore-crestapps-recipes`).

## Currencies

**Commerce > Currencies** manages the ISO codes editors may pick; new tenants are seeded with USD, EUR and GBP. The catalog feeds `ProductPart.Currency`, `ProductPartSettings.DefaultCurrency` and `SubscriptionSettings.Currency`.

```json
{
  "steps": [
    {
      "name": "Currencies",
      "Currencies": [
        { "Name": "USD", "DisplayName": "US Dollar" },
        { "Name": "CAD", "DisplayName": "Canadian Dollar" }
      ]
    }
  ]
}
```

A `Currencies` deployment step exports the catalog.

## Several prices and variants

- **`ProductPricePart`** (Product prices) holds `Prices` of `ProductPrice`: `PriceId` (stable for life), `Name`, `Currency`, `Amount`, `Kind` (one-time or recurring), `Interval`, `BillingDuration`, `BillingCycleLimit`, `TrialDays`, `StartDayDelay`, `SetupFee`, `AllowCustomAmount` with `MinimumAmount` and `MaximumAmount`, `AllowQuantity` with `MaximumQuantity`, `IsDefault`, `IsActive`, `EffectiveFromUtc`, `EffectiveToUtc`. A recurring price makes a product subscribable. Rules are enforced when the price is resolved; withdrawing a price deactivates it. A product without prices sells at `ProductPart.Price`.
- **`ProductVariantsPart`** sells one product in versions. Option names belong to the type (`ProductVariantsPartSettings.Options`, each optionally filterable); each product holds its values and one `ProductVariant` per combination (`VariantId`, values, optional `Sku`, `Price`, `CompareAtPrice`, `SalePrice`, `ImagePath`, `Enabled`). The snapshot resolver refuses a variant product without an enabled `VariantId`, and a variant's own price wins over the prices list.

## The sellable snapshot and price seams

| Type | Role |
| --- | --- |
| `ISellableProduct` | Immutable snapshot: item id and version, type, SKU, title, unit price, currency, product type, tax codes, variant |
| `IProductSnapshotResolver` | Resolves the snapshot from a `ProductSnapshotContext` (item, currency, quantity, SKU, variant); returns none when it can't be bought |
| `IPriceResolver` | Returns a currency-tagged `PriceResult` (`UnitPrice`, `Currency`, `Quantity`, `Subtotal`); applies variants, sales, volume and role discounts, buyer prices and market conversion |
| `IBuyerPriceProvider` | Gives the current buyer their own unit price |
| `IProductAvailabilityProvider` | Says how many can be bought (Inventory implements it) |
| `IProductSaleRestriction` | Says why a product can't be sold to this shopper (see `orchardcore-markets`) |
| `IShopperCurrency` | Shopper currency and conversion (see `orchardcore-markets`) |

A buyer-specific price, such as a members' discount:

```csharp
public sealed class MemberPriceProvider : IBuyerPriceProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MemberPriceProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default)
    {
        if (_httpContextAccessor.HttpContext?.User?.IsInRole("Member") == true)
        {
            return ValueTask.FromResult<decimal?>(decimal.Round(context.UnitPrice * 0.9m, 2));
        }

        return ValueTask.FromResult<decimal?>(null);
    }
}
```

Register with `services.AddScoped<IBuyerPriceProvider, MemberPriceProvider>();`. Product feeds never ask buyer price providers. Replace `IPriceResolver` only for a whole new pricing engine.

## Product Listing

Create a **Product Listing** content item (`ProductListing` type) from **Commerce > Product Listings**. Fields on a product type gain **Product listing** settings (Filterable, Sortable, Searchable, Show on product cards, Show in specifications, labels, units, swatch colors). Filters are GET query values, so every state is a bookmarkable URL; `q`, `sort`, `view`, `page` and `page-size` are reserved. Each published product has one search record in the tenant database; a listing can instead search a Lucene, Elasticsearch or Azure AI Search index of the `ProductSearch` type, with database fallback.

Extension points:

| To | Use |
| --- | --- |
| Add data to a product's search record | `IProductSearchDocumentContributor.ContributeAsync(contentItem, document)` |
| Offer that data as an automatic filter or sort | `IProductListingEntryProvider` |
| Support another field type | `IProductAttributeValueProvider` (with `FieldValueReader`) |
| Rebuild records when something else changes | `IProductSearchIndexRefresher.RefreshAsync(contentItemIds)` or `RequestRebuildAsync()` |
| Draw a filter differently | Register a style in `ProductFacetStyleOptions` and add a `ProductFacet-{Style}` template |
| Add a button to product cards and pages | A display driver placing a shape at `Location("ProductAction", "Actions")` |
| Add another search server | `IProductIndexSearchService` under the provider name, plus a `ProductSearchIndexProfileHandlerBase` |

```csharp
public sealed class GiftWrapActionDriver : ContentPartDisplayDriver<ProductPart>
{
    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
        => Initialize<GiftWrapActionViewModel>("GiftWrapAction", model => model.ContentItemId = part.ContentItem.ContentItemId)
            .Location("ProductAction", "Actions");
}
```

Listing configuration, theming shapes, search servers and SEO are in [references/product-listing.md](references/product-listing.md). Merchandising features, feeds, import and the Catalog API are in [references/merchandising-and-feeds.md](references/merchandising-and-feeds.md).

## Samples

| Recipe | Creates |
| --- | --- |
| Product listing: online store sample | *Store Product* type, 30 products, a **Shop** listing at `/shop`, carts, orders, customers, stock, shipping zones, reviews and coupons |
| Product listing: real estate sample | *Property* type listed but never sold, a **Homes for sale** listing at `/homes` |
| Product listing: vehicle dealership sample | *Vehicle* type, some sold online, a **Vehicles** listing at `/vehicles` |

The samples need a Bootstrap 5 front-end theme such as TheTheme.

## Permissions

Products use Orchard Core content permissions. `ManageProductFeeds` (Product Feeds) opens **Settings > Commerce > Product feeds**.

## Upgrading within the 3.0 line

- `IProductPositionProvider` was removed; use `ProductPart.Featured` and `ProductPart.Position`, or set `ProductSearchDocument.Position` from a contributor.
- The built-in `/ServicePlans` page was removed; plans list through a Product Listing.
- Money is `decimal` everywhere (`ProductPart.Price`), and `ProductPart` moved to `CrestApps.OrchardCore.Products.Core` (stored content is unaffected).
- The product picker search endpoint (`Admin/api/crestapps/products/search`) moved to the Product Listing feature.

# Merchandising, Feeds, Import and the Catalog API

## Merchandising on ProductPart

| Capability | Members | Behavior |
| --- | --- | --- |
| Scheduled sales | `ProductPart.SalePrice`, `SaleStartsUtc`, `SaleEndsUtc`, `ProductVariant.SalePrice` | `ProductSale.GetPrice` returns the price in force; during a sale the regular price becomes the compare-at price. `ProductSaleIndex` and a background task refresh search records within a minute. |
| Quantity rules | `MinQuantity`, `MaxQuantity`, `QuantityStep` | The cart refuses quantities that break them (`ProductVolumePricing.Check`) |
| Volume and role discounts | `PriceTiers` (`ProductPriceTier`: `MinQuantity`, `PercentOff`), `RoleDiscounts` (`ProductRoleDiscount`: `Role`, `PercentOff`) | Best single discount applies, rounded to the cent |
| Add-ons | `AddOns` (`ProductAddOn`: `AddOnId`, `Label`, `Kind` Text or Checkbox, `Price`, `PriceBasis` PerUnit, PerPerson or PercentOfPrice, `Required`) | Priced by the first cart line handler; posted as `addOns[{AddOnId}]`; copied to `OrderLine.AddOns` |
| External products | `ExternalUrl`, `ExternalButtonText` | `CanBePurchased` is false; the link replaces Add to cart |

## Optional features

| Feature | What it adds |
| --- | --- |
| Recommendations (`CrestApps.OrchardCore.Products.Recommendations`) | `ProductRecommendationsPart` with `UpsellIds` and `CrossSellIds`; `ProductUpsells`, `ProductRelated`, `ProductRecentlyViewed` shapes on the product page; `CartRecommendations` on the cart and confirmation; cards from `ProductListingService.BuildCardsAsync` and `products/cards` |
| Comparison (`CrestApps.OrchardCore.Products.Comparison`) | `ProductCompareToggle` in the `ProductAction` display, a bar of up to four products, and `products/compare` |
| Grouped Products (`CrestApps.OrchardCore.Products.Grouped`) | `ProductGroupPart.ProductIds` (up to 20); Carts renders `GroupedProducts` posting to `cart/add-many` |
| Bundles (`CrestApps.OrchardCore.Products.Bundles`) | `ProductBundlePart.Items` (up to 10, `ProductBundleIndex`); stock from parts via Inventory; `OrderLine.BundleItems`; `ProductBundleContents` shape |

## Product Feeds

`CrestApps.OrchardCore.Products.Feeds` publishes `feeds/products.xml` (Google Merchant Center RSS 2.0 with `g:` attributes, also read by Meta and TikTok) and `feeds/products.csv`. Both accept `?currency=` (a market currency) and `?key=` when `ProductFeedSettings.Key` is set; cached 15 minutes per currency. Items are purchasable, priced, non-recurring products or each enabled variant. `ProductFeedValidator` leaves out items Merchant Center would refuse and lists why under **Settings > Commerce > Product feeds** (`ManageProductFeeds`). Attach `ProductFeedPart` (`Brand`, `Gtin`, `VariantGtins`, `Mpn`, `Condition`, `GoogleProductCategory`, `ExcludeFromFeeds`) to types that need identifiers.

## Import and export with Content Transfer

With `orchardcore-content-transfer`:

- `ProductPartContentImportHandler` adds `ProductPart_*` columns (prices, sale prices and dates, SKU, currency, external link, quantity rules).
- `ProductVariantsPartContentImportHandler` adds `ProductVariantsPart_Options` (`Size: S, M; Color: Red, Blue`) and `ProductVariantsPart_Variants` (JSON).
- `StoreExportReaderTransformer` reads Shopify and WooCommerce product exports.
- `ProductImportRowFilter` matches rows to existing products by SKU (`ProductSkuIndex`) and skips duplicates.
- Inventory adds `InventoryPart_*` columns (see `orchardcore-inventory`).

## Taxation

With Taxation on, attach `TaxationPart` to the product type. `ProductTaxableItemProvider` exposes products as `ITaxableItem` with the price, the kind from `ProductType` (`Good` Physical, `Service` Service, `Digital` Digital, `Undefined` Physical) and the tax codes. See `orchardcore-taxation`.

## Catalog API

`CrestApps.OrchardCore.Products.Api` maps `GET products` (`q`, `type`, `sort` of `newest`, `price-asc`, `price-desc`, `name`, `featured`, `skip`, `take`) and `GET products/{id}` into `api/commerce/v1`, for guests. Results are limited to types the caller may view, priced through `IPriceResolver` for the caller (and market), with availability from `IProductAvailabilityProvider`. `prices` lists each `ProductPricePart` price in its own currency. See the API reference in `orchardcore-commerce`.

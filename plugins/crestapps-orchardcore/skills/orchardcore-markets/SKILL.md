---
name: orchardcore-markets
description: Skill for the CrestApps Orchard Core Markets module (CrestApps.OrchardCore.Markets). Covers markets as groups of countries with their own currency, exchange rate, MarketPriceRounding, culture and prices that include tax, how the shopper's market is resolved from the market query value, the X-Market header, the orchard-market cookie and the request culture, the IShopperCurrency seam with ShopperMarketService and ToShopperAsync, IProductSaleRestriction and the Not sold in list, market pricing of products, carts, shipping and the catalog API, MarketCheckoutTaxProfileHandler for tax-inclusive markets, the MarketPicker shape, and the ManageMarkets permission. Use it when requests mention multi-currency storefronts, selling in several countries, currency conversion, VAT-inclusive prices, a market picker or restricting products by country.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Markets

Markets (new in CrestApps.OrchardCore 3.0) sells to several countries on their own terms. A `Market` is a set of countries with a currency, an exchange rate from the store's currency, a rounding rule, a culture and whether its prices include tax. The shopper's market decides the currency of product cards, product pages, the cart, the checkout, shipping and the catalog API.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Commerce, Products (`orchardcore-commerce`, `orchardcore-products`) | Required. Markets fills Products.Core seams. |
| Carts (`orchardcore-carts`) | Reprices a cart in the shopper's currency |
| Checkout (`orchardcore-checkout`) | The store currency is `CheckoutSettings.Currency`; rates are relative to it |
| Shipping (`orchardcore-shipping`) | Offers zone methods in the market's currency, or converts the others |
| Taxation (`orchardcore-taxation`) | `PricesIncludeTax` makes checkout taxes inclusive; TaxJar is skipped for inclusive markets |

| | |
| --- | --- |
| Feature | `CrestApps.OrchardCore.Markets` (category Commerce) |
| Depends on | `CrestApps.OrchardCore.Commerce`, `CrestApps.OrchardCore.Products` |
| Seams it fills | `IShopperCurrency`, `IProductSaleRestriction` (Products.Core), `ICheckoutTaxProfileHandler` (Checkout) |
| Admin | **Commerce > Markets** (admin route `markets`), `AdminList`-based list and editor |
| Permission | `ManageMarkets` (Administrator) |

### Guidelines

- Install `CrestApps.OrchardCore.Markets` in the web/startup project.
- Set the store currency under **Settings > Commerce > Checkout** before adding markets.
- Code that shows or charges money resolves `IShopperCurrency`; never convert with your own rates.
- Markets have no recipe or deployment step; create them in the admin.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Markets"
      ],
      "disable": []
    }
  ]
}
```

## The market model

| `Market` property | Meaning |
| --- | --- |
| `Name` | Shown in the picker |
| `Countries` | Upper-case ISO alpha-2 codes; a country may belong to only one market |
| `Currency` | Upper-case ISO 4217 code |
| `ExchangeRate` | How much of `Currency` one unit of the store's currency is worth (above zero) |
| `Rounding` | `MarketPriceRounding`: `None`, `WholeUnits`, `EndIn99`, `EndIn95` (always rounds up) |
| `Culture` | A supported culture, or `null` |
| `PricesIncludeTax` | Makes checkout taxes inclusive |
| `IsEnabled` | Off markets are never resolved or offered |

Markets live in the `Markets` YesSql collection (`MarketIndex`) through `IMarketStore`. `MarketPricing.Convert(amount, fromRate, toRate)` is `amount / fromRate * toRate`.

## How the shopper's market is resolved

`ShopperMarketService.GetCurrentAsync()` resolves once per request:

1. No market in the admin, without an `HttpContext`, or when no market is on.
2. The `market` query value, then the `X-Market` header (a market id or a country code a market covers).
3. Outside `/api` only: the `orchard-market` cookie, then the market of `CultureInfo.CurrentCulture`'s region (`de-DE` resolves `DE`).

API clients must name the market on every call; they are never placed by cookie or language.

## The `IShopperCurrency` seam

```csharp
public interface IShopperCurrency
{
    ValueTask<string> GetCurrencyAsync(CancellationToken cancellationToken = default);

    ValueTask<string> GetStoreCurrencyAsync(CancellationToken cancellationToken = default);

    ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default);
}
```

- Products registers `ProductOwnShopperCurrency` with `TryAddScoped`: no shopper currency, no conversion.
- Markets replaces it with `ShopperMarketService`. `isPrice: true` applies the market's rounding; `false` rounds only to the currency's minor unit (add-ons, shipping). A currency that is neither the store's nor a market's returns `null`.
- `ShopperCurrencyExtensions.ToShopperAsync(amount, currency, isPrice)` returns `(Amount, Currency)` for display-only amounts.

Show a display-only amount in the shopper's currency:

```csharp
public sealed class GiftWrapPriceService
{
    private readonly IShopperCurrency _shopperCurrency;

    public GiftWrapPriceService(IShopperCurrency shopperCurrency)
    {
        _shopperCurrency = shopperCurrency;
    }

    public async Task<(decimal Amount, string Currency)> GetDisplayPriceAsync(decimal amount, string currency)
        => await _shopperCurrency.ToShopperAsync(amount, currency, isPrice: true);
}
```

Replace `IShopperCurrency` with your own registration to price from a live rates service; consumers do not change.

## What markets change

| Where | Behavior |
| --- | --- |
| Prices (`DefaultPriceResolver`) | A `ProductPricePart` price listed in the requested currency wins (same kind, interval and duration, offered now). Otherwise a one-time price is converted. Recurring prices and buyer-named amounts are never converted: the resolver returns `null`. |
| Product cards | Indexed price and compare-at price go through `ToShopperAsync`. Sorts and price filters stay in the products' own currency. |
| Product page | Prices listed in the shopper's currency replace the others; failing that, each is converted for display only. |
| Cart | Cart currency is the shopper's currency. Changing market reprices every line without flagging a price change. A line that can't be converted gets a problem that blocks checkout. |
| Shipping | Methods in the market's currency are quoted first; otherwise others are converted (`isPrice: false`). Free and table thresholds compare unconverted subtotals, so give a market its own methods when thresholds matter. |
| Tax | `MarketCheckoutTaxProfileHandler` sets `TaxPriceType.Inclusive` for a market with `PricesIncludeTax`. |
| Catalog API | `price` from `IPriceResolver` in the shopper's currency; `prices` lists each price in its own currency. |
| Orders | The market's `Culture` switches the storefront; orders keep `Order.Culture` and their emails use it. |

## Products not sold in a market

```csharp
public interface IProductSaleRestriction
{
    ValueTask<string> GetRestrictionAsync(ContentItem product, CancellationToken cancellationToken = default);
}
```

Markets registers `MarketSaleRestriction`, driven by the product editor's **Not sold in** list (`ProductMarketsPart.NotSoldIn`, stored beside `ProductPart` without being added to the type). Every restriction is honored by the Add to cart driver (renders `AddToCartUnavailable`), the cart (refuses the line; the Cart API answers `409 not-available`) and the catalog API (`isPurchasable: false`). The product is still listed and searchable.

Add your own rule, such as an age check:

```csharp
public sealed class AgeRestrictedSaleRestriction : IProductSaleRestriction
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AgeRestrictedSaleRestriction(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<string> GetRestrictionAsync(ContentItem product, CancellationToken cancellationToken = default)
    {
        if (product.Has("AgeRestrictedPart") &&
            _httpContextAccessor.HttpContext?.User?.HasClaim("age_verified", "true") != true)
        {
            return ValueTask.FromResult("Sign in with a verified age to buy this product.");
        }

        return ValueTask.FromResult<string>(null);
    }
}
```

Register with `services.AddScoped<IProductSaleRestriction, AgeRestrictedSaleRestriction>();`.

## The market picker

`MarketNavbarDisplayDriver` adds the `MarketPicker` shape (`MarketPickerViewModel`: `Markets`, `Current`, `StoreCurrency`, `ReturnUrl`) to the front-end `Navbar` before the cart link. It posts `marketId` and `returnUrl` to `POST /market`, which sets the `orchard-market` cookie (one year, `HttpOnly`, `SameSite=Lax`, essential) and the culture cookie when the market has a supported culture. Override `MarketPicker.cshtml` or a Liquid template to restyle, or an empty template to hide it.

## Troubleshooting

| Problem | Check |
| --- | --- |
| The picker doesn't show | A market that is on, with a currency and a rate above zero |
| Shoppers see store prices | Their country isn't in an enabled market, or they picked Everywhere else |
| No language switch | The market's culture must be a supported site culture |
| Cart says an item is priced in another currency | The product's currency is neither the store's nor a market's; price it in one of those |
| No shipping methods in a market | The zone needs enabled methods in the market's or the store's currency |
| API prices are in the product currency | Send `X-Market` or `?market=` on every call |

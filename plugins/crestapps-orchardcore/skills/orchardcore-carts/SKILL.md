---
name: orchardcore-carts
description: Skill for the CrestApps Orchard Core Carts module (CrestApps.OrchardCore.Carts, Carts.Wishlists, Carts.Api). Covers what a cart may hold, guest carts by the cart_owner cookie and merging on sign-in, re-pricing every line through IProductSnapshotResolver and IPriceResolver, ICartService, CartOptions (lifetimes, MaxLines, MaxQuantity, ExcludedParts, ChooseOnProductPageParts), ICartLineHandler with CartLinePrepareContext and CartLineEvaluateContext for line details, ICartActionProvider for cart page buttons, the AddToCart, Cart, CartLink and MiniCart shapes, the cart endpoints and Cart rate-limit group, stock and market behavior in the cart, wishlists, and the Cart API with the X-Cart-Token header. Use it when requests mention an Orchard Core shopping cart, add to cart, mini cart, cart line details, wishlists or a headless cart.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Carts

Carts (new in CrestApps.OrchardCore 3.0) lets visitors put products sold online in a cart from listing cards and product pages. A cart stores what was chosen (product, price, variant, quantity, add-ons, details) and never a price from the browser; every line is priced again whenever it is shown.

## Where it fits in the commerce suite

| Module | Relationship |
| --- | --- |
| Commerce, Products (`orchardcore-commerce`, `orchardcore-products`) | Required. Products supplies prices, variants, add-ons and availability. |
| Orders (`orchardcore-orders`) | Adds **Checkout** to the cart and turns it into an order |
| Checkout (`orchardcore-checkout`) | Takes the order's payment |
| Inventory (`orchardcore-inventory`) | Caps quantities and refuses sold-out lines |
| Markets (`orchardcore-markets`) | Prices the cart in the shopper's currency and refuses restricted products |

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Carts` | Carts | Commerce, Products, `OrchardCore.Contents` |
| `CrestApps.OrchardCore.Carts.Wishlists` | Wishlists | Carts |
| `CrestApps.OrchardCore.Carts.Api` | Cart API | Carts |

### Guidelines

- Install `CrestApps.OrchardCore.Carts` in the web/startup project, and enable Orders to check carts out.
- Use `ICartService` to add, update and price lines; never write a cart document directly.
- Keep line details under keys prefixed with your module name, such as `giftwrap.message`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Carts.Wishlists",
        "CrestApps.OrchardCore.Orders"
      ],
      "disable": []
    }
  ]
}
```

## What a cart may hold

A product that can be bought online at a one-time price. The cart refuses listing-only or unpriced products, subscription plans and recurring prices, buyer-named amounts, products that can't be converted into the cart's currency, products an `IProductSaleRestriction` forbids, quantities the price or quantity rules don't allow, and products with a part in `CartOptions.ExcludedParts`. A line whose product was unpublished, taken off sale or repriced is flagged, and the cart can't check out until it changes.

## Ownership and lifetime

| Visitor | Found by |
| --- | --- |
| Signed in | User id |
| Guest | `cart_owner` cookie with a protected token; only its hash is stored. Issued on first add. |

Signing in merges the guest cart into the user's (same product and price add up; lines in another currency are dropped). Carts live in the `Carts` YesSql collection and expire after 30 days (guest) or 90 days (user) without changes; an hourly task deletes them.

## Options

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<CartOptions>(options =>
        {
            options.GuestCartLifetime = TimeSpan.FromDays(14);
            options.UserCartLifetime = TimeSpan.FromDays(60);
            options.MaxLines = 50;
            options.MaxQuantity = 20;
            options.ExcludedParts.Add("MembershipPart");
            options.ChooseOnProductPageParts.Add("GiftWrapPart");
        });
    }
}
```

`ExcludedParts` holds `SubscriptionPart` by default. `ChooseOnProductPageParts` makes cards link to the product page instead of adding, for products whose line needs details.

## Line details with `ICartLineHandler`

Handlers run by `Order`, lowest first; product add-ons are the first handler (order 100). `PrepareAsync` keeps the details it owns from `PostedDetails` (anything no handler keeps is dropped) or calls `Refuse`. `EvaluateAsync` prices the line: change `UnitPrice`, lower `MaxQuantity`, set `PeoplePerUnit`, add `CartLineDetail`s, set `Problem`, or set `ExcludeFromDiscounts`. `ConvertAsync` converts from the product's currency into the line's.

```csharp
public sealed class GiftWrapLineHandler : ICartLineHandler
{
    private const string Detail = "giftwrap.message";

    public int Order => 200;

    public Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        if (context.PostedDetails.TryGetValue(Detail, out var message) && !string.IsNullOrWhiteSpace(message))
        {
            context.Details[Detail] = message.Trim();
        }

        return Task.CompletedTask;
    }

    public async Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        if (context.GetDetail(Detail) is not { } message)
        {
            return;
        }

        context.UnitPrice += await context.ConvertAsync(3m, cancellationToken);
        context.Details.Add(new CartLineDetail(Detail, "Gift wrapped", message));
    }
}
```

Register with `services.AddScoped<ICartLineHandler, GiftWrapLineHandler>();`. The *Add to cart* form posts details as `details[giftwrap.message]` (a field outside the form uses `form="add-to-cart-{contentItemId}"`); the Cart API posts `details`. Details are copied to `OrderLine.Details` and their descriptions to `OrderLine.DetailLines`. Two lines of the same product with different details stay apart.

## Cart page actions

```csharp
public sealed class SaveForLaterCartActionProvider : ICartActionProvider
{
    public Task<IReadOnlyList<CartAction>> GetActionsAsync(CartPricing pricing, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CartAction>>([new CartAction("Email me this cart", "/cart/email", "We'll send you a link to it.")]);
}
```

Buttons render under **Checkout**; Orders' `QuoteCartActionProvider` adds **Request a quote** this way.

## Shapes and endpoints

| Shape | Where |
| --- | --- |
| `AddToCart` | **Actions** zone of the product's `ProductAction` display (cards and pages); shows availability and the variant picker |
| `Cart` | `/cart` (`CartController`) |
| `CartLink` | The `Navbar` shape (TheTheme and the Storefront theme render it); also hosts the mini cart |
| `MiniCart` | Offcanvas loaded from `GET cart/mini` (route `CartMini`) |
| `GroupedProducts` | Grouped products' quantities, posted to `cart/add-many` |

Forms are plain antiforgery POSTs (`POST cart/add`, `cart/update`, `cart/remove`) that redirect back with a notification; with `Accept: application/json` they answer `{ succeeded, message, itemCount }`. They are in the `Cart` rate-limit group of `OrchardCore.RateLimits` (see `orchardcore-rate-limits`). Override any shape in Razor or Liquid; an empty `CartLink` template hides the cart link. The `product-variant-picker` script turns the variant list into one select per option.

## Stock and markets in the cart

- With Inventory, out-of-stock products that don't take backorders are refused and quantities are capped (*Only 3 of Mug left.*); a pre-order's release date is shown on the line. A buyer's own held units count as theirs.
- With Markets, the cart is in the shopper's currency, a market change reprices every line, and a line that can't be converted blocks checkout.

## Wishlists

`CrestApps.OrchardCore.Carts.Wishlists` keeps a `Wishlist` per signed-in user (up to 100, `WishlistIndex`). `WishlistButton` posts to `wishlist/{contentItemId}/add` or `/remove`; `account/wishlist` lists saved products with their add to cart and adds **Wishlist** to the account menu.

## Cart API

`CrestApps.OrchardCore.Carts.Api` maps into `api/commerce/v1`:

| Method | Path | Who |
| --- | --- | --- |
| `GET` | `cart` | Guest |
| `POST` | `cart/lines` (`productId`, `variantId`, `priceId`, `quantity`, `addOns`, `details`) | Guest |
| `PATCH` | `cart/lines/{lineId}` (`quantity`; zero removes) | Guest |
| `DELETE` | `cart/lines/{lineId}`, `cart` | Guest |
| `POST` | `cart/merge` (bearer token plus the guest's `X-Cart-Token`) | Buyer |

The answer that creates a guest cart carries `X-Cart-Token`; send it on every cart call and on `POST checkout` (Orders API). A restricted product answers `409 not-available`. See the API reference in `orchardcore-commerce`.

## Not included

Saved carts, recurring prices in carts and editing a paid order's lines. Checkout belongs to Orders.

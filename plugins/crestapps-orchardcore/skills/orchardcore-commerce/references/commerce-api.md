# Commerce JSON API

Base path `api/commerce/v1` (with a tenant prefix first, such as `shop/api/commerce/v1`). JSON over HTTPS, `camelCase` names, ISO 8601 UTC dates in fields ending in `Utc`, problems as RFC 9457. Shared code lives in `CrestApps.OrchardCore.Commerce.Api.Core`.

## API features

| Feature | Name | Needs | Maps |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Products.Api` | Catalog API | Product Listing | `products`, `products/{id}` |
| `CrestApps.OrchardCore.Carts.Api` | Cart API | Carts | `cart`, `cart/lines`, `cart/merge` |
| `CrestApps.OrchardCore.Checkout.Api` | Checkout API | Checkout | `checkout/{id}`, codes, payment methods, payment, complete, cancel; staff `coupons` |
| `CrestApps.OrchardCore.Orders.Api` | Orders API | Orders, Cart API, Checkout API | `POST checkout`, `checkout/{id}/gift-cards`, `me/orders` and returns, `me/quotes`, staff `orders` |
| `CrestApps.OrchardCore.Customers.Api` | Customers API | Customers | `me`, `me/addresses`, staff `customers`, `checkout/{id}/contact`, store credit and points |
| `CrestApps.OrchardCore.Shipping.Api` | Shipping API | Shipping, Checkout API | `checkout/{id}/pickup-locations`, `shipping-rates`, `shipping`; `me/orders/{orderId}/shipments`; staff fulfillment |
| `CrestApps.OrchardCore.Inventory.Api` | Inventory API | Inventory | `products/{id}/availability`; staff `inventory` |
| `CrestApps.OrchardCore.PointOfSale` | Point of Sale | Orders, Inventory, Checkout API | `pos/...` |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products.Api",
        "CrestApps.OrchardCore.Carts.Api",
        "CrestApps.OrchardCore.Checkout.Api",
        "CrestApps.OrchardCore.Orders.Api",
        "CrestApps.OrchardCore.Customers.Api",
        "CrestApps.OrchardCore.Shipping.Api",
        "CrestApps.OrchardCore.Inventory.Api",
        "OrchardCore.OpenApi",
        "OrchardCore.OpenApi.ScalarUI",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation",
        "OrchardCore.OpenId.Management",
        "OrchardCore.Cors"
      ],
      "disable": []
    }
  ]
}
```

## Setup checklist

- **OpenAPI.** With `OrchardCore.OpenApi` on, the description is at `/openapi/v1.json`; Swagger UI `/swagger`, Scalar `/scalar/v1`, ReDoc `/redoc`. Only users with **View OpenAPI Content** can read it unless anonymous access is allowed. See `orchardcore-openapi`. Generate typed clients with NSwag (`nswag openapi2tsclient`, `nswag openapi2csclient`) and add the token headers yourself.
- **Sign-in.** Enable the OpenID authorization server, token validation and management (see `orchardcore-openid`). Token validation must name this tenant, or every token call answers `401`.
  - Apps people sign in to: authorization code flow with PKCE, public client, scopes `openid`, `profile`, `roles` (staff permissions are evaluated from the `roles` claim), and `offline_access` for refresh tokens.
  - Servers: client credentials flow, confidential client, with **Client Credentials Roles** holding only the permissions needed.
- **CORS** for a browser storefront on another origin (`OrchardCore.Cors`): allow methods `GET, POST, PUT, PATCH, DELETE`; headers `Authorization, Content-Type, X-Cart-Token, X-Checkout-Token, Idempotency-Key`; expose `X-Cart-Token, X-Checkout-Token, Location`; credentials off.

## Callers and permissions

| Caller | Endpoints | Permission |
| --- | --- | --- |
| Guest, no token | Catalog, availability, cart, checkout and its steps | None |
| Signed-in buyer | Guest endpoints plus `me`, own orders, returns, shipments, store credit, points, `cart/merge` | **View own orders**, **Manage own customer profile and addresses** (Authenticated by default) |
| Staff or server | Staff endpoints | Same as the admin pages: **Manage orders**, **View customers**, **View inventory**, **Manage inventory**, **Manage coupons**, **Ship orders and update their shipments** |

No token where one is needed answers `401`; a token without the permission answers `403`. Another buyer's checkout, order or cart line answers `404`.

## Conventions

- **Money** is an `ApiMoney`: `{ "amount": 49.99, "currency": "USD" }` in major units.
- **Pages** are `ApiPage<T>`: `items`, `total`, `skip`, `take` (default 20, max 100).
- **Markets.** Send `X-Market: DE` or `?market=DE` on every catalog, cart and checkout call. API calls are never placed in a market by cookie or language. A product left out of the market has `isPurchasable: false` and adding it answers `409 not-available`.
- **Idempotency.** `Idempotency-Key` (up to 128 characters) on calls that place, pay, refund or move something returns the first final answer for 24 hours, including the `X-Cart-Token`, `X-Checkout-Token` and `Location` headers. `500`+, `429`, and `409` with `concurrency` or `idempotency-in-progress` are not kept, so the same key retries. Make a new key per new action. Completing a checkout needs no key.
- **Guest tokens.** Keep `X-Cart-Token` and `X-Checkout-Token` in secure storage, never in a link. After sign-in, `POST cart/merge` with the bearer token and the guest's `X-Cart-Token`.

| `409` code | Meaning | Client action |
| --- | --- | --- |
| `empty-cart` | Nothing to check out | Back to shopping |
| `out-of-stock` | Something ran out | Show `detail`, let the buyer change the cart |
| `not-available` | Not possible now | Show `detail` |
| `invoice-changed` | Total changed | Get the checkout, show the total, pay again |
| `concurrency` | Simultaneous change | Read again, retry |
| `unsupported` | Checkout doesn't take that | Hide the option |
| `idempotency-in-progress` | Same key still running | Wait and resend |

## A guest checkout, call by call

1. `GET products?q=mug&take=2`, then `GET products/{id}` for variants, prices, add-ons and availability.
2. `POST cart/lines` with `{ "productId": "...", "quantity": 2 }` (plus `variantId`, `priceId`, `addOns`, `details` as needed). Keep the returned `X-Cart-Token`.
3. `POST checkout` with `X-Cart-Token` and an `Idempotency-Key`. This places the order, holds stock and starts the checkout; `201` with `X-Checkout-Token`. The answer is an `ApiCheckout` with `steps`, `nextStep` and `totals` (`subtotal`, `shipping`, `discount`, `tax`, `total`, `tendered`, `amountDue`).
4. `PUT checkout/{id}/contact` with `name`, `email`, `phone`, `billingAddress` (an `ApiAddress` with `addressLine1`, `city`, `region`, `postalCode`, `country`, and so on).
5. `POST checkout/{id}/shipping-rates` with an `address`, then `PUT checkout/{id}/shipping` with the address and `rateId` (or a `pickupLocationId` from `GET checkout/{id}/pickup-locations`).
6. Optional: `POST checkout/{id}/codes`, `gift-cards`, `store-credit`, `points`.
7. `GET checkout/{id}/payment-methods`. `kind` is `gateway` or `offline`; `client` carries what the provider's SDK needs (for Stripe the publishable key, mode and amount in minor units), never a secret.
8. `POST checkout/{id}/payment` with `{ "paymentMethod": "Stripe" }` and an `Idempotency-Key`. Confirm each returned step with its `clientSecret` or send the buyer to `redirectUrl`.
9. `POST checkout/{id}/complete` until `status` is `completed` or `alreadyCompleted` (`pending` means ask again; `blocked`, `failed`, `canceled` otherwise). Show `orderNumber`.

`DELETE checkout/{id}` cancels an unpaid checkout. The store's own **Checkout details** fields have no API; collect them on the checkout page or leave that feature off.

## Staff endpoints at a glance

| Area | Endpoints |
| --- | --- |
| Orders (**Manage orders**) | `GET orders` (filters `status`, `channel`, `locationId`, `q`, `customerId`, `from`, `to`), `GET orders/{orderId}`, `GET orders/by-number/{number}`, `POST orders/{orderId}/refunds`, `POST orders/{orderId}/cancel` |
| Fulfillment (**Ship orders and update their shipments**) | `GET/POST orders/{orderId}/shipments`, `POST shipments/{shipmentId}/ship`, `POST shipments/{shipmentId}/deliver` |
| Customers (**View customers**) | `GET customers`, `GET customers/{customerId}` |
| Stock (**View inventory**, **Manage inventory**) | `GET inventory`, `GET inventory/{stockItemId}`, `POST .../receive`, `POST .../adjust`, `PUT .../count`, `POST .../transfer` |
| Coupons (**Manage coupons**) | `GET/POST coupons`, `GET/PUT/DELETE coupons/{couponId}` |

An order carries `channel` (`online`, `pos`, `draft`), `culture` and `taxes` (grouped tax lines with `rate`, `taxable`, `amount`, `includedInPrice`).

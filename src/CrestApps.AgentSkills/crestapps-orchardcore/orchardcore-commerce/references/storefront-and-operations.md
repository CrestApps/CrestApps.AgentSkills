# Storefront Shapes and Running a Store

## Storefront shapes

Controller views are thin frames (breadcrumb, heading, account menu) around a named shape. Override the shape with a Razor or Liquid template in the theme, or in **Design > Templates**. A build test fails when a storefront controller renders outside a shape.

| Page | Shapes |
| --- | --- |
| Listings and product pages | Listing and card shapes (see `orchardcore-products`); on product actions `AddToCart`, `GroupedProducts`, `ExternalProductAction`, `ProductCompareToggle`; on the page `ProductBundleContents`, `ProductUpsells`, `ProductRelated`, `ProductRecentlyViewed`; strips `ProductCardStrip` |
| Comparison | `ProductCompare` |
| Cart | `Cart`, `MiniCart`, `CartRecommendations` |
| Checkout | `CheckoutFlow` and step editors such as `CheckoutCoupon`, `CheckoutOrderBump`; `CheckoutThankYou`, `OrderConfirmation`, `OrderConfirmationOffers`; `CheckoutEnded` |
| Account | `AccountOverview`, `AccountDetails`, `AccountAddresses`, `AccountAddressEdit`, `AccountOrders`, `AccountOrder`, `AccountDownloads`, `AccountPaymentMethods`, `AccountSubscriptions`, `AccountTransactions` |
| Guests and links | `OrderLookup`, `OrderLookupSent`, `GuestOrder`, `GuestOrderInvalid`, `DownloadPage`, `TransactionPay`, `TransactionPayInvalid`, `SubscriptionUnavailable` |

The account frame is `CommerceAccountPage` (`Title`, `Content`, `Actions`); override `CommerceAccountPage.cshtml` to change every account page. The account menu shape is `CommerceAccountNavigation`.

## Storefront theme

`CrestApps.OrchardCore.StorefrontTheme` (base theme TheTheme) overrides `Layout` with a sticky header (logo, search box posting `q` to the home listing, the `Navbar` shape with cart link, market picker and user menu), the `alias:main-menu` menu, the `Title`, `Header`, `Messages` and `Footer` zones, and a footer from the typed `ReceiptSettings` (the Receipts module need not be on). Restyle a child theme with `--storefront-accent`, `--storefront-header-bg` and `--storefront-footer-bg`.

## Background tasks

Each runs per tenant while its feature is on; manage them under **Configuration > Tasks**.

| Task | Feature | Schedule |
| --- | --- | --- |
| Checkout Payment Reconciliation | Checkout | Every 5 minutes |
| Expired Carts Cleanup | Carts | Hourly |
| Abandoned orders | Orders | Every 15 minutes |
| Checkout recovery emails | Orders | Every 15 minutes |
| Expired stock holds | Inventory | Every 5 minutes |
| Customer data retention | Customers | Daily at 03:30 |
| Product Search Indexing | Products | Every minute |
| Webhook deliveries | Commerce Webhooks | Every minute |
| Pay Later Renewals | Pay Later | Every 30 minutes |
| Transaction Payment Reminders | Transactions | Every 6 hours |

## What to back up

- The tenant database (orders, carts, customers, coupons, stock, shipments, download rights, reviews).
- Digital files at `App_Data/Sites/{tenant}/DigitalFiles/`.
- Data Protection keys: download links, guest order links and webhook secrets depend on them.

## Several nodes

- Enable `OrchardCore.Redis`, `OrchardCore.Redis.Lock`, `OrchardCore.Redis.Cache`, `OrchardCore.Redis.Bus`. Checkout finalization, webhooks, coupon uses, order numbers and download counts are decided under `IDistributedLock`.
- Share the `DigitalFiles` folder and the Data Protection keys (`OrchardCore.Redis.DataProtection` or Azure data protection).

## Throttling

| What | Limit |
| --- | --- |
| `POST cart/add`, `cart/update`, `cart/remove` | The `Cart` rate-limit group of Orchard Core Rate Limits |
| Starting a payment, checking its status | 20 attempts per 5 minutes per client address and checkout (`PaymentRateLimitOptions`) |
| Coupon codes, guest order lookup | Same limit per client address (and per email for lookup) |
| Address suggestions | `address-autocomplete` group, 60 per minute per visitor |

Counts are per tenant. Every commerce record is tenant-isolated.

## Recipe checklist for a store

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Products.Listing",
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Transactions",
        "CrestApps.OrchardCore.Receipts",
        "CrestApps.OrchardCore.Inventory",
        "CrestApps.OrchardCore.Shipping",
        "CrestApps.OrchardCore.Taxation",
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.PayLater",
        "CrestApps.OrchardCore.Commerce.Webhooks"
      ],
      "disable": []
    }
  ]
}
```

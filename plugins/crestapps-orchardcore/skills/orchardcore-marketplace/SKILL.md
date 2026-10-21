---
name: orchardcore-marketplace
description: Skill for the CrestApps Orchard Core Marketplace commerce module and its Stripe Connect payouts feature. Covers vendors and the users who run them, the VendorPart on products, commission per store or per vendor, VendorEarning records held in escrow until a hold ends and the order is fulfilled, refunds taken back, early release, VendorPayout records, the IVendorPayoutProvider seam, Stripe Connect Express onboarding and transfers, MarketplaceSettings, the vendor admin and the storefront vendor dashboard. Use this skill when requests mention Orchard Core marketplace, multi-vendor stores, vendor commission, vendor payouts, escrow, or Stripe Connect. Strong matches include CrestApps.OrchardCore.Marketplace, CrestApps.OrchardCore.Marketplace.StripeConnect, Vendor, VendorPart, VendorEarning, VendorPayout, MarketplaceLedger, MarketplaceService, IVendorPayoutProvider, StripeConnectPayoutProvider, StripeConnectVendorService, MarketplaceSettings and ManageMarketplace.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Marketplace - Prompt Templates

## Sell Other Sellers' Products

You are an Orchard Core expert. Generate accurate setup and extension guidance for the CrestApps Marketplace module, which sells vendors' products, keeps a commission, holds what vendors earn in escrow, and pays them out.

### Guidelines

- Install `CrestApps.OrchardCore.Marketplace` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle) in the web/startup project.
- Enable `CrestApps.OrchardCore.Marketplace`. It depends on `CrestApps.OrchardCore.Commerce` and `CrestApps.OrchardCore.Orders`. Selling needs `CrestApps.OrchardCore.Products`, Carts and `CrestApps.OrchardCore.Checkout` with a payment provider (see `orchardcore-products`, `orchardcore-checkout` and `orchardcore-payments`).
- Enable `CrestApps.OrchardCore.Marketplace.StripeConnect` to pay vendors to their own Stripe accounts. It depends on `CrestApps.OrchardCore.Stripe`, and the store's Stripe account must have Connect turned on.
- Attach `VendorPart` to the product types sold for vendors and set `VendorId` on each product.
- Never move money in custom code by writing earnings. Use `MarketplaceService` so sales, refunds, releases and payouts stay idempotent.
- Orders and carts are covered by `orchardcore-orders` and `orchardcore-carts`; the shared Commerce menu and storefront account by `orchardcore-commerce`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except View Models.

### Features and Services

| Item | Value |
|---|---|
| Feature IDs | `CrestApps.OrchardCore.Marketplace`, `CrestApps.OrchardCore.Marketplace.StripeConnect` |
| Content part | `VendorPart` (`VendorId`) |
| YesSql collection | `Marketplace` (`Vendor`, `VendorEarning`, `VendorPayout`; `VendorUserIndex` one row per vendor user) |
| Stores | `IVendorStore`, `IVendorEarningStore`, `IVendorPayoutStore` |
| Services | `MarketplaceLedger` (arithmetic), `MarketplaceService` (books) |
| Order seam filled | `IOrderEventHandler` (`MarketplaceOrderEventHandler`) |
| Seam added | `IVendorPayoutProvider` (namespace `CrestApps.OrchardCore.Marketplace.Services`) |
| Stripe Connect | `StripeConnectVendorService`, `StripeConnectPayoutProvider` |
| Site settings | `MarketplaceSettings` (group `marketplace`, **Settings > Commerce > Marketplace**) |
| Background task | `MarketplaceBackgroundTask` (hourly release, and payouts when automatic) |
| Permission | `ManageMarketplace` (Administrator, Store manager), also guards the settings group |
| Sample recipe | `MarketplaceSample`: a Vendor Product type and a $30 mug. Add a vendor under **Commerce > Vendors**, then pick it on the mug |

## Records

- **`Vendor`**: `Name`, `Email`, `UserIds` and `UserNames` (the people who run it), `CommissionPercent` (`null` uses the store's), `Status` (`VendorStatus` Active or Suspended), `Country`, `StripeAccountId`, `PayoutsEnabled`.
- **`VendorEarning`**: one per vendor line of a paid order, or per refund. `Gross`, `CommissionPercent`, `Commission`, `Net`, `Kind` (Sale or Refund), `Status` (Held, Available or PaidOut), `HoldUntilUtc`, `AwaitsFulfillment`, `ReleasedUtc`, `PayoutId`.
- **`VendorPayout`**: one order's earnings sent to a vendor, with `Amount`, `Currency`, `EarningIds`, `Status`, `TransferId` and `Failure`.

## Settings

| `MarketplaceSettings` | Default | Meaning |
|---|---|---|
| `CommissionPercent` | 10 | Store commission |
| `HoldDays` | 7 | Days a sale is held |
| `ReleaseOnFulfillment` | true | Goods also wait until the order is fulfilled |
| `AutomaticPayouts` | false | Pay out from the background task |
| `DefaultCountry` | `US` | Country a vendor's Stripe account is created in |

Settings are site settings, so the standard Orchard Core `Settings` recipe step sets them:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Marketplace",
        "CrestApps.OrchardCore.Marketplace.StripeConnect"
      ]
    },
    {
      "name": "Settings",
      "MarketplaceSettings": {
        "CommissionPercent": 12.5,
        "HoldDays": 14,
        "ReleaseOnFulfillment": true,
        "AutomaticPayouts": false,
        "DefaultCountry": "US"
      }
    }
  ]
}
```

## The Ledger

- `MarketplaceLedger.GetLineGross` is a line's total less its share of the order's discounts. `Split` rounds the commission to the cent and gives the vendor the rest. `GetRefundShare` is a vendor's share of a refund: the refund's lines when named, else a share of the order total.
- `MarketplaceService.RecordSaleAsync` writes a held earning per vendor line of a paid order (idempotent). `RecordRefundAsync` writes a negative earning keyed by the refund.
- `ReleaseDueAsync` makes earnings available once the hold ended and, for goods, the order is fulfilled. `ReleaseOrderAsync(vendorId, orderId)` releases early, for example when a service is delivered.
- `PayOutAsync(vendor)` pays what is available, one payout per order, through the first registered `IVendorPayoutProvider`. It refuses suspended vendors and vendors without `PayoutsEnabled`. `PayOutAllAsync` pays every vendor.

## Stripe Connect

- `StripeConnectVendorService.GetOnboardingUrlAsync` creates an Express account and an onboarding link. `RefreshAsync` updates whether details are submitted and transfers are active.
- `StripeConnectPayoutProvider.PayAsync` creates a Transfer to the vendor's account with the order's charge as `SourceTransaction`, so the money moves when the charge's funds are available.
- Until Connect is on in the Stripe dashboard, setting up payouts says so and creates nothing. Configure Stripe itself as described in `orchardcore-payments`.

## Custom Payout Provider

Implement `IVendorPayoutProvider` to pay vendors another way (bank file, PayPal). Return the provider's reference, or a failure reason. Use the idempotency key so a retried payout is not sent twice. Register it in a module that references `CrestApps.OrchardCore.Marketplace`, and do not enable the Stripe Connect feature alongside it, since the first provider is used.

```csharp
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace MyCompany.Marketplace.BankPayouts;

public sealed class BankFilePayoutProvider : IVendorPayoutProvider
{
    private readonly IBankTransferClient _bank;

    public BankFilePayoutProvider(IBankTransferClient bank)
    {
        _bank = bank;
    }

    public async Task<(string TransferId, string Failure)> PayAsync(
        Vendor vendor,
        Order order,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _bank.SendAsync(vendor.Email, amount, currency, idempotencyKey, cancellationToken);

        return result.Succeeded ? (result.Reference, null) : (null, result.Error);
    }
}

[RequireFeatures("CrestApps.OrchardCore.Marketplace")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IVendorPayoutProvider, BankFilePayoutProvider>();
    }
}
```

`IBankTransferClient` stands for your own payment client. Set `Vendor.PayoutsEnabled` once the vendor's payout details are known, or `PayOutAsync` refuses the vendor.

## Pages

- **Commerce > Vendors** (`vendors`, `ManageMarketplace`) lists vendors on the `AdminList` standard. A vendor's page shows held, ready and paid-out totals, sales and payouts, **Release now** per order, and **Pay out what's ready**.
- The storefront account's **Vendor** page (`account/vendor`, `VendorPortal` route, `VendorDashboard` shape) shows the same to the vendor's users, with **Set up payouts** for Stripe.

## Troubleshooting

| Symptom | Check |
|---|---|
| No earnings after a paid order | The product needs `VendorPart` with a `VendorId` |
| Earnings stay Held | The hold has not ended, or `ReleaseOnFulfillment` waits for fulfillment. Use **Release now** |
| Payout refused | The vendor is suspended or `PayoutsEnabled` is false, or no payout provider is registered |
| Set up payouts does nothing | Turn on Connect in the Stripe dashboard and enable the Stripe Connect feature |
| Automatic payouts never run | Enable `AutomaticPayouts`; the background task runs only for tenants that have served a request |

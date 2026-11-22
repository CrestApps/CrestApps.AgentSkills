---
name: orchardcore-marketplace
description: Skill for the CrestApps Orchard Core Marketplace commerce module and its Stripe Connect payouts feature. Covers vendors and the users who run them, the VendorPart on products, commission per store or per vendor, VendorEarning records held in escrow until a hold ends and the order is fulfilled, refunds taken back, early release, VendorPayout records paid as Disbursements, the IVendorPayoutProvider seam, Stripe Connect Express onboarding and transfers, MarketplaceSettings, the vendor admin and the storefront vendor dashboard. Use this skill when requests mention Orchard Core marketplace, multi-vendor stores, vendor commission, vendor payouts, escrow, or Stripe Connect. Strong matches include CrestApps.OrchardCore.Marketplace, CrestApps.OrchardCore.Marketplace.StripeConnect, Vendor, VendorPart, VendorEarning, VendorPayout, MarketplaceLedger, MarketplaceService, IVendorPayoutProvider, StripeConnectPayoutProvider, StripeConnectVendorService, MarketplaceSettings and ManageMarketplace.
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
| Seam added | `IVendorPayoutProvider` (namespace `CrestApps.OrchardCore.Marketplace.Services`), an `IDisbursementProvider` of the Banking Disbursements building block |
| Stripe Connect | `StripeConnectVendorService`, `StripeConnectPayoutProvider` |
| Site settings | `MarketplaceSettings` (group `marketplace`, **Settings > Commerce > Marketplace**) |
| Background task | `MarketplaceBackgroundTask` (hourly release, and payouts when automatic) |
| Permission | `ManageMarketplace` (Administrator, Store manager), also guards the settings group |
| Sample recipe | `MarketplaceSample`: a Vendor Product type and a $30 mug. Add a vendor under **Commerce > Vendors**, then pick it on the mug |

## Records

- **`Vendor`**: `Name`, `Email`, `UserIds` and `UserNames` (the people who run it), `CommissionPercent` (`null` uses the store's), `Status` (`VendorStatus` Active or Suspended), `Country`, `StripeAccountId`, `PayoutsEnabled`.
- **`VendorEarning`**: one per vendor line of a paid order, or per refund. `Gross`, `CommissionPercent`, `Commission`, `Net`, `Kind` (Sale or Refund), `Status` (Held, Available or PaidOut), `HoldUntilUtc`, `AwaitsFulfillment`, `ReleasedUtc`, `PayoutId`.
- **`VendorPayout`**: one order's earnings sent to a vendor, with `Amount`, `Currency`, `EarningIds`, `Status`, `TransferId` and `Failure`, plus the id of the disbursement that paid it.

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
- `PayOutAsync(vendor)` pays what is available, one payout per order, through the `IVendorPayoutProvider` named by `MarketplaceSettings.PayoutProvider`, or the only one enabled when none is named (several enabled and none named pays nothing). Each payout is a **disbursement** (payee kind `MarketplaceVendor`, `MarketplaceConstants.VendorPartyKind`; source kind `MarketplaceOrder`, `PayoutSourceKind`, with the order id; access scope the vendor): created with the payout's idempotency key, sent in a batch of its own through the provider and released at once; the `VendorPayout` keeps the disbursement id and the transfer as its trace. Without the Disbursements feature nothing is paid and the vendor page says why (see `orchardcore-banking`). It refuses suspended vendors and vendors without `PayoutsEnabled`. Each payout's idempotency key comes from the vendor, order and earnings (`MarketplaceLedger.GetPayoutKey`), so a payout retried after the transfer went through but its save failed gets the same transfer back; a refused payout bumps the earnings' `RefusedPayouts` so the next try is a new request. `PayOutAllAsync` pays every vendor.

## Recipes, deployment and reporting

The `Vendors` step creates or updates vendors, matched by `ItemId`, then by name. Users are named by `UserNames` (must exist); commission is 0 to 100 or none. The **Vendors** deployment step (*Commerce*) writes every vendor. Stripe Connect accounts belong to the site they were made on, so account ids, onboarding keys and payout status are never exported or imported; earnings and payouts stay on the site.

```json
{
  "steps": [
    {
      "name": "Vendors",
      "Vendors": [
        { "ItemId": "4vd00000000000000000000001", "Name": "Acme Crafts", "Email": "sales@acme.test", "UserNames": [ "acme" ], "CommissionPercent": 12.5, "Status": "Active", "Country": "US" }
      ]
    }
  ]
}
```

With **Data Pipelines Core** on (Report Builder), the **Marketplace** data source (`Marketplace`, permission `ManageMarketplace`) has `Vendors`, `VendorEarnings` (refunds negative) and `VendorPayouts`. Stripe account ids, keys and transfer references are never exposed. A vendor's user and email are anonymized by `VendorPersonalDataContributor` on a personal data erasure.

## Stripe Connect

- `StripeConnectVendorService.GetOnboardingUrlAsync` creates an Express account and an onboarding link. `RefreshAsync` updates whether details are submitted and transfers are active.
- The feature depends on Marketplace, Stripe and Disbursements (`DisbursementsConstants.Features.Disbursements`). `StripeConnectPayoutProvider` (provider name and method `StripeConnect`, shown as **Stripe Connect transfer**) is registered as both `IDisbursementProvider` and `IVendorPayoutProvider`. `SubmitAsync` creates a Transfer per disbursement to the vendor's account with the order's charge as `SourceTransaction` (idempotency key `vendor-payout-{disbursement.IdempotencyKey}`), so the money moves when the charge's funds are available. A transfer that goes through settles the disbursement with the transfer id as trace; one Stripe refuses fails it with Stripe's reason. Payouts show under **Banking > Disbursements** and **Disbursement Batches**, in the Disbursements data set and in the Disbursement workflow events.
- Until Connect is on in the Stripe dashboard, setting up payouts says so and creates nothing. Configure Stripe itself as described in `orchardcore-payments`.

## Custom Payout Provider

Implement `IVendorPayoutProvider` (an `IDisbursementProvider`, `CrestApps.OrchardCore.Banking.Disbursements.Services`) to pay vendors another way (bank file, PayPal). `SubmitAsync` receives a batch of `Disbursement`s: read the vendor from `Payee` (kind `MarketplaceConstants.VendorPartyKind`) and the order from `SourceId` (kind `PayoutSourceKind`), send each with `disbursement.IdempotencyKey`, and report each disbursement's `Statuses` (`DisbursementStatus.Settled`, `Submitted` or `Failed`), `TraceNumbers` and `FailureReasons` by `ItemId`. Register one instance as both services in a module that depends on Marketplace and Disbursements, and do not enable the Stripe Connect feature alongside it unless `MarketplaceSettings.PayoutProvider` names one.

```csharp
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace MyCompany.Marketplace.BankPayouts;

public sealed class BankFilePayoutProvider : IVendorPayoutProvider
{
    private readonly IBankTransferClient _bank;
    private readonly IVendorStore _vendors;

    internal readonly IStringLocalizer S;

    public BankFilePayoutProvider(IBankTransferClient bank, IVendorStore vendors, IStringLocalizer<BankFilePayoutProvider> stringLocalizer)
    {
        _bank = bank;
        _vendors = vendors;
        S = stringLocalizer;
    }

    public string Name => "BankFile";

    public LocalizedString DisplayName => S["Bank file"];

    public string Method => "BankFile";

    public async Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
    {
        var result = new DisbursementSubmitResult { Succeeded = true };

        foreach (var disbursement in disbursements)
        {
            var vendor = disbursement.Payee.Kind == MarketplaceConstants.VendorPartyKind
                ? await _vendors.FindByIdAsync(disbursement.Payee.Id, cancellationToken)
                : null;

            var sent = vendor is null
                ? null
                : await _bank.SendAsync(vendor.Email, disbursement.Amount, disbursement.Currency, disbursement.IdempotencyKey, cancellationToken);

            if (sent?.Succeeded == true)
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Settled;
                result.TraceNumbers[disbursement.ItemId] = sent.Reference;
            }
            else
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Failed;
                result.FailureReasons[disbursement.ItemId] = sent?.Error ?? S["Unknown vendor."].Value;
            }
        }

        return result;
    }
}

[RequireFeatures("CrestApps.OrchardCore.Marketplace", "CrestApps.OrchardCore.Banking.Disbursements")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<BankFilePayoutProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDisbursementProvider, BankFilePayoutProvider>(sp => sp.GetRequiredService<BankFilePayoutProvider>()));
        services.AddScoped<IVendorPayoutProvider>(sp => sp.GetRequiredService<BankFilePayoutProvider>());
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
| Nothing paid and the vendor page says Disbursements is off | Enable Disbursements (`CrestApps.OrchardCore.Banking.Disbursements`); payouts are disbursements |
| A refused payout | Find it under **Banking > Disbursements** with Stripe's reason; **Pay out what's ready** tries again with a new request |
| Set up payouts does nothing | Turn on Connect in the Stripe dashboard and enable the Stripe Connect feature |
| Automatic payouts never run | Enable `AutomaticPayouts`; the background task runs only for tenants that have served a request |

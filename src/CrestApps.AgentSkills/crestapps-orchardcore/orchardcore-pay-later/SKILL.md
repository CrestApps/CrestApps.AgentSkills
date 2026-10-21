---
name: orchardcore-pay-later
description: Skill for the CrestApps Orchard Core Pay Later module (CrestApps.OrchardCore.PayLater), the offline payment methods of the checkout. Covers the Pay Later, Bank transfer and Cash on delivery providers (keys pay-later, bank-transfer, cash-on-delivery) built on OfflineCheckoutPaymentProvider with DefersPayment, PayLaterSettings, BankTransferSettings and CashOnDeliverySettings under Settings > Commerce > Offline payments, net terms and due dates, PayLaterTransactionCheckoutHandler recording each commitment as an outstanding transaction, recurring Pay Later commitments and PayLaterRenewalBackgroundTask, why deferred providers never settle an existing balance, ICheckoutPaymentProviderAvailability and ICheckoutPaymentProviderDescription, the confirmation and order display drivers, and adding a custom offline method such as purchase orders. Use it when requests mention pay later, invoicing at checkout, bank transfer, cash on delivery, offline payments, or a balance owed after checkout.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Pay Later

Pay Later adds offline payment methods to the checkout. They record a promise to pay instead of moving money, through the same durable ledger and reconciliation as a gateway, and leave a balance in the Transactions ledger that the customer pays later (online from **My Transactions** or a pay link) or the store records by hand.

Related skills: `orchardcore-checkout` (providers, payment step), `orchardcore-transactions` (the balance, reminders, **Record payment**), `orchardcore-payments` (provider contracts), `orchardcore-subscriptions` (recurring plans on Pay Later), `orchardcore-carts` and `orchardcore-orders` (orders paid offline show *Payment due*).

## Feature

| Feature ID | Category | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.PayLater` (`PayLaterConstants.Features.Area`) | Commerce | `CrestApps.OrchardCore.Checkout`, `CrestApps.OrchardCore.Transactions` |

Install `CrestApps.OrchardCore.PayLater` in the web project. Enabling the feature offers nothing until a method is turned on in its settings.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "CrestApps.OrchardCore.PayLater" ],
      "disable": []
    },
    {
      "name": "Settings",
      "PayLaterSettings": { "Enabled": true, "NetTermDays": 30 },
      "BankTransferSettings": { "Enabled": true, "Instructions": "Contoso Bank, account 000123456, transit 00011.", "NetTermDays": 7 },
      "CashOnDeliverySettings": { "Enabled": false, "Instructions": "Pay the courier in cash or by card." }
    }
  ]
}
```

## Methods

| Provider | Key | Offered when | Settings (defaults) |
| --- | --- | --- | --- |
| `PayLaterCheckoutPaymentProvider` | `pay-later` | `PayLaterSettings.Enabled` | `Enabled` (false), `NetTermDays` (30; `0` means no due date) |
| `BankTransferCheckoutPaymentProvider` | `bank-transfer` | `Enabled` and `Instructions` set | `Enabled`, `Instructions` (bank details), `NetTermDays` (7) |
| `CashOnDeliveryCheckoutPaymentProvider` | `cash-on-delivery` | `Enabled` and the checkout has a `CheckoutShippingInfo` address | `Enabled`, `Instructions` |

All three derive from `OfflineCheckoutPaymentProvider`, which implements `ICheckoutPaymentProvider`, `ICheckoutPaymentProviderAvailability` and `ICheckoutPaymentProviderDescription`, with capabilities one-time only and `DefersPayment = true`. Verification reports a non-authoritative amount (`ReportsAuthoritativeAmount = false`), so the checkout records the commitment on its transaction id without an amount cross-check, in `GatewayMode.Testing` outside Production. Pay Later also implements `ICheckoutRecurringPaymentProvider`.

Settings live on **Settings > Commerce > Offline payments** (group `paylater`), guarded by `ManageTransactionSettings`.

## What happens at completion

`PayLaterTransactionCheckoutHandler` records, for every succeeded attempt of an `OfflineCheckoutPaymentProvider`, one outstanding `Transaction` per obligation: titled after the order (`ICheckoutReferenceDescriber`), `Source` equal to the provider key, the guest's name and email from `CheckoutContactInfo`, due `NetTermDays` after checkout. Recording is idempotent per obligation. Sources `pay-later`, `bank-transfer` and `cash-on-delivery` are registered with `AddTransactionSource`.

- `OfflinePaymentConfirmationDisplayDriver` shows the instructions and the reference on the confirmation, which says *your order is placed* instead of *paid*.
- With Orders, `OrderOfflinePaymentDisplayDriver` shows what is owed on the order: how to pay for the buyer, a **Record payment** link to the transaction for the store.

### Never for an existing balance

A deferred provider cannot pay off a balance that already exists (it would mark it paid with nothing received). A checkout with `SettlesExistingBalance` (paying a transaction from **My Transactions** or a pay link) never offers it, the engine refuses to begin it there, and settlement ignores deferred attempts.

### Recurring commitments

A Pay Later subscription records the cycle on the transaction (`TransactionRecurrence`: period, cycle number, cycle limit). `PayLaterRenewalBackgroundTask` (`*/30 * * * *`) invoices the next period once the current one ends, copying amount and tax forward, exactly once per cycle under a lock; canceled agreements and those that billed every cycle stop. A trial or delayed start records the first debt for the first real cycle.

## A custom offline method

Reference `CrestApps.OrchardCore.PayLater` (the base class is in the module) and register the provider both as `ICheckoutPaymentProvider` and as `OfflineCheckoutPaymentProvider`, so the Pay Later handler records its balance:

```csharp
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace Contoso.PurchaseOrders;

public sealed class PurchaseOrderCheckoutPaymentProvider : OfflineCheckoutPaymentProvider
{
    public const string ProcessorKey = "purchase-order";

    internal readonly IStringLocalizer S;

    public PurchaseOrderCheckoutPaymentProvider(
        IHostEnvironment hostEnvironment,
        IStringLocalizer<PurchaseOrderCheckoutPaymentProvider> stringLocalizer)
        : base(hostEnvironment)
    {
        S = stringLocalizer;
    }

    public override string Key => ProcessorKey;

    public override string DisplayName => S["Purchase order"];

    public override string Description => S["Place the order now; we invoice your company."];

    public override Task<DateTime?> GetDueUtcAsync(DateTime now)
        => Task.FromResult<DateTime?>(now.AddDays(45));

    public override Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrEmpty(session?.OwnerId));
}
```

```csharp
using Contoso.PurchaseOrders;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;

[RequireFeatures("CrestApps.OrchardCore.PayLater")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<PurchaseOrderCheckoutPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(sp => sp.GetRequiredService<PurchaseOrderCheckoutPaymentProvider>());
        services.AddScoped<OfflineCheckoutPaymentProvider>(sp => sp.GetRequiredService<PurchaseOrderCheckoutPaymentProvider>());

        // A panel for the payment step: an IDisplayDriver<CheckoutFlowPaymentMethod> grouped by the key.
        services.AddDisplayDriver<CheckoutFlowPaymentMethod, PurchaseOrderPaymentMethodDisplayDriver>();

        services.AddTransactionSource(PurchaseOrderCheckoutPaymentProvider.ProcessorKey, source =>
        {
            source.DisplayName = LocalizationSource.Create<Startup>("Purchase order");
        });
    }
}
```

The panel driver returns `View("PurchaseOrderCheckoutPaymentMethod", method).Location("Content").OnGroup(PurchaseOrderCheckoutPaymentProvider.ProcessorKey)`.

## Upgrading from 2.x

- New in 3.0 as a standalone module (no per-scenario sub-feature).
- **Settings > Commerce > Pay Later** became **Offline payments**; Pay Later is now off until `PayLaterSettings.Enabled` is set.
- A Pay Later commitment is a `DefersPayment` provider and is no longer accepted to settle an existing balance.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| Method not on the payment step | Not enabled in settings, bank transfer without instructions, cash on delivery without a shipping address, or the checkout settles an existing balance |
| No transaction after checkout | The checkout did not complete, or the method is not registered as `OfflineCheckoutPaymentProvider` |
| Recurring Pay Later invoiced once | Background tasks not running |
| Receipts or reminders never reach a guest | No email service, or the Contact step was off so no email was collected |

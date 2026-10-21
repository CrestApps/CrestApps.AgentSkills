---
name: orchardcore-checkout
description: Skill for the CrestApps Orchard Core Checkout framework (CrestApps.OrchardCore.Checkout, CrestApps.OrchardCore.Checkout.TestProvider, CrestApps.OrchardCore.Checkout.Api). Covers starting a checkout from a custom module with ICheckoutEngine and StartCheckoutRequest (ReturnUrl, ReturnText, Title, OwnerId), steps and BillingItem lines through ICheckoutHandler and CheckoutHandlerBase, completion, failure, cancel and expiry hooks, CheckoutFlowDisplayDriver, CheckoutSession and CheckoutInvoice, guest ownership, the payment ledger and compensation refunds, the Test payments provider, discounts before tax with ICheckoutDiscountProvider, the Coupon catalog and Coupons recipe step, coupons limited by CheckoutDiscountScope and on transaction payments, tenders with ICheckoutTenderProvider, payment gates and the Checkout API. Use it when requests mention Orchard Core checkout, taking a payment from a custom module, checkout steps, coupons, discount codes, test payments, or a checkout that does not complete.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Checkout

Checkout is the one path that takes money in the CrestApps commerce suite. Orders, subscriptions, transaction pay links, installment plans, bookings and the point of sale all start a checkout session through `ICheckoutEngine`; payment providers settle it; handlers decide what a completed checkout means. It is not a storefront: a consuming module contributes what is bought and reacts to the outcome.

Related skills: `orchardcore-commerce` (suite map and API conventions), `orchardcore-payments` (provider contracts, Stripe, refunds, installment plans), `orchardcore-transactions` (the ledger of money owed), `orchardcore-pay-later` (offline methods), `orchardcore-carts` and `orchardcore-orders` (the `Order` reference type), `orchardcore-taxation`, `orchardcore-shipping`, `orchardcore-customers` (Contact step, store credit, points).

## Features

| Feature ID | Name | Adds | Depends on |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Checkout` | Checkout | Engine, public checkout pages, payment endpoints, coupons, tenders, refund and reconciliation services, **Settings > Commerce > Checkout** | `CrestApps.OrchardCore.Commerce` |
| `CrestApps.OrchardCore.Checkout.TestProvider` | Test payments | Provider key `test` that confirms every payment without moving money | Checkout |
| `CrestApps.OrchardCore.Checkout.Api` | Checkout API | Checkout endpoints in the commerce JSON API (`api/commerce/v1`) | Checkout |

Feature IDs are constants on `CheckoutConstants.Features` (`Area`, `TestProvider`, `Api`). Install `CrestApps.OrchardCore.Checkout` and at least one provider module (Stripe, Pay Later) in the web/startup project. A module that only builds on checkout references `CrestApps.OrchardCore.Checkout.Abstractions` (contracts) and, for step drivers, `CrestApps.OrchardCore.Checkout.Core`.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Checkout.TestProvider"
      ],
      "disable": []
    }
  ]
}
```

Taxation needs no integration feature: with Taxation enabled a `[RequireFeatures]` startup replaces the no-op `ICheckoutTaxService`.

## Guidelines

- Every money-moving step goes through `ICheckoutEngine`. Never call a provider, `IPaymentAttemptStore` or the reconciliation service from your own controller, webhook or task.
- Identify your purchases with your own `ReferenceType` and filter on it in every handler: each `ICheckoutHandler` runs for every checkout on the tenant.
- Put what is charged on a `CheckoutFlowStep` as `BillingItem` lines in `ActivatingAsync`. A step with nothing to ask is `Conceal = true`; `Conceal` is `[JsonIgnore]`, so set it again in `InitializingAsync`.
- Fulfil in `CompletingAsync`. It runs only after every obligation is confirmed by its provider, inside the transaction that marks the session `Completed`. Throw to refuse: the writes roll back and the reconciliation sweep retries.
- Never mark your record paid from a session status. Mark it paid in `CompletingAsync`, and read your own record afterwards.
- Side effects that must not undo a paid checkout (emails, webhooks) belong in `CompletedAsync`, which swallows and logs exceptions.
- `StartCheckoutRequest.OwnerId` and the saved-card provider data keys are server-only; never take them from a request.
- `ReturnUrl` must be local (`Url.Action`, `Url.RouteUrl` or `~/`); anything else throws `ArgumentException`.
- All recipe JSON uses `{ "steps": [...] }`; all C# classes are `sealed` except view models.

## Starting a checkout and handling the outcome

The shortest complete flow, verified against the Transactions pay-link controller, `OrderCheckoutHandler` and `TransactionSettlementCheckoutHandler`. A full module (manifest, record, controller, handler, return page, tests) is in [references/custom-checkout-example.md](references/custom-checkout-example.md).

```csharp
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Mvc;

// In a controller action:
var session = await _checkoutEngine.StartAsync(new StartCheckoutRequest
{
    ReferenceType = "RegistrationFee",
    ReferenceId = fee.Id,
    ReturnUrl = Url.Action(nameof(Show), new { feeId = fee.Id }),
    ReturnText = S["Back to my registration"],
    Title = S["Registration fee received"],
}, HttpContext.RequestAborted);

return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
{
    sessionId = session.SessionId,
    step = session.CurrentStep,
});
```

```csharp
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using OrchardCore.Entities;

// Constructor (IRegistrationFeeStore _fees, IClock _clock, IStringLocalizer S) omitted; see the reference.
public sealed class RegistrationFeeCheckoutHandler : CheckoutHandlerBase
{
    private const string StepKey = "RegistrationFee";

    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (session.ReferenceType != "RegistrationFee")
        {
            return;
        }

        var fee = await _fees.FindByIdAsync(session.ReferenceId);

        if (fee is null || fee.Status == RegistrationFeeStatus.Paid)
        {
            // Nothing is billed. The full example also refuses such a checkout in CompletingAsync.
            return;
        }

        session.Currency = fee.Currency;
        session.Steps.Add(new CheckoutFlowStep
        {
            Key = StepKey,
            Title = S["Registration fee"],
            Order = 0,
            Conceal = true,
            BillingItems =
            [
                new BillingItem { ItemId = fee.Id, Description = fee.Title, Amount = fee.Amount },
            ],
        });
    }

    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        foreach (var step in context.Flow.Session.Steps.Where(step => step.Key == StepKey))
        {
            step.Conceal = true;
        }

        return Task.CompletedTask;
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session is not CheckoutSession { ReferenceType: "RegistrationFee" } session)
        {
            return;
        }

        // Every obligation is confirmed at this point. Throwing rolls the completion back.
        var fee = await _fees.FindByIdAsync(session.ReferenceId)
            ?? throw new InvalidOperationException($"Checkout '{session.SessionId}' pays for a missing fee.");

        fee.Status = RegistrationFeeStatus.Paid;
        fee.PaidUtc = _clock.UtcNow;
        fee.CheckoutSessionId = session.SessionId;

        // Written through the request's YesSql session; the engine commits it with the completion.
        await _fees.SaveAsync(fee);
    }
}

// Startup: services.AddScoped<ICheckoutHandler, RegistrationFeeCheckoutHandler>();
```

The Payment step is added for you (always last). The customer pays, lands on `Checkout/Confirmation/{sessionId}` headed with your `Title`, and **Continue** returns them to `ReturnUrl`.

### Handler lifecycle

| Hook | When | Use it to |
| --- | --- | --- |
| `ActivatingAsync(CheckoutFlowActivatingContext)` | Once, while `StartAsync` creates the session (`context.Session` is the `CheckoutSession`) | Add steps, billing items, `session.Currency`, `session.Put(...)` data |
| `ActivatedAsync` | After activation; the invoice is built | Read the first invoice |
| `InitializingAsync` / `InitializedAsync` | Every load | Re-conceal steps, per-request decisions (signed in or not) |
| `LoadingAsync` / `LoadedAsync` | Every load of the current step | Guard step transitions |
| `CompletingAsync` | All obligations confirmed, inside the completing transaction | Fulfil; throw to refuse and retry later |
| `CompletedAsync` | After the `Completed` status is committed | Notifications; failures are logged only |
| `FailedAsync` | Verification reports an obligation failed, session `Failed` (a decline at begin leaves the session open for another card) | Release what you held |
| `CanceledAsync(context.Reason)` | `ICheckoutEngine.CancelAsync` | Release what you held |
| `ExpiredAsync` | The sweep expired an abandoned session | Release what you held |

`CheckoutFlowContextBase.Flow.Session` is an `ICheckoutFlowSession`; cast it to `CheckoutSession` to read `ReferenceType`, `ReferenceId`, `OwnerId`, `SettlesExistingBalance` and the property bag. Read the invoice with `session.TryGet<CheckoutInvoice>(out var invoice)` (`OrchardCore.Entities`).

### `ICheckoutEngine`

| Member | Returns | Notes |
| --- | --- | --- |
| `StartAsync(StartCheckoutRequest)` | `CheckoutSession` (`Pending`) | Runs activating handlers, builds the invoice, saves |
| `BeginPaymentAsync(sessionId, BeginPaymentOptions)` | `PaymentBeginOutcome` | One durable `PaymentAttempt` per obligation, written before the provider is called; `Declined` and `ProviderErrorMessage` on a decline; `InvoiceChanged` when a gate rebuilt the invoice |
| `TryCompleteAsync(sessionId)` | `CheckoutCompletionResult` | Never blocks; `Status` is `Completed`, `AlreadyCompleted`, `Pending`, `Blocked` (`BlockingStepKey`), `Failed`, `Canceled` or `NotFound`; `IsCompleted` covers the first two |
| `CancelAsync(sessionId, reason)` | `CheckoutCompletionResult` | Cancels pending attempts at the provider |
| `ExpireAsync(sessionId)` | `CheckoutCompletionResult` | A session that already collected money is never expired |

`StartCheckoutRequest` also carries `ReferenceVersionId`, `Contact` (`CheckoutContactInfo` for a guest), `PriceSelection` (applied before the flow is built) and `OwnerId` (an administrator or background task paying for a customer). `CheckoutSessionStatus` is `Pending`, `AwaitingProvider`, `PaymentPending`, `Completed`, `Failed`, `Canceled` or `Expired`.

A server-driven charge (no browser) is `StartAsync` with `OwnerId`, `BeginPaymentAsync` with `CheckoutPaymentDataKeys.ForOffSessionCharge(savedPaymentMethod)`, then `TryCompleteAsync`; cancel the session when the begin fails. The engine commits while charging, so reload any document you loaded before the call instead of saving the stale copy. See [references/custom-checkout-example.md](references/custom-checkout-example.md).

## Sessions, invoice and ledger

- `CheckoutSession` (an `Entity`): `SessionId`, the reference triple, `Currency`, `Status`, `OwnerId`, `Steps`, `SavedSteps` (`JsonObject`), `CurrentStep`, `SettlesExistingBalance`, `AcceptsDiscountsOnBalance`, `SavePaymentMethod`, `AnonymizedUtc`. Property bag entries include `CheckoutInvoice`, `CheckoutContactInfo`, `CheckoutShippingInfo`, `CheckoutReturnInfo`, `CheckoutDiscountScope`, `CheckoutTenderSelection`.
- `ICheckoutSessionStore`: `GetAsync` (ownership-checked, for customer-facing pages), `GetByReferenceAsync` (newest session for a reference), `ListByReferenceAsync`, `GetByCustomerAsync`, `GetManyAsync`. The last four do not enforce ownership; never resume a customer flow from them.
- `CheckoutInvoice` is rebuilt, never patched: `LineItems`, `Currency`, `InitialPaymentAmount`, `FirstRecurringPaymentAmount`, `Subtotals` by billing interval, `TaxAmount`, `TaxLines`, `TaxSnapshot`, `Discounts`, `Tenders`, `GrandTotal`, computed `AmountDue`. Every step is billed, including concealed ones. The currency comes from the session (set it in `ActivatingAsync`), else **Settings > Commerce > Checkout**.
- `BillingItem`: `ItemId`, `Description`, `Amount`, `Quantity`, `Plan` (`RecurringPlan` for a recurring line), `PriceId`, `ExcludeFromTax`, `ExcludeFromDiscounts`, `Sku`, tax codes, `ProductId`, `CategoryIds`.
- The ledgers live in Transactions: `PaymentAttempt` and `PaymentRefund` (`CrestApps.OrchardCore.Transactions.Models`) with `IPaymentAttemptStore` and `IPaymentRefundStore` (`CrestApps.OrchardCore.Transactions.Services`). `PaymentAttemptState` is `Created`, `Pending`, `Succeeded`, `Failed` or `Canceled`.
- `CheckoutReferenceTypes.Order` (`"Order"`) is the canonical order reference; `TransactionsConstants.ReferenceTypes.Transaction` (`"Transaction"`) settles a transaction.

Safety rules the engine enforces: an attempt is persisted before the provider is contacted; the provider reference is stored as soon as it returns; completion is driven from the browser poll, the provider webhook and the reconciliation sweep (`CheckoutReconciliationBackgroundTask`, every 5 minutes) under a session lock; a failing completing handler leaves the session `PaymentPending` for retry, never refunds; when one obligation fails the settled ones are refunded through `ICheckoutRefundService` (compensation, recorded in the refund ledger first). Money is compared with `Money.AreEqual` and rounded with `Money.Round` (`CrestApps.OrchardCore.Payments`). Abandoned sessions expire after `CheckoutSettings.SessionLifetimeHours` (24).

## Custom steps

Derive from `CheckoutFlowDisplayDriver` (`CrestApps.OrchardCore.Checkout.Core`) and register it with `services.AddDisplayDriver<CheckoutFlow, MyStepDriver>()`. It renders only while `StepKey` is the current step. Override `EditStepAsync` and `UpdateStepAsync`; on a valid post write `flow.Session.SavedSteps[StepKey] = new JsonObject { ... }`. The controller does not mark a step saved: the engine refuses completion (`Blocked`, `BlockingStepKey`) while a visible `CollectData = true` step has no `SavedSteps` entry, and the one-page layout keeps the payment section hidden. Shapes go in `Content` (and `Aside` on the payment step).

Built-in step keys: `CheckoutConstants.PaymentStepKey` (`Payment`), `CustomersConstants.ContactStepKey` (`Contact`), `TransactionsConstants.SettlementStepKey` (`TransactionSettlement`), `SubscriptionConstants.InstallmentPlans.CheckoutStepKey` (`ChooseHowToPay`), `company`.

## Public checkout

| Route | Name (`CheckoutConstants.RouteNames`) |
| --- | --- |
| `GET/POST Checkout/{sessionId}/{step?}` | `Step` (`CheckoutStep`) |
| `GET Checkout/Confirmation/{sessionId}` | `Confirmation` |
| `POST checkout/{sessionId}/payment/begin` | `BeginPayment` |
| `POST checkout/{sessionId}/payment/status` | `PaymentStatus` |

- Pages resolve the session through the ownership-checked store. A guest checkout gets a 32-byte random token in the data-protected, HTTP-only `checkout_owner` cookie; only its SHA-256 hash is stored, compared in fixed time. IP and user agent are audit fields only.
- The payment JSON endpoints are rate limited, require a same-origin request (`Sec-Fetch-Site` or `Origin`), and drop `savePaymentMethod`, `offSession`, `savedCustomerReference` and `savedPaymentMethodReference` from browser provider data.
- Rate-limit groups for Orchard Core Rate Limiting: `checkout` and `checkout-payment` (`CheckoutConstants.RateLimitGroups`).
- A completed session redirects to its confirmation; a canceled, expired or failed one shows *This checkout is no longer available.* with the **Back** link.
- `CheckoutSettings` (**Settings > Commerce > Checkout**, group `checkout`): `DefaultPaymentMethod`, `Currency` (`USD`), `SessionLifetimeHours` (24), `Layout` (`Steps` or `OnePage`). Override the `CheckoutFlow` shape in a theme to restyle the frame.
- Nothing to pay (a free item, a 100% code, a gift card that covers it) completes with **Place order** and no provider.

## Test payments

`CrestApps.OrchardCore.Checkout.TestProvider` registers `TestCheckoutPaymentProvider` (key `test`) only in the Development environment, or when `CrestApps:Checkout:TestProvider:AllowOutsideDevelopment` is `true` in the tenant configuration (read at tenant start). It implements one-time, recurring, refund and saved-card contracts, reports amounts as authoritative in `GatewayMode.Testing`, and can settle an existing balance.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Checkout": {
        "TestProvider": {
          "AllowOutsideDevelopment": true
        }
      }
    }
  }
}
```

| Amount ends in (minor units, tax included) | Outcome |
| --- | --- |
| `02` | Declined at begin; the attempt fails |
| `05` | Pending for two minutes (`PendingSettlementDelay`), then the sweep completes it |
| anything else | Succeeds |

Pay a checkout in an automated test with two requests (carry the auth or `checkout_owner` cookie; a client that sends no `Origin` header passes the same-origin check):

```http
POST /checkout/{sessionId}/payment/begin
Content-Type: application/json

{ "providerKey": "test" }
```

```http
POST /checkout/{sessionId}/payment/status
```

The second returns `{ "status": "Completed", "completed": true, ... }` once the handlers have run.

## Discounts and coupons

Coupons are part of the Checkout feature (not Marketing): `Coupon` is in Checkout.Abstractions, managed under **Commerce > Coupons** (`ManageCoupons`), and moved with the `Coupons` recipe and deployment steps. The full rules, a custom `ICheckoutDiscountProvider`, tenders and payment gates are in [references/coupons-and-tenders.md](references/coupons-and-tenders.md).

- **Discounts run before tax.** `ICheckoutDiscountService` applies every `ICheckoutDiscountProvider` to the untaxed invoice, spreads each discount over the lines it applies to (`CheckoutLineItem.DiscountAmount`), and tax sees each line's share (`TaxableItem.DiscountAmount`). A one-time coupon never touches shipping; free shipping touches only shipping; a first-cycle coupon reduces only the first recurring charge. Totals never go below zero, and a fixed amount applies only in its own currency.
- **Kinds and limits.** `CouponKind` is `Percentage`, `FixedAmount`, `FreeShipping` or `BuyXGetY`; `Method` is `Code` or `Automatic`. Limits: validity window, minimum and maximum amount, minimum quantity, total and per-customer uses, products and categories, combining rules, up to five codes per checkout.
- **Who and what it is for.** `Coupon.Sources`, `ReferenceTypes`, `CustomerId` and `CustomerEmail` limit a code to transaction sources, reference types or one customer. They are matched against the session and the `CheckoutDiscountScope` the module that knows what is paid stores on it (Transactions stores the transaction's source, reference type, owner and guest email). A scoped coupon is refused when the checkout cannot answer (fail closed).
- **On a transaction payment.** A checkout that settles an existing balance accepts codes only when `AcceptsDiscountsOnBalance` is set (Transactions sets it). Each one-time discount becomes a `Discount` adjustment on the transaction, and the code is redeemed only once nothing is owed, so a 100% code settles the balance with no payment.
- **Uses are held, then consumed on purchase.** `CouponHoldPaymentGate` holds a use before payment begins; the redemption is recorded on completion and released when the checkout ends unpaid, so an abandoned checkout never burns a single-use code and the last use cannot be taken twice.

## Tenders and payment gates

Tenders (gift cards, store credit, points, company terms) pay after tax through `ICheckoutTenderProvider` and `ICheckoutTenderLedger`; they never pay a recurring obligation or an existing balance. `ICheckoutPaymentGate` runs inside the session lock before a payment begins and can `Refuse` it or flag `InvoiceChanged` so the page reloads with the new total.

```json
{
  "steps": [
    {
      "name": "Coupons",
      "Coupons": [
        { "Code": "SPRING10", "Description": "Spring sale", "Kind": "Percentage", "Percentage": 10, "MaxRedemptions": 500, "MaxRedemptionsPerCustomer": 1, "EndsUtc": "2027-06-01T00:00:00Z", "IsEnabled": true },
        { "Code": "SHIPFREE", "Kind": "FreeShipping", "MinimumAmount": 75, "IsEnabled": true }
      ]
    }
  ]
}
```

## Checkout API

`CrestApps.OrchardCore.Checkout.Api` maps into `api/commerce/v1`: `GET checkout/{id}`, `POST checkout/{id}/codes`, `DELETE checkout/{id}/codes/{code}`, `GET checkout/{id}/payment-methods` (each with the `client` dictionary from `ICheckoutPaymentClientConfigurationProvider`), `POST checkout/{id}/payment`, `POST checkout/{id}/complete`, `DELETE checkout/{id}`, plus staff `coupons` endpoints. A guest's checkout travels in `X-Checkout-Token`. Conventions (auth, idempotency, problem codes) are in `orchardcore-commerce`.

## Permissions

| Permission | Grants |
| --- | --- |
| `ManageCheckoutSettings` | **Settings > Commerce > Checkout** |
| `ManageCoupons` | **Commerce > Coupons** and the coupon API (security critical) |

Payments and refunds screens use `ManageRefunds` from Transactions.

## Upgrading from 2.x

- There was no released checkout in 2.x; every purchase now runs through `ICheckoutEngine`. Subscriptions no longer has its own signup pipeline (see `orchardcore-subscriptions`).
- `PaymentAttempt`, `PaymentRefund`, their stores and indexes moved from `CrestApps.OrchardCore.Checkout.Models` to `CrestApps.OrchardCore.Transactions.Models` and `CrestApps.OrchardCore.Transactions.Core`; `CrestApps.OrchardCore.Commerce.Abstractions` is gone. Stored data is unchanged.
- Money is `decimal` everywhere; the `double` overloads were removed.
- The default payment method moved to the Checkout settings.
- Guest sessions are owned by the `checkout_owner` token, not by IP and user agent.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| Checkout totals `0.00` and completes for nothing | Billing items were added to no step, or the invoice was built from an older version; add them on a step in `ActivatingAsync` |
| Payment step never reached | A visible `CollectData` step has no `SavedSteps` entry |
| Session stuck in `PaymentPending` | A completing handler throws; check the log, it is retried every sweep |
| `ArgumentException` from `StartAsync` | `ReturnUrl` is absolute or protocol-relative |
| Test method not offered | Not Development and `AllowOutsideDevelopment` is not set; a warning is logged at startup |
| Begin endpoint returns 403 | Cross-site request (`Origin` host or `Sec-Fetch-Site` mismatch) |
| Charged twice concern | Begin is idempotent per obligation; a refreshed page resumes the same attempts |

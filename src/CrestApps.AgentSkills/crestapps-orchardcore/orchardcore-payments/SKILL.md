---
name: orchardcore-payments
description: Skill for the CrestApps Orchard Core payment foundations and the Stripe provider (CrestApps.OrchardCore.Payments.Abstractions, CrestApps.OrchardCore.Stripe) plus Subscriptions - Installment Plans (CrestApps.OrchardCore.Subscriptions.Installments). Covers Money and CurrencyScale, IPaymentEvent and PaymentEventBase, adding a gateway with ICheckoutPaymentProvider, PaymentProviderCapabilities, ICheckoutPaymentRefundProvider, ICheckoutRecurringPaymentProvider and ICheckoutSavedPaymentMethodProvider, refunds through ICheckoutRefundService, Stripe settings, the CrestApps_Stripe configuration section, the StripeSettings recipe step, the stripe/webhook endpoint, Stripe workflow events, saved cards and wallets, and installment plans with IInstallmentPlanService, IInstallmentOptionProvider and InstallmentPlanSettings. Use it when requests mention Stripe, a payment gateway, refunds, webhooks, saved cards or payment plans in CrestApps Orchard Core commerce.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Payments and Stripe

Payments are provider-neutral. A gateway is an `ICheckoutPaymentProvider` registered on the tenant; the checkout engine drives it, the durable ledger records it, and every consumer stays unaware of which gateway took the money. Stripe is the shipped card gateway. Installment plans (a Subscriptions sub-feature) build on the same contracts to keep a card and charge it on a schedule.

Related skills: `orchardcore-checkout` (the engine, steps, test provider), `orchardcore-transactions` (ledger screens, refunds console), `orchardcore-pay-later` (offline providers), `orchardcore-subscriptions` (recurring agreements), `orchardcore-taxation`.

## Features and packages

| Feature ID | Name | Depends on | Notes |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Stripe` | Stripe (Payment Providers) | `CrestApps.OrchardCore.Users` | With Checkout enabled it registers the card provider (key `Stripe`) automatically; with Workflows it adds Stripe events; with Point of Sale it adds `stripe-terminal` |
| `CrestApps.OrchardCore.Subscriptions.Installments` | Subscriptions - Installment Plans | Transactions, Checkout, `OrchardCore.Users` | Does not need the Subscriptions feature or Products; see [references/installment-plans.md](references/installment-plans.md) |

`CrestApps.OrchardCore.Payments.Abstractions` is a contracts package (namespace `CrestApps.OrchardCore.Payments`): `Money`, `CurrencyScale`, `GatewayMode` (`Live`, `Testing`), `IPaymentEvent`, `PaymentEventBase`, the event contexts and `RemoteSubscriptionStatus`. Provider contracts are in `CrestApps.OrchardCore.Checkout.Abstractions` (`CrestApps.OrchardCore.Checkout.Services`). Install `CrestApps.OrchardCore.Stripe` in the web/startup project.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Stripe"
      ],
      "disable": []
    }
  ]
}
```

## Guidelines

- Amounts are `decimal` everywhere. Compare with `Money.AreEqual` and `Money.IsGreaterThan`, round with `Money.Round(amount, currency)`, and convert to minor units only at the gateway boundary (`Money.ToMinorUnits`, `StripeCurrency`). `CurrencyScale.GetDecimalPlaces` knows JPY (0) and KWD (3).
- Providers are tax-agnostic: they charge the amount the checkout gives them (tax included) and report the net and tax split back in `PaymentVerificationResult`.
- Never record a payment from a webhook alone. A webhook is a hint; the engine verifies through `VerifyAsync` against the gateway's API.
- Never refund by calling a gateway. Use `ICheckoutRefundService` so the refund is recorded first, capped at what remains, taxed from the original snapshot and locked against concurrent refunds.
- Only server code sets `savePaymentMethod`, `offSession`, `savedCustomerReference` and `savedPaymentMethodReference`.
- Read Stripe options through `IOptionsMonitor<StripeOptions>.CurrentValue`; they refresh without a tenant restart when settings are saved.
- All C# classes are `sealed` except view models; recipes use `{ "steps": [...] }`.

## Adding a gateway

1. Implement `ICheckoutPaymentProvider` under a unique `Key`: `DisplayName`, `Capabilities`, `BeginAsync`, `VerifyAsync`, `CancelAsync`.
2. Declare honest `PaymentProviderCapabilities`: `SupportsOneTimePayments`, `SupportsRecurringPayments`, `SupportsHostedCheckout`, `SupportsEmbeddedElements`, `SupportsCombinedOneTimeAndRecurring`, `CollectsTaxDynamically`, `SupportsRefunds`, `SupportsSavedPaymentMethods`, `DefersPayment`. A provider that claims refunds must implement `ICheckoutPaymentRefundProvider` (an architecture test checks); one that claims recurring support must implement `ICheckoutRecurringPaymentProvider` under the same key or the engine refuses it.
3. Render its panel on the payment step with an `IDisplayDriver<CheckoutFlowPaymentMethod>` whose result is `.OnGroup(providerKey)`.
4. Optionally implement `ICheckoutPaymentProviderAvailability` (rule itself out of a checkout), `ICheckoutPaymentProviderDescription` (line under the name), `ICheckoutSavedPaymentMethodProvider` (installments, off-session charges) and `ICheckoutPaymentClientConfigurationProvider` (publishable values for API clients, never secrets).
5. Translate the gateway's webhooks into `IPaymentEvent` calls and complete through `ICheckoutEngine.TryCompleteAsync`.

A complete provider with refunds and its panel is in [references/custom-payment-provider.md](references/custom-payment-provider.md).

| Result type | Use |
| --- | --- |
| `PaymentBeginResult.Success(reference)` | Begun; set `ClientSecret`, `RedirectUrl` or `RequiresAction` for the browser |
| `PaymentBeginResult.Decline(message, reference)` | The gateway refused the card; the attempt fails and the next try gets a new idempotency key |
| `PaymentBeginResult.Failure(message)` | Could not start (network, configuration) |
| `PaymentVerificationResult` | `Status` (`PaymentStatus.Succeeded`, `Failed`, `Unknown`), `TransactionId`, `ReportsAuthoritativeAmount`, `Amount`, `TaxAmount`, `Currency`, `GatewayMode`, `RiskLevel`, `RiskScore` |
| `PaymentRefundResult.Success / Pending / Failed` | Refund outcome with the provider's refund reference |

`BeginPaymentContext` carries the `Session`, the durable `Attempt` (`ExpectedAmount`, `ExpectedTaxAmount`, `Currency`, `IdempotencyKey`, `ObligationId`), the `Invoice`, return and cancel URLs and the sanitized `ProviderData`. Use `Attempt.IdempotencyKey` on every mutating gateway call.

## Payment events

`IPaymentEvent` (derive from `PaymentEventBase`) is the normalized stream every gateway raises: `PaymentSucceededAsync`, `PaymentIntentSucceededAsync`, `CustomerSubscriptionCreatedAsync`, `PaymentFailedAsync`, `PaymentCanceledAsync`, `PaymentRefundedAsync`, `PaymentDisputeCreatedAsync`, `PaymentDisputeUpdatedAsync` (default method), `SubscriptionPaymentFailedAsync` (a renewal failed) and `SubscriptionStatusChangedAsync` (with `RemoteSubscriptionStatus`; `Unknown` means leave local state alone). Contexts carry `GatewayId`, `GatewayMode` and a `Data` bag; each method takes a `CancellationToken`.

```csharp
using CrestApps.OrchardCore.Payments;

public sealed class DisputeAlertPaymentEvent : PaymentEventBase
{
    public override Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context, CancellationToken cancellationToken = default)
    {
        // Alert the finance team. Reconcile against durable state; never mark anything paid here.
        return Task.CompletedTask;
    }
}

// services.AddScoped<IPaymentEvent, DisputeAlertPaymentEvent>();
```

## Refunds

`ICheckoutRefundService.RequestRefundAsync(new RequestPaymentRefundContext { SessionId, OriginalTransactionId, Amount, Reason })` returns the durable `PaymentRefund` (`RefundStatus`: `Requested`, `Pending`, `Succeeded`, `Failed`, `Canceled`, `PendingManualReview`). It resolves the settled attempt, refuses more than remains, derives refunded tax from the original `TaxSnapshot` through `ITaxRefundCalculator`, persists before calling the provider, and serializes refunds of one payment with `IDistributedLock`. A provider without `ICheckoutPaymentRefundProvider` gets `PendingManualReview`, closed by an operator under **Commerce > Refunds**.

Refunds observed at the gateway (for example from the Stripe dashboard) flow through `ICheckoutRefundReconciliationService`: matched by provider reference, idempotency key, then an open request of the same amount; an unmatched one is quarantined as `PendingManualReview` with `remote_refund_without_local_request`. For a transaction, prefer `ITransactionCommandService.RefundAsync` (see `orchardcore-transactions`); for an order, the order's **Manage** box. React to finished refunds with `IPaymentRefundHandler.RefundSucceededAsync`.

## Stripe

### Settings and connection

**Settings > Payments > Stripe** (group `stripe`, permission `ManageStripeSettings`). Choose **Enable Production** (off is test mode), paste the secret and publishable keys, **Save**, then **Connect**: the key is verified, the account id stored, a webhook to `/stripe/webhook` created with its signing secret, and the site domain registered for wallets (skipped on `localhost`). **Disconnect** deletes the provisioned webhook and clears stored credentials. `StripeSettings`: `IsLive`, `Live|Test` + `PublishableKey`, `PrivateSecret`, `WebhookSecret`, `AccountId`, `WebhookId`. Secrets are data-protected.

After upgrading, click **Connect** again so the auto-provisioned webhook subscribes to the newer events (subscription lifecycle, refunds, disputes).

### Configuration from appsettings or the environment

The optional `CrestApps_Stripe` section (`StripeConfigurationOptions`, read per tenant from `IShellConfiguration` at tenant start) wins over the settings page one value at a time:

```json
{
  "OrchardCore": {
    "CrestApps_Stripe": {
      "IsLive": false,
      "Test": {
        "PublishableKey": "pk_test_...",
        "SecretKey": "sk_test_...",
        "WebhookSecret": "whsec_..."
      }
    }
  }
}
```

```text
OrchardCore__CrestApps_Stripe__IsLive=false
OrchardCore__CrestApps_Stripe__Test__SecretKey=sk_test_...
OrchardCore__Contoso__CrestApps_Stripe__Test__SecretKey=sk_test_...
```

Configured values show read-only on the page and are never written to the database. With a configured webhook secret, **Connect** does not create a webhook; register `POST /stripe/webhook` in the Stripe dashboard with that secret.

### Recipe and deployment

`StripeSettings` has its own recipe and deployment step (not the `Settings` step). The deployment step exports no secrets and no account or webhook ids. The recipe step protects clear-text secrets, keeps stored secrets it does not carry, checks each key's prefix for its mode (`pk_test_`, `sk_live_`, `whsec_`) and ignores account and webhook ids.

```json
{
  "steps": [
    {
      "name": "StripeSettings",
      "Settings": {
        "IsLive": false,
        "TestPublishableKey": "pk_test_...",
        "TestPrivateSecret": "sk_test_...",
        "TestWebhookSecret": "whsec_..."
      }
    }
  ]
}
```

### What the provider does

- Card payments use Stripe Elements on the payment step: `BeginAsync` creates an unconfirmed PaymentIntent for the attempt's gross amount and returns its client secret; `VerifyAsync` reads the PaymentIntent (with Radar risk) from Stripe. The hosted-checkout mode is gone.
- Recurring obligations become real Stripe subscriptions with inline prices (`StripeRecurringPaymentProvider`); one Stripe customer is resolved per checkout so a setup fee and a plan share the card. Cancel, pause (`pause_collection`) and resume reach Stripe.
- Saved cards: `savePaymentMethod` (installments) and `offSession` charges; buyer wallets through `IStripeWalletService` with `savedCardId` and `rememberCard` provider data, listed at `account/payment-methods`.
- Apple Pay and Google Pay through the Express Checkout Element when the device has a wallet, the domain is registered and the page is HTTPS.
- Webhook `POST /stripe/webhook`: signature verified first, one distributed lock and a processed-event record per event, handler writes committed with the marker; `500` asks Stripe to retry, `409` on lock contention. Events mapped: `invoice.payment_succeeded`, `invoice.payment_failed`, `customer.subscription.created|updated|deleted`, `payment_intent.succeeded|payment_failed|canceled`, `charge.refunded`, `refund.created|updated|failed`, `charge.refund.updated`, `charge.dispute.created|updated|closed`.
- Workflow events (category **Stripe**, with `OrchardCore.Workflows`): `StripePaymentReceivedEvent`, `StripeSubscriptionCreatedEvent`, `StripePaymentIntentSucceededEvent`, `StripePaymentFailedEvent`, `StripePaymentCanceledEvent`, `StripePaymentRefundedEvent`, `StripeDisputeCreatedEvent`, `StripeRequestFailedEvent` (401, 403 or no connection; throttled to once per five minutes per tenant).

### Local development

```sh
stripe login
stripe listen --forward-to https://localhost:5001/stripe/webhook --skip-verify
stripe trigger payment_intent.succeeded
```

Paste the printed `whsec_` into **Test Webhook Signing Secret** (Advanced) or set `CrestApps_Stripe:Test:WebhookSecret`. For automated tests without Stripe use the Test payments provider (`orchardcore-checkout`). On several nodes enable `OrchardCore.Redis.Lock` and `OrchardCore.Redis.Cache` so locks and payment signals are shared.

## Upgrading from 2.x

- `PaymentMethodOptions`, `PaymentMethod` and the subscription-specific Stripe endpoints are gone; payment methods are the registered `ICheckoutPaymentProvider`s and their panels are `IDisplayDriver<CheckoutFlowPaymentMethod>` (not `SubscriptionFlowPaymentMethod`).
- Money moved from `double` to `decimal`; the `double` overloads of `StripeCurrency` were removed.
- `StripePriceSyncService` and **Sync prices** were removed; prices are sent inline.
- Stripe's *Checkout Mode* setting was removed (Elements only).
- `IOptions<StripeOptions>` readers must switch to `IOptionsMonitor<StripeOptions>`.
- Reconnect Stripe after upgrading.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Settings page denied to a role with **Manage Stripe Settings** | Fixed in 3.0 by `AddSiteSettingsPermission`; update |
| Renewals or cancellations never recorded | Click **Connect** again to re-provision the webhook events |
| Webhook returns 400 | Wrong signing secret for the mode, or a configured secret shadowing a provisioned one |
| Card form missing | Publishable key not set for the active mode |
| Wallet buttons missing | Domain not registered, not HTTPS, or nothing due now |
| *payment_method belongs to the Customer* | Fixed in 3.0: one customer per checkout |

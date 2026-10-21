---
name: orchardcore-subscriptions
description: Skill for the CrestApps Orchard Core Subscriptions module (CrestApps.OrchardCore.Subscriptions, CrestApps.OrchardCore.Subscriptions.Tenants, CrestApps.OrchardCore.Subscriptions.Installments). Covers plans with the Subscription stereotype and SubscriptionPart (billing cycle, setup fee, cycle limit, delayed start, TrialDays), signup through the shared checkout at Subscription/Signup, the durable Subscription agreement and SubscriptionStatus, ISubscriptionLifecycleService transitions, dunning grace days, renewals and cancellation reaching the gateway, SubscriptionEntitlementPart roles, ISubscriptionAccessService and ISubscriptionEntitlementApplier, ISubscriptionLifecycleHandler, subscription workflow events, selling Orchard Core sites with TenantOnboardingPart and durable provisioning jobs, and the removal of the session-based signup. Use it when requests mention selling recurring plans, memberships by subscription, tenant onboarding, subscription access, or subscription upgrade work in CrestApps commerce.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Subscriptions

Subscriptions sells recurring plans built on content items and keeps a durable **agreement** for every subscriber. It owns no payment code: a plan is bought through the shared checkout like any other purchase, and the agreement is created only from payment attempts the checkout confirmed.

Related skills: `orchardcore-checkout` (the purchase), `orchardcore-payments` (Stripe recurring billing, installment plans), `orchardcore-pay-later` (offline recurring commitments), `orchardcore-products` (plans are products), `orchardcore-memberships` (one-off role grants from an order), `orchardcore-taxation`, `orchardcore-reports`.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Subscriptions` | Subscriptions | `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Title`, `CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Checkout`, `CrestApps.OrchardCore.Receipts` |
| `CrestApps.OrchardCore.Subscriptions.Tenants` | Subscriptions - Sites (default tenant only) | Subscriptions, `OrchardCore.Tenants` |
| `CrestApps.OrchardCore.Subscriptions.Installments` | Subscriptions - Installment Plans | Transactions, Checkout, `OrchardCore.Users` (see `orchardcore-payments`) |

Payment options come from provider modules: enable `CrestApps.OrchardCore.Stripe` and/or `CrestApps.OrchardCore.PayLater`. There is no *Subscriptions - Stripe* feature. Install `CrestApps.OrchardCore.Subscriptions` in the web project; contracts are in `CrestApps.OrchardCore.Subscriptions.Abstractions` (`CrestApps.OrchardCore.Subscriptions.Services` and `.Models`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Subscriptions",
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.Products.Listing"
      ],
      "disable": []
    }
  ]
}
```

The **Commerce starter** recipe (`CommerceStarter`) enables the suite, creates a `Member` role, a **Membership Plan** type with an entitlement, a `$10` monthly plan and a **Plans** listing at `/plans`.

## Guidelines

- Ask `ISubscriptionAccessService` whether a user has access; never test `Subscription.Status`. A canceled agreement is current until its paid period ends and a past-due one through its grace window.
- Change an agreement only through `ISubscriptionLifecycleService`; it locks the agreement, re-reads it and is idempotent.
- Grant something other than a role with an `ISubscriptionEntitlementApplier`; failures are logged and never roll back the transition.
- Configure recurring provider prices as tax-inclusive; renewals are re-taxed with the rules in effect at billing time.
- Recipes use `{ "steps": [...] }`; C# classes are `sealed` except view models.

## Plans

Give a content type the **Subscription** stereotype (`SubscriptionConstants.Stereotype`); it gets `SubscriptionPart` and `ProductPart`, kept on the type. `SubscriptionPart`:

| Property | Meaning |
| --- | --- |
| `BillingDuration`, `DurationType` | Cycle length, `Year`, `Month`, `Week` or `Day` |
| `InitialAmount`, `InitialAmountDescription` | Optional one-time setup fee |
| `BillingCycleLimit` | Cycles billed before the agreement ends |
| `SubscriptionDayDelay` | Delayed start (billing begins later, card attached now) |
| `TrialDays` | Free trial; the gateway is told, so it survives restarts |
| `Sort` | Order in a listing's **Recommended** sort when the Product part has no position |

`SubscriptionPartSettings.ContentTypes` names content the subscriber fills in during checkout (the Content step). The price comes from `ProductPart` (or the **Product prices** part for monthly/annual or per-seat choices) through `IProductSnapshotResolver`, in the plan's own currency. List plans with a Product Listing (there is no built-in plans page). `SubscriptionEntitlementPart.RoleNames` (attach **Subscription Entitlements**) lists the roles a subscriber holds.

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "MembershipPlan",
          "DisplayName": "Membership Plan",
          "Settings": { "ContentTypeSettings": { "Creatable": true, "Listable": true, "Stereotype": "Subscription" } },
          "ContentTypePartDefinitionRecords": [
            { "PartName": "TitlePart", "Name": "TitlePart" },
            { "PartName": "ProductPart", "Name": "ProductPart", "Settings": { "ProductPartSettings": { "DefaultCurrency": "USD" } } },
            { "PartName": "SubscriptionPart", "Name": "SubscriptionPart" },
            { "PartName": "SubscriptionEntitlementPart", "Name": "SubscriptionEntitlementPart" }
          ]
        }
      ]
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "monthlyplan00000000000000a",
          "ContentType": "MembershipPlan",
          "DisplayText": "Monthly membership",
          "Published": true,
          "Latest": true,
          "TitlePart": { "Title": "Monthly membership" },
          "ProductPart": { "Price": 10, "Currency": "USD" },
          "SubscriptionPart": { "BillingDuration": 1, "DurationType": "Month", "TrialDays": 14 },
          "SubscriptionEntitlementPart": { "RoleNames": [ "Member" ] }
        }
      ]
    }
  ]
}
```

With `CrestApps.OrchardCore.Recipes` enabled, JSON Schema is contributed for `SubscriptionPart`, `SubscriptionSummaryPart` and (with Sites) `TenantOnboardingPart`.

## Buying a plan

**Sign up** links to `Subscription/Signup/{contentItemId}` (route `SubscriptionSignup`). `SubscriptionsController` prices the plan through `SubscriptionPlanPricing` (a plan without a currency or a recurring price shows *This plan can't be bought right now* and logs why), then calls `ICheckoutEngine.StartAsync` with `ReferenceType = "Subscription"` (`SubscriptionCheckout.ReferenceType`), the plan id and version, and the buyer's `PriceSelection`.

Steps: a concealed plan step carrying the recurring line and setup fee, **Content** (when `ContentTypes` is set), **Registration** (visitor not signed in; hidden again once they sign in), **Site details** (Sites), **Payment**. A guest's account is created before money moves and deleted if the checkout fails (only the account this checkout created). **Allow guest sign-up** in **Settings > Subscriptions** lets buyers subscribe without an account. `SubscriptionPlanCheckoutHandler` refuses to complete an invoice that bills nothing recurring.

## The agreement

`Subscription` (`CatalogItem`, collection `Subscription`): owner, `ProviderKey`, `ProviderSubscriptionId`, `Status`, `Amount`, `TaxAmount`, cycle, `CyclesBilled`, `CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`, `NextBillingUtc`, `TrialEndsUtc`, `PastDueSinceUtc`, `GraceEndsUtc`, `CancelAtPeriodEnd`, `Lines`, `Events`, `Entitlements`, and `IsCurrent(utcNow)`. `SubscriptionStatus`: `Active`, `Trialing`, `PastDue`, `Canceled`, `Expired`, `Paused`, `Incomplete`. Read with `ISubscriptionStore` (`GetByOwnerAsync`, `GetByProviderSubscriptionIdAsync`, `GetByCheckoutSessionAsync`, `PageAsync`); write with `ISubscriptionManager`.

`ISubscriptionLifecycleService`:

| Method | Effect |
| --- | --- |
| `RecordRenewalAsync(id, periodStartUtc, context)` | Advances one cycle from the previous period end; the same cycle twice advances once |
| `MarkPastDueAsync(id, reason)` | Starts dunning; a second failure does not restart the grace clock |
| `CancelAsync(id, atPeriodEnd, reason, source)` | Stops billing at the gateway; at period end keeps the paid-through date |
| `PauseAsync(id, reason)` / `ResumeAsync(id)` | Suspends and resumes collection at the gateway (`pause_collection` on Stripe) |
| `ExpireAsync(id, reason)` | Ends the agreement |
| `SyncFromProviderAsync(id, context)` | Adopts the gateway's period and status; never revives a local cancellation or undoes a local pause |

`SubscriptionLifecycleBackgroundTask` (every 30 minutes) expires agreements past their grace window and closes those that billed every cycle; it never charges. Gateways (Stripe) bill renewals and report them by webhook; offline agreements (Pay Later) are advanced by the sweep and invoiced by the Pay Later renewal task. `SubscriptionSettings` (**Settings > Subscriptions**, group `subscriptions`): `Currency`, `AllowGuestSignup`, `DunningGraceDays` (7).

React to every change with `ISubscriptionLifecycleHandler` (`SubscriptionLifecycleHandlerBase`), whose `SubscriptionLifecycleContext` has `Subscription`, `PreviousStatus`, `Source` and `StatusChanged`.

## Member-only access

```csharp
using CrestApps.OrchardCore.Subscriptions.Services;

public sealed class PremiumContentGate
{
    private readonly ISubscriptionAccessService _access;

    public PremiumContentGate(ISubscriptionAccessService access)
    {
        _access = access;
    }

    public Task<bool> CanReadAsync(string userId)
        => _access.HasEntitlementAsync(userId, "Role", "Member");
}
```

`HasEntitlementAsync(userId, kind, value)` (no value matches any of the kind), `GetEntitlementsAsync(userId)`, `GetCurrentSubscriptionsAsync(userId)`. Built-in kinds: `Role` and `Tenant` (`SubscriptionConstants.EntitlementKinds`). Roles from the plan's `SubscriptionEntitlementPart` are copied onto the agreement at creation and applied while it is current by `RoleSubscriptionEntitlementApplier`.

A custom kind: put a `SubscriptionEntitlement { Kind, Value, Data }` on the agreement (the Sites feature adds its `Tenant` entitlement from its checkout handler and saves through `ISubscriptionManager`), and register an applier:

```csharp
using CrestApps.OrchardCore.Subscriptions.Services;

public sealed class SeatEntitlementApplier : ISubscriptionEntitlementApplier
{
    public string Kind => "Seats";

    public Task ApplyAsync(SubscriptionEntitlementContext context)
    {
        // context.Subscription.OwnerId gets context.Entitlement.Value seats.
        return Task.CompletedTask;
    }

    public Task RevokeAsync(SubscriptionEntitlementContext context)
    {
        // Called when the agreement stops being current.
        return Task.CompletedTask;
    }
}

// services.AddScoped<ISubscriptionEntitlementApplier, SeatEntitlementApplier>();
```

## Workflow events

With `OrchardCore.Workflows`: **Subscription Started** (`SubscriptionStartedEvent`), **Subscription Renewed**, **Subscription Past Due**, **Subscription Canceled**, **Subscription Expired**. Each carries the subscription id, title, owner, current and previous status, amount, access-through date and grace end, and is correlated by subscription id. Only a real status change raises one. Sites adds **Subscribed tenant setup succeeded** and **failed** events.

## Selling sites

**Subscriptions - Sites** (default tenant only) sells whole Orchard Core sites. Attach `TenantOnboardingPart` (`RecipeName`, `FeatureProfile`) to the plan. The buyer names the site and its administrator on the **Site details** step; name, domain and prefix clashes are refused before payment. Completion records a `TenantProvisioningJob`; `TenantProvisioningBackgroundTask` (every minute) builds it under a lock with back-off, then marks it abandoned after repeated failures. The administrator password is data-protected, unprotected just before setup, and dropped at a terminal outcome.

- **Subscriptions > Tenant onboarding** (`SubscriptionOnboardingSettings`, `ManageSubscriptionSettings`): `DatabaseProvider` (empty means `Sqlite`, one file per site), protected connection string (purpose `SubscriptionOnboardingConnectionString`, never sent back), `Schema`. Each site gets a table prefix from its name.
- **Subscriptions > Site provisioning** lists jobs; retry a waiting or failed one; **Mark as created** closes an abandoned job after creating the site by hand. Customers watch **My Sites**.
- The `Tenant` entitlement disables (never deletes) the tenant when the subscription lapses and re-enables it when it resumes.

## Screens, reports, permissions

- **Agreements** (admin, `ManageSubscriptions`): filter by status, cancel now or at period end, pause, resume; every action is in the agreement's history. **My Plans** and the storefront `account/subscriptions` let the owner cancel (`ManageOwnSubscriptions`), defaulting to the end of the paid period.
- Payments and refunds live in Transactions; a plan bought by card is on the payment ledger, not a transaction.
- With Reports: **Reports > Commerce** revenue, dashboard, expiring, new trend, tax collected and product performance, read from the payment ledger and agreements, per currency (`ManageSubscriptions`).
- Permissions: `ManageSubscriptionSettings`, `ManageSubscriptions`, `ManageOwnSubscriptions`, `ManageInstallmentPlans`.
- Rate-limit groups `checkout` and `checkout-payment` (owned by Checkout) cover signup and payment.

## Upgrading from 2.x

- The session-based signup was removed: `SubscriptionFlow` and its contexts, `ISubscriptionHandler`, `SubscriptionSession`, `ISubscriptionSessionStore`, the anonymous Stripe and Pay Later signup endpoints, the hosted-checkout return route, the session admin list, the subscriber dashboard, `StripePriceSyncService` and **Sync prices**, `PaymentMethodOptions` and `PaymentMethod`. `Subscription/Signup/{contentItemId}` still starts a purchase; signups in progress at upgrade time cannot resume. Port custom steps to `ICheckoutHandler` and `CheckoutFlowDisplayDriver` (see `orchardcore-checkout`).
- *Subscriptions - reCaptcha* was removed; *Subscriptions - Tenant Onboarding* became **Subscriptions - Sites** (`CrestApps.OrchardCore.Subscriptions.Tenants`). Existing sites keep running.
- The default payment method moved to the Checkout settings.
- Reports read the payment ledger and agreements and report per currency; abandoned checkouts no longer count as revenue.
- The `/ServicePlans` page is gone; list plans with Product Listing.
- Installment plan settings moved to **Settings > Installment Plans**.
- Reconnect Stripe so the webhook delivers subscription lifecycle events.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| *This plan can't be bought right now* | No currency on the plan or its type, or the price does not recur |
| Subscriber charged after canceling | Pre-3.0 behavior; cancel now reaches the gateway. Check the provider supports recurring cancel |
| Renewals not recorded | Stripe webhook not re-provisioned (**Connect** again) |
| Role not removed | The agreement is still current (paid period or grace window) |
| Site never created | Background tasks off, onboarding database misconfigured, or job abandoned (see **Site provisioning** and the log) |

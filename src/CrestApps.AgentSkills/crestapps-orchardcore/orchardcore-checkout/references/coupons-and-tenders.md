# Discounts, coupons, tenders and payment gates

Coupons and discounts are part of the **Checkout** feature (`CrestApps.OrchardCore.Checkout`). `Coupon`, `CouponCode`, `CouponRedemption`, `DiscountLine`, `CheckoutDiscountScope` and `TenderLine` live in `CrestApps.OrchardCore.Checkout.Models` (Checkout.Abstractions); the services in `CrestApps.OrchardCore.Checkout.Services`. There is no separate coupon feature to enable.

## How a discount is applied

`CheckoutInvoiceBuilder` runs, in order: discounts (`ICheckoutDiscountService`), then tax (`ICheckoutTaxService`), then tenders (`ICheckoutTenderService`). The invoice is rebuilt, not patched, whenever a code is applied or removed.

- **Before tax.** A line is taxed on what the buyer pays for it. Each discount's share is spread over the lines it applies to in proportion to their totals (`CheckoutLineItem.DiscountAmount`, the last line taking the rounding) and passed to tax as `TaxableItem.DiscountAmount`. Shipping, which no item coupon touches, is taxed in full. Example: 10% off a $50 item with $15 shipping and 10% tax takes $5 off, charging $4.50 tax on the item and $1.50 on shipping.
- **Separate buckets.** `DiscountTarget.OneTime` reduces the goods charged now (never a shipping line), `FirstCycle` only the recurring charges collected now (the gateway is always told the full cycle price; Stripe gets a single-use coupon), `Shipping` only the shipping lines.
- **Never below zero**, and the invoice records what was actually taken off.
- **Fixed amounts** apply only to an invoice in the coupon's `Currency`.
- **Combining.** Each `DiscountLine` has a `DiscountClass` (`Product`, `Order`, `Shipping`) and `CombinesWith` (`DiscountCombination` flags, default `All`). `DefaultCheckoutDiscountService.Choose` keeps all when every pair combines; otherwise it picks the compatible set that takes off the most (exhaustive up to 12 candidates). Dropped ones go to `CheckoutInvoice.SkippedDiscounts` with `GaveWayTo`.
- Up to `CheckoutCoupon.MaxCodes` (5) codes per checkout, in `CheckoutCoupon.Codes`. Form actions: `apply-coupon` and `remove-coupon:{code}` (`CheckoutConstants.FormActions`). Applying codes is throttled per address (`IPaymentAttemptLimiter`, scope `coupon-code`).

## The coupon catalog

Managed under **Commerce > Coupons** (`ManageCoupons`), through the commerce API `coupons` endpoints (Checkout API feature), or the `Coupons` recipe and deployment steps. Every path validates with `CouponEditor`.

| `Coupon` member | Meaning |
| --- | --- |
| `Code`, `Description`, `IsEnabled` | Matched without case; description shown to the buyer |
| `Method` | `CouponMethod.Code` (typed) or `Automatic` (offered on every invoice it applies to; its code cannot be typed) |
| `Kind` | `Percentage`, `FixedAmount`, `FreeShipping`, `BuyXGetY` |
| `Percentage`, `Amount`, `Currency` | Value; `Amount` and `Currency` for a fixed amount |
| `Target` | `OneTime` or `FirstCycle` (free shipping aims at `Shipping`) |
| `StartsUtc`, `EndsUtc` | Validity window |
| `MinimumAmount`, `MaximumAmount`, `MinimumQuantity` | Spend and quantity bounds, counted on the lines the coupon applies to |
| `MaxRedemptions`, `MaxRedemptionsPerCustomer` | Usage limits; per customer counts redeemed and held uses by account or email |
| `ProductIds`, `CategoryIds` | Limit to products or taxonomy terms (pickers come from the Orders feature) |
| `BuyQuantity`, `GetQuantity`, `BuyProductIds`, `BuyCategoryIds`, `MaxApplicationsPerOrder` | Buy X get Y |
| `CombinesWithProductDiscounts`, `CombinesWithOrderDiscounts`, `CombinesWithShippingDiscounts` | Combining rules |
| `Sources`, `ReferenceTypes`, `CustomerId`, `CustomerEmail` | Who and what it is for (below) |
| `OnlyGeneratedCodes` | Only codes from `ICouponStore.GenerateCodesAsync` work |
| `RedemptionCount` | Kept by the site; never in recipes or deployments |

A coupon with neither a usage limit nor an end date discounts until somebody notices; the list warns about it.

### Coupons recipe step

The step adds or updates coupons by code and refuses what the editor refuses (each reason in the recipe errors). `IsScoped`, `HasItemLimits` and `EffectiveTarget` are computed and never serialized.

```json
{
  "steps": [
    {
      "name": "Coupons",
      "Coupons": [
        {
          "Code": "WELCOME10",
          "Description": "10% off your first order",
          "Kind": "Percentage",
          "Percentage": 10,
          "Target": "OneTime",
          "MaxRedemptionsPerCustomer": 1,
          "IsEnabled": true
        },
        {
          "Code": "FREESHIP75",
          "Kind": "FreeShipping",
          "MinimumAmount": 75,
          "IsEnabled": true
        },
        {
          "Code": "SETUP50",
          "Description": "Half off the first month",
          "Kind": "Percentage",
          "Percentage": 50,
          "Target": "FirstCycle",
          "EndsUtc": "2027-12-31T00:00:00Z",
          "MaxRedemptions": 200,
          "IsEnabled": true
        },
        {
          "Code": "FEES25",
          "Description": "Registration fee discount",
          "Kind": "FixedAmount",
          "Amount": 25,
          "Currency": "USD",
          "Sources": [ "installment-plan", "pay-later" ],
          "IsEnabled": true
        }
      ]
    }
  ]
}
```

### Limiting a coupon to a source, a reference type or one customer

| Property | Matched against |
| --- | --- |
| `Sources` | `CheckoutDiscountScope.Source` (the source of the transaction a checkout settles), case-insensitive |
| `ReferenceTypes` | `CheckoutSession.ReferenceType`, or `CheckoutDiscountScope.ReferenceType` (what the transaction is for), case-insensitive |
| `CustomerId` | `CheckoutSession.OwnerId`, or `CheckoutDiscountScope.CustomerId` (the transaction owner, user or guest customer id) |
| `CustomerEmail` | `CheckoutDiscountScope.CustomerEmail`, the `CheckoutContactInfo.Email` collected, or the session owner's account email (`ICustomerContactResolver`), case-insensitive |

Every limit set must match, and a scoped coupon is refused on a checkout that does not provide what the limit needs (fail closed). `CouponScopeEvaluator` holds the rule; `CouponCheckoutFlowDisplayDriver` refuses the code on entry (*That code can't be used for this payment.*) and `CouponDiscountProvider` stops applying it on rebuild. A module that knows what its checkout pays for stores a scope in `ActivatingAsync`:

```csharp
session.Put(new CheckoutDiscountScope
{
    Source = "club-registration",
    ReferenceType = "RegistrationFee",
    CustomerId = fee.UserId,
});
```

### Coupons on a transaction payment

A checkout that pays an existing balance (`SettlesExistingBalance`, set by Transactions for `ReferenceType = "Transaction"`) has no price of its own. A discount there is safe only if the settling module takes it off the balance, so a second flag decides:

- `AcceptsDiscountsOnBalance = false`: no promotion code box, posted codes ignored, no discounts applied (earlier ones cleared), nothing redeemed.
- `AcceptsDiscountsOnBalance = true` (Transactions sets it): discounts apply as usual, but `CouponRedemptionCheckoutHandler` redeems nothing. On completion `TransactionSettlementCheckoutHandler` records each one-time discount line as a `Discount` adjustment of exactly that amount (`ITransactionCommandService.AdjustAsync`), applies the confirmed payment, and calls each `ICheckoutDiscountProvider.RedeemAsync` only when nothing is owed. A 100% code settles with no payment attempt; a partial code plus the payment settles in full; a partial code with no confirmed payment records nothing.

A module of your own that settles an existing balance and sets `SettlesExistingBalance` must either leave `AcceptsDiscountsOnBalance` false or do the same adjustment and redemption itself.

### Holding, redeeming and returning uses

`CouponHoldPaymentGate` (an `ICheckoutPaymentGate`) holds a use of each code inside the session lock before a payment begins (or before a nothing-to-pay checkout completes). A code whose last use is gone is taken off, the invoice is rebuilt, and the payment is refused with `InvoiceChanged` (*The code LAST1 ran out before you paid, so it was taken off.*). Each use is a `CouponRedemption` (`CouponRedemptionIndex`): `Held` (expires with `SessionLifetimeHours`), `Redeemed` on completion, `Released` on cancel, failure or expiry, `Returned` when the order is canceled (`ICouponRedemptionService.ReturnAsync`). A code is consumed on purchase, not on entry, so an abandoned checkout never burns a single-use code.

### Discount links and generated codes

- `GET discount/{code}?redirect=/path` stores a typeable code in the `orchard-discount-code` cookie (seven days) and redirects to a local path; `DiscountLinkCheckoutHandler` adds it to each new checkout except one that settles a balance.
- `ICouponStore.GenerateCodesAsync(coupon, count, prefix, usesPerCode)` creates up to `CheckoutConstants.MaxGeneratedCodes` (5000) eight-character `CouponCode` documents (no 0, O, 1 or I), each with its own limit. Referrals use it with the `REF-` prefix. Export them as CSV from the coupon page.

### A discount provider of your own

```csharp
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;

public sealed class StaffDiscountProvider : ICheckoutDiscountProvider
{
    public string Key => "staff";

    public Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default)
    {
        // Decide only what to take off; the checkout caps it, allocates it per line and taxes the rest.
        var discountable = CheckoutDiscountBase.GetAmount(context.Invoice, DiscountTarget.OneTime);

        if (discountable <= 0m || !IsStaff(context.Flow.Session.OwnerId))
        {
            return Task.FromResult<IReadOnlyList<DiscountLine>>([]);
        }

        return Task.FromResult<IReadOnlyList<DiscountLine>>(
        [
            new DiscountLine
            {
                ProviderKey = Key,
                Description = "Staff discount",
                Amount = discountable * 0.2m,
                Target = DiscountTarget.OneTime,
                Class = DiscountClass.Order,
            },
        ]);
    }

    public Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static bool IsStaff(string userId) => false;
}

// services.AddScoped<ICheckoutDiscountProvider, StaffDiscountProvider>();
```

## Tenders: balances that pay after tax

A tender pays part of the total, tax included, from a balance the buyer holds: gift cards (Orders, key `gift-card`), store credit (`store-credit`) and loyalty points (`loyalty-points`) from Customers, company terms (`company-terms`) from Companies. It is not a discount.

| Type | Role |
| --- | --- |
| `ICheckoutTenderProvider` | `Key`, `GetTendersAsync(CheckoutTenderContext)`, `GetBalanceAsync`, `DebitAsync`, `CreditAsync` |
| `ICheckoutTenderService` (`DefaultCheckoutTenderService`) | Applies the chosen balances, in the buyer's order, to `InitialPaymentAmount`; `GrandTotal` stays the full total |
| `ICheckoutTenderLedger` | `HoldAsync`, `RedeemAsync`, `ReleaseAsync`, `ReturnAsync`, `GetHeldByOthersAsync` on `CheckoutTenderHold` records |
| `TenderHoldPaymentGate` | Holds balances before payment (after `CouponHoldPaymentGate`) |
| `TenderCheckoutHandler` | Redeems while completing, releases on cancel, failure and expiry |

Rules: tenders never pay a recurring obligation, never apply to a session that settles a balance or saves a payment method, and the tax the remaining card charge cannot hold is carried by the tenders (`TenderLine.TaxAmount`). The invoice exposes `TenderTotal`, `TenderTaxAmount` and `AmountDue`.

To add a kind of balance:

1. Implement `ICheckoutTenderProvider`, scoped, and keep every change to a balance under `CheckoutTenderLocks.GetKey(Key, reference)`.
2. Add a `CheckoutFlowDisplayDriver` for `CheckoutConstants.PaymentStepKey` (shape in `Aside`) that adds the reference to the session's `CheckoutTenderSelection` on `apply-tender` (or `apply-tender:{yourKey}`) and removes it on `remove-tender:{key}:{reference}`, then rebuilds the invoice with `CheckoutInvoiceBuilder.BuildAsync`.
3. Never debit from your own checkout handler; the ledger holds, redeems, releases and returns through your provider.

## Payment gates

`ICheckoutPaymentGate.BeginningAsync(CheckoutPaymentBeginningContext)` runs inside the session lock before a payment begins and before a nothing-to-pay checkout completes. Call `context.Refuse(message)` to stop it; set `context.InvoiceChanged = true` after rebuilding the invoice so the payment script reloads the page with the new total. `CompanyApprovalPaymentGate` (an order that needs approval) and the inventory hold are other examples.

```csharp
public sealed class RegistrationOpenPaymentGate : ICheckoutPaymentGate
{
    public Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        if (context.Flow.Session.Steps.Any(step => step.Key == "RegistrationFee") && RegistrationIsClosed())
        {
            context.Refuse("Registration closed before you paid.");
        }

        return Task.CompletedTask;
    }

    private static bool RegistrationIsClosed() => false;
}

// services.AddScoped<ICheckoutPaymentGate, RegistrationOpenPaymentGate>();
```

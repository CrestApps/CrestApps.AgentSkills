# Installment plans

**Subscriptions - Installment Plans** (`CrestApps.OrchardCore.Subscriptions.Installments`, in the Subscriptions module) lets a balance be paid in a down payment plus a fixed number of equal payments. It depends on Transactions, Checkout and `OrchardCore.Users` only; it needs neither the Subscriptions feature nor Products (without Products the admin form asks for the currency code). It owns no payment code: every payment is a [transaction](#data) with source `installment-plan`, and every charge goes through `ICheckoutEngine`.

A plan is created three ways: by an administrator (**Commerce > Installment plans**, card taken on the plan's page), by another module (`IInstallmentPlanService.CreateAsync` with a reference), or by the customer on a **Choose how to pay** checkout step when paying an outstanding transaction.

## Requirements

- A provider with `SupportsSavedPaymentMethods` and `SupportsEmbeddedElements` and an `ICheckoutSavedPaymentMethodProvider` (Stripe, or the Test payments provider in development). Without one, new plans are refused.
- Background tasks running (`InstallmentPlanBackgroundTask`, `*/15 * * * *`).
- Optional: **Transaction Reminders** (invoices with pay links, reminders) and **Send Payment Receipts**.
- A public **Base URL** (**Settings > General**) so reminder pay links built by the background task are absolute.

## Settings

**Settings > Installment Plans** (group `installment-plans`), permission `ManageInstallmentPlans` (*Manage subscriptions settings* still works). Stored as `InstallmentPlanSettings` in site settings; not read from `appsettings.json`.

```json
{
  "steps": [
    {
      "name": "Settings",
      "InstallmentPlanSettings": {
        "DefaultCollectionMethod": 1,
        "RetryDays": [ 2, 4 ],
        "CheckoutSources": [ "pay-later" ],
        "CheckoutInstallmentCounts": [ 3, 6 ],
        "CheckoutFrequency": 2,
        "CheckoutMinimumAmount": 100
      }
    }
  ]
}
```

| Property | Default | Meaning |
| --- | --- | --- |
| `DefaultCollectionMethod` | `0` | `0` charges the saved card (`AutoCharge`), `1` invoices (`Invoice`) |
| `RetryDays` | `[1, 3, 5]` | Days between retries of a declined automatic charge; then the payment is handed to the customer |
| `CheckoutSources` | `[]` | Transaction sources the built-in option provider offers installments for |
| `CheckoutInstallmentCounts` | `[]` | Up to six counts between 2 and 120 |
| `CheckoutFrequency` | `2` | `0` weekly, `1` every two weeks, `2` monthly, `3` quarterly |
| `CheckoutMinimumAmount` | `0` | Smaller balances are offered **Pay in full** only |

`ManageInstallmentPlans` (*Manage installment plans and charge customers' saved cards*) is separate from the other subscription permissions because it allows **Charge now**.

## Creating a plan from code

```csharp
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;

public sealed class SeasonFeePlans
{
    private readonly IInstallmentPlanService _plans;

    public SeasonFeePlans(IInstallmentPlanService plans)
    {
        _plans = plans;
    }

    public async Task<string> CreateAsync(string parentUserId, string registrationId)
    {
        var result = await _plans.CreateAsync(new CreateInstallmentPlanRequest
        {
            Title = "2027 season fee",
            CustomerUserId = parentUserId,
            Currency = "USD",
            TotalAmount = 300m,

            // No down payment: payment 1 is due now and taken while the card is entered.
            DownPaymentAmount = 0m,
            InstallmentCount = 3,
            Frequency = InstallmentFrequency.Monthly,
            CollectionMethod = InstallmentCollectionMethod.AutoCharge,
            ReferenceType = "Registration",
            ReferenceId = registrationId,
            Properties = new Dictionary<string, object>
            {
                ["season"] = "2027",
            },
        });

        if (!result.Succeeded)
        {
            // Errors are keyed by the request member they are about.
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Value)));
        }

        // The first payment is collected by completing result.Plan.DownPaymentSessionId through ICheckoutEngine,
        // or on the plan's admin payment page.
        return result.Plan.ItemId;
    }
}
```

Validation: down payment zero or more and below the total; 1 to 120 later payments; with a down payment the first scheduled payment is after today; `ReferenceType` and `ReferenceId` both set or both empty; every payment above zero at the currency's precision. A new customer can be created from `NewCustomerName` and `NewCustomerEmail` (no password; an email already in use is refused). `InstallmentScheduleCalculator` splits evenly, the last payment absorbing rounding.

| `IInstallmentPlanService` | Use |
| --- | --- |
| `CreateAsync` | Draft plan, its first-payment transaction and checkout session |
| `CreateFromTransactionAsync` | Turn an outstanding transaction into a plan in the customer's checkout |
| `ActivateAsync` | Schedule the remaining payments once the first is paid (idempotent) |
| `ChargeAsync(planId, paymentNumber)` | **Charge now** |
| `ProcessAsync(planId, waitForLock)` | Bring a plan up to date; safe any time |
| `CancelAsync(planId, reason)` | Cancel what is not yet received |

`IInstallmentPlanStore`: `PageAsync(InstallmentPlanQuery)`, `GetOpenAsync`, `GetByReferenceAsync(referenceType, referenceId)`, `GetBySourceTransactionAsync(transactionId)`. Use the service, not the store, for changes: every change runs under the lock `INSTALLMENT_PLAN_{planId}`.

## Lifecycle

- Plan status: `Draft`, `Active`, `PastDue`, `Completed`, `Canceled`. Payment status: `Scheduled`, `Due`, `Paid`, `Failed`, `Canceled`. History events: `Created`, `Activated`, `PaymentReceived`, `ChargeFailed`, `PaymentDue`, `Completed`, `Canceled`, `Note`.
- When the first payment's transaction settles, `InstallmentPlanTransactionPaymentHandler` (an `ITransactionPaymentHandler`) activates the plan: reads the kept card back, creates a `Pending` transaction per scheduled payment (with `TransactionAutoCollection` for auto-charge), moves the plan to `Active`. If no card was kept, the plan switches to invoicing and says why in its history.
- On each due date: auto-charge plans charge the card off-session through a new checkout session (`CheckoutPaymentDataKeys.ForOffSessionCharge(plan.PaymentMethod)`); invoice plans make the transaction `Outstanding` (invoice number, pay link reminders).
- Declines: an unreachable gateway retries an hour later without using a retry; a declined card marks the payment and its transaction `Failed` and schedules the next `RetryDays` try; after the last retry the transaction becomes `Outstanding` with no automatic collection. An early **Charge now** that is declined leaves the payment `Scheduled`.
- Double-charge protection: succeeded attempts not yet applied are applied through `ITransactionSettlementService` before any new charge; a session still `AwaitingProvider` or `PaymentPending` is completed instead of replaced.

## Installments at checkout

A checkout with `ReferenceType` `Transaction` offers the `ChooseHowToPay` step (`SubscriptionConstants.InstallmentPlans.CheckoutStepKey`, order 10, `CollectData = true`) when options exist and the transaction can be split (`Outstanding`, nothing paid, authenticated owner, has a currency, not already a plan payment or auto-collected). Options come from `IInstallmentOptionProvider` implementations matched on `Transaction.Source` (the built-in `SiteSettingsInstallmentOptionProvider` reads the settings):

```csharp
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;

public sealed class SeasonFeeInstallmentOptionProvider : IInstallmentOptionProvider
{
    public string Source => "club-registration";

    public Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<InstallmentOption>>(
        [
            new InstallmentOption
            {
                Id = "3-monthly",
                Title = "3 monthly payments",
                InstallmentCount = 3,
                Frequency = InstallmentFrequency.Monthly,
            },
        ]);
}

// In a startup with [RequireFeatures("CrestApps.OrchardCore.Subscriptions.Installments")]:
// services.AddScoped<IInstallmentOptionProvider, SeasonFeeInstallmentOptionProvider>();
```

Only the option id is posted (never `full`, at most 64 characters) and it is matched against options read again on the server. Choosing one calls `CreateFromTransactionAsync`: the checkout moves onto the plan's first payment, `CheckoutSession.SavePaymentMethod` is set (only card-keeping providers are offered), and the original transaction is canceled once the first payment arrives.

## Data

- Plans: YesSql collection `InstallmentPlan`, `InstallmentPlanIndex` (title, customer, status, currency, total, next due, created, `ReferenceType`, `ReferenceId`, `SourceTransactionId`).
- Transactions: source `installment-plan`, `ReferenceType` `InstallmentPlan`, `ReferenceId` the plan id, `ObligationId` the payment number (`0` is the down payment), `ParentReferenceType` / `ParentReferenceId` the record the plan pays for, plus a copy of `Properties`.
- Find a record's payments with `ITransactionStore.GetByParentReferenceAsync("Registration", id)`; react to each with `ITransactionPaymentHandler` filtered on `ReferenceType == "InstallmentPlan"`.
- Computed members (`DownPayment`, `FirstPayment`, `AmountPaid`, `AmountOutstanding`, `NextPayment`) are `[JsonIgnore]`; keep any derived property you add out of the document too, or its lists double on every save.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Plan stuck in `Draft` | The first payment was never completed; take it again or cancel. Checkout-created drafts close when their checkout ends |
| Nothing is charged | Background tasks running, `InstallmentPlanBackgroundTask` enabled under **Configuration > Tasks**, plan history for declines |
| Reminders without a pay link | Set **Base URL** |
| Auto-charge plan switched to invoicing | The provider did not keep the card |

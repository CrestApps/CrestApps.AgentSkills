# Ledger Automation: Scheduled Charges, Customer Credit, Workflows, Webhooks and Portability

Supporting material for [SKILL.md](../SKILL.md).

## Charging a saved card on a schedule, with retries

`ISavedPaymentMethodChargeService` (`CrestApps.OrchardCore.Transactions.Services`, `DefaultSavedPaymentMethodChargeService`) is the one charge-and-retry path for every debt collected with a card: invoices charged automatically, installment plan payments and booking balances. The schedule lives on the transaction as `Transaction.AutoCharge` (`TransactionAutoCharge`: `SavedPaymentMethodId` or `SourceSessionId`, `RetryDays`, `NextAttemptUtc`, `Attempts`, `LastAttemptUtc`, `LastFailure`).

| Member | What it does |
| --- | --- |
| `ScheduleAsync(transactionId, savedPaymentMethodId, chargeOnUtc)` | Sets `AutoCharge` and `AutoCollection`. A `null` card takes the owner's default (`ISavedPaymentMethodStore.GetDefaultAsync`); refused without a chargeable card or without Checkout |
| `ScheduleAsync(SavedPaymentMethodChargeRequest)` | `TransactionId`, `SavedPaymentMethodId` or `SourceSessionId` (the card a checkout kept; reaches a guest's card), `ChargeOnUtc`, `RetryDays` (`null` uses the settings' `ChargeRetryDays`, empty means no retry). Replaces any charge before |
| `ChargeAsync(transactionId)` | Charges now. `Succeeded`/`Pending` removes the schedule; `Declined` counts an attempt, moves the next one and raises `AutoChargeDeclinedAsync`; `Failed` retries within the hour without counting; `RequiresAuthentication`, a refusal, or no retry left hands the debt to its owner, records an `AutoChargeFailed` payment issue and raises `AutoChargeAbandonedAsync` |
| `ChargeDueAsync(utcNow)` | Charges up to 100 due transactions (`TransactionIndex.AutoChargeNextAttemptUtc`); the **Saved Card Charges** background task (`30 * * * *`, registered by Transaction Reminders) calls it |
| `CancelAsync(transactionId)` | Removes the schedule (the transaction page's **Cancel the charge**) |

Each returns a `SavedPaymentMethodChargeResult` (`Succeeded`, `Transaction`, `ChargeStatus`, `NextAttemptUtc`, `HandedOver`, `Errors`). Each command takes the lock `Transactions:AutoCharge:{id}`. A module that creates the transaction may set `Transaction.AutoCharge` before saving it instead, as installment plans and booking balances do.

```csharp
using CrestApps.OrchardCore.Transactions.Services;

public sealed class RenewalScheduler
{
    private readonly ISavedPaymentMethodChargeService _charges;
    private readonly ITransactionCommandService _commands;

    public RenewalScheduler(ISavedPaymentMethodChargeService charges, ITransactionCommandService commands)
    {
        _charges = charges;
        _commands = commands;
    }

    public async Task ScheduleAsync(string transactionId, DateTime dueUtc, CancellationToken cancellationToken)
    {
        var result = await _charges.ScheduleAsync(new SavedPaymentMethodChargeRequest
        {
            TransactionId = transactionId,
            ChargeOnUtc = dueUtc,
            RetryDays = [1, 3],
        }, cancellationToken);

        if (!result.Succeeded)
        {
            // No card on file: send the payment request with its pay link instead.
            await _commands.SendInvoiceAsync(transactionId, cancellationToken);
        }
    }
}
```

While a charge is scheduled, the reminder sweep sends no overdue reminder for the transaction. Handle `ITransactionEventHandler.AutoChargeAbandonedAsync` to send your own notice when the card is given up.

## Customer credit

What a payer pays above a balance can be kept as their **customer credit**, and their credit pays toward a balance. The credit is the Customers **Store Credit** feature's (`CrestApps.OrchardCore.Customers.StoreCredit`, `ICustomerCreditService`); Transactions offers credit only when it is registered.

- **Who can hold it.** Any payer but a guest: a user, a customer record, or another party such as a business account (`ICustomerCreditService.CanHold(party)`). A customer may spend a party's credit only with billing access to it (`CanSpendAsync`).
- **Excess to credit.** `RecordPaymentAsync` with `TransactionPaymentRequest.CreditExcess` keeps the rest as credit (`TransactionEvent.CreditedAmount`, `TransactionCommandResult.CreditedAmount`), and is refused when the payer can't hold credit. On **Record payment**, the checkbox **Keep anything above what is owed as customer credit** sets it.
- **Applying credit.** `ApplyCreditAsync(id, amount, note)` spends at most what the payer has and records a payment with settlement method `customer-credit`.
- **Taking back.** Reversing a credit payment gives the credit back; reversing a payment whose excess was kept takes that credit back (refused when already spent).
- **Durable intent.** A command saves its timeline entry first with `CreditState = Pending` (`TransactionIndex.PendingCredit`), then changes the credit with the reference `Transaction:{transactionId}:{entryId}` and marks the entry `Settled`. A stopped process is finished the next time a command loads the transaction, by the **Transaction Payment Reminders** task (up to 100 per run), or by `ReconcileCreditAsync(id)`. A change that can't be made marks the entry `Failed` and records a `CreditNotReconciled` payment issue. What did not finish comes back in `TransactionCommandResult.Warnings`.

## Workflows

With Workflows on, the ledger starts these events (category **Transactions**), each with optional `Sources` and `ReferenceTypes` filters (comma-separated, case-insensitive):

| Activity | Starts when |
| --- | --- |
| `TransactionCreatedEvent` | A transaction is created |
| `TransactionPaymentRecordedEvent` | A payment is applied |
| `TransactionSettledEvent` | Nothing more is owed |
| `TransactionOverdueEvent` | The sweep finds it overdue |
| `TransactionCanceledEvent` | It is canceled (`Reason`) |
| `TransactionRefundedEvent` | A refund is recorded |
| `TransactionEntryReversedEvent` | A payment or adjustment was taken back (`Reason`, `ReversedEventId`, `ReversedEventType`) |

The input holds `TransactionId`, `Title`, `Source`, `Status`, `Currency`, `TotalAmount`, `AmountPaid`, `AmountAdjusted`, `AmountRefunded`, `OutstandingAmount`, `OwnerId`, `PayerKind`, `PayerId`, `ReferenceType`, `ReferenceId`, `InvoiceNumber` and `DueUtc`; a payment or refund adds `EventId`, `Amount`, `Method` and `Reference`. Events start after the request's work is saved.

Tasks (Liquid fields `TransactionId` and `OutputName`, output `Transaction` by default; outcomes `Done`, `NotFound`, `Failed`):

| Task | Fields |
| --- | --- |
| `RecordTransactionPaymentTask` (Record Transaction Payment) | `Amount`, `PaymentMethod`, `Reference`, `Note` |
| `CancelTransactionTask` (Cancel Transaction) | `Reason` |
| `AdjustTransactionTask` (Adjust Transaction) | `Amount`, `AdjustmentKind` (`Discount`, `Waiver`, `Credit`, `WriteOff`), `Reason` |

With CrestApps Recipes, each activity has a schema for the `WorkflowType` step.

## Webhooks

With Commerce Webhooks (`CrestApps.OrchardCore.Commerce.Webhooks`), the ledger publishes `transaction.created`, `transaction.payment-recorded`, `transaction.settled`, `transaction.overdue`, `transaction.canceled`, `transaction.refunded` and `transaction.entry-reversed` (`TransactionsConstants.WebhookEvents`). The payload holds `transaction` and, for a payment, refund or reversal, `entry` (with its reason and document number); a reversal adds `reversedEntry`. Event ids are stable, so a receiver can drop repeats.

## Moving the ledger between sites

With Deployment on, the **Transactions** deployment step writes one `Transactions` recipe step with every transaction (with `Events`) and, with Checkout, the payment attempts, refunds and payment issues:

```json
{
  "steps": [
    {
      "name": "Transactions",
      "Transactions": [],
      "PaymentAttempts": [],
      "PaymentRefunds": [],
      "PaymentIssues": []
    }
  ]
}
```

`TransactionLedgerImporter` writes records as they are, never through the commands: nothing is posted, numbered, e-mailed or announced to handlers, workflows or webhooks. A record the site already has (by `ItemId`) is kept. Payment attempts and refunds need Checkout on the importing site. `TransactionLedgerExporter.ExportAsync(ids)` writes the same step for a set of transactions; Invoicing's **Invoices** deployment step uses it (`IncludeTransactions`). Receipt numbers on imported payments are kept; the importing site's series does not know them.

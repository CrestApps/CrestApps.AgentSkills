---
name: orchardcore-transactions
description: Skill for the CrestApps Orchard Core Transactions module (Transactions, Transaction Reminders, Payment Terms, Send Payment Receipts), the provider-agnostic ledger of money owed. Covers the Transaction model with payer and tax lines, ITransactionManager, AddTransactionSource, online settlement through the checkout, paying several transactions at once, ITransactionCommandService (record payment, customer credit, adjust, reverse, cancel, refund, charge a kept card), ISavedPaymentMethodChargeService scheduled charges with retries, ITransactionEventHandler, ITransactionPayUrlProvider, ITransactionVisibilityService, payment terms, staged reminders, message templates, payment requests as business documents, pay links, customer pages, workflows, webhooks, moving the ledger between sites, Payment issues and permissions. Use it when requests mention outstanding balances, My Transactions, recording a payment, adjustments, payment terms, pay links, charging a card on file, payment reminders or refunds.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Transactions

Transactions answers one question for the tenant: what has not been paid yet? A `Transaction` is a durable record of one obligation, whatever created it (Invoicing, Pay Later, bank transfer, installment plans, business account terms, bookings, your module). The module also owns the payment ledgers (`PaymentAttempt`, `PaymentRefund`) the checkout writes, the **Commerce > Payments** and **Commerce > Refunds** screens, the store's payment terms, and the one charge-and-retry path for saved cards.

Related skills: `orchardcore-invoicing` (invoices and credit notes posted to this ledger), `orchardcore-checkout` (online settlement, coupons on balances), `orchardcore-payments` (refund service, installment plans), `orchardcore-pay-later`, `orchardcore-receipts`, `orchardcore-business-documents` (printed payment requests, number series), `orchardcore-customers` (owner and guest contact, store credit), `orchardcore-exchange-rates`.

## Features

| Feature ID | Name | Depends on | Adds |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Transactions` | Transactions | Commerce Foundation (`CrestApps.OrchardCore.Commerce.Foundation`), `CrestApps.OrchardCore.Users` | Ledger, **Commerce > Transactions**, **My Transactions**, pay links, commands, the `Transactions` recipe step; Payments, Refunds and Payment issues screens (with Checkout) |
| `CrestApps.OrchardCore.Transactions.Notification` | Transaction Reminders | Transactions, `OrchardCore.Notifications` | Reminders, payment requests, **Settings > Commerce > Transactions**, the **Transaction Payment Reminders** (`0 * * * *`) and **Saved Card Charges** (`30 * * * *`) background tasks |
| `CrestApps.OrchardCore.Transactions.PaymentTerms` | Payment Terms | Transactions | **Commerce > Payment terms**, `IPaymentTermManager`, `IPaymentTermCalculator`, the `PaymentTerms` step |
| `CrestApps.OrchardCore.Transactions.Receipts` | Send Payment Receipts | Transactions, Receipts, `OrchardCore.Notifications` | A numbered receipt after every payment and a notice after every refund |

Turning Transactions on enables Commerce Foundation (the Commerce menu and icon), not the store setup guide. Scheduled saved-card charges are run by a background task of **Transaction Reminders**: turn it on wherever invoices, installment plans or bookings charge cards automatically. Printable invoices and receipts need the Receipts module; online payment of a balance needs Checkout. Constants: `TransactionsConstants.Features`, `RouteNames`, `DisplayTypes.DetailAdmin`, `ReferenceTypes.Transaction` (`"Transaction"`), `SettlementMethods` (`online`, `offline`, `adjusted`, `customer-credit`), `OfflinePaymentMethods` (`cash`, `check`, `bank-transfer`, `card-terminal`, `other`), `WebhookEvents`, `BusinessDocumentKinds.PaymentRequest`.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Transactions",
        "CrestApps.OrchardCore.Transactions.Notification",
        "CrestApps.OrchardCore.Transactions.PaymentTerms",
        "CrestApps.OrchardCore.Transactions.Receipts",
        "CrestApps.OrchardCore.Checkout"
      ],
      "disable": []
    }
  ]
}
```

Packages: `CrestApps.OrchardCore.Transactions` in the web project; modules reference `CrestApps.OrchardCore.Transactions.Abstractions` (models, services, view models, constants) and `CrestApps.OrchardCore.Transactions.Core` (`AddTransactionSource`, `TransactionsPermissions`, `TransactionCustomerPagesOptions`, `TransactionLedgerExporter`).

## Guidelines

- Change transactions through `ITransactionManager` or `ITransactionCommandService`, never by writing documents: the manager raises lifecycle events and checks concurrency.
- Offer staff actions on your own screens through `ITransactionCommandService`; it does not check permissions, so authorize first.
- Settle online only through the checkout (`ReferenceType = "Transaction"`); settlement uses the amount the provider confirmed.
- Charge saved cards on a schedule only through `ISavedPaymentMethodChargeService`; don't write your own retry loop.
- Register your `Source` with `AddTransactionSource` so it appears in the report filter.
- A guest owner (`CustomerOwnerKind.Guest`) is never shown to an account; guests pay through pay links.
- Handlers (`ITransactionPaymentHandler`, `IPaymentRefundHandler`, `ITransactionEventHandler`) run after the save; a throwing handler is logged and never undoes the change.
- If you bill documents, use Invoicing: don't post a debt and call it an invoice.
- C# classes are `sealed` except view models; recipes use `{ "steps": [...] }`.

## The model

`Transaction` (`CrestApps.OrchardCore.Transactions.Models`): `Title`, `Source`, `OwnerId`, `OwnerKind`, `GuestContactName`, `GuestContactEmail`, `PayerKind`, `PayerId`, `PayerContact`, `ReferenceType`, `ReferenceId`, `ReferenceVersionId`, `ParentReferenceType`, `ParentReferenceId`, `CheckoutSessionId`, `ObligationId`, `Currency`, `Amount`, `TaxAmount`, `TaxLines`, `TotalAmount`, `ExchangeRate`, `BaseCurrency`, `AmountPaid`, `AmountAdjusted`, `AmountRefunded`, computed `OutstandingAmount` (`TotalAmount - AmountAdjusted - AmountPaid`, never negative), `AllowPartialPayment`, `MinimumPayment`, `Status`, `DueUtc`, `SettledUtc`, `SettlementMethod`, `ReminderCount`, `ReminderStepsSent`, `RemindersPaused`, `BecameOverdueUtc`, `InvoiceNumber`, `AutoCollection`, `AutoCharge` (`TransactionAutoCharge`), `Recurrence`, `Events` (timeline) and the inherited `Properties`.

- **Payer.** `transaction.GetPayer()` returns the `PartyRef` that owes the debt (`PayerKind`/`PayerId`, else an `Account` parent reference, else the owner as `User` or `Guest`); `transaction.SetPayer(party)` sets it (`TransactionPayerExtensions`). An account's debts placed by different buyers are grouped by payer.
- `TransactionStatus`: `Pending`, `Outstanding`, `PartiallyPaid`, `Paid`, `Canceled`, `Failed`, `Abandoned`, `Refunded`.
- `TransactionEventType`: `Created`, `StatusChanged`, `PaymentRecorded`, `ReminderSent`, `Canceled`, `Note`, `Refunded`, `Adjusted`, `InvoiceSent`, `PaymentFailed`, `Reversed`. Terminal states (paid, canceled, refunded, abandoned) refuse payments, cancels and reminders (`TransactionStateMachine`).

### Recording a balance your module leaves owing

```csharp
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Modules;

public sealed class RegistrationBalances
{
    private readonly ITransactionManager _transactions;
    private readonly IClock _clock;

    public RegistrationBalances(ITransactionManager transactions, IClock clock)
    {
        _transactions = transactions;
        _clock = clock;
    }

    public async Task<Transaction> RecordAsync(string userId, string registrationId, decimal amount, string currency)
    {
        var now = _clock.UtcNow;
        var transaction = await _transactions.NewAsync();

        transaction.Title = "Season registration";
        transaction.Source = "club-registration";
        transaction.OwnerId = userId;
        transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        transaction.ReferenceType = "Registration";
        transaction.ReferenceId = registrationId;
        transaction.Currency = currency;
        transaction.Amount = amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = amount;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = now.AddDays(14);
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = "Registration balance recorded.",
        });

        await _transactions.CreateAsync(transaction);

        return transaction;
    }
}
```

```csharp
using CrestApps.OrchardCore.Transactions.Core;
using OrchardCore.Localization;

// In the module Startup:
services.AddTransactionSource("club-registration", source =>
{
    source.DisplayName = LocalizationSource.Create<Startup>("Club registration");
    source.Description = LocalizationSource.Create<Startup>("Season fees owed by families.");
});
```

`TransactionSource.DisplayName` and `Description` are `LocalizationSource` values set at startup, never serialized.

## Settlement

- **Online.** **Pay** (on **My Transactions**, the pay link page, or your code) starts a checkout with `ReferenceType = "Transaction"` and `ReferenceId = transaction.ItemId`. `TransactionSettlementCheckoutHandler` adds a concealed `TransactionSettlement` step whose billing item is the outstanding amount with `ExcludeFromTax = true`, refuses deferred providers such as Pay Later, accepts promotion codes on the balance, and on completion applies the confirmed attempts through `ITransactionSettlementService` (idempotent, same currency only; less than owed is `PartiallyPaid`).
- **Offline.** **Record payment**, **Mark paid**, **Apply customer credit** or **Adjust or waive** in the console, or the command service.
- **Paid through a page of its own.** When an `ITransactionPayUrlProvider` returns a URL for a transaction (an order's invoice paid through the order's checkout), pay links, reminders and the pay page go there, and the ledger's checkout refuses it, alone or with others.
- **Shared checkout payment.** A purchase checkout's payment can be applied by shares to other transactions recording the same `CheckoutSessionId` (`ITransactionStore.GetByCheckoutSessionAsync`), such as an invoice and its shipping debit note; refunds are split back so none gives back more than it got.
- With installment plans enabled, a **Choose how to pay** step can turn the balance into a plan (see `orchardcore-payments`).

```csharp
var session = await _checkoutEngine.StartAsync(new StartCheckoutRequest
{
    ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
    ReferenceId = transaction.ItemId,
    ReturnUrl = Url.Action("Index", "Payments"),
    ReturnText = S["Back to my payments"],
    Title = S["Payment received"],
});

return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId, step = session.CurrentStep });
```

### Paying several transactions at once

Set `StartCheckoutRequest.References` to one `CheckoutReference` per transaction. Every transaction must exist, still owe something, be collectable, not be paid through a page of its own, and all must have one payer (or owner) and one currency; otherwise `StartAsync` throws `CheckoutRejectedException` (readable `Errors`) and saves nothing. The customer sees one line per transaction, no promotion code and no installment plan. One payment attempt (empty `ReferenceId`) is split across the transactions in list order through `ITransactionSettlementService.ApplyShareAsync`; refunds stay per transaction. Check that the signed-in customer may see the transactions (`ITransactionVisibilityService`) before you start.

## Transaction commands

`ITransactionCommandService` runs every staff action with the console's timeline entries, receipts and handler calls. Each takes a distributed lock on the transaction, checks `TransactionStateMachine`, saves through the manager, commits, then runs payment handlers, and returns a `TransactionCommandResult` (`Succeeded`, `Errors`, `Warnings`, `Transaction`, `Event`, `Refunds`, `ChargeStatus`, `UnappliedAmount`, `CreditedAmount`).

| Command | Effect |
| --- | --- |
| `RecordOfflinePaymentAsync(id, amount, reference, note)` | Records money received outside the site, capped at what is owed |
| `RecordPaymentAsync(id, TransactionPaymentRequest)` | `Amount`, `PaymentMethod` (stored as its `OfflinePaymentMethods` key when it matches), `ReceivedUtc`, `Reference`, `Note`, `CreditExcess` (keep money above the balance as the payer's customer credit; otherwise it comes back in `UnappliedAmount`) |
| `ApplyCreditAsync(id, amount, note)` | Pays with the payer's customer credit (settlement method `customer-credit`) |
| `ReconcileCreditAsync(id)` | Finishes credit changes a stopped process left `Pending` |
| `MarkPaidAsync(id, note)` | Records the rest as an offline payment |
| `CancelAsync(id, reason)` | Cancels a collectable transaction |
| `CancelUnpaidAsync(id, reason)` | Cancels only when nothing was paid, checked under the lock (Invoicing's void uses it) |
| `AdjustAsync(id, amount, kind, reason)` / `AdjustAsync(id, TransactionAdjustmentRequest)` | `Discount`, `Waiver`, `Credit` or `WriteOff`, never above `OutstandingAmount`; the request form also names the document (`DocumentKind`, `DocumentId`, `DocumentNumber`); nothing left owing makes it `Paid` with `SettlementMethod = "adjusted"` |
| `ReverseAsync(id, eventId, reason)` | Takes back a payment recorded by hand or an adjustment with a `Reversed` entry, so it is owed again |
| `SendInvoiceAsync(id)` | Numbers and sends the payment request with the pay link (needs Transaction Reminders; refused for auto-collected) |
| `RefundAsync(id, amount?, reason)` | Refunds confirmed attempts newest first through `ICheckoutRefundService` (needs Checkout) |
| `ChargeSavedPaymentMethodAsync(id, savedPaymentMethodId)` | Charges what is owed to a card the owner kept, without the owner present |
| `ChargeKeptPaymentMethodAsync(id, sourceSessionId)` | The same with the card a checkout kept (also reaches a guest's card) |
| `SetRemindersPausedAsync(id, paused)` | The **Pause reminders** button |

Charge commands first apply any succeeded attempt not yet recorded, so money a provider took is never charged twice, and finish a charge still settling instead of starting another (lock `Transactions:Charge:{id}`). Charge statuses (`SavedPaymentMethodChargeStatus`): `Succeeded`, `Pending` (do not charge again), `Declined`, `RequiresAuthentication` (the owner must pay), `Failed` (provider unreachable), `NotCharged` (refused before the provider was asked). The Test payments provider declines an off-session charge ending in `02` and asks for approval on one ending in `03`.

Scheduled charging with retries, customer credit, workflows, webhooks and moving the ledger between sites are in [references/ledger-automation.md](references/ledger-automation.md).

## Reacting to changes

| Contract | Raised |
| --- | --- |
| `ITransactionPaymentHandler.PaymentRecordedAsync(TransactionPaymentRecordedContext)` | Once per payment applied, however paid |
| `IPaymentRefundHandler.RefundSucceededAsync(PaymentRefund)` | Once per refund that succeeds |
| `ITransactionEventHandler` (`TransactionEventHandlerBase`) | `CreatedAsync`, `StatusChangedAsync(transaction, previous, current)`, `CanceledAsync(transaction, reason)`, `BecameOverdueAsync` (once, by the reminder sweep, even when reminders are off), `RefundedAsync(transaction, refund)`, `ReversedAsync(transaction, reversal, reversed)`, `AutoChargeDeclinedAsync(transaction, reason, nextAttemptUtc)`, `AutoChargeAbandonedAsync(transaction, reason)` |

The last four have default implementations that do nothing.

```csharp
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

public sealed class RegistrationPaymentStatusHandler : TransactionEventHandlerBase
{
    public override Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (transaction.ReferenceType != "Registration")
        {
            return Task.CompletedTask;
        }

        // Move the registration to unpaid, paid or refunded. A save that keeps the status raises nothing.
        return Task.CompletedTask;
    }

    public override Task AutoChargeAbandonedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
    {
        // The saved card could not be charged; tell the family to pay from the pay link.
        return Task.CompletedTask;
    }
}

// services.AddScoped<ITransactionEventHandler, RegistrationPaymentStatusHandler>();
```

**A card on the transaction page.** The admin page is built by display drivers of `Transaction` for `TransactionsConstants.DisplayTypes.DetailAdmin`: the summary is at `Content:5`, the timeline at `Content:20`, the actions at `Actions:5`. Add your own card with a `DisplayDriver<Transaction>` that returns `View(...).Location(TransactionsConstants.DisplayTypes.DetailAdmin, "Content:10")` when `context.DisplayType` matches.

## Payment terms

The **Payment Terms** feature keeps the store's terms, which invoices, account terms, pay-later purchases and work orders read instead of a bare number of days. `PaymentTerm`: `Name`, `Description`, `Kind`, `Days`, `DayOfMonth`, `DiscountPercent`, `DiscountDays`, `Enabled`.

| `Kind` | Due on |
| --- | --- |
| `DueOnReceipt` | The issue date |
| `NetDays` | The issue date plus `Days` |
| `EndOfMonth` | The last day of the issue month plus `Days` |
| `DayOfNextMonth` | Day `DayOfMonth` of the next month, or its last day |

```csharp
var term = await _paymentTermManager.FindByIdAsync(termId);
var dueDate = _paymentTermCalculator.GetDueDate(term, issueDate);
var discount = _paymentTermCalculator.GetEarlyPaymentDiscount(term, issueDate, total, currency);
```

Both work on `DateOnly` local dates in the site's time zone; `Transaction.DueUtc` is the end of the due date in that zone. `IPaymentTermManager.GetEnabledAsync()` lists the enabled terms.

```json
{
  "steps": [
    {
      "name": "PaymentTerms",
      "Terms": [
        { "ItemId": "4paymentterm00000000000001", "Name": "Net 30", "Kind": "NetDays", "Days": 30, "Enabled": true },
        { "ItemId": "4paymentterm00000000000002", "Name": "2/10 net 30", "Kind": "NetDays", "Days": 30, "DiscountPercent": 2, "DiscountDays": 10, "Enabled": true }
      ]
    }
  ]
}
```

## Payment requests, pay links and reminders

- **The printed invoice.** The payment request a customer opens is a business document of kind `PaymentRequest` built by `ITransactionInvoiceBuilder.BuildAsync(transaction)`, which returns a `BusinessDocument` (the business details, the charge, adjustments still in force, a tax line per rate from `TaxLines`, the total owed, the payer as recipient), rendered with the **Payment requests** template of Business Documents. It is the seam an invoicing module replaces; Invoicing's own invoices are separate documents (see `orchardcore-invoicing`).
- **Numbering.** `ITransactionInvoiceService.EnsureInvoiceNumberAsync` numbers a transaction the first time its owner is told. `IFinancialDocumentNumberGenerator` (`SequentialFinancialDocumentNumberGenerator`) now draws from `INumberSeries` series `Financial.{Kind}` (`FinancialDocumentSeries`, defaults `INV-`, `R-`, `CN-`, `RF-` from 1001), seeded from the old counters; formats are edited under **Settings > Business documents > Number series**.
- **Pay links.** `ITransactionPayLinkService.CreatePayUrlAsync` makes a data-protected link to `transactions/pay/{token}` (route `TransactionsPay`) valid 60 days, unless an `ITransactionPayUrlProvider` names another page. Links use the site **Base URL**.
- **Reminders** (`ITransactionReminderService`) go through `ICustomerOwnerNotifier`: users by `INotificationService`, guests by e-mail. The sweep skips paused transactions, parties kept out of reminders, transactions with a scheduled card charge, and ones being paid; it sends only between `SendFromHour` and `SendUntilHour` in the site's time zone. `Steps` (days relative to the due date, such as `[-3, 0, 7, 14, 30]`) replace the delay, interval, maximum and upcoming settings when set; a negative step is sent as the upcoming notice.

`TransactionReminderSettings` (**Settings > Commerce > Transactions**, group `transactions`, `ManageTransactionSettings`):

```json
{
  "steps": [
    {
      "name": "Settings",
      "TransactionReminderSettings": {
        "Enabled": true,
        "FirstReminderDelayDays": 0,
        "ReminderIntervalDays": 7,
        "MaxReminders": 3,
        "UpcomingReminderDays": 3,
        "Steps": [ -3, 0, 7, 14, 30 ],
        "SendFromHour": 8,
        "SendUntilHour": 20,
        "ChargeRetryDays": [ 1, 3, 5 ]
      }
    }
  ]
}
```

**Message templates.** Each message has its own commerce e-mail kind under **Settings > Commerce > Emails** (`TransactionEmailKinds`): `payment-request` and `payment-reminder` (Transaction Reminders), `payment-receipt` and `payment-refund` (Send Payment Receipts). Templates read `{{ Transaction }}`; requests and reminders also get `{{ PayUrl }}`, `{{ DocumentUrl }}`, `{{ DocumentNumber }}`, `{{ AmountDue }}` and `{{ DueDate }}`.

**Keeping a party out.** `PartyCollectionSettings` (`Party`, `RemindersPaused`, `LateFeesExempt`, one per party, `IPartyCollectionSettingsService`) is changed on the **Reminders and late fees** card of an account page (with Accounts) or a customer page (with Customers), under **Manage transactions**. The reminder sweep and Invoicing's late fees read it.

## Customer pages in the site theme

| Route | Name | Shape | Needs |
| --- | --- | --- | --- |
| `my-transactions` | `TransactionsStatement` | `TransactionStatement` | Transactions |
| `my-transactions/invoices/{itemId}` | `TransactionsInvoice` | `TransactionInvoice` | Transactions, Receipts |
| `my-transactions/receipts/{itemId}/{paymentId}` | `TransactionsReceipt` | `TransactionReceipt` | Transactions, Receipts |

The admin versions are named `MyTransactions`, `TransactionInvoice` and `TransactionReceipt`. Both sides build through `ITransactionCustomerPageService`. What a customer sees is decided by `ITransactionVisibilityService` (`CanViewAsync`, `CreateQueryAsync`): transactions their account owns, and those whose payer is a party they have billing access to (their customer record, or a business account they are an admin of). A plain account member sees only what they own; a guest transaction is never shown to an account. Links in receipts and reminders open the admin pages by default; set `OrchardCore:CrestApps:Transactions:CustomerPages:LinkTarget` to `Site` (or configure `TransactionCustomerPagesOptions`). A portal controller example is in [references/customer-pages.md](references/customer-pages.md).

## Admin screens and permissions

| Location | Permission |
| --- | --- |
| **Commerce > Transactions** (report and console) | `ManageTransactions` |
| **Commerce > My Transactions** | `ViewOwnTransactions` |
| **Commerce > Payment terms** (Payment Terms) | `ManagePaymentTerms` |
| **Commerce > Payments**, **Commerce > Refunds**, **Commerce > Payment issues** (with Checkout) | `ManageRefunds` |
| **Settings > Commerce > Transactions** (Transaction Reminders) | `ManageTransactionSettings` |

| Permission | Grants |
| --- | --- |
| `ManageTransactions` | The report and console (reminder, record payment, apply credit, mark paid, adjust or waive, take back, send request, cancel, pause reminders, cancel a scheduled charge, note); any invoice or receipt; party reminder and late-fee settings |
| `ManageTransactionSettings` | Reminder settings; also the Offline payments settings of Pay Later |
| `ManagePaymentTerms` | Create, change and delete payment terms |
| `ViewOwnTransactions` | **My Transactions**, own invoices and receipts, paying them |
| `ManageRefunds` | Payments, Refunds and Payment issues; the only place a refund starts in the admin |

**Payment issues** lists money operations that failed and must be finished by hand, with **Mark resolved**. Record your own with `IPaymentIssueRecorder.RecordAsync(PaymentIssue)`; built-in kinds in `PaymentIssueKinds`: `RefundFailed`, `GatewaySubscriptionChangeFailed`, `DiscountRedemptionFailed`, `CreditNotReconciled`, `AutoChargeFailed`.

## Data source

With **Data Pipelines Core** enabled, Transactions registers the **Transactions** data source (`Transactions`): `Transactions` (`ManageTransactions`), and with Checkout `PaymentAttempts` and `PaymentRefunds` (`ManageRefunds`). Offline payment methods are grouped by their stable keys. See `orchardcore-report-builder`.

## Upgrading

- From 2.x: the ledger types moved here from Checkout (`PaymentAttempt`, `PaymentRefund`, their stores); console logic moved to `ITransactionCommandService`; `OutstandingAmount` subtracts `AmountAdjusted`; `TransactionSource.DisplayName` is a `LocalizationSource`.
- Since 3.0 previews: Transactions depends on Commerce Foundation instead of Commerce; `ITransactionInvoiceBuilder.BuildAsync` returns a `BusinessDocument` instead of a `ReceiptDocument`; `IFinancialDocumentPolicy` and its context and result types are removed; document numbers come from `INumberSeries`; the reminder task runs hourly instead of every 6 hours; the payment request has its own `payment-request` e-mail kind (it used the reminder template before).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| **Pay** shows a message instead of a checkout | Checkout is disabled, the transaction is terminal, or it is paid through a page of its own (`ITransactionPayUrlProvider`) |
| Pay Later not offered when paying a balance | By design: deferred providers cannot settle an existing balance |
| `CheckoutRejectedException` paying several | Different payers or currencies, one owes nothing, or one pays through its own page |
| Scheduled card charges never run | Transaction Reminders is off (it runs the **Saved Card Charges** task), or the Checkout feature is off |
| A kept-card charge returns `RequiresAuthentication` | The bank wants the cardholder; send the payment request or pay link |
| No reminders | Transaction Reminders off, **Enabled** off, outside `SendFromHour`-`SendUntilHour`, reminders paused on the transaction or the party, a card charge scheduled, notification methods missing |
| Customer credit not offered | Customers **Store Credit** (`CrestApps.OrchardCore.Customers.StoreCredit`) off, guest payer, or no credit in the currency |
| "Reload and try again" | Two managers edited at once (concurrency check) |

---
name: orchardcore-transactions
description: Skill for the CrestApps Orchard Core Transactions module (CrestApps.OrchardCore.Transactions, Transactions.Notification, Transactions.Receipts), the provider-agnostic ledger of money owed. Covers the Transaction model and TransactionStatus, guest owners, ITransactionManager, AddTransactionSource, online settlement through the checkout, paying several transactions in one checkout, ITransactionCommandService (record payment, mark paid, cancel, AdjustAsync, SendInvoiceAsync, RefundAsync, ChargeSavedPaymentMethodAsync for a kept card), ITransactionPaymentHandler, IPaymentRefundHandler, ITransactionEventHandler, invoices, pay links, reminders, receipts, customer pages, the PaymentAttempt and PaymentRefund ledgers, Payment issues, the Report Builder data source and permissions. Use it when requests mention outstanding balances, My Transactions, recording a payment, adjustments or waivers, invoices, pay links, charging a card on file, payment reminders or refunds in CrestApps commerce.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Transactions

Transactions answers one question for the tenant: what has not been paid yet? A `Transaction` is a durable record of one obligation, whatever created it (Pay Later, bank transfer, installment plans, business account terms, your module). The module also owns the payment ledgers (`PaymentAttempt`, `PaymentRefund`) the checkout writes, and the **Commerce > Payments** and **Commerce > Refunds** screens.

Related skills: `orchardcore-checkout` (online settlement, coupons on balances), `orchardcore-payments` (refund service, installment plans), `orchardcore-pay-later`, `orchardcore-receipts`, `orchardcore-customers` (owner and guest contact, notifier).

## Features

| Feature ID | Name | Depends on | Adds |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Transactions` | Transactions | Commerce, `CrestApps.OrchardCore.Users` | Ledger, **Commerce > Transactions**, **My Transactions**, pay links, commands, Payments and Refunds screens (with Checkout) |
| `CrestApps.OrchardCore.Transactions.Notification` | Transaction Reminders | Transactions, `OrchardCore.Notifications` | Reminders, invoices sent with pay links, `TransactionReminderBackgroundTask` (every 6 hours) |
| `CrestApps.OrchardCore.Transactions.Receipts` | Send Payment Receipts | Transactions, Receipts, `OrchardCore.Notifications` | A numbered receipt after every payment and a notice after every refund |

Printable invoices and receipts need only Transactions plus the Receipts module. Online payment of a balance needs Checkout. Constants: `TransactionsConstants.Features`, `RouteNames`, `Shapes`, `ReferenceTypes.Transaction` (`"Transaction"`), `SettlementStepKey` (`TransactionSettlement`), `SettlementMethods` (`online`, `offline`, `adjusted`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Transactions",
        "CrestApps.OrchardCore.Transactions.Notification",
        "CrestApps.OrchardCore.Transactions.Receipts",
        "CrestApps.OrchardCore.Checkout"
      ],
      "disable": []
    }
  ]
}
```

Packages: `CrestApps.OrchardCore.Transactions` in the web project; modules reference `CrestApps.OrchardCore.Transactions.Abstractions` (models, services, view models, constants) and `CrestApps.OrchardCore.Transactions.Core` (`AddTransactionSource`, `TransactionCustomerPagesOptions`).

## Guidelines

- Change transactions through `ITransactionManager` or `ITransactionCommandService`, never by writing documents: the manager raises lifecycle events and checks concurrency.
- Offer staff actions on your own screens through `ITransactionCommandService`; it does not check permissions, so authorize first.
- Settle online only through the checkout (`ReferenceType = "Transaction"`); settlement uses the amount the provider confirmed.
- Register your `Source` with `AddTransactionSource` so it appears in the report filter.
- A guest owner (`CustomerOwnerKind.Guest`) is never shown to an account; guests pay through pay links.
- Handlers (`ITransactionPaymentHandler`, `IPaymentRefundHandler`, `ITransactionEventHandler`) run after the save; a throwing handler is logged and never undoes the change.
- C# classes are `sealed` except view models; recipes use `{ "steps": [...] }`.

## The model

`Transaction` (`CrestApps.OrchardCore.Transactions.Models`): `Title`, `Source`, `OwnerId`, `OwnerKind`, `GuestContactName`, `GuestContactEmail`, `ReferenceType`, `ReferenceId`, `ReferenceVersionId`, `ParentReferenceType`, `ParentReferenceId`, `CheckoutSessionId`, `ObligationId`, `Currency`, `Amount`, `TaxAmount`, `TotalAmount`, `AmountPaid`, `AmountAdjusted`, `AmountRefunded`, computed `OutstandingAmount` (`TotalAmount - AmountAdjusted - AmountPaid`, never negative), `Status`, `DueUtc`, `SettledUtc`, `SettlementMethod`, `ReminderCount`, `BecameOverdueUtc`, `InvoiceNumber`, `AutoCollection`, `Recurrence`, `Events` (timeline) and the inherited `Properties`.

`TransactionStatus`: `Pending`, `Outstanding`, `PartiallyPaid`, `Paid`, `Canceled`, `Failed`, `Abandoned`, `Refunded`. `TransactionEventType`: `Created`, `StatusChanged`, `PaymentRecorded`, `ReminderSent`, `Canceled`, `Note`, `Refunded`, `Adjusted`, `InvoiceSent`, `PaymentFailed`. Terminal states (paid, canceled, refunded, abandoned) refuse payments, cancels and reminders (`TransactionStateMachine`).

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

`TransactionSource.DisplayName` and `Description` are `LocalizationSource` values.

## Settlement

- **Online.** **Pay** (on **My Transactions**, the pay link page, or your code) starts a checkout with `ReferenceType = "Transaction"` and `ReferenceId = transaction.ItemId`. `TransactionSettlementCheckoutHandler` adds a concealed `TransactionSettlement` step whose billing item is the outstanding amount with `ExcludeFromTax = true`, sets `SettlesExistingBalance` (deferred providers such as Pay Later are refused) and `AcceptsDiscountsOnBalance`, stores a `CheckoutDiscountScope`, and on completion applies the confirmed attempts through `ITransactionSettlementService` (idempotent, same currency only; less than owed is `PartiallyPaid`). Promotion codes become `Discount` adjustments and are redeemed only when nothing is owed.
- **Offline.** **Record payment**, **Mark paid** or **Adjust or waive** in the console, or the command service.
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

One checkout can settle several outstanding transactions of one customer with one payment:

```csharp
var session = await _checkoutEngine.StartAsync(new StartCheckoutRequest
{
    References = transactionIds
        .Select(id => new CheckoutReference
        {
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = id,
        })
        .ToList(),
    ReturnUrl = Url.Action("Index", "Payments"),
    ReturnText = S["Back to my payments"],
    Title = S["Payment received"],
});
```

- **Checked first.** Every transaction must exist, still owe something, still be collectable, and all must belong to one customer (same owner kind and id, or the same guest email) in one currency. Otherwise `StartAsync` throws `CheckoutRejectedException` (readable `Errors`) and saves nothing. Check that the signed-in customer owns the transactions before you start, as the single **Pay** action does.
- **What the customer sees.** One line per transaction at what it still owes. No promotion code (`AcceptsDiscountsOnBalance` is off) and no installment plan.
- **How it is applied.** One payment attempt (`ReferenceType = "Transaction"`, empty `ReferenceId`). On completion the confirmed amount is split across the transactions in list order, each up to what it owes, through `ITransactionSettlementService.ApplyShareAsync`; each records its share as its own payment naming the attempt, so status, receipts, payment handlers and lifecycle events work per transaction. Completing again applies nothing twice.
- **Refunds stay per transaction.** **Refund** on one transaction gives back only its share and names it (`PaymentRefund.ReferenceType`/`ReferenceId`). A refund of the whole payment from **Commerce > Payments** or the gateway is split back over the transactions in order.
- `ICheckoutSessionStore.GetByReferenceAsync("Transaction", id)` and `IPaymentAttemptStore.GetByReferenceAsync` find only single-transaction checkouts; a shared payment is found through the transaction's `PaymentRecorded` event (`PaymentAttemptId`).

## Transaction commands

`ITransactionCommandService` runs every staff action with the console's timeline entries, receipts and handler calls. Each takes a distributed lock on the transaction, checks `TransactionStateMachine`, saves through the manager, commits, then runs payment handlers, and returns a `TransactionCommandResult` (`Succeeded`, `Errors`, `Transaction`, `Event`, `Refunds`, and `ChargeStatus` for a kept-card charge).

| Command | Effect |
| --- | --- |
| `RecordOfflinePaymentAsync(id, amount, reference, note)` | Records money received outside the site, capped at what is owed; raises `PaymentRecordedAsync` |
| `MarkPaidAsync(id, note)` | Records the rest as an offline payment |
| `CancelAsync(id, reason)` | Cancels a collectable transaction; reason to `TransactionEvent.Reason` |
| `AdjustAsync(id, amount, kind, reason)` | `TransactionAdjustmentKind` `Discount`, `Waiver`, `Credit` or `WriteOff`; never above `OutstandingAmount`; nothing left owing makes it `Paid` with `SettlementMethod = "adjusted"`; no payment handler runs |
| `SendInvoiceAsync(id)` | Issues `INV-` number and sends the invoice with the pay link (needs Transaction Reminders; refused for auto-collected) |
| `RefundAsync(id, amount?, reason)` | Refunds the confirmed attempts newest first through `ICheckoutRefundService` (needs Checkout); from a payment that settled several transactions only this one's share |
| `ChargeSavedPaymentMethodAsync(id, savedPaymentMethodId)` | Charges what is owed to a card the owner kept (`ISavedPaymentMethodStore`), without the owner present, through the checkout (needs Checkout and a provider that charges saved methods) |

```csharp
public sealed class FinancialAid
{
    private readonly ITransactionCommandService _commands;

    public FinancialAid(ITransactionCommandService commands)
    {
        _commands = commands;
    }

    public async Task<IReadOnlyList<string>> WaiveAsync(string transactionId, decimal amount)
    {
        var result = await _commands.AdjustAsync(transactionId, amount, TransactionAdjustmentKind.Waiver, "Financial aid");

        return result.Errors.ToArray();
    }
}
```

### Charging a kept card

`ChargeSavedPaymentMethodAsync` collects a transaction from the card its owner kept, for example a renewal charged to the card on file, and lets you fall back to the invoice. It refuses without contacting the provider a transaction that owes nothing, a card whose `CustomerPaymentMethod.OwnerId` is not `Transaction.OwnerId`, and an expired card (`NotCharged`). A charge of the same transaction still settling is finished instead of repeated (lock `Transactions:Charge:{id}`). It starts a checkout on the owner's behalf and begins the payment with `CheckoutPaymentDataKeys.ForOffSessionCharge(card)`, so a taken payment is verified, recorded, receipted and raised to the handlers like any checkout payment. A charge not taken adds a `TransactionEventType.PaymentFailed` (**Payment failed**) timeline event and leaves the transaction as it was.

| `SavedPaymentMethodChargeStatus` | Meaning | `Succeeded` |
| --- | --- | --- |
| `Succeeded` | Taken and recorded | `true` |
| `Pending` | Accepted, not confirmed; the reconciliation sweep records it; do not charge again | `true` |
| `Declined` | The provider refused it | `false` |
| `RequiresAuthentication` | The bank wants the cardholder to approve (`PaymentBeginResult.RequiresAuthentication`); the owner must pay, for example from the pay link | `false` |
| `Failed` | Could not start (provider unreachable); nothing charged | `false` |
| `NotCharged` | Refused before the provider was asked | `false` |

```csharp
public sealed class RenewalCollector
{
    private readonly ITransactionCommandService _commands;
    private readonly ISavedPaymentMethodStore _paymentMethods;

    public RenewalCollector(ITransactionCommandService commands, ISavedPaymentMethodStore paymentMethods)
    {
        _commands = commands;
        _paymentMethods = paymentMethods;
    }

    public async Task CollectAsync(Transaction renewal, CancellationToken cancellationToken)
    {
        var card = await _paymentMethods.GetDefaultAsync(renewal.OwnerId, cancellationToken);

        var result = card is null
            ? null
            : await _commands.ChargeSavedPaymentMethodAsync(renewal.ItemId, card.ItemId, cancellationToken);

        // No card, a decline, or a bank that wants the cardholder: send the invoice instead.
        if (result?.ChargeStatus is not (SavedPaymentMethodChargeStatus.Succeeded or SavedPaymentMethodChargeStatus.Pending))
        {
            await _commands.SendInvoiceAsync(renewal.ItemId, cancellationToken);
        }
    }
}
```

Only server code may call it; never pass a kept payment method id from a request without checking whose card it is. The Test payments provider declines an off-session charge ending in `02` and refuses one ending in `03` as needing approval.

## Reacting to changes

| Contract | Raised |
| --- | --- |
| `ITransactionPaymentHandler.PaymentRecordedAsync(TransactionPaymentRecordedContext)` | Once per payment applied, however paid (`context.Transaction`, `context.Payment` with `Amount`, `Method`, `ReceiptNumber`) |
| `IPaymentRefundHandler.RefundSucceededAsync(PaymentRefund)` | Once per refund that succeeds (immediately, by webhook, by reconciliation, or settled by hand) |
| `ITransactionEventHandler` (`TransactionEventHandlerBase`) | `CreatedAsync`, `StatusChangedAsync(transaction, previous, current)`, `CanceledAsync(transaction, reason)`, `BecameOverdueAsync` (once, by the reminder sweep, even when reminders are off) |

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
}

// services.AddScoped<ITransactionEventHandler, RegistrationPaymentStatusHandler>();
```

`TransactionRefundHandler` records each refund on its transaction (`Refunded` event keyed by refund id, `AmountRefunded`; fully refunded becomes `Refunded`; `AmountPaid` never goes down).

## Invoices, pay links and reminders

- `ITransactionInvoiceService.EnsureInvoiceNumberAsync` numbers a transaction the first time its owner is told (`INV-1001`...). `IFinancialDocumentNumberGenerator` (`SequentialFinancialDocumentNumberGenerator`) issues `INV-` and `R-` series under a distributed lock, starting at 1001; replace it for your own numbering.
- `ITransactionPayLinkService.CreatePayUrlAsync` makes a data-protected link to `transactions/pay/{token}` (route `TransactionsPay`) valid 60 days. It opens a public invoice page with **Pay** for guests and customers without a password. Links use the site **Base URL**; without it a link made outside a request is omitted.
- Reminders (`ITransactionReminderService`: `SendReminderAsync`, `SendUpcomingReminderAsync`, `SendInvoiceAsync`) go through `ICustomerOwnerNotifier`: users by `INotificationService`, guests by email.

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
        "UpcomingReminderDays": 3
      }
    }
  ]
}
```

## Customer pages in the site theme

| Route | Name | Shape | Needs |
| --- | --- | --- | --- |
| `my-transactions` | `TransactionsStatement` | `TransactionStatement` | Transactions |
| `my-transactions/invoices/{itemId}` | `TransactionsInvoice` | `TransactionInvoice` | Transactions, Receipts |
| `my-transactions/receipts/{itemId}/{paymentId}` | `TransactionsReceipt` | `TransactionReceipt` | Transactions, Receipts |

The admin versions (`[CustomerAdminPage]`) are named `MyTransactions`, `TransactionInvoice` and `TransactionReceipt` (`TransactionsConstants.RouteNames.AdminStatement`, `AdminInvoice`, `AdminReceipt`). Both sides build through `ITransactionCustomerPageService` (`BuildStatementAsync`, `BuildInvoiceAsync`, `BuildReceiptAsync`), which answers `Forbidden` without **View own transactions** or **Manage transactions** and `NotFound` for someone else's transaction, an unissued invoice or a non-payment event. Override a page with a theme template of the shape name, for example `Views/TransactionInvoice.cshtml`. `ITransactionInvoiceBuilder` and `ITransactionReceiptBuilder` are registered only with Receipts, so resolve them as optional.

Links in receipts and reminders open the admin pages by default; switch to the site pages:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Transactions": {
        "CustomerPages": {
          "LinkTarget": "Site"
        }
      }
    }
  }
}
```

or `services.Configure<TransactionCustomerPagesOptions>(options => options.LinkTarget = TransactionCustomerLinkTarget.Site)`. A portal controller example is in [references/customer-pages.md](references/customer-pages.md).

## Admin screens and permissions

| Permission | Grants |
| --- | --- |
| `ManageTransactions` | **Commerce > Transactions** report and console (reminder, record payment, mark paid, adjust or waive, send invoice, cancel, note); any invoice or receipt |
| `ManageTransactionSettings` | Reminder settings; also the Offline payments settings of Pay Later |
| `ViewOwnTransactions` | **My Transactions**, own invoices and receipts, paying them |
| `ManageRefunds` | **Commerce > Payments** (every attempt, expected and settled amounts, gateway reference), **Commerce > Refunds** (manual settlement of `PendingManualReview`) and **Commerce > Payment issues**; the only place a refund starts in the admin |

Every list uses Orchard Core's `AdminList` shape (see `orchardcore-admin-lists`).

**Commerce > Payment issues** (`ManageRefunds`) lists money operations that failed and must be finished by hand (a compensation refund the checkout could not issue, a subscription cancel, pause or resume the provider refused, a coupon use not counted after payment), with **Mark resolved**. Record your own with `IPaymentIssueRecorder.RecordAsync(PaymentIssue)` (Transactions.Abstractions; `services.AddPaymentIssues()` from Transactions.Core, registered by Checkout); built-in kinds are in `PaymentIssueKinds` (`RefundFailed`, `GatewaySubscriptionChangeFailed`, `DiscountRedemptionFailed`). Recording never throws.

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) enabled, as the Report Builder does, Transactions registers the **Transactions** data source (`Transactions`). Outstanding balances are rows with status `Outstanding` or `PartiallyPaid`. Notes, history and settlement references are never exposed. See `orchardcore-report-builder`.

| Data set | One row per | Needs | Permission |
| --- | --- | --- | --- |
| `Transactions` | Ledger transaction | Transactions | `ManageTransactions` |
| `PaymentAttempts` | Payment the checkout tried to take | Transactions and Checkout | `ManageRefunds` |
| `PaymentRefunds` | Refund of a payment | Transactions and Checkout | `ManageRefunds` |

## Upgrading from 2.x

- The ledger types moved here from Checkout: `CrestApps.OrchardCore.Transactions.Models.PaymentAttempt`, `PaymentRefund`, `IPaymentAttemptStore`, `IPaymentRefundStore` (`CrestApps.OrchardCore.Transactions.Services`), stores and indexes in `CrestApps.OrchardCore.Transactions.Core`. Data is unchanged.
- Console logic moved to `ITransactionCommandService`; `OutstandingAmount` now subtracts `AmountAdjusted`.
- `ITransactionInvoiceBuilder`, `ITransactionReceiptBuilder` and their view models moved to the abstractions package (same namespaces).
- `TransactionSource.DisplayName` is a `LocalizationSource`.
- A declined installment charge marks its transaction `Failed`.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| **Pay** shows a message instead of a checkout | Checkout is disabled, or the transaction is terminal |
| Pay Later not offered when paying a balance | By design: deferred providers cannot settle an existing balance |
| `CheckoutRejectedException` paying several transactions | Different customers or currencies, or one transaction no longer owes anything |
| A kept-card charge returns `RequiresAuthentication` | The bank wants the cardholder; send the invoice or pay link instead of retrying |
| Settled amount lower than the balance | The provider confirmed less; the transaction is `PartiallyPaid` |
| "Reload and try again" | Two managers edited at once (concurrency check) |
| No reminders | Transaction Reminders off, **Enabled** off, notification methods not configured, or guest without an email service |

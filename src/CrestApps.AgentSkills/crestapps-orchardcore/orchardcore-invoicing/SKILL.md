---
name: orchardcore-invoicing
description: Skill for the CrestApps Orchard Core Invoicing module (CrestApps.OrchardCore.Invoicing with Estimates, Billing, API, Collections and Recurring), invoices and credit notes posted to the Transactions ledger. Covers features, the Invoice and CreditNote model, IInvoiceManager, IInvoiceCommandService (issue, void, write off, approval, revision), ICreditNoteCommandService, the ledger mirror, tax through Taxation, deposits, payment schedules, auto charge, PDF archive and share links, My invoices, estimates, recurring plans, late fees and debit notes, statements, unbilled charges, Time Tracker billing, usage meters, the commerce JSON API, AI tools, IInvoiceObserver, IDocumentTaxProvider, IInvoiceDocumentContributor, workflows, webhooks, recipe steps, reports, admin menus and permissions. Use it for invoicing, quotes, recurring invoices, late fees, statements or usage billing. Strong matches include InvoicingConstants, InvoicingPermissions, InvoicingSettings, LateFeePolicy, RecurringPlan and UsageMeter.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Invoicing

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Invoicing module. Invoicing owns the documents: an invoice is drafted with lines, issued with a number and posted to the Transactions ledger as what a party owes. Everything about the money (payments, refunds, adjustments, reminders, pay links, saved-card charges) stays in the ledger, and the invoice mirrors what the ledger says.

Related skills: `orchardcore-transactions` (the ledger, payment terms, reminders, saved-card charges), `orchardcore-business-documents` (templates, number series, PDF rendering), `orchardcore-taxation` (rates and the tax breakdown), `orchardcore-exchange-rates`, `orchardcore-orders` (draft orders and quotes), `orchardcore-time-tracker`, `orchardcore-work-orders`, `orchardcore-accounts`.

## Guidelines

- Install `CrestApps.OrchardCore.Invoicing` in the web/startup project. From your own module reference only `CrestApps.OrchardCore.Invoicing.Abstractions` (models, contracts, `InvoicingConstants`) and `CrestApps.OrchardCore.Invoicing.Core` (`InvoicingPermissions`, `AddDocumentTaxProvider`, services), never the module project.
- Put integration code in a `[RequireFeatures(InvoicingConstants.Features.Area)]` startup unless your feature depends on Invoicing.
- Never post a debt to the ledger and call it an invoice, never number or render an invoice yourself, and never change an issued invoice outside the commands. `InvoiceHandler` refuses any change to an invoice that is no longer a draft.
- Never set `AmountPaid`, `AmountCredited`, `AmountDue` or `Settlement`: `InvoiceLedgerSync` copies them from the transaction.
- The command services check no permission. Authorize the user first with `InvoicingPermissions`.
- Every command takes the distributed lock `Invoicing:Invoice:{id}`, is idempotent (`AlreadyDone`), and returns refusals in `Errors` instead of throwing. A refused or failed issue takes no number and posts nothing.
- All recipe JSON is wrapped in `{ "steps": [...] }`. All C# classes are `sealed`, except view models.

## Features

| Feature | ID | Depends on |
| --- | --- | --- |
| Invoicing | `CrestApps.OrchardCore.Invoicing` | Transactions, Payment Terms, Business Documents, Business Documents PDF, Resources, Sales Tax (`CrestApps.OrchardCore.Taxation`) |
| Invoicing Estimates | `CrestApps.OrchardCore.Invoicing.Estimates` | Invoicing |
| Invoicing Billing | `CrestApps.OrchardCore.Invoicing.Billing` | Invoicing (time billing lights up with Time Tracker) |
| Invoicing API | `CrestApps.OrchardCore.Invoicing.Api` | Invoicing |
| Invoicing Collections | `CrestApps.OrchardCore.Invoicing.Collections` | Invoicing |
| Invoicing Recurring | `CrestApps.OrchardCore.Invoicing.Recurring` | Invoicing |

Optional integrations are `[RequireFeatures]` startups: Workflows, Commerce Webhooks (`CrestApps.OrchardCore.Commerce.Webhooks`), Data Pipelines Core, Reports, Deployment, CrestApps Recipes, Accounts, Work Orders, Orders, Exchange Rates, Installment Plans, AI.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Invoicing",
        "CrestApps.OrchardCore.Invoicing.Estimates",
        "CrestApps.OrchardCore.Invoicing.Billing",
        "CrestApps.OrchardCore.Invoicing.Collections",
        "CrestApps.OrchardCore.Invoicing.Recurring",
        "CrestApps.OrchardCore.Invoicing.Api"
      ],
      "disable": []
    }
  ]
}
```

## The model

Invoices and credit notes are catalog records in the `Invoicing` collection (`InvoicingConstants.CollectionName`), saved with concurrency checks. `Invoice` and `CreditNote` share the `InvoicingDocument` header:

- Header: `Number`, `BillTo` (`DocumentParty`: `Party` as a `PartyRef`, `Contact`, `ShippingAddress`, `TaxId`, `TaxTreatment`, `ExemptionReason`, `CopyTo`), `Issuer`, `Currency`, `ExchangeRate`, `BaseCurrency`, `PricesIncludeTax`, `Lines`, `Charges`, `Taxes`, `Totals`, `IssueDate`, `ServiceStart`, `ServiceEnd`, `PurchaseOrderNumber`, `Reference`, `SourceType`, `SourceId`, `Notes`, `Terms`, `Footer`, `InternalNote`, `CustomFields`, `TemplateId`, `Culture`, `IssuedUtc`, `ContentHash`, `ArchiveFile`, `History`.
- `Invoice` adds `Kind` (`Standard`, `DebitNote`, `Prepayment`), `Status` (`Draft`, `PendingApproval`, `Open`, `Void`, `Uncollectible`), `Settlement` (`Unpaid`, `PartiallyPaid`, `Paid`), `Delivery`, `DueDate`, `PaymentTermId`, `TransactionId`, `AmountPaid`, `AmountCredited`, `AmountDue`, `AllowPartialPayment`, `AutoCharge`, `RemindersPaused`, `LateFeesExempt`, `RevisionOfId`, `ReplacedById`, `RelatedInvoiceId`, `BilledItems`, `Approval`, `Schedule`, `InstallmentPlanId`.
- `DocumentLine`: `Id`, `Kind` (`Item`, `Service`, `Time`, `Expense`, `Usage`, `Fee`, `Deposit`, `Text`, `Section`), `ProductId`, `Sku`, `Name`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `DiscountPercent` or `DiscountAmount`, `TaxCategoryCode`, `Taxes`, `SourceType`, `SourceId`. Leave the net, tax and total to the calculator.

`IInvoiceManager` (a catalog manager) creates and changes drafts and adds `FindByTransactionIdAsync`, `FindByNumberAsync` and `PageAsync(page, pageSize, InvoiceQuery)` (`Search`, `Status`, `Settlement`, `BillTo`, `OverdueOn`, `SourceType`, `SourceId`, `RelatedInvoiceId`, `IssuedOnly`, `OnlyBillTo`). `InvoiceIndex` is declared `.ExclusiveToDocumentType().SingleRecordPerDocument()`.

## Commands

`IInvoiceCommandService` returns an `InvoiceCommandResult` (`Succeeded`, `Invoice`, `AlreadyDone`, `NeedsApproval`, `Errors`).

| Command | Effect |
| --- | --- |
| `IssueAsync(id)` | Fills what the draft leaves empty from the bill-to's billing profile, the user store, the party directory and the settings; validates; checks the issue date (`AllowFutureIssueDate`, `ChronologicalIssueDates`); calculates; sets the due date from the payment term (`IPaymentTermCalculator`); takes the number (`IFinancialDocumentNumberGenerator`, kind `Invoice`); posts the transaction and opens the invoice, in one unit of work. A zero total posts nothing and is paid at once. Above the approval threshold it returns `NeedsApproval` and the draft stays. |
| `VoidAsync(id, reason)` | Refused when an issued credit note credits it; cancels the transaction through `ITransactionCommandService.CancelUnpaidAsync` (refused when anything was paid). Keeps the number. |
| `WriteOffAsync(id, reason)` | `WriteOff` adjustment for what is still owed; status `Uncollectible`. |
| `ReinstateAsync(id, reason)` | Takes the write-off back and reopens the invoice. |
| `SubmitForApprovalAsync(id, submittedBy)`, `ApproveAsync(id, approvedBy)`, `RejectAsync(id, rejectedBy, reason)` | Approval of invoices at or above `InvoicingSettings.ApprovalThreshold`; the submitter can't approve. |
| `ReviseAsync(id)` | Drafts a copy (`RevisionOfId`); issuing it voids the original (`ReplacedById`). |

The transaction an invoice posts has `ReferenceType` `Invoice`, `ReferenceId` the invoice id, source `Invoicing`, the invoice number, the bill-to as payer (`SetPayer`) with `PayerContact`, the tax lines, the exchange rate and part payments when allowed, due at the end of the due date in the site's time zone. A user bill-to owns it; anyone else is a guest owner reached at the bill-to's e-mail.

**Credit notes** (`ICreditNoteCommandService`, under the invoice's lock): `CreateDraftAsync(invoiceId)` drafts one line per creditable invoice line (one draft at a time), `IssueAsync(creditNoteId)` validates against what is creditable, takes the number (kind `CreditNote`) and moves the money by `Disposition`: `ReduceBalance` (a `Credit` adjustment), `Refund` (`ITransactionCommandService.RefundAsync`), `CustomerCredit` (`ICustomerCreditService.AddAsync`, needs Customers Store Credit) or `RefundedElsewhere` (used by Orders; nothing moves). An issued credit note is never deleted.

## Delivery, customers and payment

- `IInvoiceDocumentBuilder` builds a `BusinessDocument` of kind `Invoice` or `CreditNote` (`InvoicingConstants.BusinessDocumentKinds`); `IInvoiceDocumentContributor` adds to it; `IInvoicePdfService` renders it. Issuing archives the PDF in a private tenant store (`App_Data/Sites/{tenant}/InvoiceArchives`) and later downloads and e-mails use that file.
- `IInvoiceSender` sends the commerce e-mail kind `invoice-issued` with the PDF, `PayUrl` and `ViewUrl`. With **Sending a draft issues it** (`IssueOnSend`, on by default) a draft is issued first.
- `IInvoiceShareLinkService` signs `invoices/view/{token}` (route `InvoicingView`) for `ShareLinkDays`; the first open by someone without View invoices records `ViewedUtc`.
- **My invoices** (`my-invoices`, route `InvoicingMyInvoices`, **View own invoices**, in the customer account menu) lists issued invoices the user may see through `IInvoiceVisibilityService` (themselves, their customer record, accounts they are an admin of); **Pay selected** pays several in one checkout. With Accounts, the account page gets an **Invoices** section.
- `AutoCharge` invoices are charged to the payer's default saved card through Transactions' `ISavedPaymentMethodChargeService` on the morning of the due day; when no card can be charged or retries run out, the invoice is e-mailed instead.
- A payment schedule (`Schedule`, with Installment Plans) splits the debt into installment transactions at issue.
- Deposits: `IInvoicePrepaymentService` (`RequestDepositAsync`, `ListDeductibleAsync`, `DeductAsync`) makes `Prepayment` invoices and deducts them with negative `Deposit` lines.
- With Orders, Invoicing issues the invoice of a draft order (**Send invoice**) that is paid through the order's checkout, and invoices orders and deferred debts (Pay Later, account terms, renewals) by taking over their ledger debt. With Work Orders, `InvoicingWorkOrderBilling` bills work orders.
- With Exchange Rates and a reporting currency, issue records the issue day's rate on the invoice and its transaction.

Tax is the Taxation module's: `TaxationDocumentTaxProvider` asks Taxation's rules for each line and `ITaxBreakdownCalculator` works out the breakdown, rounded at **Settings > Commerce > Tax calculation**. A credit note keeps the taxes its lines carry (`KeepTypedTaxes`).

Estimates, recurring plans, late fees, statements, billing and usage are in [references/invoicing-features.md](references/invoicing-features.md). Code samples are in [references/invoicing-extensions.md](references/invoicing-extensions.md).

## Admin menu

| Location | Feature | Permission |
| --- | --- | --- |
| **Commerce > Invoices** (also credit notes, from an invoice) | Invoicing | View invoices |
| **Commerce > Estimates** | Estimates | View invoices (changing them needs Manage estimates) |
| **Commerce > Recurring invoices** | Recurring | View invoices (changing them needs Manage recurring invoices) |
| **Commerce > Late fees**, **Commerce > Statements** | Collections | Manage invoicing settings, View invoices |
| **Commerce > Unbilled charges**, **Commerce > Usage meters** | Billing | Create invoices and change drafts |
| **Settings > Commerce > Invoicing** (group `invoicing`) | Invoicing | Manage invoicing settings |
| Customer account menu **Invoices** (`~/my-invoices`) | Invoicing | View own invoices |

## Permissions

| Permission | Display name | Notes |
| --- | --- | --- |
| `ViewInvoices` | View invoices | Every invoice and credit note; implied by the changing permissions |
| `ManageInvoices` | Create invoices and change drafts | Drafts, deposits, unbilled work, usage meters |
| `IssueInvoices` | Issue invoices | Security critical |
| `VoidInvoices` | Void and write off invoices | Security critical |
| `ManageCreditNotes` | Issue credit notes | Security critical |
| `RecordInvoicePayments` | Record invoice payments | Security critical |
| `ApproveInvoices` | Approve invoices | Security critical |
| `ManageEstimates` | Manage estimates | Estimates feature |
| `ManageRecurringInvoices` | Manage recurring invoices | Security critical; Recurring feature |
| `ManageInvoicingSettings` | Manage invoicing settings | Settings and late-fee policies |
| `ViewOwnInvoices` | View own invoices | Authenticated users |

Administrators and store managers get every permission. None of the changing permissions implies another.

## Settings

`InvoicingSettings` travels with Orchard's `Settings` step (schema `InvoicingSettings` with CrestApps Recipes):

```json
{
  "steps": [
    {
      "name": "Settings",
      "InvoicingSettings": {
        "DefaultPaymentTermId": "4paymentterm00000000000001",
        "DefaultCurrency": "USD",
        "PricesIncludeTax": false,
        "AllowPartialPayment": true,
        "IssueOnSend": true,
        "ApprovalThreshold": 10000,
        "ShareLinkDays": 60,
        "EstimateValidDays": 30,
        "AllowFutureIssueDate": false,
        "ChronologicalIssueDates": false,
        "DefaultNotes": "Thank you for your business.",
        "DefaultTerms": "Payment is due by the due date."
      }
    }
  ]
}
```

Payment terms are the Transactions **Payment Terms** feature's (`PaymentTerms` step); templates and number series are Business Documents'.

## Recipe and deployment steps

| Step | Entries key | Feature | Notes |
| --- | --- | --- | --- |
| `Invoices` | `Invoices` | Invoicing | Imported by `InvoicingDocumentImporter`; an issued document keeps its number and never posts; the deployment step option `IncludeTransactions` (on by default) writes a `Transactions` step first |
| `CreditNotes` | `CreditNotes` | Invoicing | Import invoices before their credit notes |
| `Estimates` | `Estimates` | Estimates | |
| `RecurringPlans` | `Plans` | Recurring | |
| `LateFeePolicies` | `Policies` | Collections | |
| `UsageMeters` | `Meters` | Billing | Meters only, not usage |

An open invoice whose transaction is not on the site can't be paid there until the `Transactions` step brings it. Samples are in [references/invoicing-features.md](references/invoicing-features.md#recipes).

## Workflows, webhooks and AI

- Events (category **Invoicing**, with Workflows): `InvoiceCreatedEvent`, `InvoiceIssuedEvent`, `InvoiceSentEvent`, `InvoiceViewedEvent`, `InvoicePaymentReceivedEvent`, `InvoicePaidEvent`, `InvoiceOverdueEvent`, `InvoiceVoidedEvent`, `CreditNoteIssuedEvent`; with Estimates `EstimateSentEvent`, `EstimateAcceptedEvent`, `EstimateDeclinedEvent`, `EstimateExpiredEvent`; with Recurring `RecurringInvoiceGeneratedEvent`, `RecurringRunFailedEvent`.
- Tasks: `CreateInvoiceTask`, `IssueInvoiceTask`, `SendInvoiceTask`, `RecordInvoicePaymentTask`, `VoidInvoiceTask`, `CreateCreditNoteTask` (Liquid fields; outcomes `Done`, `NotFound`, `Failed`; output name `Invoice` by default).
- Webhooks (with Commerce Webhooks, `InvoicingConstants.WebhookEvents`): `invoice.created`, `invoice.issued`, `invoice.sent`, `invoice.viewed`, `invoice.payment-received`, `invoice.paid`, `invoice.overdue`, `invoice.voided`, `credit-note.issued`, `estimate.sent`, `estimate.accepted`, `estimate.declined`, `estimate.expired`, `estimate.converted`, `recurring-plan.invoice-generated`, `recurring-plan.run-failed`.
- AI tools (with AI, **Invoicing** category, admin profiles only): `findInvoices`, `summarizeReceivables` (View invoices), `draftInvoice`, `sendInvoiceReminder` (Create invoices and change drafts).

## Reports and data

With Data Pipelines Core, the **Invoicing** data source (`CommerceReportDataSets.InvoicingSource`) has **Invoices**, **Invoice lines**, **Invoice taxes**, **Credit notes**, plus **Estimates** and **Recurring plans** with their features (View invoices). With Reports, **Reports > Commerce** gets Receivables aging, Invoice register, Payments collected, Invoiced sales by customer, Invoiced sales by item and Invoiced tax summary. `InvoicingPersonalDataContributor` exports and scrubs invoices billed to a person (numbers, amounts and archived PDFs are kept).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Issue returns `NeedsApproval` | The total is at or above **Approve invoices from**; submit it for approval |
| Issue refused for the date | `AllowFutureIssueDate` off, or `ChronologicalIssueDates` on and an earlier date than the last issued invoice |
| Void refused | Something was paid, or an issued credit note credits it; issue a credit note or write off instead |
| Invoice shows no way to pay | Its transaction is not on this site (imported without `IncludeTransactions`), or it belongs to an order and pays through the order's checkout |
| Invoice amounts not updated after a payment | The mirror runs after the ledger saves; opening the invoice page catches up |
| Order or deferred-debt invoice stays a draft | Its total differs from the ledger debt; correct and issue it |
| No late fees | Collections off, no enabled policy, invoice `LateFeesExempt`, party exempt, or still in its grace days |
| Time is not offered for billing | Time Tracker off, or the entries are not ended or already billed |
| PDF changed after a template change | It doesn't: issued documents use the archived PDF |

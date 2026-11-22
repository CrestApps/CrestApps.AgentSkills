# Invoicing Features, API and Recipes

The sub-features of `CrestApps.OrchardCore.Invoicing`, the commerce JSON API and recipe samples. See the main [SKILL.md](../SKILL.md) for the model and commands.

## Estimates (`CrestApps.OrchardCore.Invoicing.Estimates`)

`Estimate` is a catalog record with the invoice's header and lines (`EstimateIndex`). `IEstimateCommandService` (each command under the estimate's lock, idempotent):

- **Send** numbers a draft from the `Invoicing.Estimate` series (`EST-{seq}` from 1001), sets an expiry `EstimateValidDays` days later when it has none (30 by default) and locks it. Without e-mail, **Mark sent** numbers it so its link can be shared. `EstimateSender` e-mails the `estimate-sent` kind with the PDF and the link.
- **Accept** records the customer's typed name, time, address and the terms shown (`EstimateAcceptance`) while valid, and invoices what `OnAcceptance` says (nothing, the whole of it, or its deposit). **Decline** records an optional reason.
- **Revise** drafts a copy numbered after it (`EST-1001-2`) and cancels the original. **Cancel** withdraws one not invoiced.
- **Convert** `Full` makes a draft invoice (`SourceType` `Estimate`); `Deposit` makes a `Prepayment` invoice with one `Deposit` line.
- **Progress invoicing**: `InvoiceProgressAsync(estimateId, percentByLine)` invoices a percent of each chosen line (**Design (30%)**); `GetInvoicedPercentsAsync` reads what is invoiced; never past 100%.
- Status: `Draft`, `Sent`, `Accepted`, `Declined`, `Expired`, `Canceled`, `Invoiced`.
- The customer page is `estimates/view/{token}` (route `InvoicingEstimateView`, signed by `EstimateShareLinkService`).
- **Estimate Expiry** background task (`EstimateExpiryBackgroundTask`, `15 * * * *`) expires sent estimates past their date in the site's time zone.
- Permission **Manage estimates** (`ManageEstimates`); making the invoice also needs **Create invoices and change drafts**.

## Recurring plans (`CrestApps.OrchardCore.Invoicing.Recurring`)

`RecurringPlan` (an `InvoicingDocument`) has `Name`, `Rule` (RFC 5545, expanded by the Calendars recurrence engine), `StartDate`, `EndDate`, `MaxRuns`, `Mode` (`Draft`, `Issue`, `IssueAndSend`, `IssueAndCharge`), `Status` (`Draft`, `Active`, `Paused`, `Completed`, `Canceled`), `NextRunDate`, `RunCount`, `PaymentTermId`, `PriceIncreasePercent` with `PriceIncreaseEveryRuns`, `BillUnbilledCharges`, `UsageCharges` and `Runs` (`Occurrence`, `RunUtc`, `Outcome`, `InvoiceId`, `InvoiceNumber`, `Message`).

- `IRecurringPlanService`: **Activate**, **Pause**, **Resume** (from today, skipping what was missed), **Cancel**, **Run**.
- A run makes each due occurrence's invoice (at most 12 per plan per sweep), `SourceType` `RecurringPlan` and `SourceId` `{planId}:{yyyy-MM-dd}`, so an occurrence is never invoiced twice. The period is written into line names, descriptions and notes with `{{ period.start }}`, `{{ period.end }}`, `{{ period.month }}`, `{{ period.year }}` or `{{ period.start | date: "MMMM yyyy" }}`.
- The draft is saved first; issuing and sending run in their own unit of work, so a failed issue keeps the draft and the run is recorded as failed. `IssueAndCharge` marks the invoice `AutoCharge`.
- **Recurring Invoices** background task (`RecurringInvoiceBackgroundTask`, `*/30 * * * *`).
- **Start** refuses a plan with no line or usage charge, or no customer.
- Permission **Manage recurring invoices** (security critical).

## Collections (`CrestApps.OrchardCore.Invoicing.Collections`)

**Late fees.** `LateFeePolicy`: `Name`, `Description`, `Enabled`, `Kind` (`Flat`, `Percent`), `Amount`, `Percent`, `GraceDays`, `RepeatEveryDays` (0 charges one fee), `MaxFees`, `CapAmount`, `MinimumBalance`, `Taxable`, `SendDebitNote`. `ILateFeeService` charges each step as a **debit note** (`Invoice.Kind = DebitNote`, `RelatedInvoiceId` the invoice, `SourceType` `LateFee`, `SourceId` `{invoiceId}:{policyId}:{step}`), never as a line on an issued invoice. **Invoice Late Fees** background task (`LateFeeBackgroundTask`, `15 * * * *`) assesses up to 200 overdue invoices per run. An invoice with `LateFeesExempt`, a debit note, or a party marked **Charge no late fees** (Transactions' `PartyCollectionSettings`) gets none. The invoice page offers **Charge the late fee due now** (**Issue invoices**).

**Statements.** `IInvoiceStatementService` builds an `InvoiceStatement` per customer and currency: `OpenItem` (owed on a day, aged not due, 1-30, 31-60, 61-90, over 90) or `Activity` (opening balance, entries with the running balance). `BuildDocumentAsync` prints it as business document kind `InvoiceStatement`; `SendAsync` e-mails the `invoice-statement` kind. **Commerce > Statements** (`StatementsAdminController`, View invoices).

## Billing (`CrestApps.OrchardCore.Invoicing.Billing`)

- `IBillableSource` (`Type`, `ListUnbilledAsync`, `MarkBilledAsync`, `ReleaseAsync`) offers unbilled work. Built in: `charges` (`ChargesBillableSource`, from `AddInvoicingBilling()`) and `time` (`TimeTrackerBillableSource`, with Time Tracker). Lines carry `SourceType` `billable:{Type}`.
- `IBillingService` adds chosen items to a draft (`AddToInvoiceAsync`, `AddToDraftAsync`) and keeps `Invoice.BilledItems` in step: removing a line, deleting a draft, voiding an invoice or fully crediting a line releases the item.
- `BillableItem` (a charge to bill later): `Party`, `PartyName`, `Kind`, `Date`, `Description`, `Quantity`, `Unit`, `UnitCost`, `MarkupPercent`, `Currency`, `TaxCategoryCode`. **Commerce > Unbilled charges**. A billed charge can't change.
- Time Tracker entries gain `BilledInvoiceId` and `BilledLineId`; billed time can't be changed or removed.
- A draft invoice's page has an **Unbilled work** card; a recurring plan with `BillUnbilledCharges` adds charges up to each run.

**Usage meters.** `UsageMeter`: `Name`, `Code` (lowercase, unique, such as `api-calls`), `Unit`, `Aggregation` (`Sum`, `Maximum`, `Last`), `Enabled`. `IUsageService` records a `UsageRecord` once per meter and event id. A plan's `UsageCharges` (`RecurringUsageCharge`: `MeterId`, `Name`, `TaxCategoryCode`, `TierMode` `Graduated` or `Volume`, `Tiers` of `UpTo` and `UnitPrice`, the last without `UpTo`) bill usage in arrears. **Commerce > Usage meters** lists meters and records usage by hand.

## Commerce JSON API (`CrestApps.OrchardCore.Invoicing.Api`)

On the commerce API group (`api/commerce/v1`, `Api` authentication scheme with an API token, Problem Details errors, `Idempotency-Key` on commands, tag `Invoices`):

| Method and path | Permission |
| --- | --- |
| `GET invoices` (`status`, `settlement`, `billTo`, `q`, `skip`, `take`) | View invoices |
| `GET invoices/{invoiceId}`, `GET invoices/by-number/{number}` | View invoices |
| `POST invoices` (`issue: true` also issues) | Create invoices and change drafts (+ Issue invoices) |
| `POST invoices/{invoiceId}/issue` | Issue invoices |
| `POST invoices/{invoiceId}/send` (`to`) | Create invoices and change drafts |
| `POST invoices/{invoiceId}/void` (`reason`) | Void and write off invoices |
| `POST invoices/{invoiceId}/payments` (`amount`, `method`, `reference`, `note`) | Record invoice payments |
| `GET invoices/{invoiceId}/pdf` | View invoices |
| `POST usage` (`meter`, `customer`, `quantity`, `occurredAt`, `eventId`; with Billing) | Create invoices and change drafts |
| `GET me/invoices`, `GET me/invoices/{invoiceId}` | View own invoices |

A refused command answers `409` with code `invalid-state`; an invalid draft answers `400`.

```json
{
  "billTo": { "party": "Account:4k8f2n0q1w3e5r7t9y2u4i6o8p" },
  "currency": "USD",
  "reference": "PO-2026-118",
  "lines": [
    { "kind": "Service", "name": "Consulting", "quantity": 10, "unit": "h", "unitPrice": 150, "taxCategoryCode": "services" }
  ],
  "issue": true
}
```

A guest bill-to sends `name` and `email` instead of `party`.

## Recipes

```json
{
  "steps": [
    {
      "name": "LateFeePolicies",
      "Policies": [
        {
          "ItemId": "4latefeepolicy000000000001",
          "Name": "1.5% monthly",
          "Enabled": true,
          "Kind": "Percent",
          "Percent": 1.5,
          "GraceDays": 10,
          "RepeatEveryDays": 30,
          "MaxFees": 6,
          "CapAmount": 500,
          "MinimumBalance": 25,
          "Taxable": false,
          "SendDebitNote": true
        }
      ]
    },
    {
      "name": "UsageMeters",
      "Meters": [
        { "ItemId": "4usagemeter000000000000001", "Name": "API calls", "Code": "api-calls", "Unit": "calls", "Aggregation": "Sum", "Enabled": true }
      ]
    },
    {
      "name": "RecurringPlans",
      "Plans": [
        {
          "ItemId": "4recurringplan000000000001",
          "Name": "Monthly support retainer",
          "Rule": "FREQ=MONTHLY;BYMONTHDAY=1",
          "StartDate": "2026-12-01",
          "Mode": "IssueAndSend",
          "Status": "Draft",
          "Currency": "USD",
          "BillTo": { "Party": { "Kind": "Account", "Id": "4k8f2n0q1w3e5r7t9y2u4i6o8p" } },
          "Lines": [
            { "Id": "line1", "Kind": "Service", "Name": "Support retainer {{ period.month }} {{ period.year }}", "Quantity": 1, "UnitPrice": 1200 }
          ],
          "UsageCharges": [
            {
              "MeterId": "4usagemeter000000000000001",
              "Name": "API calls",
              "TierMode": "Graduated",
              "Tiers": [ { "UpTo": 10000, "UnitPrice": 0 }, { "UnitPrice": 0.002 } ]
            }
          ]
        }
      ]
    }
  ]
}
```

Import a plan as `Draft` and start it with **Start** on its page (or `IRecurringPlanService`), so its first occurrence is worked out by the service. The `Invoices` and `CreditNotes` steps take documents as the deployment steps write them: export from a site rather than writing them by hand.

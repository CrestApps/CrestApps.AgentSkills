---
name: orchardcore-payroll
description: Skill for the CrestApps Orchard Core Payroll module (CrestApps.OrchardCore.Payroll) with Pay Setup, US and Canada Payroll Filings, Garnishments, My Pay and Payroll AI Tools. Covers pay schedules and pay codes shared with the Workforce time clock, employers, permissions per employer, pay profiles with protected tax ids, elections and net pay splits, pay items, the calculation engine and IPayCalculationStep, IPayInputSource, pay runs approved through Approvals and paid by Banking direct deposit or checks, voids, income tax through Taxation, liabilities and deposits, Forms 941, 940, 945, W-2, 1099-NEC, T4 and RL-1, year end, garnishment orders, the payroll journal, data sets, reports, workflows, webhooks and recipe steps. Use this skill when requests mention payroll, pay runs, paychecks, pay stubs, pay codes, withholding, garnishments, W-2 or T4. Strong matches include PayRun, PayStatement, EmployeePayProfile, PayCode, PaySchedule, IPayRunService, AddPayInputSource and PayrollPermissions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Payroll

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Payroll module. Payroll calculates and prepares pay; the business deposits taxes and files returns. Taxes on income belong to Taxation's **Income Tax** feature (`orchardcore-taxation`), bank accounts, checks and direct deposit to Banking (`orchardcore-banking`), the approval of a pay run to Approvals (`orchardcore-approvals`), and people and their pay rates to Staff (`orchardcore-staff`).

## Guidelines

- Install `CrestApps.OrchardCore.Payroll` (and `CrestApps.OrchardCore.Banking` to pay by deposit or check) in the web/startup project. Your own modules reference `CrestApps.OrchardCore.Payroll.Abstractions` (models, managers, `AddPayCodeKind`, `AddPayInputSource`, `AddPaySetupUsage`) or `CrestApps.OrchardCore.Payroll.Core` (stores, engine, filings), never the module project.
- Payroll never references Workforce: the time clock depends on **Pay Setup** and hands hours to pay runs as an `IPayInputSource`.
- Never read or write payroll documents directly. Use the managers (`IEmployerManager`, `IPayProfileManager`, `IPayRunManager`, ...) and services (`IPayRunService`, `IPayProfileService`), which hold locks, keep accumulators consistent and tell observers.
- Authorize per employer with `IPayrollScopeService` (`GetScopeAsync`, `AuthorizeAsync(user, permission, employerId)`), never with a plain `AuthorizeAsync` on the global permission.
- Never log, return or export a tax id or a bank account number. `TaxId` is a `ProtectedValue`; read it only through `IPayProfileService.RevealTaxIdAsync`, which records the read.
- Pay calculation is pure: an `IPayCalculationStep` fills the context's lines and keeps nothing.
- All recipe JSON is wrapped in `{ "steps": [...] }`. All C# classes are `sealed`, except view models.

## Features

| Feature | ID | Depends on |
|---|---|---|
| Pay Setup | `CrestApps.OrchardCore.Payroll.PaySetup` | none (Workforce Time Clock and Payroll turn it on) |
| Payroll | `CrestApps.OrchardCore.Payroll` | Pay Setup, `CrestApps.OrchardCore.Staff`, `CrestApps.OrchardCore.Staff.Compensation`, `CrestApps.OrchardCore.Approvals`, Addresses, `CrestApps.OrchardCore.Taxation.IncomeTax` |
| United States Payroll Filings | `CrestApps.OrchardCore.Payroll.UnitedStates` | Payroll, `CrestApps.OrchardCore.Taxation.IncomeTax.UnitedStates` |
| Canada Payroll Filings | `CrestApps.OrchardCore.Payroll.Canada` | Payroll, `CrestApps.OrchardCore.Taxation.IncomeTax.Canada` |
| Garnishments | `CrestApps.OrchardCore.Payroll.Garnishments` | Payroll |
| My Pay | `CrestApps.OrchardCore.Payroll.SelfService` | Payroll |
| Payroll AI Tools | `CrestApps.OrchardCore.Payroll.AI` | Payroll, AI Services |

Payroll lights up more when other features are on: Banking (employer funding account, deposit blockers), Banking's **Direct Deposit** and **Checks** (payment methods), Teams (ledger code and work site jurisdictions), Notifications, Audit Trail, Workflows, Webhooks, Reports, Calendars, Data Pipelines and CrestApps Recipes.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Payroll",
        "CrestApps.OrchardCore.Payroll.UnitedStates",
        "CrestApps.OrchardCore.Payroll.Garnishments",
        "CrestApps.OrchardCore.Payroll.SelfService",
        "CrestApps.OrchardCore.Banking.Checks",
        "CrestApps.OrchardCore.Banking.DirectDeposit"
      ],
      "disable": []
    }
  ]
}
```

## Admin menu

Payroll adds a top-level **Payroll** group. Items appear only for users who hold the permission for at least one employer.

| Location | Shown with |
|---|---|
| **Payroll > Pay Runs** | `ViewPayRuns` (any employer) |
| **Payroll > Garnishments** | `ManageGarnishments` (Garnishments feature) |
| **Payroll > Liabilities and Filings > Liabilities / Filings / Year end** | `ManagePayrollFilings` |
| **Payroll > Setup > Employers** | `ManagePayrollSettings` |
| **Payroll > Setup > Pay Schedules / Pay Codes** | `ManagePaySetup` |
| **Settings > Payroll** (group `payroll`) | `ManagePayrollSettings` |
| **Commerce > Taxation > Income tax rules** | `ManageIncomeTaxRules` (Taxation) |

There is no employees list: a person's pay profiles are the **Payroll** card in the **Pay** section of their staff profile (**Staff > All Staff**). My Pay is linked from **Staff > My Profile** and at `~/Admin/my-pay`.

## Permissions

`PayrollPermissions` (Abstractions). Every permission in `PayrollPermissions.PerEmployer` also exists per employer (`PayrollPermissionTemplates.For(permission, employerId)`, named `{Permission}_{employerId}`); the role editor shows a group per employer such as **Payroll: Contoso**.

| Permission | Allows |
|---|---|
| `ManagePaySetup` | Pay schedules and pay codes |
| `ManagePayrollSettings` | Employers, registrations, ledger codes, tax setup, settings (security critical) |
| `ViewPayProfiles` / `ManagePayProfiles` | See or change pay profiles, pay items, elections |
| `ViewTaxIds` | See a full tax id; every view is recorded (security critical) |
| `ManageGarnishments` | Garnishment orders |
| `PreparePayRuns`, `ApprovePayRuns`, `ProcessPayRuns` (security critical), `VoidPayStatements` | Each step of a pay run; each implies `ViewPayRuns` |
| `ViewPayRuns` | Runs and their statements |
| `ViewPayrollReports` / `ExportPayrollData` (security critical) | Reports and data sets; exports |
| `ManagePayrollFilings` | Liabilities, deposits, filings, year end |
| `ViewPayOfManagedStaff` | A manager sees the pay of staff they manage (not per employer) |
| `ViewOwnPay`, `ManageOwnPayDetails` | One's own pay; one's own elections and net pay split |

Pay pages carry `[ConfirmPassword]`: the user retypes their password, then pay pages open for 10 minutes. The recipe **Payroll: sample roles** (`PayrollSampleRoles`) adds Payroll administrator, Payroll clerk and Payroll approver roles.

## Pay Setup

- **Pay schedules** (`PaySchedule`, `IPayScheduleManager`): `Frequency` (`Weekly`, `Biweekly`, `FourWeekly`, `SemiMonthly`, `Monthly`, `Quarterly`, `Yearly`), `AnchorDate`, `SecondPeriodStartDay`, `WeekStart` (overtime week), `PayDateRule`, `PayDateOffsetDays`, `FirstPayDay`, `SecondPayDay`, `NonBusinessDayShift` (`Before`, `After`, `None`), `ApprovalLeadDays`, `ExtraPayPeriod` (`Standard` or `DivideByPayDates`). `PeriodOn(date)`, `WeekOf(date)` and `PayDateFor(periodEnd, isBusinessDay)` are pure.
- **Pay codes** (`PayCode`, `IPayCodeManager`, codes upper case and unique): `Code`, `Name`, `Class` (`Earning`, `Deduction`, `EmployerContribution`, `ImputedIncome`, `Reimbursement`, `Memo`), `Kind` (open string, built-ins in `PayCodeKinds`), `Multiplier`, `PremiumPerHour`, `ExportCode`, `Enabled`, and module settings in `Properties`. Pay Setup seeds `REG`, `OT`, `DT`, `PTO`, `PP`, `HOL`, `WKND`, `UNPAID`; Payroll adds `SAL`, `BONUS`, `COMM`, `TIPS`, `REIMB`, `LOAN` (and `TIPMU`, `CTIPS`); Garnishments adds `GARN`.
- A schedule or code in use can't be deleted (turn it off). Payroll's **Payroll** card on the pay code editor stores `PayrollPayCodeSettings` (`Calculation`, `BaseCodeIds`, `TaxTreatment` per income tax category, `Supplemental`, `CountsInRegularRate`, `CountsAsHoursWorked`, `InRetroPay`, `PeriodLimit`, `YearLimits`, `LifetimeLimit`, `Priority`, `Arrears`, `Proration`, `FormBoxes`).

```csharp
// In a module Startup: a pay code kind of your own, and a record that keeps schedules and codes from being deleted.
services.AddPayCodeKind("Mileage", LocalizationSource.Create<Startup>("Mileage"));
services.AddPaySetupUsage<FleetPaySetupUsage>();
```

## Employers, pay profiles and pay items

- **Employer** (`IEmployerManager`, a party kind `Employer`): `Name`, `LegalName`, `TaxId`, `Address`, `LogoPath`, `CountryPack`, `Currency`, `FundingBankAccountId` (with Banking, an account owned by `PartyRef("Employer", id)`), signatory and contact, `Registrations` (`Jurisdiction`, `Agency`, `AccountId`, `DepositFrequency`, dated `Rates`), `Status`, `ClosedYears`. An employer in use (`IEmployerUsage`) can't be deleted.
- **EmployeePayProfile** (`IPayProfileManager`, one per staff member and employer): pay schedule, `WorkerType` (`Employee`, `Contractor`), `OvertimeStatus`, `Status`, legal name and `HomeAddress`, work site team, `DefaultAllocation`, `Tipped`, `BackupWithholding`, protected `TaxId` with a keyed `TaxIdHash`, `Elections` (dated `TaxElection` rows, never edited; `ElectionOn(jurisdiction, day)`) and `Distribution` (`NetPayRow`s by `Method` `BankAccount` or `Check`, `Split` amount, percent or `Remainder`; the last row is the remainder).
- **Blockers**: `IPayProfileService.GetBlockersAsync` lists what keeps a profile from being paid (no schedule, tax id, election, net pay split or pay rate; Banking adds unverified accounts). Add your own with `IPayProfileBlockerProvider`.
- **EmployeePayItem** (`IPayItemManager`): a standing amount or percent on a profile with dates, a goal, period and year limits, an employer match (`EmployerMatchPercent`, `EmployerMatchLimitPercent`) and arrears.
- `MayChangePayOfAsync` refuses a user changing their own pay unless **Settings > Payroll** allows it (`AllowChangingOwnPay`).

## Pay runs

`PayRun` (`IPayRunManager`, numbered from the `Payroll:PayRuns` series): one employer, a schedule or named people, `Type` (`Regular`, `Supplemental`, `OffCycle`, `Final`, `Correction`, `Contractor`, `Reversal`, `Historical`), period, pay date and a status (`Draft`, `Calculating`, `Calculated`, `PendingApproval`, `Approved`, `Processing`, `Paid`, `PartiallyPaid`, `Failed`, `Canceled`).

| `IPayRunService` | Does |
|---|---|
| `OpenAsync` | Opens a run (once per employer, schedule, type and period) |
| `CalculateAsync(run, user, staffMemberIds)` | Draft `PayStatement` per active profile, under a distributed lock; a pending approval is cancelled |
| `SubmitAsync(run, user)` | Asks Approvals' `PayRunApproval` policy (approval type `PayRun`), scoped to the employer; the preparer may not approve unless `AllowPreparerToApprove` |
| `ProcessAsync(run, user)` | Numbers statements (`Payroll:PayStatements`), marks inputs consumed (`payrun:{runId}`), pays each payment through `IPayStatementPaymentMethod`, sets Paid, PartiallyPaid or Failed |
| `CancelAsync` | Cancels an open run and removes its drafts |
| `VoidStatementAsync(statement, user, reason)` | Stops payments, adds a reversing statement, releases inputs |
| `ReissuePaymentAsync(statement, index, user, reason)` | Stops a payment and pays it again |

With Banking's **Direct Deposit** on, net pay goes out as an ACH `Disbursement` from the employer's funding account (idempotency key `payroll:{statementId}:{index}`); with **Checks** on, as a paycheck with the statement as voucher. A deposit row without Direct Deposit is paid by check. The calculation steps, pay inputs, accumulators, retro pay, tips and gross-up are in [references/pay-engine.md](references/pay-engine.md).

## Income tax

The taxes step maps each statement to Taxation's `IncomeTaxRequest` (registrations and their rates on the pay date, home and work jurisdictions, elections, periods per year, regular and supplemental wages per income tax category from the pay codes' tax treatment, year-to-date totals) and maps `IncomeTaxLine`s back to tax lines. **Settings > Payroll** names the provider (`PayrollSettings.TaxProvider`); empty uses the only provider, or the only one besides the built-in income tax rules (`TaxTables`). With several engines and none named, nothing is withheld until one is chosen. Contractors have nothing withheld except backup withholding (`IncomeTaxCategories.ContractorWithholding`). Rules live under **Commerce > Taxation > Income tax rules**; Payroll never owns a rule. Run the `IncomeTaxUnitedStatesFederal2026` or `IncomeTaxCanada2026` recipe for the federal rules.

## Settings

`PayrollSettings` (**Settings > Payroll**, Settings step key `PayrollSettings`): `AllowPreparerToApprove`, `AllowChangingOwnPay`, `EmployeeReview` (people review their coming pay), `DefaultCountryPack`, `DefaultCurrency`, `TaxProvider`, `MinimumNetPay`, `MinimumHourlyWage`, `MaximumTipCredit`. `PayrollJournalSettings` (card **Journal accounts**) holds the journal's accounts. With Teams, `PayrollTeamSettings` (`LedgerCode`, `Jurisdictions`) is a **Payroll** card on the team editor.

```json
{
  "steps": [
    {
      "name": "Settings",
      "PayrollSettings": {
        "AllowPreparerToApprove": false,
        "AllowChangingOwnPay": false,
        "EmployeeReview": true,
        "DefaultCountryPack": "US",
        "DefaultCurrency": "USD",
        "TaxProvider": "",
        "MinimumNetPay": 0
      }
    }
  ]
}
```

## Recipes and deployment

| Step | Entries key | Matched by |
|---|---|---|
| `PaySchedules` | `Schedules` | id, then name |
| `PayCodes` | `Codes` | id, then code |
| `PayrollEmployers` | `Employers` | id, then name |
| `PayrollPayProfiles` | `Profiles` | `EmployeeNumber` or `StaffMemberId`, `EmployerName` or `EmployerId`, `PayScheduleName` |
| `PayrollPayItems` | `Items` | id; items name their `PayCode` |
| `PayrollGarnishments` | `Orders` | id (Garnishments) |
| `PayrollTaxDeposits` | `Deposits` | id |
| `PayrollFilings` | `Filings` | id |
| `PayrollPayHistory` | `Statements` | imports final statements into a historical run |

Each has a deployment step (**Pay schedules**, **Pay codes**, **Employers**, **Pay profiles**, **Garnishment orders**, **Tax deposits and filings**, **Pay history**). The sensitive ones fail closed without a user and write tax ids, case numbers or people's wage statements only when asked and permitted. Full samples, the shipped recipes (`PayrollSample`, `PayrollSampleRoles`, `PayrollScheduledRun`, `PayrollPayInputsExport`, `PayrollJournalExport`, `PayrollW2Efw2`, `PayrollT4Xml`) and the data pipeline steps are in [references/payroll-recipes.md](references/payroll-recipes.md).

## Extension points

| Contract | Register | Purpose |
|---|---|---|
| `IPayInputSource` | `services.AddPayInputSource<T>()` | Hand hours or amounts to pay runs |
| `IPaySetupUsage` | `services.AddPaySetupUsage<T>()` | Keep schedules and codes your records use |
| `IPayCalculationStep` | `TryAddEnumerable` scoped | Add a calculation step (`Order`), or replace one with the same order |
| `IPayProfileBlockerProvider` | `TryAddEnumerable` scoped | Add what keeps a profile from being paid |
| `IPayStatementBalanceSource` | `services.AddPayStatementBalanceSource<T>()` | Balances printed on statements |
| `IPayStatementPaymentMethod` | scoped | Pay a `NetPayMethod` |
| `IPayrollFilingForm` | `services.AddPayrollFilingForm<T>()` | A country's return or slip |
| `IEmployerUsage` | scoped | Keep employers your records name |
| `IPayRunObserver`, `IPayStatementObserver`, `IPayProfileObserver`, `IPayrollFilingObserver`, `IGarnishmentObserver` | `TryAddEnumerable` scoped | React after changes are saved |
| `AddPayrollFormBox(name, displayName)` | extension | A form box a pay code can map to |

```csharp
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

public sealed class MileagePayInputSource : IPayInputSource
{
    private readonly IMileageLogStore _logs;

    public MileagePayInputSource(IMileageLogStore logs)
    {
        _logs = logs;
    }

    public string SourceKind => "MileageLog";

    public async Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
    {
        var logs = await _logs.ListApprovedAsync(query.PeriodStart, query.PeriodEnd, query.StaffMemberIds, query.IncludeConsumed, cancellationToken);

        return logs.Select(log => new PayInput
        {
            SourceKind = SourceKind,
            SourceId = log.Id,
            StaffMemberId = log.StaffMemberId,
            PayScheduleId = query.PayScheduleId,
            PeriodStart = query.PeriodStart ?? log.Date,
            PeriodEnd = query.PeriodEnd ?? log.Date,
            PayCodeId = log.PayCodeId,
            Date = log.Date,
            Amount = log.Amount,
            Consumed = log.ConsumedBy is not null,
        }).ToList();
    }

    // consumerId is "payrun:{runId}" or "pipeline:{runId}"; a void releases what a run took.
    public Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
        => _logs.MarkConsumedAsync(consumerId, sourceIds, cancellationToken);

    public Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
        => _logs.ReleaseAsync(consumerId, sourceIds, cancellationToken);
}

// services.AddPayInputSource<MileagePayInputSource>();
```

Built-in sources: Workforce's locked timesheets, Workforce Time Off payouts (`TimeOffPayout`), Point of Sale declared tips (`RegisterShift`) and Orders commissions (`Order`).

## Workflows, notifications, webhooks and reports

- **Events**: Pay Run Opened, Calculated, Submitted, Approved, Rejected, Processed, Paid, Failed, Canceled; Pay Statement Issued and Voided; Pay Profile Changed; Payroll Filing Prepared, Filed, Amended; Payroll Year Closed, Reopened; Tax Deposit Recorded; Garnishment Order Created, Withheld, Remittance Voided, Order Completed.
- **Tasks**: Open Pay Run, Add Pay Input, Calculate Pay Run (outcome `Blocked` when a person has a blocker; `IncludeTotals` writes Gross, Net and CashNeeded only for a user who may view the employer's reports), Submit Pay Run for Approval, Approve Pay Run, Reject Pay Run, Process Pay Run (`Paid`, `PartiallyPaid`, `Failed`). The `PayrollScheduledRun` recipe adds a disabled workflow that opens, calculates and submits each period's run.
- **Notification types**: `PayStatementReady`, `PayDetailsChanged`, `PayReviewRequested`, `PayReviewQuestioned`, `TaxFormReady`, `DepositReturned`, `BankAccountChanged`; none carries an amount or account number.
- **Webhooks**: `payroll.payrun.approved`, `payroll.payrun.processed`, `payroll.statement.issued`, `payroll.statement.voided` (no amounts).
- **Data source** `Payroll` (Data Pipelines Core): Pay inputs (`PayInputs`), Employees (`PayrollEmployees`), Pay runs, Pay statements, Earnings, Deductions, Taxes, Employer contributions, Payments of net pay, Year-to-date balances, Journal entries, Tax liabilities, Garnishments, and with the filings features W-2 wage records and T4 slips. Readers see only the employers in their `ViewPayrollReports` or `ExportPayrollData` scope.
- **Reports** (with Reports on): Payroll Register, Payroll Summary, Cash Requirement, Deductions and Contributions, Labor Distribution, Year-to-date Earnings; dashboard widgets Payroll Cash Requirement and Payroll Summary.
- **Calendars**: the `Payroll.PayDates` source. **Audit Trail**: category **Payroll**. **Personal data**: profiles and final statements are exported, never anonymized.

Filings, year end, garnishments, My Pay and the AI tools are in [references/filings-garnishments-and-self-service.md](references/filings-garnishments-and-self-service.md).

## Troubleshooting

| Symptom | Check |
|---|---|
| No **Payroll** menu | The user holds no payroll permission for any employer; the menu asks the payroll scope, not a global permission |
| Nothing withheld, warning "no provider" | Income Tax rules not imported for the employer's registered jurisdictions, or several tax engines on and **Settings > Payroll > Tax provider** empty |
| **Submit for approval** disabled | The run isn't calculated or a profile has blockers; recalculating cancels a pending approval |
| Preparer can't approve their own run | By design; turn on `AllowPreparerToApprove` |
| Deposit paid by check | Banking's Direct Deposit is off, or the employer has no funding account |
| Can't delete a pay code, schedule or employer | It is in use (`IPaySetupUsage`, `IEmployerUsage`, pay profiles or items); disable it |
| Run can't be opened or voided | The year is closed (`Employer.ClosedYears`); reopen the year |
| Pay inputs missing from a run | Timesheet not locked, input already consumed by another run or pipeline, or wrong schedule or period |
| Canadian employee blocked | No province of employment on the work site team's **Payroll** card |

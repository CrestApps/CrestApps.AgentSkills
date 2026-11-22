# Payroll calculation engine

`IPayCalculator` works out one pay from a `PayCalculationContext` (`PayCalculationContextFactory` builds it from the site): the profile, employer, schedule, period and pay date, the staff member's `StaffPayRates`, hire and termination dates, `Inputs`, `Items`, `Codes`, `Garnishments`, `History`, `Accumulators`, `SupplementalMethod`, `MinimumNet`, `GrossUp`, `HomeJurisdictions` and `WorkJurisdictions`. Steps fill `Lines` (`PayStatementLine`), `Warnings` and `Payments`; nothing is saved.

## Steps

Steps run by `Order` (`PayCalculationOrder`). Register your own as a scoped `IPayCalculationStep`; registering one with a built-in order replaces that step.

| Order | Constant | Step |
|---|---|---|
| 100 | `Earnings` | Salary for the period (prorated for hires, terminations and salary changes), hours times rate times multiplier (or the code's premium), amounts, recurring earnings and imputed income from pay items |
| 110 | `RetroPay` | Regular runs: what earlier final statements of the year owe since a rate or salary went up |
| 120 | `TipCredit` | Tipped profiles: makes each week up to `PayrollSettings.MinimumHourlyWage` on the `MinimumWageMakeup` code (`TIPMU`); cash tips on `CTIPS` |
| 200 | `RegularRate` | Overtime premium the regular rate requires (non-exempt profiles) |
| 210 | `EarnedRegularRate` | Overtime of earlier weeks re-priced by a bonus earned in them (`EarnedFrom`/`EarnedTo`) |
| 300 | `PreTaxDeductions` | Deductions exempt from at least one tax category, by priority, within `PayLimits` |
| 400 | `Contributions` | Employer contributions, reimbursements, matches of pre-tax items (before taxes, so taxable ones are taxed) |
| 500 | `Taxes` | `TaxesPayCalculationStep`: income tax through Taxation's `IIncomeTaxProvider` |
| 600 | `PostTaxDeductions` | The other deductions, by priority, within limits |
| 700 | `Garnishments` | `DisposableEarningsPayCalculationStep`: deductions that are a percent of disposable earnings |
| 750 | `Garnishments + 50` | `GarnishmentsPayCalculationStep` (Garnishments feature) |
| 800 | `NetCheck` | Below `MinimumNet`, after-tax deductions are cut, the largest priority number first; arrears are recorded |
| 900 | `AfterTaxMatches` | The employer's match of after-tax deductions |
| 1000 | `Distribution` | Net pay split by the profile's rows (`Money.Allocate`); no row means a check |

```csharp
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

// A flat wellness stipend on every regular pay, after earnings and before taxes.
public sealed class WellnessStipendPayCalculationStep : IPayCalculationStep
{
    public int Order => PayCalculationOrder.Earnings + 50;

    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        var code = context.Codes.Values.FirstOrDefault(code => code.Code == "WELL");

        if (code is null || context.Profile.WorkerType != PayWorkerType.Employee)
        {
            return Task.CompletedTask;
        }

        context.Lines.Add(new PayStatementLine
        {
            Type = PayLineType.Earning,
            PayCodeId = code.ItemId,
            Description = code.Name,
            Amount = 25m,
        });

        return Task.CompletedTask;
    }
}

// services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, WellnessStipendPayCalculationStep>());
```

## Tax treatment and limits

- Income tax categories are Taxation's open list (`IncomeTaxCategories`: `IncomeTax`, `SocialInsurance`, `Unemployment`, plus `ContractorWithholding` from United States Income Tax and `InsurableEarnings` from Canada Income Tax). A pay code's `TaxTreatment` is `Taxed` or `Exempt` per category; a missing category takes the class default (earnings, imputed income, deductions taxed; contributions, reimbursements and memo exempt).
- `PayCodeTaxMatrix.WagesFor(category, lines)` gives the wages of a category; `PayLimits.Apply(...)` applies the code's period, dated yearly and lifetime limits and the pay item's maximums and goal against the accumulators. Both are pure (`CrestApps.OrchardCore.Payroll.Core.Calculation`).

## Statements and accumulators

- `PayStatement`: run, period, pay date, gross, net, `Lines`, `Payments` (`PayStatementPayment`, `Attempt` for reissues), snapshot of names and addresses, `Balances`, `Review`, `VoidOf`/`VoidedBy`, status `Draft`, `Final`, `Paid` or `Voided`. Only drafts can be deleted.
- Accumulators (`IPayAccumulatorService`) are never stored: they sum `PayStatementLineIndex` rows (amounts in minor units, hours in hundredths) by pay code (`PayAccumulators.CodeKey`) and tax (`TaxKey`) over the period, month, quarter, year and lifetime. A void is a statement with negative lines, so the two cancel.
- **Preview the next pay** on a profile runs the engine for the current period without saving.

## Pay inputs

`IPayInputSource` (`AddPayInputSource<T>()`) returns `PayInput` rows (`SourceKind`, `SourceId`, staff member, schedule and period, `PayCodeId`, `Date`, `Hours` or `Amount`, `Rate`, `EarnedFrom`/`EarnedTo`, `Allocation` with department, site, activity and job, `Consumed`). `IPayInputService` reads every source; `MarkConsumedAsync`/`ReleaseAsync` receive a `PayInputConsumption` (`ConsumerId` `payrun:{runId}` or `pipeline:{runId}`, `Sources`).

A run also has its own `PayRunInput`s (added on the run page, by the **Add Pay Input** task, or by the **Save pay inputs** pipeline step through `PayRunInputImporter`). An input marked **The amount is what the person takes home** (`IsNet`) is grossed up.

Regular, final and correction runs pay salaries, pay items and source inputs; other run types pay only their own inputs.

## Second pays and supplemental pay

A second pay in a period is withheld on the period's total less what earlier pays were taxed on (`PeriodTaxableWages`); a bonus paid alone carries `IncomeTaxRequest.RegularIncomePerPeriod` so the aggregate or bonus method adds it to a period of regular pay. Canada's methods use the CRA bonus method.

## Payroll journal

Payroll keeps no ledger. `PayrollJournal.EntriesFor` turns each final line into a debit and a credit (earnings debit wages expense and credit net pay owed; deductions credit deductions payable; taxes credit taxes payable; employer contributions debit benefits expense), offered as the **Journal entries** data set with each line's department `PayrollTeamSettings.LedgerCode`. Accounts are `PayrollJournalSettings` (`NetPayAccount`, `WageExpenseAccount`, `DeductionLiabilityAccount`, `TaxLiabilityAccount`, `EmployerTaxExpenseAccount`, `ContributionExpenseAccount`, `ContributionLiabilityAccount`, `ReimbursementExpenseAccount`, `Mappings` per pay code or tax code).

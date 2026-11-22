# Filings, garnishments, My Pay and AI tools

## Liabilities and deposits

`ITaxLiabilityService` derives liabilities (never stored) from the tax lines of final statements, both sides, by the jurisdiction they are remitted to (`PayStatementLine.RemittedTo`, else their own) and group (`TaxLiabilityGroups.Withholding`, `Unemployment`, `NonPayrollWithholding`), into periods of the registration's `DepositFrequency` (`DepositSchedules`: `Monthly`, `SemiWeekly`, `Quarterly`, `QuarterlyMidMonth`, `TwiceMonthly`, `FourTimesMonthly`, `Annual`; US unemployment always by quarter). The business records what it paid as `TaxDeposit`s (`ITaxDepositManager`, the **Liabilities** page or the `PayrollTaxDeposits` step). Due dates are not moved off weekends or holidays (except the working days of `FourTimesMonthly`).

## Filings

A country pack registers `IPayrollFilingForm`s (`services.AddPayrollFilingForm<T>()`: `Name`, `DisplayName` with `[JsonIgnore]`, `Jurisdiction`, `IsQuarterly`, per-person statements, `PrepareAsync(PayrollFilingContext)`). `IPayrollFilingService` prepares a `PayrollFiling` as a draft (prepare again on request), marks it filed with the day and confirmation (then immutable; people get `TaxFormReady`), and amends it with a new draft naming it (`AmendsId`). A filed form and the deposits counted in it are kept.

| Feature | Forms (`FormName`) |
|---|---|
| United States Payroll Filings | `US-941`, `US-940` (reads the `US-FUTA` rule), `US-945` (backup withholding), `US-W2` (W-2 and W-3; box 12 from pay codes' `FormBoxes` such as `W2:12D`), `US-1099NEC` (1099-NEC and 1096, threshold 2,000 from 2026, 600 before) |
| Canada Payroll Filings | `CA-T4` (T4 slips and T4 Summary, box 10 province of employment, box 45 dental benefits from the employer's **Canada** card), `CA-RL1` (RL-1 slips and Summary) |

- **SSA EFW2**: the **W-2 wage records** data set and the `efw2` file format write RA, RE, RW, RT and RF records for one employer and year. The full SSN is read only for `ViewTaxIds`, each read recorded, and travels encrypted (`ProtectedSsn`) so only the EFW2 format reads it. The submitter id is the account id of an employer registration whose agency is `SSA`. A row without an SSN, or a box the file can't write, refuses the file.
- **CRA T4 XML**: the **T4 slips** data set and the `t4xml` format (`Submission`, `T619`, `Return`, `T4Slip`, `T4Summary`). A slip without a valid SIN or province of employment, or an employer without an RP account, contact or dental code, refuses the file. Not validated against the CRA XSD.
- Canada: an employee of an employer registered in `CA` needs exactly one province of employment from the work site team's **Payroll** card jurisdictions (a pay profile blocker). A SIN must pass the mod-10 check. Not yet supported: Record of Employment, Canadian direct deposit (pay by check), commission methods, Quebec's other employer contributions.
- State and local taxes are income tax rules set up by hand (see `orchardcore-taxation`) or come from a tax engine.

## Year end

`YearEndBlockersAsync`, `CloseYearAsync`, `ReopenYearAsync`: a year closes once every run with a pay date in it is paid or cancelled. Closing prepares the per-person forms and adds the year to `Employer.ClosedYears`; no run of a closed year is opened, calculated or processed, and no statement voided.

Pages: **Payroll > Liabilities and Filings > Liabilities**, **Filings** (prepare, mark as filed, prepare again, amend, people's PDFs) and **Year end**, each for `ManagePayrollFilings` per employer.

## Garnishments

`GarnishmentOrder` (`IGarnishmentOrderManager`, Payroll collection): pay profile, `Type`, case number, agency, deduction pay code, `Amount` and/or `Percent` of disposable earnings, `LimitPercent`, `ProtectedWeeklyAmount`, `ExemptAmount`, `TotalOwed`, `Withheld`, `Priority`, received, start and end dates, `Status`, payee (name, address, `PaymentMethod` `Check`, `BankAccount` or `Outside`).

- `GarnishmentDefaults` gives each type its US federal limit and priority: support 50% (60, or 50 with `OtherDependents`, plus 5 for `ArrearsOver12Weeks`, at most 65), student loan 15%, creditor 25% with 30 times the federal minimum wage a week protected; support first, then tax levies, bankruptcy, student loans, creditors. A federal levy needs `ExemptAmount`.
- The step (order 750) adds one line per order (`PayStatementLine.GarnishmentOrderId`), with a warning when a limit held it back.
- When a statement is issued, `GarnishmentStatementObserver` moves `Withheld` (lock `Payroll:Garnishment:{order}`) and sends the money to the payee from the employer's funding account by check or ACH (source kind `PayrollGarnishment`, idempotency key `payroll-garnishment:{statement}:{order}`). A failed remittance is retried with **Send again**. A void stops the payment and gives the withholding back.
- **Payroll > Garnishments** (`ManageGarnishments` per employer). An order that withheld anything is released, never deleted. Nobody manages their own order unless `AllowChangingOwnPay`.
- Data set **Garnishments** (`ExportPayrollData`; case number and court only for `ManageGarnishments` of the employer).

## My Pay

My Pay (`CrestApps.OrchardCore.Payroll.SelfService`) shows a person their own pay in the **Pay** section of their own staff profile, linked from **Staff > My Profile** (`ViewOwnPay`); `~/Admin/my-pay` redirects there. `MyPayController` reads only the records of the staff member linked to the signed-in user; drafts and other people's records are not found.

- Statements (not drafts), stubs (PDF, `PayStubService`), filed tax forms (W-2, 1099-NEC, T4, RL-1), pay profiles and a link to Banking's **My bank accounts**.
- **Pay details** (`ManageOwnPayDetails`): the profile editor in the `MyPay` group, limited to tax elections and the net pay split. A save notifies `PayDetailsChanged` to the person.
- With `PayrollSettings.EmployeeReview`, a calculation sends **Review your coming pay** (`PayReviewRequested`); the person answers at `my-pay/review/{id}` with **This looks right** or **Ask about it**, which notifies the preparer (`PayReviewQuestioned`). Nothing is blocked.
- The person's own pay always asks for the password each time their profile opens (`StaffPayPrivacy`).

## Payroll AI Tools

`CrestApps.OrchardCore.Payroll.AI` (off by default; pay goes to the site's AI provider when a profile uses a tool) registers two tools with `AddCoreAITool`, in the **Payroll** category of an AI profile's tools. Both run as the signed-in person and never return a tax id or account number.

| Tool name | Mirrors | Answers |
|---|---|---|
| `payrollMyPayStatement` | My Pay, `ViewOwnPay` | The person's latest (or numbered) final statement: lines, gross, net, how net was paid, balances, change since the previous statement |
| `payrollPayRunChanges` | The pay run page, `ViewPayRuns` for its employer | A run against the latest paid run of the same employer and schedule: totals, joiners and leavers, the 25 biggest net changes |

See `orchardcore-ai-tools` to add the tools to an AI profile.

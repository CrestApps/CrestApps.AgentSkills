# Payroll recipes, deployment and pipelines

Every step below is described to CrestApps Recipes (JSON Schema) when that feature is on. Import order matters: Staff (with Staff Compensation pay), Banking accounts, pay codes and schedules, employers, pay profiles, pay items, garnishments.

## Pay setup

```json
{
  "steps": [
    {
      "name": "PaySchedules",
      "Schedules": [
        {
          "ItemId": "46vehrw7f1shx55yfm94qd8p1x",
          "Name": "Every two weeks",
          "Frequency": "Biweekly",
          "AnchorDate": "2026-01-04",
          "WeekStart": "Sunday",
          "PayDateRule": "DaysAfterPeriodEnd",
          "PayDateOffsetDays": 6,
          "NonBusinessDayShift": "Before",
          "ApprovalLeadDays": 2,
          "Enabled": true
        }
      ]
    },
    {
      "name": "PayCodes",
      "Codes": [
        {
          "Code": "401K",
          "Name": "401(k) retirement plan",
          "Class": "Deduction",
          "Kind": "Contribution",
          "Multiplier": 1,
          "Enabled": true,
          "Properties": {
            "PayrollPayCodeSettings": {
              "Calculation": "PercentOfEarnings",
              "TaxTreatment": { "IncomeTax": "Exempt" },
              "YearLimits": [ { "EffectiveFrom": "2026-01-01", "Amount": 24500 } ],
              "Priority": 20,
              "FormBoxes": [ "W2:12D" ]
            }
          }
        },
        {
          "Code": "NIGHT",
          "Name": "Night shift",
          "Class": "Earning",
          "Kind": "ShiftDifferential",
          "PremiumPerHour": 2.25,
          "Properties": {
            "WorkforcePayCodeSettings": { "WindowStart": "22:00:00", "WindowEnd": "06:00:00", "Weekdays": [] }
          }
        }
      ]
    }
  ]
}
```

Workforce's `WorkforceTimeClock` step still reads its older `PayPeriods` and `PayCodes` keys and saves them through these managers.

## Employers, profiles and items

```json
{
  "steps": [
    {
      "name": "PayrollEmployers",
      "Employers": [
        {
          "ItemId": "4gn8cjc0e2ffzb2kyq1gjwaf3m",
          "Name": "Contoso",
          "LegalName": "Contoso, Inc.",
          "TaxId": "12-3456789",
          "Address": { "AddressLine1": "100 Main Street", "City": "Austin", "Region": "TX", "PostalCode": "78701", "Country": "US" },
          "CountryPack": "US",
          "Currency": "USD",
          "FundingBankAccountId": "49rj58f0yqys0k5nvjtxz8e0m5",
          "SignatoryName": "Avery Lee",
          "SignatoryTitle": "Finance manager",
          "ContactName": "Avery Lee",
          "ContactEmail": "payroll@example.com",
          "ContactPhone": "+1 512 555 0100",
          "Registrations": [
            { "Jurisdiction": "US", "Agency": "Internal Revenue Service", "AccountId": "12-3456789", "DepositFrequency": "Monthly" },
            { "Jurisdiction": "US-TX", "Agency": "Texas Workforce Commission", "AccountId": "99-999999-9", "DepositFrequency": "Quarterly", "Rates": [ { "EffectiveFrom": "2026-01-01", "Rate": 2.7 } ] }
          ],
          "Status": "Active"
        }
      ]
    },
    {
      "name": "PayrollPayProfiles",
      "Profiles": [
        {
          "ItemId": "43d8p5352djz324djvsakc3yqn",
          "EmployeeNumber": "P102",
          "EmployerName": "Contoso",
          "PayScheduleName": "Every two weeks",
          "WorkerType": "Employee",
          "OvertimeStatus": "NonExempt",
          "Status": "Active",
          "TaxId": "987-65-4321",
          "HomeAddress": { "AddressLine1": "48 Elm Street", "City": "Austin", "Region": "TX", "PostalCode": "78704", "Country": "US" },
          "Elections": [
            { "Jurisdiction": "US", "EffectiveFrom": "2026-01-01", "Values": { "FormYear": "2020", "FilingStatus": "HeadOfHousehold", "Dependents": "2000" } },
            { "Jurisdiction": "US-TX", "EffectiveFrom": "2026-01-01", "Values": {} }
          ],
          "Distribution": [
            { "Method": "BankAccount", "BankAccountId": "4tbs4xkxcfz2qmdqasdxvhp0e8", "Split": "Remainder" }
          ]
        }
      ]
    },
    {
      "name": "PayrollPayItems",
      "Items": [
        {
          "ProfileId": "43d8p5352djz324djvsakc3yqn",
          "PayCode": "401K",
          "Percent": 5,
          "StartDate": "2026-01-01",
          "EmployerMatchPercent": 50,
          "EmployerMatchLimitPercent": 3,
          "Note": "5% of pay, matched half up to 3% of pay."
        }
      ]
    }
  ]
}
```

- `TaxId` is given in clear and protected by the step. Other profile keys: `StaffMemberId`, `EmployerId`, `PayScheduleId`, `BackupWithholding`, `Tipped`, `LegalName`, `WorkSiteTeamId`, `DefaultAllocation`, `RetroPayFrom`.
- Pay item keys: `PayCodeId` or `PayCode`, `Amount` or `Percent`, `StartDate`, `EndDate`, `Goal`, `PaidTowardGoal`, `PeriodMaximum`, `YearMaximum`, `ArrearsBalance`, `Note`.
- The **Pay profiles** deployment step writes both steps, nothing when run without a user, and tax ids only with **Include the tax ids** and `ViewTaxIds` (each recorded as a reveal).

## Garnishments, deposits, filings and history

```json
{
  "steps": [
    {
      "name": "PayrollGarnishments",
      "Orders": [
        {
          "ProfileId": "4nettp6gcxecw3avr24e27e4fv",
          "Type": "Support",
          "CaseNumber": "FC-2026-000123",
          "Agency": "County family court",
          "PayCodeId": "4xjfjxzfvbn4tk1eg6zsr0fqwx",
          "Amount": 150,
          "ReceivedDate": "2026-10-15",
          "StartDate": "2026-11-01",
          "Status": "Active",
          "PayeeName": "State disbursement unit",
          "PaymentMethod": "Check"
        }
      ]
    },
    {
      "name": "PayrollTaxDeposits",
      "Deposits": [
        { "EmployerId": "4gn8cjc0e2ffzb2kyq1gjwaf3m", "Jurisdiction": "US", "Group": "Withholding", "PeriodStart": "2026-10-01", "PeriodEnd": "2026-10-31", "Amount": 18450.22, "PaidOn": "2026-11-13", "Reference": "EFTPS 2711" }
      ]
    }
  ]
}
```

- Garnishment `Type`: `Support`, `FederalTaxLevy`, `StateTaxLevy`, `Bankruptcy`, `StudentLoan`, `Creditor`, `Other`; `Status`: `Active`, `Suspended`, `Completed`, `Released`; `PaymentMethod`: `Check`, `BankAccount` (with `PayeeBankAccountId`), `Outside`. Also `Percent`, `LimitPercent`, `ProtectedWeeklyAmount`, `ExemptAmount`, `TotalOwed`, `Priority`, `EndDate`, `PayeeAddress`, `Notes`. Required: `ProfileId`, `PayCodeId`, `StartDate`.
- `PayrollFilings` (`Filings`: `EmployerId`, `Form`, `Year`, `Quarter`, `Status`, `Boxes`, `People`, `FiledOn`, `Reference`, `AmendsId`) is written by the **Tax deposits and filings** deployment step; people's wage statements only with **Include people's wage statements** and `ViewTaxIds`.
- `PayrollPayHistory` (`Statements`) imports final statements into a paid, read-only historical run per employer and pay date; tax lines need `TaxableWages`. The opt-in **Pay history** deployment step exports them.

## Shipped recipes

| Recipe name | Does |
|---|---|
| `PayrollSample` | Turns on Payroll, US filings, Garnishments, My Pay and Banking; runs `IncomeTaxUnitedStatesFederal2026`; adds the employer Contoso, a check layout, a pay schedule, `401K` and `CSUP` codes, four staff members with profiles (test tax ids and account numbers) |
| `PayrollSampleRoles` | Payroll administrator, clerk and approver roles |
| `PayrollScheduledRun` | A disabled workflow that opens, calculates and submits each period's run on weekday mornings |
| `PayrollPayInputsExport` | Draft pipeline: read Pay inputs, `MarkPayInputsExported`, aggregate, write a CSV |
| `PayrollJournalExport` | Draft pipeline: journal entries grouped by pay date, employer, account and department to CSV |
| `PayrollW2Efw2` | Draft pipeline writing W-2 wage records as an SSA EFW2 file (`efw2` format) |
| `PayrollT4Xml` | Draft pipeline writing T4 slips as CRA T4 XML (`t4xml` format) |

Run one from **Tools > Recipes**, or from another recipe:

```json
{
  "steps": [
    {
      "name": "Recipes",
      "Values": [
        { "executionid": "MySetup", "name": "PayrollSample" }
      ]
    }
  ]
}
```

No recipe adds pay inputs: open a run under **Payroll > Pay Runs**, add hours as run inputs, then calculate.

## Data pipeline steps

| Step type | Feature | Does |
|---|---|---|
| `MarkPayInputsExported` | Pay Setup (with Data Pipelines) | Passes rows through; when the whole run succeeds, marks their `SourceKind`/`SourceId` consumed by `pipeline:{runId}`. The publisher needs `ExportPayrollData` |
| `SavePayInputs` | Payroll | A destination saving rows (run number, employee number, pay code, hours or amount, rate, date, note) as run inputs; rejected rows go to **Rejected** with an `Error` field. The run comes from a `PayRun` field or run parameter |

The **Pay inputs** data set (`PayInputs`) holds inputs not yet taken; the parameter `IncludeExported=true` adds the others, and `PayScheduleId` and `PeriodStart` filters narrow it. Sum amounts, then round.

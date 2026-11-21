---
name: orchardcore-banking
description: Skill for the CrestApps Orchard Core Banking module (Bank Accounts, Checks, Disbursements, Direct Deposit) and the Plaid module. Covers bank accounts with protected numbers, routing checks, verification and the audited reveal, IBankAccountService, link providers, verifiers, usages and observers, check layouts with the MICR line, the check register, printing, void, reissue, stop payment, stale checks and positive pay, disbursements recorded once by an idempotency key and released in batches through providers, Nacha ACH files, prenotes, return and change files, ISourceAccess, Plaid Link with the secret in Orchard Secrets, recipe and deployment steps, data sets, workflow activities and permissions. Use this skill when requests mention bank accounts, paying by check or direct deposit, ACH files, payouts or Plaid in CrestApps. Strong matches include BankAccount, IBankAccountService, ICheckService, IDisbursementService, IDisbursementProvider, NachaOriginSettings and PlaidSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Banking

## Bank Accounts, Checks, Disbursements, Direct Deposit and Plaid

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the CrestApps Banking module, the building block every module that sends money by bank uses (Payroll net pay and garnishments, Marketplace payouts). It keeps one protected record per bank account, prints checks, records money sent out once and releases it in batches through a provider, and writes ACH bank files. The Plaid module links a person's bank so the account is saved with the bank's own numbers and verified at once.

### Guidelines

- Install `CrestApps.OrchardCore.Banking` (and `CrestApps.OrchardCore.Plaid` when needed) in the web/startup project. Reference `CrestApps.OrchardCore.Banking.Abstractions` (models, contracts, `Add*` registration helpers) or `CrestApps.OrchardCore.Banking.Core` from your own module, never the module project.
- Write account numbers and statuses only through `IBankAccountService` (`ApplyNumbers`, `TrySetStatus`, `ChangeStatusAsync`, `ApplyLink`). A new routing or account number always moves the account back to `Unverified`; only a `Verified` account receives payments (`CanReceivePayments()`).
- `IBankAccountService`, `ICheckService` and `IDisbursementService` check no permission: authorize the user first.
- Never log, return or store an account number in clear text. `RevealAccountNumberAsync` is the only read, and it is always recorded.
- Send money through `IDisbursementService.CreateAsync` with a stable `IdempotencyKey`; never write a `Disbursement` directly. One key is one attempt: after a failure, cancel, return or reversal, a new attempt needs a new key.
- A module that writes checks or disbursements for its own records registers an `ISourceAccess` for its `SourceKind` so people only see and act on what the source allows.
- No MICR font ships: a site uploads a MICR E-13B TrueType font it is licensed to use before a layout with **Print the check line** prints.
- Payroll pays net pay and garnishments through these features; see `orchardcore-payroll`. Parties (`PartyRef`) come from `orchardcore-teams`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`. All C# classes must use the `sealed` modifier, except View Models.

### Features

| Feature | ID | Depends on |
|---|---|---|
| Bank Accounts | `CrestApps.OrchardCore.Banking` | CrestApps Resources |
| Checks | `CrestApps.OrchardCore.Banking.Checks` | Bank Accounts, Addresses |
| Disbursements | `CrestApps.OrchardCore.Banking.Disbursements` | Bank Accounts |
| Direct Deposit | `CrestApps.OrchardCore.Banking.DirectDeposit` | Disbursements |
| Plaid Bank Account Linking | `CrestApps.OrchardCore.Plaid` | Bank Accounts, `OrchardCore.Secrets`, Provider Webhook Inbox (`CrestApps.OrchardCore.ContactCenter.ProviderInbox`, `WebhooksConstants.Feature.ProviderInbox`) |

All are in the Finance category. Optional integrations light up when their feature is on: Audit Trail, Workflows, Recipes, Deployment, CrestApps Recipes (step schemas), Data Pipelines Core (data sets) and Data Pipelines (the **Read a disbursement batch file** step).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Banking",
        "CrestApps.OrchardCore.Banking.Checks",
        "CrestApps.OrchardCore.Banking.Disbursements",
        "CrestApps.OrchardCore.Banking.DirectDeposit"
      ],
      "disable": []
    }
  ]
}
```

## The Bank Account

A `BankAccount` (`CrestApps.OrchardCore.Banking.Models`) is a YesSql document in the `Banking` collection with `BankAccountIndex` (name, owner kind and id, status, country, currency, created; never a number).

| Field | Notes |
|---|---|
| `Name` | Listed name, such as "Payroll funding". |
| `Owner` | `PartyRef(Kind, Id)`: a staff member, user, account or any party kind. Empty for the business's own accounts. |
| `HolderName`, `BankName` | Name on the account; the bank's name. |
| `Kind` | `Checking` or `Savings`. |
| `Country`, `Currency` | ISO codes. The country picks the routing validator. |
| `RoutingNumber` | No spaces or dashes. |
| `AccountNumber` | A `ProtectedValue` (purpose `Banking.AccountNumber`) with at most the last four characters as a hint (`••••6789`). |
| `Status`, `StatusChangedUtc`, `StatusReason`, `VerifiedUtc`, `VerificationMethod` | Verification state. |
| `Properties` | Other features' settings, such as `NachaOriginSettings`. |

Status moves follow `BankAccountStatuses.Transitions`: `Unverified`, `VerificationSent`, `Verified`, `Failed` move among each other; `Closed` is final.

### IBankAccountService

| Member | Does |
|---|---|
| `ApplyNumbers(account, routing, account)` | Validates for the country, normalizes, protects. Blank keeps the stored value. Returns refused numbers. |
| `TrySetStatus` / `ChangeStatusAsync` | Moves the status when the table allows (the second also saves). |
| `StartVerificationAsync(account, verifierName)` | Runs an `IBankAccountVerifier` and saves the returned status. |
| `RevealAccountNumberAsync(account, reason)` | Reads the full number, logs who and why (never the number), tells observers. Only for users with `RevealBankAccountNumbers` or a payment file. |
| `GetUsagesAsync(account)` | Why other records point to it. An account in use can't be deleted; close it instead. |
| `ApplyLink(account, providerName, result)` | Applies a link provider's result (numbers, kind, bank name, status). |
| `Verifiers`, `LinkProviders` | What the enabled features registered. |

`IBankAccountManager` stores accounts and lists them by owner (`ListByOwnersAsync`).

### Extension points

| Contract | Register with | Use |
|---|---|---|
| `IBankRoutingValidator` | `AddBankRoutingValidator<T>()` | Number rules of one country (US ABA 3-7-1 check digit is built in; other countries default to up to 34 letters and digits). |
| `IBankAccountVerifier` | `AddBankAccountVerifier<T>()` | A **Check with** button (Direct Deposit adds **Test entry (prenote)**). |
| `IBankAccountLinkProvider` | `AddBankAccountLinkProvider<T>()` | Link a bank in the browser (Plaid). |
| `IBankAccountObserver` | `AddBankAccountObserver<T>()` | After each save or reveal (`Created`, `Updated`, `NumberChanged`, `StatusChanged`, `Deleted`, `NumberRevealed`). Never gets the number. |
| `IBankAccountUsage` | `AddBankAccountUsage<T>()` | Blocks deletion of an account your records use. |

Code samples for a verifier, a usage, an observer, a disbursement provider, a check voucher source and a source access are in [references/banking-extensions.md](references/banking-extensions.md).

## Checks, Disbursements and Direct Deposit

- **Checks** keeps `CheckLayout`s (stock, form, field positions in points, calibration, MICR settings, payer, signatures) and a register of `CheckRecord`s numbered per bank account from `INumberSequence` series `Checks:{bankAccountId}` (starting at 1001). `ICheckService` issues, prints, reprints, voids, reissues, stops, clears and marks stale. Signature images and the MICR font live in an encrypted tenant file store, never in the media library.
- **Disbursements** records a `Disbursement` once per idempotency key (`IDisbursementService.CreateAsync`), batches pending ones by provider, source account, currency, source kind and scope (`CreateBatchAsync`), and releases them (`ReleaseBatchAsync`). Built-in providers: **Printed check** (`Check`, when Checks is on) and **Paid outside the system** (`Manual`).
- **Direct Deposit** adds the **Bank file (ACH)** provider (`Nacha`, method `Ach`, US dollars only), the `NachaOriginSettings` card (**Bank file (ACH)**) on the business's bank account, the prenote verifier, the return and change file import, and the **Read a disbursement batch file** pipeline step (`ReadDisbursementBatchFile`).

Statuses, layouts, the MICR line, providers, batches, returns and the full workflow input lists are in [references/checks-and-disbursements.md](references/checks-and-disbursements.md).

## Plaid

Plaid is Banking's first link provider. Settings (`PlaidSettings`: `Environment` `Sandbox` or `Production`, `ClientId`, `SecretName`) hold only the name of an Orchard secret. Setup, the link flow, tokenized account numbers, the signed webhook and the CSP entries are in [references/plaid.md](references/plaid.md).

```json
{
  "steps": [
    { "name": "Feature", "enable": [ "OrchardCore.Secrets", "CrestApps.OrchardCore.Plaid" ], "disable": [] },
    { "name": "Secrets", "Secrets": [ { "Name": "Plaid.Secret", "Store": "Database" } ] },
    {
      "name": "Settings",
      "PlaidSettings": { "Environment": "Sandbox", "ClientId": "your-client-id", "SecretName": "Plaid.Secret" }
    }
  ]
}
```

## Admin Menu

| Location | Feature | Permission |
|---|---|---|
| **Banking > Bank Accounts** | Bank Accounts | `ManageBankAccounts` |
| **Banking > My Bank Accounts** | Bank Accounts | `ManageOwnBankAccounts` |
| **Banking > Checks** (register, **Write a check**, **Next numbers**) | Checks | `ViewCheckRegister` (actions `ManageChecks`) |
| **Banking > Check Layouts** | Checks | `ManageCheckLayouts` |
| **Banking > Disbursements** | Disbursements | `ViewDisbursements` (actions `ManageDisbursements`) |
| **Banking > Disbursement Batches** (release, download, mark paid, cancel, **Import a return file**, **Reopen after checking**) | Disbursements | `ManageDisbursements` |
| **Settings > Plaid** | Plaid | `ManagePlaidSettings` |

The Plaid secret itself is added under **Tools > Security > Secrets**.

## Permissions

| Permission | Feature | Notes |
|---|---|---|
| `ManageBankAccounts` | Bank Accounts | Every account; full numbers stay hidden. Implies `ManageOwnBankAccounts`. |
| `ManageOwnBankAccounts` | Bank Accounts | One's own accounts (owned by the user or a party the user is); no status, no reveal. |
| `RevealBankAccountNumbers` | Bank Accounts | Security critical. Every reveal is an Audit Trail event. |
| `ManageCheckLayouts` | Checks | Security critical. |
| `ManageChecks` | Checks | Security critical. Write, print, reprint, void, reissue, stop, clear, next numbers. Implies `ViewCheckRegister`. |
| `ViewCheckRegister` | Checks | The register and the Check register data set. |
| `ManageDisbursements` | Disbursements | Security critical. Record, batch, release, return files. Implies `ViewDisbursements`. |
| `ViewDisbursements` | Disbursements | The list and the Disbursements data set. |
| `ManagePlaidSettings` | Plaid | Security critical. |

Administrators get all of them by default.

## Recipes and Deployment

| Step | Entries | Deployment step | Notes |
|---|---|---|---|
| `BankAccounts` | `Accounts` | **Bank accounts** (`IncludeSensitiveData` writes numbers; needs `RevealBankAccountNumbers`, each number recorded as a reveal) | Matched by `ItemId`. Numbers are read in clear text and protected with the importing site's keys. A status is kept only with numbers. |
| `CheckLayouts` | `Layouts` | **Check layouts** | Matched by id then name; files are not exported. |
| `CheckRegister` | `Checks` | **Check register** (opt-in, needs `ViewCheckRegister`) | Imported checks are history (`IsHistorical`); an existing bank account and number is skipped. |
| `Disbursements` | `Disbursements` | **Disbursements** (opt-in, needs `ViewDisbursements`) | History only; an existing idempotency key is skipped. No account number is written. |

```json
{
  "steps": [
    {
      "name": "BankAccounts",
      "Accounts": [
        {
          "Name": "Payroll funding",
          "HolderName": "Contoso Ltd",
          "Kind": "Checking",
          "Country": "US",
          "Currency": "USD",
          "BankName": "First Bank",
          "RoutingNumber": "021000021",
          "AccountNumber": "123456789",
          "Status": "Verified",
          "Properties": {
            "NachaOriginSettings": {
              "Enabled": true,
              "ImmediateDestination": "021000021",
              "ImmediateDestinationName": "FIRST BANK",
              "ImmediateOrigin": "1123456789",
              "ImmediateOriginName": "CONTOSO INC",
              "CompanyName": "CONTOSO INC",
              "CompanyIdentification": "1123456789",
              "CompanyEntryDescription": "PAYROLL",
              "IncludeOffset": false
            }
          }
        },
        {
          "Name": "Avery's pay account",
          "OwnerKind": "Staff",
          "OwnerId": "staffmember0001",
          "HolderName": "Avery Lee",
          "Kind": "Checking",
          "Country": "US",
          "Currency": "USD",
          "RoutingNumber": "021000021",
          "AccountNumber": "000123456789",
          "Status": "Verified"
        }
      ]
    }
  ]
}
```

More step samples (layouts, register, disbursements) and the **Checks: positive pay file** recipe (`ChecksPositivePay`) are in [references/checks-and-disbursements.md](references/checks-and-disbursements.md).

## Data Sets

With Data Pipelines Core on:

| Data source | Data set | Permission | Notes |
|---|---|---|---|
| `Banking` | `BankAccounts` | `ManageBankAccounts` | Last digits only; numbers are never columns. |
| `Checks` | `CheckRegister` | `ViewCheckRegister` | Date column is the indexed `ModifiedUtc`; restricted by source access. Feeds positive pay. |
| `Disbursements` | `Disbursements` | `ViewDisbursements` | Restricted by source access; no account number. |

## Workflow Activities

| Feature | Events | Tasks |
|---|---|---|
| Bank Accounts | Bank Account Changed, Bank Account Status Changed (setting **Status**) | none |
| Checks | Check Issued, Check Printed, Check Voided, Check Stop Payment, Check Reissued, Check Stale, Check Cleared | **Mark Stale Checks** (`AgeInDays`, default 180; output `StaleChecks`) |
| Disbursements | Disbursement Created, Added to Batch, Removed from Batch, Submitted, Settled, Returned, Failed, Canceled, Reversed; Disbursement Batch Created, Released, Reopened, Canceled, Settled; Disbursement Returns Applied | **Settle Disbursement Batches** (`AgeInDays`, default 3; output `SettledDisbursements`) |

There is no background task: schedule **Mark Stale Checks** and **Settle Disbursement Batches** from a Timer Event once a day. No event input carries an account number.

## Troubleshooting

| Symptom | Check |
|---|---|
| Disbursement refused for a bank file | The payee account is not `Verified`, has no numbers, or has another currency; the source account is not a business account (empty `Owner`) or is closed. |
| "The key was already used" | The idempotency key's disbursement failed, was cancelled, returned or reversed: use a new key for a new attempt. |
| Batch stuck in `Releasing` | The provider threw. Check with the bank, then **Reopen after checking** on **Banking > Disbursement Batches**. |
| A layout refuses to print | **Print the check line** is on and no MICR font is uploaded, or a number doesn't fit the check line. |
| Account can't be deleted | An `IBankAccountUsage` (checks, disbursements, Payroll) points to it; close it. |
| ACH file refused | No company entry description, an account number over 17 characters, or an arrival date in the past. |
| Plaid button missing | Plaid settings not configured (`IsAvailableAsync` false), or the secret has no value for the environment. |
| Plaid Link doesn't open | The site's CSP blocks `https://cdn.plaid.com` (scripts) or `https://*.plaid.com` (frames). |

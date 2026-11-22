# Checks, Disbursements and Direct Deposit

Reference for the Checks (`CrestApps.OrchardCore.Banking.Checks`), Disbursements (`CrestApps.OrchardCore.Banking.Disbursements`) and Direct Deposit (`CrestApps.OrchardCore.Banking.DirectDeposit`) features. Namespaces: `CrestApps.OrchardCore.Banking.Checks.*` and `CrestApps.OrchardCore.Banking.Disbursements.*`.

## Check layouts

A `CheckLayout` (named catalog entry, matched by id then name in recipes):

| Field | Notes |
|---|---|
| `Stock` | `Blank` (prints payer, bank and check line) or `PrePrinted`. |
| `Form` | `CheckOnTop`, `CheckInMiddle`, `CheckOnBottom` (one check and two vouchers per letter sheet) or `ThreePerPage`. |
| `Fields` | Dictionary keyed by `CheckFieldNames` (`Date`, `Payee`, `PayeeAddress`, `Amount`, `AmountInWords`, `Memo`, `Number`, `Payer`, `Bank`, `Signature`, `SecondSignature`) of `X`, `Y`, `Width`, `FontSize` (default 10), `Hidden`, in points from the top left of the check. |
| `OffsetX`, `OffsetY` | Calibration in points (at most 72 either way). |
| `PrintMicr`, `MicrCountry` (`US`), `UseAuxiliaryOnUs` (business format, default `true`), `MicrFontSize` (12), `MicrSymbolCharacters` (`ABCD`) | The check line. |
| `PayerName`, `PayerAddress` | Printed on blank stock; the address uses the Addresses editor. |
| `SecondSignatureAbove` | Amount above which a second signature line prints. |
| `HasSignature`, `HasSecondSignature`, `HasMicrFont` | Whether the files are stored (PNG/JPEG up to 1 MB, TrueType up to 2 MB) in the encrypted store under `App_Data/Sites/{tenant}/Checks`. |

The editor's **Check designer** draws the check at 612 by 264 points and posts the same `Fields[i]` inputs as **Exact positions**, so the form works without its script.

```json
{
  "steps": [
    {
      "name": "CheckLayouts",
      "Layouts": [
        {
          "Name": "Contoso paychecks",
          "Stock": "Blank",
          "Form": "CheckOnTop",
          "PrintMicr": false,
          "PayerName": "Contoso Ltd",
          "SecondSignatureAbove": 10000,
          "Fields": {
            "Date": { "X": 460, "Y": 40, "Width": 110, "FontSize": 10 },
            "Amount": { "X": 470, "Y": 80, "Width": 100, "FontSize": 11 }
          }
        }
      ]
    }
  ]
}
```

## The check register

`CheckRecord`: `BankAccountId` (a business account), `BankAccountName`, `Number`, `Payee` (`PartyRef`), `PayeeName`, `PayeeAddress`, `Amount`, `Currency` (the account's), `CheckDate` (`DateOnly`), `Memo`, `SourceKind`, `SourceId`, `AccessScope`, `Status`, the dates of each change, `ReissuedById`/`ReissueOfId`, `PrintRunId`, `IsHistorical`. It never holds an account number.

| From | To (`CheckStatuses.Transitions`) |
|---|---|
| `Issued` | `Printed`, `Voided`, `Reissued` |
| `Printed` | `Voided`, `StopPayment`, `Cleared`, `Reissued`, `Stale` |
| `StopPayment`, `Stale` | `Voided`, `Reissued`, `Cleared` |
| `Reissued` | `Cleared` (flagged **Paid after reissue**) |
| `Voided`, `Cleared` | none |

`ICheckService`: `IssueAsync(CheckIssueRequest)`, `GetNextNumberAsync`, `SetNextNumberAsync`, `MarkPrintedAsync(checks, printRunId)`, `MarkReprintedAsync`, `VoidAsync`, `ReissueAsync` (new number, dated today in the site time zone), `StopPaymentAsync`, `MarkClearedAsync(check, clearedOn)`, `MarkStaleAsync`, `MarkStaleChecksAsync(ageInDays)`. Issuing refuses an account another party owns, a closed or failed account, one without numbers, another currency and a non-positive amount. Historical checks are never printed or reissued.

Printing returns one PDF (`Cache-Control: no-store`) for the whole print run. The check line of every check is laid out before anything is drawn, and the account number is read once per bank account per run as an audited Banking reveal.

The United States check line (`UnitedStatesCheckMicrFormat`): amount field positions 1 to 12 blank; account number ending at 15 with the on-us symbol at 14; routing number between transit symbols at 43 and 33; on a business check the check number (six digits or more) between on-us symbols from 45. Other countries register `ICheckMicrFormat` with `AddCheckMicrFormat<T>()`; other languages of the amount line register `IAmountInWords` with `AddAmountInWords<T>()`.

```json
{
  "steps": [
    {
      "name": "CheckRegister",
      "Checks": [
        {
          "BankAccountId": "contosooperating0001",
          "BankAccountName": "Contoso operating account",
          "Number": 1001,
          "PayeeName": "Sam Rivera",
          "Amount": 1520.35,
          "Currency": "USD",
          "CheckDate": "2026-10-16",
          "Memo": "Pay 2026-10-16",
          "Status": "Cleared",
          "ClearedOn": "2026-10-20"
        }
      ]
    }
  ]
}
```

### Positive pay

The **Checks: positive pay file** recipe (`ChecksPositivePay`) enables Checks and `OrchardCore.DataPipelines` and adds a draft pipeline that reads the `CheckRegister` data set rows changed since the last successful run (**Read only new rows** on `ModifiedUtc`) and writes a CSV. Publish it under **Tools > Data Pipelines**, add the bank's SFTP or web API step, and schedule it from a workflow timer. A fixed-width bank layout is an `IDataFileFormat`.

## Disbursements

`Disbursement` (collection `Disbursements`, `DisbursementIndex`, never an account number, no amount in the index): `Payee`, `PayeeName`, `PayeeAddress`, `IsBusinessPayee`, `Amount`, `Currency`, `Method` (open string; `DisbursementsConstants.Methods` has `Ach`, `Check`, `Manual`, `Provider`), `Provider`, `SourceBankAccountId`, `DestinationBankAccountId`, `SourceKind`, `SourceId`, `AccessScope`, `Memo`, `IdempotencyKey`, `IsPrenote`, `Status`, `BatchId`, `EffectiveDate`, `TraceNumber`, `CheckId`, `FailureCode`, `FailureReason`, `IsHistorical`.

| From | To (`DisbursementStatuses.Transitions`) |
|---|---|
| `Pending` | `Batched`, `Submitted`, `Settled`, `Failed`, `Canceled` |
| `Batched` | `Pending`, `Submitted`, `Settled`, `Failed`, `Canceled` |
| `Submitted` | `Settled`, `Returned`, `Failed`, `Canceled` |
| `Settled` | `Returned`, `Reversed` |
| `Returned`, `Failed`, `Canceled`, `Reversed` | none |

`DisbursementBatch` status: `Open`, `Releasing`, `Released`, `Settled`, `Canceled`.

### IDisbursementService

| Member | Does |
|---|---|
| `CreateAsync(DisbursementRequest)` | Records one, or returns the existing one for the key (`DisbursementResult.AlreadyExisted`). Default key `SourceKind:SourceId:Method:DestinationBankAccountId`. Same key with different payee, amount, currency, method or destination is refused. A distributed lock per key. |
| `CancelAsync(disbursement, reason)` | Cancels a pending one. |
| `CreateBatchAsync(provider, ids, effectiveDate[, entryDescription])` | Batches pending disbursements of the provider's method, one source account, currency, source kind and access scope; returns `(Batch, Problems)`. Arrival date can't be in the past. |
| `ReleaseBatchAsync(batch)` | Saves `Releasing`, calls the provider, stores the file encrypted (purpose `Disbursements.Files`), keeps trace numbers and check ids. A refusal reopens; a throw leaves `Releasing`. |
| `ReopenReleasingBatchAsync(batch, reason)` | After checking with the provider. |
| `CancelBatchAsync`, `SettleBatchAsync`, `SettleReleasedBatchesAsync(ageInDays)` | Cancel an open batch; mark paid; settle every released batch past its age. |
| `MarkSettledAsync`, `MarkClosedAsync(disbursement, status, code, reason)` | Move one disbursement. |
| `ApplyReturnsAsync`, `ApplyChangeNoticesAsync` | Returns mark disbursements returned (codes in `NachaReasonCodes`); notices of change (C01, C02, C03, C05, C06, C07) correct the payee's account, which becomes unverified. Overloads take a `SourceAccessFilter`. |

```csharp
using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Parties.Models;

public sealed class VendorPayoutSender
{
    private readonly IDisbursementService _disbursements;

    public VendorPayoutSender(IDisbursementService disbursements)
    {
        _disbursements = disbursements;
    }

    public async Task<DisbursementResult> SendAsync(string payoutId, string vendorAccountId, string vendorBankAccountId, string fundingAccountId, decimal amount, DateOnly payOn)
    {
        var result = await _disbursements.CreateAsync(new DisbursementRequest
        {
            Payee = new PartyRef("Account", vendorAccountId),
            PayeeName = "Northwind Traders",
            IsBusinessPayee = true,
            Amount = amount,
            Currency = "USD",
            Method = DisbursementsConstants.Methods.Ach,
            SourceBankAccountId = fundingAccountId,
            DestinationBankAccountId = vendorBankAccountId,
            SourceKind = "VendorPayout",
            SourceId = payoutId,
            AccessScope = vendorAccountId,
            EffectiveDate = payOn,
            IdempotencyKey = $"vendor-payout:{payoutId}",
        });

        // result.Errors says why it was refused; result.AlreadyExisted on a retry.
        return result;
    }
}
```

### Providers

| Provider (`Name`) | Display name | Method | Feature | Does |
|---|---|---|---|---|
| `Nacha` | Bank file (ACH) | `Ach` | Direct Deposit | Nacha file: PPD batch for people, CCD for businesses, prenotes (codes 23 and 33), optional offset debit, entry hashes, trace numbers, file id modifier per day, blocks of ten. USD only. Each account number read once per file as an audited reveal. |
| `Check` | Printed check | `Check` | Disbursements, when Checks is on | One check per disbursement, dated the effective date. Cleared settles, voided or stopped cancels, reissue moves it to the new check. |
| `Manual` | Paid outside the system | `Manual` | Disbursements | Releasing records them settled. |

Add your own with `AddDisbursementProvider<T>()` (see [banking-extensions.md](banking-extensions.md)).

### Direct Deposit details

- `NachaOriginSettings` (in the business account's `Properties`, card **Bank file (ACH)**): `Enabled`, `ImmediateDestination`, `ImmediateDestinationName`, `ImmediateOrigin`, `ImmediateOriginName`, `CompanyName`, `CompanyIdentification`, `CompanyEntryDescription`, `IncludeOffset`.
- **Prenotes**: **Check with > Test entry (prenote)** (`NachaPrenoteVerifier`) records a zero-amount disbursement (`IsPrenote`, source kind `Prenote`) from the first business account set up for bank files and moves the account to `VerificationSent`. A settled batch without return verifies it; a return fails it (`NachaPrenoteObserver`).
- **Return and change files**: **Banking > Disbursement Batches > Import a return file** reads type 99 (returns) and type 98 (notices of change) addenda.
- **Read a disbursement batch file** (`ReadDisbursementBatchFile`, with Data Pipelines on): the bank files of batches released since the last successful run, for the SFTP, FTP, web API or email destinations. The publisher needs `ManageDisbursements` and must be allowed every disbursement of a batch. The watermark moves only on success. Each file sent is logged and, with Audit Trail, recorded as **Bank file sent**.

```json
{
  "steps": [
    {
      "name": "Disbursements",
      "Disbursements": [
        {
          "IdempotencyKey": "legacy:2026-10-16:0001",
          "PayeeName": "Avery Lee",
          "Amount": 2450.12,
          "Currency": "USD",
          "Method": "Ach",
          "Provider": "Nacha",
          "SourceKind": "PayStatement",
          "SourceId": "legacystatement0001",
          "Status": "Settled",
          "EffectiveDate": "2026-10-16",
          "TraceNumber": "021000020000001"
        }
      ]
    }
  ]
}
```

## Who sees which checks and disbursements

`ISourceAccess` (`CrestApps.OrchardCore.SourceAccess`, register with `AddSourceAccess<T>()`) returns, for a user and a `SourceAccessLevel` (`View` or `Manage`), `SourceAccessScope.All`, `SourceAccessScope.None` or `SourceAccessScope.Of(scopes)` for one `SourceKind`. `ISourceAccessService` applies every source access to the lists (a condition on the index `AccessKey`), data sets, deployment exports and each action by id (not found otherwise). A batch is shown only when all its disbursements are allowed. Records written by hand, and source kinds without a source access, are visible to anyone the permissions allow. Payroll registers one for `PayStatement` and `PayrollGarnishment`.

## Workflow event inputs

- **Bank Account Changed / Status Changed**: `BankAccountId`, `Name`, `OwnerKind`, `OwnerId`, `Status`, `PreviousStatus`, `Reason`, `Changes`, `Actor`. A reveal starts neither.
- **Check events**: `CheckId`, `Number`, `BankAccountId`, `PayeeName`, `Amount`, `Currency`, `CheckDate`, `SourceKind`, `SourceId`, `Status`, `Change` (`Reprinted` on a reprint), `ReplacedById`, `Reason`, `Actor`.
- **Disbursement events**: `DisbursementId`, `PayeeName`, `Amount`, `Currency`, `Method`, `Status`, `Change`, `SourceKind`, `SourceId`, `BatchId`, `TraceNumber`, `FailureCode`, `FailureReason`, `IsPrenote`, `EffectiveDate`, `Actor`.
- **Batch events**: `BatchId`, `Provider`, `SourceBankAccountId`, `SourceBankAccountName`, `EffectiveDate`, `Count`, `TotalAmount`, `Currency`, `Status`, `PreviousStatus`, `SourceKind`, `AccessScope`, `FileName`, `ProviderReference`, `Change`, `Reason`, `Settled`, `Actor`.
- **Disbursement Returns Applied**: `Returned`, `Problems`, `ProblemMessages`, `Actor`.

Audit Trail categories **Banking** (account added, changed, number changed, status changed, removed, number shown) and the Disbursements events record masked numbers only.

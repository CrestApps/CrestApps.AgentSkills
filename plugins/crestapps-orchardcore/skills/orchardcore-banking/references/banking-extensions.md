# Banking Extension Examples

Register these from your module's `Startup` (or its `.Core` registration). The `Add*` helpers live in `CrestApps.OrchardCore.Banking.Abstractions` (`BankingServiceCollectionExtensions`, `ChecksServiceCollectionExtensions`, `DisbursementsServiceCollectionExtensions`) and `CrestApps.OrchardCore.Abstractions` (`AddSourceAccess`).

## Keep an account in use from being deleted

```csharp
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;

public sealed class VendorBankAccountUsage : IBankAccountUsage
{
    private readonly IVendorStore _vendors;

    public VendorBankAccountUsage(IVendorStore vendors)
    {
        _vendors = vendors;
    }

    // Return null when nothing of this module points to the account.
    public async Task<string> GetUsageAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        var count = await _vendors.CountPayingToAsync(account.ItemId, cancellationToken);

        return count == 0 ? null : $"{count} vendor(s) are paid to this account.";
    }
}

// services.AddBankAccountUsage<VendorBankAccountUsage>();
```

## React to account changes

```csharp
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;

public sealed class PayAccountChangedNotifier : IBankAccountObserver
{
    public Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        if (change.Account.Owner.Kind != "Staff" || !change.Has(BankAccountChangeKind.NumberChanged))
        {
            return Task.CompletedTask;
        }

        // Send a security notice naming change.Actor. Never include the account number.
        return Task.CompletedTask;
    }
}

// services.AddBankAccountObserver<PayAccountChangedNotifier>();
```

## A verifier

```csharp
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.Extensions.Localization;

public sealed class MicroDepositVerifier : IBankAccountVerifier
{
    internal readonly IStringLocalizer S;

    public MicroDepositVerifier(IStringLocalizer<MicroDepositVerifier> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => "MicroDeposit";

    public LocalizedString DisplayName => S["Two small deposits"];

    public Task<BankAccountVerificationResult> StartAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        if (account.Currency != "USD")
        {
            return Task.FromResult(BankAccountVerificationResult.Refused("Only US dollar accounts can be checked this way."));
        }

        // Send the deposits, then return VerificationSent; confirm later with IBankAccountService.ChangeStatusAsync.
        return Task.FromResult(BankAccountVerificationResult.Started());
    }
}

// services.AddBankAccountVerifier<MicroDepositVerifier>();
```

## A disbursement provider

```csharp
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.Localization;

public sealed class WireTransferProvider : IDisbursementProvider
{
    internal readonly IStringLocalizer S;

    public WireTransferProvider(IStringLocalizer<WireTransferProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => "Wire";

    public LocalizedString DisplayName => S["Wire transfer"];

    public string Method => "Wire";

    public async Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
    {
        // Use batch.ItemId (or each disbursement's IdempotencyKey) as the idempotency key at the provider.
        var result = new DisbursementSubmitResult
        {
            Succeeded = true,
            ProviderReference = batch.ItemId,
        };

        foreach (var disbursement in disbursements)
        {
            result.Statuses[disbursement.ItemId] = DisbursementStatus.Submitted;
        }

        return await Task.FromResult(result);
    }
}

// services.AddDisbursementProvider<WireTransferProvider>();
```

Return `DisbursementSubmitResult.Failed("reason")` to refuse the whole batch (it reopens). A thrown exception leaves the batch `Releasing` until someone reopens it after checking with the provider.

## A check voucher for your source kind

```csharp
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;

public sealed class VendorPaymentVoucherSource : ICheckVoucherSource
{
    public string SourceKind => "VendorPayout";

    public Task<CheckVoucher> GetVoucherAsync(CheckRecord check, CancellationToken cancellationToken = default)
        => Task.FromResult(new CheckVoucher
        {
            Title = "Payment advice",
            Columns = ["Invoice", "Amount"],
            Lines =
            [
                new CheckVoucherLine { Label = check.Memo, Values = [check.Amount.ToString("N2")] },
                new CheckVoucherLine { Label = "Total", Values = [check.Amount.ToString("N2")], IsEmphasized = true },
            ],
        });
}

// services.AddCheckVoucherSource<VendorPaymentVoucherSource>();
```

Observers of checks and batches: `AddCheckObserver<T>()`, `AddDisbursementObserver<T>()` (`OnChangedAsync(DisbursementChange)`, kinds `Created`, `Batched`, `Unbatched`, `Submitted`, `Settled`, `Returned`, `Failed`, `Canceled`, `Reversed`) and `AddDisbursementBatchObserver<T>()`.

## Who sees your checks and disbursements

```csharp
using System.Security.Claims;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;

public sealed class VendorPayoutSourceAccess : ISourceAccess
{
    private readonly IAuthorizationService _authorizationService;

    public VendorPayoutSourceAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public string SourceKind => "VendorPayout";

    public async Task<SourceAccessScope> GetScopeAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default)
    {
        var permission = level == SourceAccessLevel.Manage ? VendorPermissions.PayVendors : VendorPermissions.ViewVendorPayouts;

        return await _authorizationService.AuthorizeAsync(user, permission)
            ? SourceAccessScope.All
            : SourceAccessScope.None;
    }
}

// services.AddSourceAccess<VendorPayoutSourceAccess>();
```

Return `SourceAccessScope.Of(scopes)` to allow only some `AccessScope` values (Payroll returns the employers in the user's payroll scope). The same seam limits approval requests of a type (see `orchardcore-approvals`).

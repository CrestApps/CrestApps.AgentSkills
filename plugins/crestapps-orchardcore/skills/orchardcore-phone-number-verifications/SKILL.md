---
name: orchardcore-phone-number-verifications
description: Skill for verifying and revalidating phone numbers on Orchard Core content items with pluggable providers. Covers the dependency-only CrestApps.OrchardCore.PhoneNumbers.Verifications feature and its AbstractApi, Veriphone and Twilio provider features, provider configuration and permissions, PhoneNumberVerificationPart, provider results, resilient provider HTTP clients that never replay unsafe requests, background revalidation, SQL indexing, the Phone Verifications Queue admin list, the shared Reports integration, and Omnichannel contact integration through IContactMethodsAccessor. Use this skill when requests mention Orchard Core phone verification, phone validation APIs, verification queues, or revalidating contact numbers. Strong matches include work with CrestApps.OrchardCore.PhoneNumbers.Verifications, IPhoneNumberVerificationManager, IPhoneNumberVerificationProvider, PhoneNumberVerificationPart, PhoneNumberVerificationResult, and PhoneNumberRevalidationBackgroundTask.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Phone Number Verifications - Prompt Templates

## Verify Contact Phone Numbers

You are an Orchard Core expert. Generate accurate code, settings, recipes, and integration guidance for the CrestApps Phone Number Verifications module. It is a provider-agnostic framework that persists verification data on content items and supports later revalidation.

### Guidelines

- Install `CrestApps.OrchardCore.PhoneNumbers.Verifications` in the web/startup project.
- Enable a provider feature; the core feature is `EnabledByDependencyOnly` and is turned on by the provider's dependency.
- The core feature depends on `OrchardCore.Contents`, `CrestApps.OrchardCore.Resources`, and `CrestApps.OrchardCore.PhoneNumbers`.
- Attach `PhoneNumberVerificationPart` only to content types representing a contact or another verified number owner.
- Use `IPhoneNumberVerificationManager` to select and invoke the configured provider.
- Store results with `PhoneNumberVerificationPartExtensions`, not a hand-written parallel data structure.
- Do not equate a transport/provider failure with `Invalid`; failures are retryable.
- Use `IPhoneNumberVerificationProvider` to add a provider and give it a stable provider key.
- Add provider settings as a site display driver rather than application-wide static configuration.
- Let the built-in background task throttle and revalidate due records.
- Register provider HTTP clients with `AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods())` so a lost response never replays a paid lookup.
- Install external-provider packages and CrestApps packages in the web/startup project.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except for View Models.

### Features

| Feature | Feature ID | Purpose |
|---|---|---|
| Core | `CrestApps.OrchardCore.PhoneNumbers.Verifications` | Parts, manager, settings, index, queue, report, and scheduled revalidation |
| AbstractAPI | `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | AbstractAPI Phone Validation provider |
| Veriphone | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Veriphone provider |
| Twilio | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Twilio Lookup provider |

### Enable a Provider

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.PhoneNumbers.Verifications",
        "CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio"
      ],
      "disable": []
    }
  ]
}
```

Enable exactly the provider features that the tenant can configure. The framework is useful without a provider only for existing stored data; it cannot newly verify numbers until a provider is enabled and configured.

## Architecture

| Abstraction | Responsibility |
|---|---|
| `IPhoneNumberVerificationProvider` | Calls an external provider and returns `PhoneNumberVerificationResult` |
| `IPhoneNumberVerificationManager` | Resolves the selected provider, runs verification, and invokes lifecycle handlers |
| `PhoneNumberVerificationProviderOptions` | Holds registered provider descriptors keyed by provider key |
| `PhoneNumberVerificationPart` | Persists normalized verification data on a content item |
| `PhoneNumberVerificationPartExtensions` | Reads and updates the part and serialized result |
| `IPhoneNumberVerificationHandler` | Receives verification lifecycle notifications |
| `PhoneNumberRevalidationBackgroundTask` | Finds due records and processes revalidation |

Provider keys identify a provider independently of its display name. The built-in keys are exposed through `PhoneNumberVerificationsConstants.Providers`.

### Permissions

| Permission | Grants |
|---|---|
| `ManagePhoneNumberVerificationSettings` | **Settings → Phone Number Verifications** and provider tabs. The settings group registers it with `AddSiteSettingsPermission`, so a role holding only this permission can open the page. |
| `RunPhoneNumberVerificationsReport` | **Tools → Phone Verifications Queue** and the report under **Reports**. Granted to the built-in **Supervisor** role on new tenants; grant it manually on existing tenants. |
| `VerifyPhoneNumbers` | Re-queuing records (**Retry now**, **Retry selected**, **Retry all failed**). |

All three are granted to **Administrator** by default.

## Configure Settings

Open **Settings → Phone Number Verifications**:

| Setting | Default | Meaning |
|---|---|---|
| Default provider | First enabled provider | Provider chosen when no explicit valid selection exists |
| Revalidation interval | `365` days | How long a completed result remains current |
| Maximum verification attempts | `3` | Consecutive failed requests allowed before manual attention is required |
| Request delay | `1000` ms | Delay between background provider requests |

Each enabled provider feature adds a tab to the same settings screen. Enable the provider on that tab and enter the provider credentials. A provider appears in the default-provider list only when its enable switch is on.

Secrets saved by a provider settings driver must be protected. Do not put API keys in recipes, source code, client-side configuration, or reports.

## Attach the Content Part

Attach **Phone Number Verification Part** to a contact content type through **Content → Content Definition → Content Types**. The migration creates an attachable `PhoneNumberVerificationPart` definition.

The part stores:

| Data | Meaning |
|---|---|
| `PhoneNumber` | Submitted number |
| `NormalizedPhoneNumber` | E.164 number returned by the verification flow |
| `LastVerifiedUtc` and `LastVerifiedByUserId` | Completed-verification audit data |
| `VerificationProvider` and `VerificationStatus` | Provider identity and normalized state |
| `VerificationResultJson` | Full normalized result payload |
| `VerificationAttemptCount` and `FailedAttemptCount` | Lifetime and consecutive failure counts |
| `LastError` and `LastAttemptUtc` | Retry diagnostic information |
| `NextVerificationDueUtc` | Revalidation scheduling value |

The module maintains `PhoneNumberVerificationPartIndex` for common reporting and queue queries. Do not query serialized JSON when an indexed field is sufficient.

## Explicit Verification

Resolve `IPhoneNumberVerificationManager`, call `VerifyAsync`, then update the part using the extension method:

```csharp
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using OrchardCore.ContentManagement;

namespace MyCompany.OrchardCore.Contacts;

public sealed class ContactVerificationService
{
    private readonly IPhoneNumberVerificationManager _verificationManager;

    public ContactVerificationService(IPhoneNumberVerificationManager verificationManager)
    {
        _verificationManager = verificationManager;
    }

    public async Task VerifyAsync(
        ContentItem contentItem,
        string phoneNumber,
        string userId,
        int revalidationIntervalDays,
        CancellationToken cancellationToken)
    {
        var result = await _verificationManager.VerifyAsync(phoneNumber, cancellationToken: cancellationToken);

        contentItem.AlterPhoneNumberVerificationResult(
            result,
            verifiedByUserId: userId,
            revalidationIntervalDays: revalidationIntervalDays);
    }
}
```

Use `contentItem.TryGet<PhoneNumberVerificationPart>(out var part)` to check whether the content type carries the part. Use `TryGetPhoneNumberVerificationResult` to read the stored normalized result.

## Result and Status Semantics

`PhoneNumberVerificationResult` is the abstraction returned by
`IPhoneNumberVerificationProvider`. `IPhoneNumberVerificationManager.VerifyAsync`
accepts a phone-number string, an optional provider key, and a cancellation
token. It selects the configured or default provider; providers do not select
themselves.

| Outcome | Correct behavior |
|---|---|
| Provider returns valid/invalid answer | Persist `Verified` or `Invalid` result |
| Rate limit, HTTP, or parsing failure | Record a retryable failure; do not mark the number invalid |
| Existing verified result and later provider outage | Retain the completed status and due date while recording the failure |
| Completed provider response after failures | Reset failed count and last error |

The raw provider response can be retained in `VerificationResultJson`, but UI and reporting should use the normalized result model. Avoid displaying provider raw payloads to untrusted users.

## Background Revalidation and Queue

`PhoneNumberRevalidationBackgroundTask` runs every five minutes. It locates due records, processes bounded throttled batches, and uses distributed locking so multiple instances do not duplicate work.

After the configured maximum consecutive failures, a record is shown as **Needs attention** and stops automatic retries. Administrators with the appropriate permission can requeue a single item, selected items, or all matching failed records from the **Tools → Phone Verifications Queue**.

Queue retry actions set records pending and defer actual provider requests until after state is saved. Do not build a synchronous bulk web action that calls every external provider immediately.

The queue renders through Orchard Core's `AdminList` shape (list name `PhoneNumberVerificationRecords`). Its row display model is `PhoneNumberVerificationRecord` in the module's `Models` namespace (it was a view model in 2.x), displayed by `PhoneNumberVerificationRecordDisplayDriver`. Customize the List layout with `PhoneNumberVerificationRecord_SummaryAdmin` placement and the Grid layout with `SummaryAdminGrid` placement; see `orchardcore-admin-lists`.

## Reporting

With `CrestApps.OrchardCore.Reports` enabled, the **Phone Number Verifications** report appears under **Reports → General** for users with `RunPhoneNumberVerificationsReport`. It reads `PhoneNumberVerificationPartIndex` and uses the shared report renderer and CSV and Excel exports. See `orchardcore-reports`.

## Omnichannel Contacts

When `CrestApps.OrchardCore.Omnichannel.Managements` is enabled, `OmnichannelContactPhoneNumberVerificationHandler` reads the contact's numbers through `IContactMethodsAccessor`, so it works for contact types that keep numbers in the `ContactMethods` bag or in their own fields through an `IContactMethodsProvider` (see `orchardcore-omnichannel-management`). It prefers `Cell` (a number typed `Mobile` counts as cell), then `Home`, `Office`, `Work`, and `Other`, stores a changed value as unverified in the current content save, and schedules external verification after that save.

Whether a lookup is needed is decided from the verification stored on the contact. A save that rebuilds the bag item holding the number does not queue a new paid lookup; the item gets the contact's copy of the verification back. Removing the number clears the stored verification.

This handler is feature-gated. Do not assume it runs for arbitrary custom contact types that do not carry `OmnichannelContactPart`.

## Custom Providers

Reference the Phone Numbers abstractions and implement the provider:

```csharp
using CrestApps.OrchardCore.PhoneNumbers;

namespace MyCompany.OrchardCore.Verifications;

public sealed class MyPhoneNumberVerificationProvider : IPhoneNumberVerificationProvider
{
    public Task<PhoneNumberVerificationResult> VerifyAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        // Call the remote service and map its response to the common result model.
        throw new NotImplementedException();
    }
}
```

Register a named provider in a dedicated feature startup:

```csharp
services.AddHttpClient(nameof(MyPhoneNumberVerificationProvider))
    .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

services.AddPhoneNumberVerificationProvider<MyPhoneNumberVerificationProvider>(
    "MyProvider",
    options =>
    {
        options.DisplayName = S["My Provider"];
        options.Description = S["Verifies phone numbers with My Provider."];
    });
```

Use the provider registration overload that includes a settings type when the
provider has tenant settings. It adds the keyed provider and an
`IPhoneNumberVerificationProviderConfiguration` that determines whether that
provider is selectable. Keep the provider key and keyed-service registration
identical.

## Upgrading from 2.x

- `PhoneNumberVerificationRecord` moved from view models to `Models` and is the queue's row display model; the queue uses `AdminList`, so overrides of the old list markup must move to `SummaryAdmin` and `SummaryAdminGrid` placement.
- Provider HTTP clients retry only safe, idempotent requests. Apply `Retry.DisableForUnsafeHttpMethods()` to custom provider clients too.
- The Omnichannel handler reads numbers through `IContactMethodsAccessor`; custom code that built it by hand must register `services.AddContactMethods()`.
- Grant `RunPhoneNumberVerificationsReport` to the **Supervisor** role on existing tenants if supervisors should see the queue and report.

## Troubleshooting

| Symptom | Check |
|---|---|
| No provider is selected | Enable and configure at least one provider tab |
| Results do not persist | Attach `PhoneNumberVerificationPart` to the content type |
| A failure shows invalid | Correct the provider mapping; request failures must remain retryable |
| Background work is slow | Tune request delay and provider limits, not unbounded concurrent calls |
| Records never retry | Check maximum attempts, due date, and queue failure state |
| Contact changes do not verify | Confirm the Omnichannel Management feature and that the type's numbers are readable through `IContactMethodsAccessor` |

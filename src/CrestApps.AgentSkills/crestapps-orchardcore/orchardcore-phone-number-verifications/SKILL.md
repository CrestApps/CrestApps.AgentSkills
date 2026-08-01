---
name: orchardcore-phone-number-verifications
description: Skill for verifying and revalidating phone numbers on Orchard Core content items with pluggable providers. Covers provider configuration, PhoneNumberVerificationPart, provider results, revalidation, SQL indexing, queues, and Omnichannel contact integration. Use this skill when requests mention Orchard Core phone verification, phone validation APIs, verification queues, or revalidating contact numbers. Strong matches include work with CrestApps.OrchardCore.PhoneNumbers.Verifications, IPhoneNumberVerificationManager, IPhoneNumberVerificationProvider, PhoneNumberVerificationPart, PhoneNumberVerificationResult, and PhoneNumberRevalidationBackgroundTask.
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
- Enable a provider feature as well as the core verification feature; a provider feature enables the core feature through its dependency.
- The core feature depends on `OrchardCore.Contents` and `CrestApps.OrchardCore.PhoneNumbers`.
- Attach `PhoneNumberVerificationPart` only to content types representing a contact or another verified number owner.
- Use `IPhoneNumberVerificationManager` to select and invoke the configured provider.
- Store results with `PhoneNumberVerificationPartExtensions`, not a hand-written parallel data structure.
- Do not equate a transport/provider failure with `Invalid`; failures are retryable.
- Use `IPhoneNumberVerificationProvider` to add a provider and give it a stable provider key.
- Add provider settings as a site display driver rather than application-wide static configuration.
- Let the built-in background task throttle and revalidate due records.
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
| `PhoneNumberVerificationProviderOptions` | Holds provider descriptors and enabled provider registrations |
| `PhoneNumberVerificationPart` | Persists normalized verification data on a content item |
| `PhoneNumberVerificationPartExtensions` | Reads and updates the part and serialized result |
| `IPhoneNumberVerificationHandler` | Receives verification lifecycle notifications |
| `PhoneNumberRevalidationBackgroundTask` | Finds due records and processes revalidation |

Provider keys identify a provider independently of its display name. The built-in keys are exposed through `PhoneNumberVerificationsConstants.Providers`.

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
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;

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

`PhoneNumberVerificationResult` unifies external responses. It includes phone and normalized values, validity and reachability, line-type flags, country and location data, carrier, time zone, risk data, provider reference, raw response, metadata, and a normalized `PhoneNumberVerificationStatus`.

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

## Omnichannel Contacts

When `CrestApps.OrchardCore.Omnichannel.Managements` is enabled, `OmnichannelContactPhoneNumberVerificationHandler` examines contact methods. It prefers `Cell`, then `Home`, `Office`, `Work`, and `Other`, stores a changed value as unverified in the current content save, and schedules external verification after that save.

This handler is feature-gated. Do not assume it runs for arbitrary custom contact types unless they use the matching Omnichannel model.

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
    .AddStandardResilienceHandler();

services.AddPhoneNumberVerificationProvider<MyPhoneNumberVerificationProvider>(
    "MyProvider",
    options =>
    {
        options.DisplayName = S["My Provider"];
        options.Description = S["Verifies phone numbers with My Provider."];
    });
```

Add `AddSiteDisplayDriver<MyProviderSettingsDisplayDriver>()` when the provider has tenant settings. Keep provider-key and keyed-service registration identical.

## Troubleshooting

| Symptom | Check |
|---|---|
| No provider is selected | Enable and configure at least one provider tab |
| Results do not persist | Attach `PhoneNumberVerificationPart` to the content type |
| A failure shows invalid | Correct the provider mapping; request failures must remain retryable |
| Background work is slow | Tune request delay and provider limits, not unbounded concurrent calls |
| Records never retry | Check maximum attempts, due date, and queue failure state |
| Contact changes do not verify | Confirm the Omnichannel Management feature and contact-method structure |

---
name: orchardcore-outreach
description: Skill for the CrestApps Orchard Core Outreach engine (CrestApps.OrchardCore.Outreach), the one bulk-send engine behind Messaging broadcasts and Marketing campaigns. Covers outreach runs with an audience source, channel sender, template and renderer, batches on the leased work queue, per-recipient consent checks, quiet hours from a Business Hours schedule, retries that never send twice, failing a run after five attempts, run observers, the e-mail sender, and the leased work queue building block (ILeasedWorkItem, ILeasedWorkQueue, LeasedWorkBackoff, the YesSql queues). Use this skill when requests mention bulk sends, broadcasts, campaign sending, outreach runs, adding an audience or channel to bulk messaging, opt-outs in bulk sends, or a durable retrying work queue. Strong matches include IOutreachEngine, OutreachRun, IOutreachAudienceSource, IOutreachChannelSender, IOutreachTemplateRenderer, IOutreachRecipientPolicy, IOutreachRunObserver, ILeasedWorkQueue and AddLeasedWorkQueue.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Outreach

## One Engine for Every Bulk Send

You are an Orchard Core expert. Generate accurate guidance and extension code for the CrestApps Outreach engine. A module describes a bulk send as an **outreach run** and plugs in what is specific to it (who it goes to, how it is sent, how it is rendered); the engine pages the audience, checks consent, holds sends in quiet hours, retries without sending twice and keeps count.

### Guidelines

- Install `CrestApps.OrchardCore.Outreach` (or the `CrestApps.OrchardCore.Cms.Core.Targets` bundle, which includes it) in the web/startup project.
- The feature is `EnabledByDependencyOnly`: it never appears to be switched on by hand. Add `OutreachConstants.Feature` (`CrestApps.OrchardCore.Outreach`) to your feature's `Dependencies`.
- Contracts are in `CrestApps.OrchardCore.Outreach.Abstractions` (namespace `CrestApps.OrchardCore.Outreach`, models in `.Models`); the engine, stores, `EmailOutreachChannelSender` and the `AddOutreach*` helpers in `CrestApps.OrchardCore.Outreach.Core`. Reference those from your module.
- Do not call `AddOutreachEngine()` yourself; the Outreach feature does. Register only your sources, senders, renderers, policies and observers.
- There are no admin screens, permissions, recipe steps, deployment steps or workflow activities. The owning module (broadcasts, campaigns) shows progress and travels in its own steps.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Item | Value |
|---|---|
| Feature ID | `CrestApps.OrchardCore.Outreach` (Outreach, category Communication, enabled by dependency only) |
| Hard dependencies | None |
| Lit up by | `OrchardCore.Email` registers the `Email` sender; Business Hours (an `IOpenHoursService`) gives runs their sending hours; any feature that registers a consent source (`AddConsentSource<T>()`, such as Customers, DNC Registry or Omnichannel Management) adds the `IConsentService` that consent checks use |
| Used by | `CrestApps.OrchardCore.Omnichannel.Messaging` (broadcasts) and `CrestApps.OrchardCore.Marketing` (campaigns), which depend on it |
| Storage | YesSql collection `Outreach`: `OutreachRun` (`OutreachRunIndex`) and `OutreachBatch` (`OutreachBatchIndex`) |
| Background task | **Outreach**, every minute, up to 100 batches per run of the task |
| Retention | `OutreachBatches` policy removes completed and dead-lettered batches after 30 days; the run keeps its totals |

## A Run

`OutreachRun` (a `CatalogItem`) is started with `IOutreachEngine.StartAsync(run)`:

| Concern | Properties | Notes |
|---|---|---|
| Owner | `Name`, `OwnerType`, `OwnerId` | The record the run sends for, such as `MessagingBroadcast` or `MarketingCampaign`. |
| Audience | `AudienceSource`, `PageSize` (50) | Name of an `IOutreachAudienceSource`; required. |
| Sending | `Sender`, `Channel`, `SenderAddress`, `ActorId` | Name of an `IOutreachChannelSender`; required. |
| Content | `Template` (`Subject`, `Body`, `IsHtml`), `Renderer` | Empty `Renderer` means `Text` (the template as it is). |
| Consent | `ConsentChannel`, `ConsentContactKind` | Such as `ConsentChannels.Email` or `SMS`, and `ConsentContactKinds.Customer` or `Contact`. |
| Quiet hours | `SendingHoursScheduleId` | A Business Hours schedule, used only when the sender observes quiet hours. |
| Progress | `Status` (`Queued`, `Running`, `Completed`, `Canceled`, `Failed`), `Cursor`, `BatchesCompleted`, `Sent`, `Skipped`, `Failed`, `LastError`, `CreatedUtc`, `StartedUtc`, `CompletedUtc` | Kept by the engine; `IsFinished` for the last three statuses. |

- `StartAsync` stores the run and enqueues its first batch **in the caller's session**, so starting is atomic with the change that asked for it (the outbox pattern). A run may start with a `Cursor` and counters to carry over earlier progress.
- `FindRunAsync(runId)` reads a run. `CancelAsync(runId)` marks it `Canceled`; queued batches then complete without sending. Cancelling does not notify observers, so update your own record yourself.

## How a Batch Is Sent

Each page is an `OutreachBatch` (`RunId`, `Sequence`, `Cursor`, `NextCursor`, `ProcessedKeys`, `Sent`, `Skipped`, `Failed`, plus the `ILeasedWorkItem` fields). `OutreachBatchProcessor` runs each due batch in its own scope:

1. Claims the batch for five minutes, so only one node sends it.
2. Waits five minutes, without using an attempt, when the run's source, sender or renderer is not registered (its feature is off).
3. When `sender.ObservesQuietHours(run)` and the run's sending hours are closed, waits until the schedule next opens (or one hour when no opening is known in the next 8 days), without using an attempt.
4. Reads one page from the source at the batch cursor.
5. For each recipient: skips duplicates within the batch (by `Key`, else `Address`); skips when the source set `SkipReason`, the address is empty, or any `IOutreachRecipientPolicy` returns a reason (consent included); otherwise renders and sends. Progress is saved after every recipient, with whatever the sender wrote in the same session, so a retried batch resumes after the last saved recipient.
6. Adds its counts to the run, enqueues the next page and calls the observers in the same transaction. A `null` next cursor, or one equal to the current cursor, completes the run.

| Outcome | Effect |
|---|---|
| `OutreachSendResult.Success()` | Counted as sent. |
| `OutreachSendResult.Failure(error)` | Counted as failed; the run carries on. |
| Sender, source or renderer throws | Batch retried with back-off of 1 minute doubling to 30 minutes; after 5 failed attempts it is dead-lettered and the run fails with `LastError`. |

## Consent and Opt-outs

`ConsentOutreachRecipientPolicy` (registered by the engine) asks the first `IConsentService` with `ConsentQuery { Channel = run.ConsentChannel, Address, ContactKind = run.ConsentContactKind, ContactId = recipient.ContactId }` just before each send, and skips with `OptedOut` when `HasOptedOut`. An opt-out recorded after the run started still stops the message. No check is made when the run has no `ConsentChannel` or no feature registers the consent service. `OutreachConstants.SkipReasons` has `NoAddress`, `OptedOut` and `NoConsent`.

## Built-in Runs

| Record | `AudienceSource` | `Sender` | `Renderer` | Consent | Record fields |
|---|---|---|---|---|---|
| Messaging broadcast | `MessagingBroadcastRecipients` (the broadcast's list, by position, first place of a number only) | `Messaging` (a 1:1 thread per recipient; observes quiet hours when the channel's capabilities say so) | `Text` | Broadcast's channel, `Contact` | `SentCount`, `FailedCount`, `SkippedCount`, `OutreachRunId` |
| Marketing campaign | `MarketingSegment` (segment customers by document id; customers not accepting marketing are skipped) | `Email` | `MarketingCampaign` (Liquid with the unsubscribe link) | `Email`, `Customer` | `Sent`, `Skipped`, `Failed`, `Cursor`, `OutreachRunId` |

Marketing now depends on `OrchardCore.Email` (and Outreach), so a campaign cannot be queued on a site without e-mail. Broadcasts and campaigns keep their admin screens; their recipe steps reset `OutreachRunId` and counters on import. See `orchardcore-omnichannel-messaging` and `orchardcore-marketing`.

## Extension Points

| Seam | Register with | Contract |
|---|---|---|
| `IOutreachAudienceSource` | `AddOutreachAudienceSource<T>()` | `Name`; `GetPageAsync(run, cursor, pageSize)` returns `OutreachAudiencePage` (`Recipients`, `NextCursor`, `null` on the last page). Give each `OutreachRecipient` (`Key`, `Address`, `ContactId`, `DisplayName`, `SkipReason`) a key stable within the run. |
| `IOutreachChannelSender` | `AddOutreachChannelSender<T>()` | `Name`, `ObservesQuietHours(run)`, `SendAsync(run, recipient, content)`. Return `Failure` for a permanent refusal; throw for a transient error. |
| `IOutreachTemplateRenderer` | `AddOutreachTemplateRenderer<T>()` | `Name`, `RenderAsync(run, recipient)` returns `OutreachContent`. |
| `IOutreachRecipientPolicy` | `AddOutreachRecipientPolicy<T>()` | `GetSkipReasonAsync(run, recipient)` returns a reason or `null`. Consent is already covered. |
| `IOutreachRunObserver` | `AddOutreachRunObserver<T>()` | `RunUpdatedAsync(run)` copies status and counters onto your record; runs inside the batch transaction and must not commit. |

The helpers are in the `Microsoft.Extensions.DependencyInjection` namespace and use `TryAddEnumerable`, so registering twice is harmless.

```csharp
public sealed class SmsOutreachChannelSender : IOutreachChannelSender
{
    private readonly ISmsService _smsService;

    public SmsOutreachChannelSender(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public string Name => "Sms";

    public bool ObservesQuietHours(OutreachRun run) => true;

    public async Task<OutreachSendResult> SendAsync(OutreachRun run, OutreachRecipient recipient, OutreachContent content, CancellationToken cancellationToken = default)
    {
        var result = await _smsService.SendAsync(new SmsMessage { To = recipient.Address, Body = content.Body }, cancellationToken);

        return result.Succeeded
            ? OutreachSendResult.Success()
            : OutreachSendResult.Failure("The SMS provider refused the message.");
    }
}

// Startup (feature depends on CrestApps.OrchardCore.Outreach and OrchardCore.Sms):
// services.AddOutreachChannelSender<SmsOutreachChannelSender>();
```

A complete audience source, renderer, policy, observer and the code that starts and cancels a run are in [references/outreach-extensions.md](references/outreach-extensions.md).

## The Leased Work Queue

The queue the batches run on is a platform building block for any durable, multi-node work (the Contact Center outbox and the provider webhook inbox use it too).

| Type | Package | Role |
|---|---|---|
| `ILeasedWorkItem` | `CrestApps.OrchardCore.Abstractions` (`CrestApps.OrchardCore.WorkQueues`) | `ItemId`, `Status` (`Pending`, `Claimed`, `Completed`, `DeadLettered`), `OwnerToken`, `FenceToken`, `AttemptCount`, `NextAttemptUtc`, `LastError` on your record |
| `ILeasedWorkQueue<T>` | same | `EnqueueAsync` (caller's session), `ClaimDueAsync`, `TryClaimAsync`, `CompleteAsync`, `RetryAsync(claimed, error, backoff, countAttempt)`, `AbandonAsync` |
| `LeasedWorkBackoff.Exponential(base, max)` | same | The wait before the next attempt |
| `AddLeasedWorkQueue<T>(options => ...)`, `LeasedWorkQueue<T>`, `LeasedWorkItemIndex`, `LeasedWorkItemIndexSchema.CreateAsync` | `CrestApps.OrchardCore.YesSql.Core` | YesSql queue on the shared index; options `CollectionName`, `MaxAttempts` (10), `RemoveCompletedItems` (false) |
| `YesSqlLeasedWorkQueue<T, TIndex>`, `CatalogLeasedWorkQueue<T>` | same | A queue over your own index, or one that commits in the caller's isolated session (what Outreach uses) |

Claims are saved with an optimistic concurrency check; a settlement from a claim whose lease expired is refused. A full queue example is in [references/outreach-extensions.md](references/outreach-extensions.md).

## Gotchas

| Symptom | Check |
|---|---|
| Run stays `Queued` | Background tasks are off, or the source, sender or renderer name has no registration (the batch waits five minutes each time; see the log). |
| E-mail campaign never sends | `OrchardCore.Email` must be enabled for the `Email` sender, with a default provider configured. |
| Every recipient failed but no retry | The sender returned `Failure` (such as an e-mail service that refused); only exceptions retry. |
| Texts go out at night | Quiet hours apply only when the run has a `SendingHoursScheduleId`, Business Hours is on and the sender's `ObservesQuietHours` is true. The built-in broadcast run does not set a schedule. |
| A person gets the same message twice | Keys are de-duplicated only within one batch; the audience source must not return a recipient on two pages. |
| Opt-outs not honored | The run has no `ConsentChannel`, or no `IConsentService` is registered. |
| Observer changes are lost | Observers must not call `SaveChangesAsync`; the batch commits them. |
| Starting a run in a background task does nothing | `StartAsync` writes into the current session; the scope must commit it. |

Related skills: `orchardcore-omnichannel-messaging`, `orchardcore-marketing`, `orchardcore-omnichannel-management`, `orchardcore-customers`, `orchardcore-email`, `orchardcore-background-tasks`.
